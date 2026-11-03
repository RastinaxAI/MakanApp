using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfOfficialSpacesStore
{
    public async Task<IReadOnlyList<PersonalClassBackfillPlanRow>> PlanPersonalClassesAsync(Guid? classId, CancellationToken ct)
    {
        var classes = await db.PersonalClasses.AsNoTracking().Where(c => !classId.HasValue || c.Id == classId).OrderBy(c => c.Id).ToArrayAsync(ct);
        var result = new List<PersonalClassBackfillPlanRow>();
        foreach (var c in classes)
        {
            var bound = await db.Conversations.AsNoTracking().Where(s => s.PersonalClassId == c.Id).ToArrayAsync(ct);
            var group = bound.SingleOrDefault(s => s.Type == ConversationType.Group);
            var channel = bound.SingleOrDefault(s => s.Type == ConversationType.Channel);
            var candidates = await db.Conversations.AsNoTracking().Where(s => s.PersonalClassId == null && s.Scope == ConversationScope.Personal &&
                s.CreatedByUserId == c.HostUserId && s.Title == c.Title && (s.Type == ConversationType.Group || s.Type == ConversationType.Channel))
                .OrderBy(s => s.Id).Select(s => s.Id).ToArrayAsync(ct);
            var hasManager = c.ManagerUserId.HasValue && await db.PersonalClassParticipants.AnyAsync(m => m.PersonalClassId == c.Id && m.StudentUserId == c.ManagerUserId &&
                m.EndedAtUtc == null && (m.LocalRoles & PersonalClassLocalRoles.Manager) != 0, ct);
            var conflict = candidates.Length > 0 || !hasManager || bound.Any(s => s.Status != ConversationStatus.Active);
            string Action(Conversation? destination) => conflict ? "ManualDecision" : destination is not null ? "Reuse" : c.State == PersonalClassState.Archived ? "PreserveArchived" : "CreateMissing";
            var migrationState = !hasManager ? PersonalClassMigrationState.NeedsManagerMapping : conflict ? PersonalClassMigrationState.Conflict :
                c.State != PersonalClassState.Archived && (group is null || channel is null) ? PersonalClassMigrationState.NeedsSpaceProvisioning : c.MigrationState;
            var row = new PersonalClassBackfillPlanRow(c.Id, c.HostUserId, c.ManagerUserId, migrationState.ToString(),
                await db.PersonalClassParticipants.CountAsync(m => m.PersonalClassId == c.Id && m.EndedAtUtc == null, ct),
                group?.Id, channel?.Id, candidates, Action(group), Action(channel), conflict,
                candidates.Length > 0 ? "UnboundConversationOwnership" : !hasManager ? "ManagerMappingRequired" : conflict ? "ConversationUnavailable" : null);
            result.Add(row with { PlanSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(row))) });
        }
        return result;
    }
    public async Task<bool> EnsurePersonalClassPairAsync(Guid classId, CancellationToken ct, bool approvedLegacyBackfill = false)
    {
        var source = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId, ct) ?? throw NotFound();
        if (!source.FormationRequestId.HasValue && !approvedLegacyBackfill && !await db.OfficialSpaceProvisions.AnyAsync(p => p.PersonalClassId == classId, ct)) return false;
        if (!source.FormationRequestId.HasValue && (await PlanPersonalClassesAsync(classId, ct)).Single().Conflict) return false;
        var group = await EnsurePersonalClassPurposeSafelyAsync(classId, ConversationType.Group, ct);
        var channel = await EnsurePersonalClassPurposeSafelyAsync(classId, ConversationType.Channel, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var classroom = await LockPersonalClass(classId, ct);
        var before = classroom.State;
        classroom.RecordProvisioning(group && channel);
        if (classroom.State != before)
            foreach (var user in await db.PersonalClassParticipants.Where(m => m.PersonalClassId == classId && m.EndedAtUtc == null).Select(m => m.StudentUserId).ToArrayAsync(ct))
                Invalidate(user, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return group && channel;
    }

    private async Task<bool> EnsurePersonalClassPurposeSafelyAsync(Guid classId, ConversationType purpose, CancellationToken ct)
    {
        try { return await EnsurePersonalClassPurposeAsync(classId, purpose, ct); }
        catch (DbUpdateException)
        {
            // تراکنش مقصد شکست‌خورده برگشته است؛ مقصد موفق و محتوای آن حفظ می‌شوند.
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            _ = await LockPersonalClass(classId, ct);
            var provision = await db.OfficialSpaceProvisions.SingleOrDefaultAsync(p => p.PersonalClassId == classId && p.Purpose == purpose, ct);
            if (provision is null) { provision = OfficialSpaceProvision.RequestPersonalClass(classId, purpose, clock.GetUtcNow().UtcDateTime); db.OfficialSpaceProvisions.Add(provision); }
            provision.RecordAttempt(clock.GetUtcNow().UtcDateTime); provision.RequireRepair("PersistenceFailure");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
            logger.LogError("آماده‌سازی فضای کلاس مستقل شکست خورد. ClassId={ClassId}; Purpose={Purpose}; Category={Category}", classId, purpose, "PersistenceFailure");
            return false;
        }
    }

    private async Task<bool> EnsurePersonalClassPurposeAsync(Guid classId, ConversationType purpose, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var classroom = await LockPersonalClass(classId, ct);
        var provision = await db.OfficialSpaceProvisions.SingleOrDefaultAsync(p => p.PersonalClassId == classId && p.Purpose == purpose, ct);
        var conversation = await db.Conversations.SingleOrDefaultAsync(c => c.PersonalClassId == classId && c.Type == purpose, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (provision is null)
        {
            provision = OfficialSpaceProvision.RequestPersonalClass(classId, purpose, now);
            db.OfficialSpaceProvisions.Add(provision);
        }
        if (conversation?.Status == ConversationStatus.Active && provision.State == OfficialSpaceState.Ready)
        {
            await SynchronizePersonalClassAsync(conversation, ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
        }
        provision.RecordAttempt(now);
        if (conversation is { Status: not ConversationStatus.Active } || classroom.State == PersonalClassState.Archived && conversation is null)
        {
            provision.RequireRepair(conversation is null ? "SourceUnavailable" : "ConversationUnavailable");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return false;
        }
        if (conversation is null)
        {
            conversation = Conversation.CreatePersonalClassSpace(purpose, classId, DisplayTitle(classroom.Title, purpose), now);
            db.Conversations.Add(conversation);
        }
        await SynchronizePersonalClassAsync(conversation, ct);
        provision.Ready();
        foreach (var user in await db.PersonalClassParticipants.Where(m => m.PersonalClassId == classId && m.EndedAtUtc == null).Select(m => m.StudentUserId).ToArrayAsync(ct)) Invalidate(user, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
    }

    private async Task SynchronizePersonalClassAsync(Conversation conversation, CancellationToken ct)
    {
        var users = await (from member in db.PersonalClassParticipants join user in db.Users on member.StudentUserId equals user.Id
            where member.PersonalClassId == conversation.PersonalClassId && member.EndedAtUtc == null && user.AccountStatus == MakanApp.Domain.Identity.UserAccountStatus.Active
            select user.Id).Distinct().ToArrayAsync(ct);
        var participants = await db.ConversationParticipants.Where(p => p.ConversationId == conversation.Id && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in participants.Where(p => !users.Contains(p.UserId))) { old.EndSystemAccess(now); Invalidate(old.UserId, now); }
        foreach (var user in users.Where(id => participants.All(p => p.UserId != id)))
        { db.ConversationParticipants.Add(ConversationParticipant.CreateActive(conversation.Id, user, now)); Invalidate(user, now); }
    }

    private async Task<PersonalClass> LockPersonalClass(Guid id, CancellationToken ct) => await db.PersonalClasses
        .FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
        .SingleOrDefaultAsync(ct) ?? throw NotFound();

    public async Task<OfficialSpacesPairResult> ReadPersonalClassAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        var classroom = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId &&
            db.PersonalClassParticipants.Any(m => m.PersonalClassId == c.Id && m.StudentUserId == userId && m.EndedAtUtc == null), ct) ?? throw NotFound();
        var provisions = await db.OfficialSpaceProvisions.AsNoTracking().Where(p => p.PersonalClassId == classId).ToArrayAsync(ct);
        var conversations = await db.Conversations.AsNoTracking().Where(c => c.PersonalClassId == classId).ToArrayAsync(ct);
        var grants = await OfficialSpaceAccessQuery.Conversations(db).Where(g => g.UserId == userId && conversations.Select(c => c.Id).Contains(g.ConversationId)).ToArrayAsync(ct);
        var participants = await db.ConversationParticipants.AsNoTracking().Where(p => p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
            conversations.Select(c => c.Id).Contains(p.ConversationId)).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var actorAvailable = await db.Users.AnyAsync(u => u.Id == userId && u.AccountStatus == MakanApp.Domain.Identity.UserAccountStatus.Active &&
            u.ProfileCompletedAtUtc != null && u.PersonId != null && u.Username != null && (u.MessagingRestrictedUntilUtc == null || u.MessagingRestrictedUntilUtc <= now), ct);
        var restrictions = await db.ConversationMemberRestrictions.AsNoTracking().Where(r => conversations.Select(c => c.Id).Contains(r.ConversationId) &&
            r.UserId == userId && (r.UntilUtc == null || r.UntilUtc > now)).ToDictionaryAsync(r => r.ConversationId, ct);
        OfficialSpaceEndpointResult Endpoint(ConversationType purpose)
        {
            var c = conversations.SingleOrDefault(c => c.Type == purpose);
            var p = provisions.SingleOrDefault(p => p.Purpose == purpose);
            var grant = grants.FirstOrDefault(g => g.ConversationId == c?.Id);
            var readable = c?.Status == ConversationStatus.Active && grant is not null && participants.Any(p => p.ConversationId == c.Id);
            var participant = participants.SingleOrDefault(p => p.ConversationId == c?.Id);
            var restriction = c is null ? null : restrictions.GetValueOrDefault(c.Id);
            var effective = c is null || participant is null ? ConversationEffectiveCapabilities.None : ConversationCapabilityPolicy.Evaluate(
                new(c.Type, c.ManagementPolicy, c.Status, readable, participant.Role, participant.AdminPermissions, c.MemberCapabilities,
                    grant?.CanManage == true, grant?.CanPublish == true, !actorAvailable, restriction?.IsBanned == true, restriction?.DeniedCapabilities ?? GroupMemberCapability.None));
            return new(c?.Id, c?.Status == ConversationStatus.Active ? "Ready" : c is not null ? "RepairRequired" : p?.State == OfficialSpaceState.Ready ? "Pending" : p?.State.ToString() ?? "Missing",
                new(readable, effective.CanPublish, effective.CanManageMessages), c is null ? null : Metadata(c), p?.AttemptCount ?? 0, p?.LastFailureCategory)
                { EffectiveCapabilities = effective };
        }
        var versions = conversations.Select(c => Convert.ToBase64String(c.RowVersion)).Concat(provisions.Select(p => Convert.ToBase64String(p.RowVersion)))
            .Concat(participants.Select(p => Convert.ToBase64String(p.RowVersion))).Concat(restrictions.Values.Select(r => Convert.ToBase64String(r.RowVersion)))
            .Concat(grants.Select(g => $"{g.ConversationId:N}:{g.CanPublish}:{g.CanManage}")).Append(Convert.ToBase64String(classroom.RowVersion)).Append(actorAvailable.ToString()).Order();
        var version = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join('|', versions))));
        return new(OfficialSourceKind.PersonalClass, classId, null, version, Endpoint(ConversationType.Group), Endpoint(ConversationType.Channel));
    }
}
