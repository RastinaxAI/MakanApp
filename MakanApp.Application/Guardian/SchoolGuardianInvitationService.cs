using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Guardian;

public sealed record CreateSchoolGuardianInvitationCommand(Guid ClientOperationId, string? Username, string? PhoneNumber, DateTime ExpiresAtUtc, string Reason);
public sealed record SchoolGuardianInvitationResult(Guid Id, Guid OrganizationId, Guid LearnerOrganizationPersonId,
    InvitationStatus Status, DateTime ExpiresAtUtc, string Version, bool Replayed, string NextAction);

public interface ISchoolGuardianInvitationStore
{
    Task<Guid?> FindTargetAsync(string? normalizedUsername, string? normalizedPhone, CancellationToken ct);
    Task<Invitation?> FindOperationAsync(Guid organizationId, Guid operationId, CancellationToken ct);
    Task<Invitation?> FindPendingAsync(Guid organizationId, Guid learnerId, Guid targetUserId, CancellationToken ct);
    Task<Invitation?> GetAsync(Guid organizationId, Guid invitationId, CancellationToken ct);
    void Add(Invitation invitation);
}

public sealed class SchoolGuardianInvitationService(SchoolManagementAccessResolver access, IOrganizationPersonStore people,
    ISchoolGuardianInvitationStore invitations, ISchoolReadStore authority, TimeProvider clock)
{
    public async Task<SchoolGuardianInvitationResult> CreateAsync(Guid userId, Guid sessionId, Guid learnerId, CreateSchoolGuardianInvitationCommand command, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        var username = string.IsNullOrWhiteSpace(command.Username) ? null : command.Username.Trim().TrimStart('@').ToUpperInvariant();
        var phoneInput = string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber;
        string? phone = null;
        if ((username is null) == (phoneInput is null) || username?.Length is > 32 or < 3 || command.ClientOperationId == Guid.Empty ||
            command.ExpiresAtUtc.Kind != DateTimeKind.Utc) throw Invalid();
        if (phoneInput is not null)
        {
            if (!PhoneNumberNormalizer.TryNormalize(phoneInput, out var normalized)) throw Invalid();
            phone = normalized;
        }
        await using var tx = await people.BeginAsync(ct);
        if (!await authority.CanManageOrganizationAsync(context, ct)) throw Denied();
        var nowUtc = Now();
        var previous = await invitations.FindOperationAsync(context.OrganizationId!.Value, command.ClientOperationId, ct);
        var target = await invitations.FindTargetAsync(username, phone, ct) ?? throw Unavailable();
        if (previous is not null)
        {
            if (previous.GuardianLearnerOrganizationPersonId != learnerId || previous.DestinationUserId != target || previous.ExpiresAtUtc != command.ExpiresAtUtc) throw Conflict();
            await tx.CommitAsync(ct); return Map(previous, true);
        }
        if (command.ExpiresAtUtc <= nowUtc || command.ExpiresAtUtc > nowUtc.AddDays(30)) throw Invalid();
        var pending = await invitations.FindPendingAsync(context.OrganizationId.Value, learnerId, target, ct);
        var learner = await people.GetAsync(context.OrganizationId.Value, learnerId, true, ct);
        if (learner is null || !learner.Record.IsActive) throw Unavailable();
        if (pending is not null)
        {
            if (pending.GetEffectiveStatus(nowUtc) != InvitationStatus.Expired)
            {
                await tx.CommitAsync(ct); return Map(pending, true);
            }
            pending.Accept(Guid.Empty, Guid.Empty, nowUtc); await people.SaveAsync(ct);
        }
        var invitation = Invitation.CreateGuardianLink(context.OrganizationId.Value, learnerId, target, userId,
            command.ClientOperationId, nowUtc, command.ExpiresAtUtc);
        invitations.Add(invitation);
        await people.AuditAsync(context, "organization.guardian.invitation.create", "Invitation", invitation.Id, reason, nowUtc, null,
            $"learner={learnerId:D};target={target:D}", ct);
        await people.SaveAsync(ct); await tx.CommitAsync(ct);
        return Map(invitation, false);
    }

    public async Task<SchoolGuardianInvitationResult> RevokeAsync(Guid userId, Guid sessionId, Guid invitationId, OnboardingStateCommand command, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        await using var tx = await people.BeginAsync(ct);
        if (!await authority.CanManageOrganizationAsync(context, ct)) throw Denied();
        var invitation = await invitations.GetAsync(context.OrganizationId!.Value, invitationId, ct) ?? throw Unavailable();
        if (invitation.Status == InvitationStatus.Revoked) { await tx.CommitAsync(ct); return Map(invitation, true); }
        SchoolOnboardingValidation.Version(invitation.RowVersion, command.ExpectedRowVersion);
        if (invitation.Status != InvitationStatus.Pending) throw Conflict();
        invitation.Revoke(Now());
        await people.AuditAsync(context, "organization.guardian.invitation.revoke", "Invitation", invitation.Id, reason, Now(), "status=Pending", "status=Revoked", ct);
        await people.SaveAsync(ct); await tx.CommitAsync(ct);
        return Map(invitation, false);
    }

    private SchoolGuardianInvitationResult Map(Invitation invitation, bool replayed) => new(invitation.Id, invitation.OrganizationId,
        invitation.GuardianLearnerOrganizationPersonId!.Value, invitation.GetEffectiveStatus(Now()), DateTime.SpecifyKind(invitation.ExpiresAtUtc, DateTimeKind.Utc),
        Convert.ToBase64String(invitation.RowVersion), replayed,
        invitation.GetEffectiveStatus(Now()) == InvitationStatus.Pending ? "AWAIT_PARENT_CONFIRMATION" : "NONE");
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
    private static OrganizationException Invalid() => new(SchoolErrorCodes.Invalid, "فقط یکی از username یا تلفن معتبر، شناسه عملیات و انقضای UTC حداکثر ۳۰ روزه لازم است.");
    private static OrganizationException Unavailable() => new(SchoolErrorCodes.GuardianTargetUnavailable, "حساب یا فراگیر مجاز برای دعوت در دسترس نیست.");
    private static OrganizationException Conflict() => new(SchoolErrorCodes.GuardianInvitationConflict, "دعوت با عملیات یا وضعیت قبلی تعارض دارد.");
    private static OrganizationException Denied() => new(OrganizationErrorCodes.WorkspaceNotAllowed, "مجوز جاری مدیریت مدرسه لازم است.");
}
