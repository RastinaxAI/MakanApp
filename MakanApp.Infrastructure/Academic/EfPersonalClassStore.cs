using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Academic;

public sealed class PersonalClassOptions
{
    public int InvitationLifetimeHours { get; set; } = 168;
}

public sealed partial class EfPersonalClassStore(MakanDbContext db, EfCommercialStore commerce, IOptions<PersonalClassOptions> options,
    RealtimeInvalidationWriter realtime, OfficialSpaceEventWriter spaces, EfOfficialSpacesStore officialSpaces,
    CommercialActorAccess actors, TimeProvider clock) : IPersonalClassStore, IPersonalClassLifecycleStore
{
    public Task<PersonalClassDto> CreateAsync(Guid host, CreatePersonalClassCommand command, DateTime now, CancellationToken ct) =>
        throw Error(AcademicErrorCodes.PersonalClassReviewRequired, "برای تشکیل تازه ابتدا درخواست را ثبت و ارسال کنید.");

    public async Task<IReadOnlyList<PersonalClassDto>> ListAsync(Guid userId, CancellationToken ct) =>
        (await ClassesAsync(userId, 1, 100, ct)).Items;

    public async Task<PersonalClassDto> GetAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        if (!await commerce.CanAccessPersonalClassAsync(userId, classId, ct)) throw NotFound();
        return Map(await db.PersonalClasses.AsNoTracking().SingleAsync(c => c.Id == classId, ct), userId);
    }

    // قراردادهای قدیمیِ بدون نسخه، مجوز mutation تازه ایجاد نمی‌کنند.
    public Task<PersonalClassDto> ArchiveAsync(Guid userId, Guid classId, DateTime now, CancellationToken ct) =>
        throw Error(AcademicErrorCodes.PersonalClassVersionConflict, "نسخه و دلیل بایگانی لازم است.");
    public Task EndParticipantAsync(Guid userId, Guid classId, Guid participantId, DateTime now, CancellationToken ct) =>
        throw Error(AcademicErrorCodes.PersonalClassVersionConflict, "نسخه و دلیل پایان عضویت لازم است.");

    public async Task<PersonalClassInvitationDto> InviteAsync(Guid actor, Guid classId, InvitePersonalClassStudentCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await LockClass(classId, ct); await RequireManager(actor, entity, ct);
        var existing = await db.PersonalClassInvitations.SingleOrDefaultAsync(i => i.PersonalClassId == classId && i.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.DestinationUserId != command.DestinationUserId || existing.LocalRoles != command.LocalRoles || existing.DelegatedCapabilities != command.DelegatedCapabilities) throw Conflict();
            await tx.CommitAsync(ct); return Invitation(existing, now);
        }
        RequireAdmissions(entity);
        await RequireHosting(entity, now, ct);
        if (!await db.Users.AnyAsync(u => u.Id == command.DestinationUserId && u.AccountStatus == UserAccountStatus.Active, ct)) throw NotFound();
        if (await ActiveMembers(classId).AnyAsync(m => m.StudentUserId == command.DestinationUserId, ct)) throw Conflict();
        var pending = await db.PersonalClassInvitations.SingleOrDefaultAsync(i => i.PersonalClassId == classId && i.DestinationUserId == command.DestinationUserId &&
            i.AcceptedAtUtc == null && i.DeclinedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct);
        if (pending is not null) throw Conflict();
        var invitation = PersonalClassInvitation.Create(classId, command.DestinationUserId, command.ClientOperationId, now, now.AddHours(options.Value.InvitationLifetimeHours), command.LocalRoles, command.DelegatedCapabilities);
        db.PersonalClassInvitations.Add(invitation); await SaveClass(entity, now, ct); await tx.CommitAsync(ct); return Invitation(invitation, now);
    }

    public async Task<IReadOnlyList<PersonalClassInvitationDto>> InvitationsAsync(Guid userId, CancellationToken ct) =>
        (await InvitationsPageAsync(userId, 1, 100, ct)).Items;

    public async Task<PersonalClassDto> AcceptAsync(Guid userId, Guid invitationId, DateTime now, CancellationToken ct)
    {
        var classId = await db.PersonalClassInvitations.AsNoTracking().Where(i => i.Id == invitationId && i.DestinationUserId == userId)
            .Select(i => (Guid?)i.PersonalClassId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = await LockClass(classId, ct);
        var invitation = await db.PersonalClassInvitations.SingleAsync(i => i.Id == invitationId, ct);
        var member = await ActiveMembers(classId).SingleOrDefaultAsync(m => m.StudentUserId == userId, ct);
        if (invitation.AcceptedAtUtc.HasValue || invitation.ClosureReason == "MembershipEstablished")
        {
            if (member is null) throw Error(AcademicErrorCodes.PersonalClassMembershipInactive, "عضویت پایان یافته است.");
            await tx.CommitAsync(ct); return Map(entity, userId);
        }
        if (invitation.StatusAt(now) != "Pending") throw Conflict();
        RequireAdmissions(entity);
        if (member is null)
        {
            if (invitation.LocalRoles.HasFlag(PersonalClassLocalRoles.Learner)) await RequireLearnerSeat(entity, userId, now, ct);
            else await RequireHosting(entity, now, ct);
            invitation.Accept(userId, now);
            member = PersonalClassParticipant.Join(invitation, now); db.PersonalClassParticipants.Add(member);
        }
        else invitation.Accept(userId, now);
        await CloseAdmissionIntents(entity.Id, userId, member.Id, invitation.Id, now, ct, member.LocalRoles.HasFlag(PersonalClassLocalRoles.Learner));
        await SaveClass(entity, now, ct); await tx.CommitAsync(ct); return Map(entity, userId);
    }

    public async Task<IReadOnlyList<PersonalClassParticipantDto>> ParticipantsAsync(Guid userId, Guid classId, CancellationToken ct) =>
        (await MembersAsync(userId, classId, 1, 100, ct)).Items.Select(p => new PersonalClassParticipantDto(p.Id, p.UserId, p.JoinedAtUtc, p.EndedAtUtc == null)).ToArray();

    private async Task<PersonalClass> LockClass(Guid classId, CancellationToken ct)
    {
        var host = await db.PersonalClasses.AsNoTracking().Where(c => c.Id == classId).Select(c => (Guid?)c.HostUserId).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(host), ct, requireActive: false);
        return await db.PersonalClasses.FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={classId}")
            .SingleAsync(ct);
    }
    private IQueryable<PersonalClassParticipant> ActiveMembers(Guid id) => db.PersonalClassParticipants.Where(m => m.PersonalClassId == id && m.EndedAtUtc == null);
    private async Task RequireManager(Guid actor, PersonalClass c, CancellationToken ct)
    {
        if (c.ManagerUserId != actor || !await ActiveMembers(c.Id).AnyAsync(m => m.StudentUserId == actor && (m.LocalRoles & PersonalClassLocalRoles.Manager) != 0, ct)) throw NotFound();
    }
    private static void RequireAdmissions(PersonalClass c)
    {
        if (c.State != PersonalClassState.Active || !c.Capacity.HasValue)
            throw Error(AcademicErrorCodes.PersonalClassNotReady, "کلاس فعال با ظرفیت صریح لازم است.");
    }
    private async Task RequireHosting(PersonalClass c, DateTime now, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == c.HostUserId && u.AccountStatus == UserAccountStatus.Active, ct))
            throw EfCommercialStore.Error(CommerceErrorCodes.SubscriptionInactive);
        var subscription = await commerce.CurrentAsync(SubscriptionOwner.Individual(c.HostUserId), now, ct) ?? throw EfCommercialStore.Error(CommerceErrorCodes.SubscriptionInactive);
        var version = await commerce.VersionAsync(subscription.PlanVersionId, ct);
        if (!version.Entitlements.Any(e => e.Key == EntitlementKey.PersonalClassHosting && e.Enabled)) throw EfCommercialStore.Error(CommerceErrorCodes.FeatureNotEntitled);
    }
    private async Task RequireLearnerSeat(PersonalClass c, Guid userId, DateTime now, CancellationToken ct)
    {
        RequireAdmissions(c);
        await RequireHosting(c, now, ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active, ct)) throw NotFound();
        var count = await ActiveMembers(c.Id).CountAsync(m => (m.LocalRoles & PersonalClassLocalRoles.Learner) != 0, ct);
        if (count >= c.Capacity) throw Error(AcademicErrorCodes.PersonalClassCapacityReached, "ظرفیت آموزشی کلاس تکمیل است.");
        var consumesSeat = !await (from p in db.PersonalClassParticipants join k in db.PersonalClasses on p.PersonalClassId equals k.Id
            where k.HostUserId == c.HostUserId && k.ArchivedAtUtc == null && p.EndedAtUtc == null && p.StudentUserId == userId &&
                (p.LocalRoles & PersonalClassLocalRoles.Learner) != 0 select p.Id).AnyAsync(ct);
        await commerce.RequireCapacityAsync(SubscriptionOwner.Individual(c.HostUserId), EntitlementKey.PersonalClassHosting, QuotaKey.ActiveLearners, consumesSeat ? 1 : 0, now, ct);
    }
    private async Task CloseAdmissionIntents(Guid classId, Guid userId, Guid memberId, Guid? acceptedInvite, DateTime now, CancellationToken ct, bool learner = true)
    {
        foreach (var pending in await db.PersonalClassJoinRequests.Where(j => j.PersonalClassId == classId && j.UserId == userId && j.Status == PersonalClassJoinRequestStatus.Pending).ToArrayAsync(ct))
            if (pending.Status == PersonalClassJoinRequestStatus.Pending) pending.Close(learner ? PersonalClassJoinRequestStatus.Approved : PersonalClassJoinRequestStatus.Cancelled,
                "MembershipEstablished", now, learner ? memberId : null);
        foreach (var pending in await db.PersonalClassInvitations.Where(i => i.PersonalClassId == classId && i.DestinationUserId == userId && i.Id != acceptedInvite &&
            i.AcceptedAtUtc == null && i.DeclinedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now).ToArrayAsync(ct))
            pending.Close(false, "MembershipEstablished", now);
    }
    private async Task SaveClass(PersonalClass c, DateTime now, CancellationToken ct)
    {
        await realtime.CapturePersonalClassesAsync(ct);
        if (c.FormationRequestId.HasValue || await db.OfficialSpaceProvisions.AnyAsync(p => p.PersonalClassId == c.Id, ct))
            spaces.PersonalClassChanged(c.Id, now);
        await db.SaveChangesAsync(ct);
    }
    private void Audit(PersonalClassActor actor, Guid classId, string action, string reason, DateTime now) => db.AdminAuditLogs.Add(AdminAuditLog.Create(
        actor.UserId, null, actor.SessionId, "personal_class." + action, "PersonalClass", classId.ToString(), null, reason, now,
        actor.CorrelationId, AdminAuditResult.Succeeded, null, null));
    private static PersonalClassDto Map(PersonalClass c, Guid actor) => new(c.Id, c.HostUserId, c.Title, c.ArchivedAtUtc.HasValue, c.HostUserId == actor, false, "NotAvailable", c.CreatedAtUtc)
    {
        ManagerUserId = c.ManagerUserId, Capacity = c.Capacity, AcceptingJoinRequests = c.AcceptingJoinRequests, State = c.State.ToString(),
        MigrationState = c.MigrationState.ToString(), Version = Convert.ToBase64String(c.RowVersion),
        Capabilities = new(c.ManagerUserId == actor && c.State == PersonalClassState.Active, c.ManagerUserId == actor && c.State != PersonalClassState.Archived,
            c.ManagerUserId == actor && c.State != PersonalClassState.Archived, false, false, false, c.ManagerUserId == actor && c.State != PersonalClassState.Archived, false)
    };
    private static PersonalClassInvitationDto Invitation(PersonalClassInvitation i, DateTime now) => new(i.Id, i.PersonalClassId, i.DestinationUserId, i.ExpiresAtUtc, i.AcceptedAtUtc.HasValue)
        { Status = i.StatusAt(now), Version = Convert.ToBase64String(i.RowVersion), LocalRoles = i.LocalRoles, DelegatedCapabilities = i.DelegatedCapabilities };
    private static PersonalClassMembershipDto Member(PersonalClassParticipant m) => new(m.Id, m.StudentUserId, m.LocalRoles, m.DelegatedCapabilities,
        m.EndedAtUtc.HasValue ? "Ended" : "Active", m.JoinedAtUtc, m.EndedAtUtc, m.EndReason, Convert.ToBase64String(m.RowVersion));
    private static PersonalClassJoinDto Join(PersonalClassJoinRequest j) => new(j.Id, j.PersonalClassId, j.UserId, j.Status, j.CreatedAtUtc, j.ClosedAtUtc, j.ClosureReason, j.MembershipId, Convert.ToBase64String(j.RowVersion));
    private static PersonalClassTransferDto Transfer(PersonalClassManagerTransfer t) => new(t.Id, t.PersonalClassId, t.TargetUserId, t.PreviousManagerUserId, t.Status, t.Reason, t.CreatedAtUtc, Convert.ToBase64String(t.RowVersion));
    private static void Version(byte[] actual, string expected) => EfPersonalClassFormationStore.Version(actual, expected);
    private static AcademicException Conflict() => EfPersonalClassFormationStore.Conflict();
    private static AcademicException NotFound() => EfPersonalClassFormationStore.NotFound();
    private static AcademicException Error(string code, string message) => new(code, message);
}
