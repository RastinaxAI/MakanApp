using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfOfficialSpacesStore(MakanDbContext db, IOutbox outbox, TimeProvider clock, ILogger<EfOfficialSpacesStore> logger) : IOfficialSpacesStore
{
    public async Task<OfficialSpacesPairResult> ReadAsync(Guid userId, AccessContext context, Guid? classId, CancellationToken ct)
    {
        var org = await AuthorizeAsync(userId, context, classId, ct);
        var provisions = await db.OfficialSpaceProvisions.AsNoTracking().Where(p => p.OrganizationId == org && p.ClassId == classId).ToArrayAsync(ct);
        // intent قبل از binding خوانده می‌شود؛ commit بین دو SELECT نباید Ready بدون شناسهٔ مقصد بسازد.
        var conversations = await Bound(org, classId).AsNoTracking().ToArrayAsync(ct);
        var reasons = await OfficialSpaceAccessQuery.SourceReasons(db, org, classId).Where(r => r.UserId == userId).ToArrayAsync(ct);
        var conversationIds = conversations.Select(c => c.Id).ToArray();
        var participants = await db.ConversationParticipants.AsNoTracking().Where(p => conversationIds.Contains(p.ConversationId) &&
            p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null).ToArrayAsync(ct);
        OfficialSpaceEndpointResult Endpoint(ConversationType purpose)
        {
            var conversation = conversations.SingleOrDefault(c => c.Type == purpose);
            var provision = provisions.SingleOrDefault(p => p.Purpose == purpose);
            var ready = conversation?.Status == ConversationStatus.Active;
            var hasAccess = ready && participants.Any(p => p.ConversationId == conversation!.Id);
            var state = ready ? "Ready" : conversation is not null ? "RepairRequired"
                : provision?.State == OfficialSpaceState.Ready ? "Pending" : provision?.State.ToString() ?? "Missing";
            return new(conversation?.Id, state,
                new(hasAccess, hasAccess && (purpose == ConversationType.Group || reasons.Any(r => r.CanPublishChannel)), false),
                conversation is null ? null : Metadata(conversation), provision?.AttemptCount ?? 0, provision?.LastFailureCategory);
        }
        var revisions = conversations.Select(c => Convert.ToBase64String(c.RowVersion)).Concat(provisions.Select(p => Convert.ToBase64String(p.RowVersion)))
            .Concat(participants.Select(p => Convert.ToBase64String(p.RowVersion)))
            .Concat(reasons.Select(r => $"{userId:N}:{r.ReasonId:N}:{r.Kind}:{r.CanManage}:{r.CanPublishChannel}")).Order();
        return new(classId.HasValue ? OfficialSourceKind.Class : OfficialSourceKind.Organization, classId ?? org, org,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', revisions)))), Endpoint(ConversationType.Group), Endpoint(ConversationType.Channel));
    }

    public async Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadDeliveryAsync(Guid userId, AccessContext context, Guid classId, Guid eventId, CancellationToken ct)
    {
        var org = await AuthorizeAsync(userId, context, classId, ct);
        if (!await db.AcademicActivityPublications.AnyAsync(p => p.Id == eventId && p.OrganizationId == org && p.ClassId == classId, ct)) throw NotFound();
        return await db.AcademicActivityDeliveries.AsNoTracking().Where(d => d.PublicationId == eventId).OrderBy(d => d.Purpose)
            .Select(d => new AcademicActivityDeliveryResult(d.PublicationId, d.Purpose, d.State, d.ConversationId, d.MessageId, d.AttemptCount, d.LastFailureCategory)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadSourceDeliveryAsync(Guid userId, AccessContext context, Guid classId,
        AcademicActivityKind kind, Guid sourceVersionId, int revision, CancellationToken ct)
    {
        var org = await AuthorizeAsync(userId, context, classId, ct);
        if (!Enum.IsDefined(kind) || sourceVersionId == Guid.Empty || revision < 1) throw NotFound();
        var eventId = await db.AcademicActivityPublications.AsNoTracking()
            .Where(p => p.OrganizationId == org && p.ClassId == classId && p.Kind == kind && p.SourceRevision == revision &&
                (kind == AcademicActivityKind.Lesson && p.LessonId == sourceVersionId ||
                 kind == AcademicActivityKind.Assignment && p.AssignmentVersionId == sourceVersionId ||
                 kind == AcademicActivityKind.Exam && p.ExamVersionId == sourceVersionId))
            .Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct) ?? throw NotFound();
        return await ReadDeliveryAsync(userId, context, classId, eventId, ct);
    }

    private async Task<Guid> AuthorizeAsync(Guid userId, AccessContext context, Guid? classId, CancellationToken ct)
    {
        if (context.WorkspaceType != WorkspaceType.Organization || context.OrganizationId is not Guid org || context.UserId != userId ||
            classId.HasValue && !await db.Classes.AnyAsync(c => c.Id == classId && c.OrganizationId == org && c.Status == ClassStatus.Active, ct) ||
            !await OfficialSpaceAccessQuery.SourceReasons(db, org, classId).AnyAsync(r => r.UserId == userId, ct)) throw NotFound();
        return org;
    }

    // تنها worker/ابزار داخلی معتبر این عملیات را صدا می‌زند؛ GET و API ساخت عمومی چنین مسیری ندارند.
    public async Task<bool> EnsurePairAsync(Guid organizationId, Guid? classId, CancellationToken ct)
    {
        var group = await EnsurePurposeAsync(organizationId, classId, ConversationType.Group, ct);
        var channel = await EnsurePurposeAsync(organizationId, classId, ConversationType.Channel, ct);
        return group && channel;
    }

    private async Task<bool> EnsurePurposeAsync(Guid org, Guid? classId, ConversationType purpose, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (title, active) = await LockSourceAsync(org, classId, ct);
        var provision = await db.OfficialSpaceProvisions.SingleOrDefaultAsync(p => p.OrganizationId == org && p.ClassId == classId && p.Purpose == purpose, ct);
        if (provision is null) { provision = OfficialSpaceProvision.Request(org, classId, purpose, clock.GetUtcNow().UtcDateTime); db.OfficialSpaceProvisions.Add(provision); }
        var conversation = await Bound(org, classId).SingleOrDefaultAsync(c => c.Type == purpose, ct);
        if (active && conversation?.Status == ConversationStatus.Active && provision.State == OfficialSpaceState.Ready)
        {
            await SynchronizeAsync(conversation, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
        }
        provision.RecordAttempt(clock.GetUtcNow().UtcDateTime);
        var ambiguous = classId is null && conversation is null && await db.Conversations.AnyAsync(c => c.OrganizationId == org &&
            c.AcademicClassId == null && !c.IsOfficialOrganizationSpace && c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && c.Type == purpose, ct);
        if (!active || ambiguous || conversation is { Status: not ConversationStatus.Active })
        {
            provision.RequireRepair(!active ? "SourceUnavailable" : ambiguous ? "BindingConflict" : "ConversationUnavailable");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
            logger.LogWarning("آماده‌سازی فضای رسمی نیازمند بررسی است. OrganizationId={OrganizationId}; ClassId={ClassId}; Purpose={Purpose}; Category={Category}", org, classId, purpose, !active ? "SourceUnavailable" : "BindingConflict");
            return false;
        }
        if (conversation is null)
        {
            var display = title[..Math.Min(title.Length, 120)] + (purpose == ConversationType.Group ? " — گروه رسمی" : " — کانال رسمی");
            conversation = classId.HasValue ? Conversation.CreateSystemManagedAcademic(purpose, org, display, null, clock.GetUtcNow().UtcDateTime)
                : Conversation.CreateOfficialOrganization(purpose, org, display, clock.GetUtcNow().UtcDateTime);
            if (classId.HasValue) conversation.BindAcademicClass(classId.Value);
            db.Conversations.Add(conversation);
        }
        await SynchronizeAsync(conversation, ct); provision.Ready();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        logger.LogInformation("فضای رسمی آماده شد. OrganizationId={OrganizationId}; ClassId={ClassId}; Purpose={Purpose}; State={State}", org, classId, purpose, "Ready");
        return true;
    }

    public async Task SynchronizeExistingAsync(Guid org, Guid? classId, CancellationToken ct)
    {
        if (classId.HasValue)
        {
            if (await db.OfficialSpaceProvisions.AnyAsync(p => p.OrganizationId == org && p.ClassId == classId, ct))
            { _ = await EnsurePairAsync(org, classId, ct); return; }
            await SynchronizeBoundAsync(org, classId, ct); return;
        }
        // همگام‌سازی رابطه، backfill خودکار منابع قدیمیِ بدون binding نیست.
        await SynchronizeBoundAsync(org, null, ct);
        var classes = await db.Conversations.Where(c => c.OrganizationId == org && c.AcademicClassId != null &&
            c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic).Select(c => c.AcademicClassId).Distinct().ToArrayAsync(ct);
        foreach (var id in classes) await SynchronizeBoundAsync(org, id, ct);
    }

    private async Task SynchronizeBoundAsync(Guid org, Guid? classId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await LockSourceAsync(org, classId, ct);
        foreach (var conversation in await Bound(org, classId).ToArrayAsync(ct)) await SynchronizeAsync(conversation, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }

    private async Task SynchronizeAsync(Conversation conversation, CancellationToken ct)
    {
        var sourceActive = conversation.AcademicClassId is null || await db.Classes.AnyAsync(c => c.Id == conversation.AcademicClassId && c.Status == ClassStatus.Active, ct);
        var users = sourceActive ? await OfficialSpaceAccessQuery.SourceReasons(db, conversation.OrganizationId!.Value, conversation.AcademicClassId)
            .Select(r => r.UserId).Distinct().ToArrayAsync(ct) : [];
        var participants = await db.ConversationParticipants.Where(p => p.ConversationId == conversation.Id && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in participants.Where(p => !users.Contains(p.UserId))) { old.EndSystemAccess(now); Invalidate(old.UserId, now); }
        foreach (var userId in users.Where(id => participants.All(p => p.UserId != id)))
        {
            db.ConversationParticipants.Add(ConversationParticipant.CreateActive(conversation.Id, userId, now)); Invalidate(userId, now);
        }
    }

    private void Invalidate(Guid userId, DateTime now) => outbox.Enqueue(new(Guid.NewGuid(), RealtimeInvalidationWriter.MessageType,
        JsonSerializer.Serialize(new InvalidationEnvelope("access", TargetUserId: userId, TraceId: Activity.Current?.TraceId.ToString())), now));

    private async Task<(string Title, bool Active)> LockSourceAsync(Guid org, Guid? classId, CancellationToken ct)
    {
        // Academic ابتدا Class را قفل می‌کند و CommercialWritePolicy سپس Organization را؛ worker همین ترتیب را رعایت می‌کند.
        MakanApp.Domain.Academic.Class? classroom = null;
        if (classId.HasValue)
            classroom = await db.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={org} AND [Id]={classId}")
                .SingleOrDefaultAsync(ct) ?? throw NotFound();
        var organization = await db.Organizations.FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={org}")
            .SingleOrDefaultAsync(ct) ?? throw NotFound();
        return classroom is null ? (organization.Name, organization.IsActive)
            : (classroom.Title, organization.IsActive && classroom.Status == ClassStatus.Active);
    }

    private IQueryable<Conversation> Bound(Guid org, Guid? classId) => db.Conversations.Where(c => c.OrganizationId == org &&
        c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && (classId.HasValue ? c.AcademicClassId == classId : c.IsOfficialOrganizationSpace));

    public async Task<IReadOnlyList<OfficialBackfillPlanRow>> PlanAsync(CancellationToken ct, Guid? organizationId = null)
    {
        var sources = (await db.Organizations.AsNoTracking().Where(o => organizationId == null || o.Id == organizationId).OrderBy(o => o.Id).Select(o => new { OrganizationId = o.Id, ClassId = (Guid?)null }).ToArrayAsync(ct)).ToList();
        sources.AddRange(await db.Classes.AsNoTracking().Where(c => organizationId == null || c.OrganizationId == organizationId).OrderBy(c => c.OrganizationId).ThenBy(c => c.Id).Select(c => new { c.OrganizationId, ClassId = (Guid?)c.Id }).ToArrayAsync(ct));
        var result = new List<OfficialBackfillPlanRow>();
        foreach (var source in sources)
        {
            var candidates = await Bound(source.OrganizationId, source.ClassId).AsNoTracking().ToArrayAsync(ct);
            var group = candidates.SingleOrDefault(c => c.Type == ConversationType.Group); var channel = candidates.SingleOrDefault(c => c.Type == ConversationType.Channel);
            var ambiguous = source.ClassId is null && await db.Conversations.AnyAsync(c => c.OrganizationId == source.OrganizationId && c.AcademicClassId == null &&
                !c.IsOfficialOrganizationSpace && c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic, ct);
            var active = await db.Organizations.AnyAsync(o => o.Id == source.OrganizationId && o.Status == OrganizationStatus.Active, ct) &&
                (source.ClassId is null || await db.Classes.AnyAsync(c => c.Id == source.ClassId && c.OrganizationId == source.OrganizationId && c.Status == ClassStatus.Active, ct));
            var conflict = !active || ambiguous || candidates.Any(c => c.Status != ConversationStatus.Active);
            result.Add(new(source.ClassId.HasValue ? OfficialSourceKind.Class : OfficialSourceKind.Organization, source.ClassId ?? source.OrganizationId,
                source.OrganizationId, group?.Id, channel?.Id, group is null, channel is null,
                conflict ? "ManualDecision" : group is not null && channel is not null ? "NoChange" : group is not null || channel is not null ? "ReuseAndCreateMissing" : "CreatePairAfterApproval", conflict));
        }
        return result;
    }

    internal static OfficialSpaceMetadata? Metadata(Conversation c) => OfficialSpaceMetadata.From(c);

    private static MessagingException NotFound() => new(MessagingErrorCodes.ConversationNotFound, "منبع رسمی مجاز یافت نشد.");
}
