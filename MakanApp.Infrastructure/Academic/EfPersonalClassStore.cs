using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Academic;

public sealed class PersonalClassOptions
{
    public int InvitationLifetimeHours { get; set; } = 168;
}

public sealed class EfPersonalClassStore(MakanDbContext db, EfCommercialStore commerce, IOptions<PersonalClassOptions> options) : IPersonalClassStore
{
    public async Task<PersonalClassDto> CreateAsync(Guid host, CreatePersonalClassCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owner = SubscriptionOwner.Individual(host);
        await commerce.LockOwnerAsync(owner, ct);
        var existing = await db.PersonalClasses.SingleOrDefaultAsync(c => c.HostUserId == host && c.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.Title != command.Title) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
            await tx.CommitAsync(ct); return Map(existing, host);
        }
        await commerce.RequireCapacityAsync(owner, EntitlementKey.PersonalClassHosting, QuotaKey.ActiveClasses, 1, now, ct);
        var entity = PersonalClass.Create(host, command.Title, command.ClientOperationId, now);
        db.PersonalClasses.Add(entity); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(entity, host);
    }

    public async Task<IReadOnlyList<PersonalClassDto>> ListAsync(Guid userId, CancellationToken ct) =>
        (await db.PersonalClasses.AsNoTracking().Where(c => c.HostUserId == userId || db.PersonalClassParticipants.Any(p => p.PersonalClassId == c.Id
            && p.StudentUserId == userId && p.EndedAtUtc == null)).OrderByDescending(c => c.CreatedAtUtc).Take(100).ToArrayAsync(ct))
            .Select(c => Map(c, userId)).ToArray();

    public async Task<PersonalClassDto> GetAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        if (!await commerce.CanAccessPersonalClassAsync(userId, classId, ct)) throw NotFound();
        return Map(await db.PersonalClasses.AsNoTracking().SingleAsync(c => c.Id == classId, ct), userId);
    }

    public async Task<PersonalClassDto> ArchiveAsync(Guid userId, Guid classId, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(userId), ct);
        var entity = await db.PersonalClasses.SingleOrDefaultAsync(c => c.Id == classId && c.HostUserId == userId, ct) ?? throw NotFound();
        entity.Archive(now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(entity, userId);
    }

    public async Task<PersonalClassInvitationDto> InviteAsync(Guid host, Guid classId, InvitePersonalClassStudentCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(host), ct);
        var entity = await db.PersonalClasses.SingleOrDefaultAsync(c => c.Id == classId && c.HostUserId == host && c.ArchivedAtUtc == null, ct) ?? throw NotFound();
        var existing = await db.PersonalClassInvitations.SingleOrDefaultAsync(i => i.PersonalClassId == classId && i.ClientOperationId == command.ClientOperationId, ct);
        if (existing is not null)
        {
            if (existing.DestinationUserId != command.DestinationUserId) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
            await tx.CommitAsync(ct); return Invitation(existing);
        }
        var subscription = await commerce.CurrentAsync(SubscriptionOwner.Individual(host), now, ct) ?? throw EfCommercialStore.Error(CommerceErrorCodes.SubscriptionInactive);
        var version = await commerce.VersionAsync(subscription.PlanVersionId, ct);
        if (!version.Entitlements.Any(e => e.Key == EntitlementKey.PersonalClassHosting && e.Enabled)) throw EfCommercialStore.Error(CommerceErrorCodes.FeatureNotEntitled);
        if (!await db.Users.AnyAsync(u => u.Id == command.DestinationUserId && u.AccountStatus == UserAccountStatus.Active, ct)) throw NotFound();
        var invitation = PersonalClassInvitation.Create(entity.Id, command.DestinationUserId, command.ClientOperationId, now,
            now.AddHours(options.Value.InvitationLifetimeHours));
        db.PersonalClassInvitations.Add(invitation); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Invitation(invitation);
    }

    public async Task<IReadOnlyList<PersonalClassInvitationDto>> InvitationsAsync(Guid userId, CancellationToken ct) =>
        (await db.PersonalClassInvitations.AsNoTracking().Where(i => i.DestinationUserId == userId && i.RevokedAtUtc == null)
            .OrderByDescending(i => i.CreatedAtUtc).Take(100).ToArrayAsync(ct)).Select(Invitation).ToArray();

    public async Task<PersonalClassDto> AcceptAsync(Guid userId, Guid invitationId, DateTime now, CancellationToken ct)
    {
        // ترتیب قفل برای ظرفیت همهٔ کلاس‌های یک میزبان یکسان است.
        var header = await (from i in db.PersonalClassInvitations.AsNoTracking() join c in db.PersonalClasses.AsNoTracking()
            on i.PersonalClassId equals c.Id where i.Id == invitationId && i.DestinationUserId == userId select new { c.Id, c.HostUserId }).SingleOrDefaultAsync(ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owner = SubscriptionOwner.Individual(header.HostUserId);
        foreach (var id in new[] { header.HostUserId, userId }.Distinct().Order())
            await commerce.LockOwnerAsync(SubscriptionOwner.Individual(id), ct);
        var entity = await db.PersonalClasses.SingleAsync(c => c.Id == header.Id, ct);
        var invitation = await db.PersonalClassInvitations.SingleAsync(i => i.Id == invitationId && i.DestinationUserId == userId, ct);
        if (entity.ArchivedAtUtc.HasValue) throw NotFound();
        if (invitation.AcceptedAtUtc.HasValue)
        {
            if (!await db.PersonalClassParticipants.AnyAsync(p => p.InvitationId == invitationId && p.EndedAtUtc == null, ct)) throw NotFound();
            await tx.CommitAsync(ct); return Map(entity, userId);
        }
        var already = await db.PersonalClassParticipants.AnyAsync(p => p.PersonalClassId == entity.Id && p.StudentUserId == userId && p.EndedAtUtc == null, ct);
        if (already) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
        var consumesSeat = !await (from p in db.PersonalClassParticipants join c in db.PersonalClasses on p.PersonalClassId equals c.Id
            where c.HostUserId == header.HostUserId && c.ArchivedAtUtc == null && p.EndedAtUtc == null && p.StudentUserId == userId select p.Id).AnyAsync(ct);
        await commerce.RequireCapacityAsync(owner, EntitlementKey.PersonalClassHosting, QuotaKey.ActiveLearners, consumesSeat ? 1 : 0, now, ct);
        try { invitation.Accept(userId, now); } catch (InvalidOperationException) { throw NotFound(); }
        db.PersonalClassParticipants.Add(PersonalClassParticipant.Join(invitation, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(entity, userId);
    }

    public async Task<IReadOnlyList<PersonalClassParticipantDto>> ParticipantsAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        var entity = await GetAsync(userId, classId, ct);
        return await db.PersonalClassParticipants.AsNoTracking().Where(p => p.PersonalClassId == classId && (entity.IsHost || p.StudentUserId == userId))
            .OrderBy(p => p.JoinedAtUtc).Take(100).Select(p => new PersonalClassParticipantDto(p.Id, p.StudentUserId, p.JoinedAtUtc, p.EndedAtUtc == null)).ToArrayAsync(ct);
    }

    public async Task EndParticipantAsync(Guid userId, Guid classId, Guid participantId, DateTime now, CancellationToken ct)
    {
        var entity = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId, ct) ?? throw NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(entity.HostUserId), ct, requireActive: false);
        var participant = await db.PersonalClassParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.PersonalClassId == classId
            && (entity.HostUserId == userId || p.StudentUserId == userId), ct) ?? throw NotFound();
        participant.End(now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private static PersonalClassDto Map(PersonalClass entity, Guid userId) => new(entity.Id, entity.HostUserId, entity.Title,
        entity.ArchivedAtUtc.HasValue, entity.HostUserId == userId, false, "NotAvailable", entity.CreatedAtUtc);
    private static PersonalClassInvitationDto Invitation(PersonalClassInvitation i) => new(i.Id, i.PersonalClassId, i.DestinationUserId, i.ExpiresAtUtc, i.AcceptedAtUtc.HasValue);
    private static CommerceException NotFound() => EfCommercialStore.Error(CommerceErrorCodes.ResourceNotFound);
}
