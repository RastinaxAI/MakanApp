using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfPersonalClassFormationStore(MakanDbContext db, CommercialActorAccess actors,
    IPlatformPermissionResolver permissions, EfCommercialStore commerce, OfficialSpaceEventWriter spaces,
    RealtimeInvalidationWriter realtime) : IPersonalClassFormationStore
{
    public async Task<PersonalClassRequestDto> CreateAsync(PersonalClassActor actor, CreatePersonalClassRequestCommand command, DateTime now, CancellationToken ct)
    {
        var input = Normalize(command.Details);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(actor.UserId), ct);
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var existing = await db.PersonalClassRequests.SingleOrDefaultAsync(r => r.RequesterUserId == actor.UserId && r.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.CreationPayloadHash != hash) throw Conflict();
            await tx.CommitAsync(ct); return Map(existing, actor.UserId);
        }
        await ValidateSuggestions(input, ct);
        var request = PersonalClassRequest.Create(actor.UserId, command.ClientOperationId, hash, input.Title, input.Purpose,
            input.Description, input.SuggestedCapacity, input.SuggestedManagerUserId, input.SuggestedTeacherUserId, now);
        db.PersonalClassRequests.Add(request);
        await realtime.CapturePersonalClassesAsync(ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(request, actor.UserId);
    }

    public async Task<PersonalClassPage<PersonalClassRequestDto>> ListAsync(Guid actor, bool platform, PersonalClassRequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.PersonalClassRequests.AsNoTracking().Where(r => (platform ? r.Status != PersonalClassRequestStatus.Draft : r.RequesterUserId == actor) && (!status.HasValue || r.Status == status));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(await ProgressAsync(rows.Select(r => Map(r, actor)).ToArray(), ct), page, pageSize, total);
    }

    public async Task<PersonalClassRequestDto> GetAsync(Guid actor, Guid id, bool platform, CancellationToken ct)
    {
        var request = await db.PersonalClassRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id &&
            (platform ? r.Status != PersonalClassRequestStatus.Draft : r.RequesterUserId == actor), ct) ?? throw NotFound();
        return (await ProgressAsync([Map(request, actor)], ct))[0];
    }

    private async Task<PersonalClassRequestDto[]> ProgressAsync(PersonalClassRequestDto[] requests, CancellationToken ct)
    {
        var ids = requests.Where(r => r.CreatedClassId.HasValue).Select(r => r.CreatedClassId!.Value).ToArray();
        if (ids.Length == 0) return requests;
        var classes = await db.PersonalClasses.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var provisions = await db.OfficialSpaceProvisions.AsNoTracking().Where(p => p.PersonalClassId != null && ids.Contains(p.PersonalClassId.Value)).ToArrayAsync(ct);
        return requests.Select(r => r.CreatedClassId is Guid id && classes.TryGetValue(id, out var c) ? r with
        {
            Progress = new(c.State.ToString(), c.ManagerUserId.HasValue,
                provisions.SingleOrDefault(p => p.PersonalClassId == id && p.Purpose == MakanApp.Domain.Messaging.ConversationType.Group)?.State.ToString() ?? "Missing",
                provisions.SingleOrDefault(p => p.PersonalClassId == id && p.Purpose == MakanApp.Domain.Messaging.ConversationType.Channel)?.State.ToString() ?? "Missing")
        } : r).ToArray();
    }

    public async Task<PersonalClassRequestDto> EditAsync(PersonalClassActor actor, Guid id, EditPersonalClassRequestCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await Lock(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (r.RequesterUserId != actor.UserId) throw NotFound();
        Version(r.RowVersion, command.ExpectedVersion);
        if (!r.CanEdit) throw Conflict();
        var input = Normalize(command.Details); await ValidateSuggestions(input, ct);
        r.Edit(input.Title, input.Purpose, input.Description, input.SuggestedCapacity, input.SuggestedManagerUserId, input.SuggestedTeacherUserId, now);
        await realtime.CapturePersonalClassesAsync(ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(r, actor.UserId);
    }

    public async Task<PersonalClassRequestDto> TransitionAsync(PersonalClassActor actor, Guid id, PersonalClassVersionCommand command, bool submit, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await Lock(id, ct); await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (r.RequesterUserId != actor.UserId) throw NotFound();
        var replay = submit ? r.Status is PersonalClassRequestStatus.Submitted or PersonalClassRequestStatus.UnderReview : r.Status == PersonalClassRequestStatus.Cancelled;
        if (!replay)
        {
            Version(r.RowVersion, command.ExpectedVersion);
            try { if (submit) r.Submit(now); else r.Cancel(now); } catch (InvalidOperationException) { throw Conflict(); }
            Audit(actor, r.Id, submit ? "Submitted" : "Cancelled", submit ? "ارسال صریح درخواست تشکیل" : "لغو توسط درخواست‌کننده", now);
            await realtime.CapturePersonalClassesAsync(ct); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return Map(r, actor.UserId);
    }

    public async Task<PersonalClassRequestDto> ReviewAsync(PersonalClassActor actor, Guid id, ReviewPersonalClassRequestCommand command, DateTime now, CancellationToken ct)
    {
        var requester = await db.PersonalClassRequests.AsNoTracking().Where(r => r.Id == id).Select(r => (Guid?)r.RequesterUserId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // قفل میزبان قبل از درخواست، با سهمیهٔ تجاری و تمام پذیرش‌ها ترتیب یکسان دارد.
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(requester), ct);
        var r = await Lock(id, ct);
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesReview, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز بررسی معتبر نیست.");
        var finalManager = command.FinalManagerUserId ?? r.RequesterUserId;
        var replay = r.Status == command.Decision && (command.Decision == PersonalClassRequestStatus.UnderReview ||
            r.DecisionReason == command.Reason.Trim() && (command.Decision != PersonalClassRequestStatus.Approved || r.FinalManagerUserId == finalManager));
        if (replay) { await tx.CommitAsync(ct); return Map(r, actor.UserId); }
        Version(r.RowVersion, command.ExpectedVersion);
        if (r.Status is not (PersonalClassRequestStatus.Submitted or PersonalClassRequestStatus.UnderReview)) throw Conflict();
        if (command.Decision == PersonalClassRequestStatus.UnderReview) r.BeginReview(now);
        else if (command.Decision == PersonalClassRequestStatus.Approved)
        {
            if (!await db.Users.AnyAsync(u => u.Id == finalManager && u.AccountStatus == UserAccountStatus.Active, ct)) throw NotFound();
            await commerce.RequireCapacityAsync(SubscriptionOwner.Individual(r.RequesterUserId), EntitlementKey.PersonalClassHosting, QuotaKey.ActiveClasses, 1, now, ct);
            var entity = PersonalClass.FromApproval(r, now);
            db.PersonalClasses.Add(entity);
            if (finalManager == r.RequesterUserId)
            {
                entity.SetManager(finalManager);
                db.PersonalClassParticipants.Add(PersonalClassParticipant.Admit(entity.Id, finalManager, PersonalClassLocalRoles.Manager, now));
            }
            else db.PersonalClassManagerTransfers.Add(PersonalClassManagerTransfer.Create(entity.Id, actor.UserId, null, finalManager,
                r.ClientOperationId, command.Reason, now));
            spaces.RequestPersonalClassPair(entity.Id, now);
            await realtime.CapturePersonalClassesAsync(ct);
            // نخست کلاس درج می‌شود؛ رابطهٔ برگشتی Approved فقط پس از وجود FK معتبر برقرار می‌شود.
            await db.SaveChangesAsync(ct);
            r.Decide(command.Decision, actor.UserId, command.Reason, now, entity.Id, finalManager);
        }
        else r.Decide(command.Decision, actor.UserId, command.Reason, now);
        Audit(actor, r.Id, command.Decision.ToString(), command.Reason, now);
        await realtime.CapturePersonalClassesAsync(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(r, actor.UserId);
    }

    public async Task<PersonalClassPage<PersonalClassDecisionDto>> HistoryAsync(Guid actor, Guid id, bool platform, int page, int pageSize, CancellationToken ct)
    {
        _ = await GetAsync(actor, id, platform, ct);
        var target = id.ToString();
        var query = db.AdminAuditLogs.AsNoTracking().Where(a => a.TargetType == "PersonalClassRequest" && a.TargetId == target && a.Result == AdminAuditResult.Succeeded);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.OccurredAtUtc).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new PersonalClassDecisionDto(a.Id, a.Action, a.Reason, a.OccurredAtUtc, platform ? a.ActorUserId : null)).ToArrayAsync(ct);
        return new(items, page, pageSize, count);
    }

    private async Task<PersonalClassRequest> Lock(Guid id, CancellationToken ct) => await db.PersonalClassRequests
        .FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClassRequests] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
        .SingleOrDefaultAsync(ct) ?? throw NotFound();
    private async Task ValidateSuggestions(PersonalClassRequestInput input, CancellationToken ct)
    {
        var ids = new[] { input.SuggestedManagerUserId, input.SuggestedTeacherUserId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length > 0 && await db.Users.CountAsync(u => ids.Contains(u.Id) && u.AccountStatus == UserAccountStatus.Active, ct) != ids.Length) throw NotFound();
    }
    private void Audit(PersonalClassActor a, Guid id, string action, string reason, DateTime now) => db.AdminAuditLogs.Add(AdminAuditLog.Create(
        a.UserId, null, a.SessionId, "personal_class.request." + action, "PersonalClassRequest", id.ToString(), null, reason,
        now, a.CorrelationId, AdminAuditResult.Succeeded, null, null));
    private static PersonalClassRequestInput Normalize(PersonalClassRequestInput input) => input with
    { Title = input.Title.Trim(), Purpose = input.Purpose.Trim(), Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim() };
    private static PersonalClassRequestDto Map(PersonalClassRequest r, Guid actor) => new(r.Id, r.RequesterUserId,
        new(r.Title, r.Purpose, r.Description, r.SuggestedCapacity, r.SuggestedManagerUserId, r.SuggestedTeacherUserId), r.Status,
        Convert.ToBase64String(r.RowVersion), r.CreatedAtUtc, r.SubmittedAtUtc, r.DecidedAtUtc, r.DecisionReason, r.CreatedClassId, r.FinalManagerUserId,
        new(actor == r.RequesterUserId && r.CanEdit, actor == r.RequesterUserId && r.CanEdit, actor == r.RequesterUserId && r.CanCancel,
            actor == r.RequesterUserId && r.Status == PersonalClassRequestStatus.NeedsChanges));
    internal static void Version(byte[] actual, string expected)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(expected); } catch (Exception e) when (e is FormatException or ArgumentNullException) { throw Conflict(); }
        if (!actual.AsSpan().SequenceEqual(bytes)) throw new AcademicException(AcademicErrorCodes.PersonalClassVersionConflict, "نسخه تغییر کرده است؛ وضعیت جاری را دوباره دریافت کنید.");
    }
    internal static AcademicException Conflict() => new(AcademicErrorCodes.PersonalClassRequestConflict, "عملیات با وضعیت جاری سازگار نیست.");
    internal static AcademicException NotFound() => new(AcademicErrorCodes.PersonalClassNotFound, "کلاس یا درخواست در دسترس نیست.");
}
