using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed class OrganizationPersonService(
    SchoolManagementAccessResolver accessResolver, IOrganizationPersonStore store, TimeProvider clock)
    : IOrganizationPersonService
{
    public async Task<OrganizationPersonDto> CreateAsync(Guid userId, Guid sessionId,
        CreateOrganizationPersonCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (command.ClientOperationId == Guid.Empty) throw SchoolOnboardingValidation.Invalid("شناسه عملیات لازم است.");
        var first = SchoolOnboardingValidation.Name(command.FirstName);
        var last = SchoolOnboardingValidation.Name(command.LastName);
        var display = SchoolOnboardingValidation.DisplayName(command.DisplayName);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { first, last, display }))));
        await using var transaction = await store.BeginAsync(cancellationToken);
        var existing = await store.FindCreationAsync(actor.OrganizationId!.Value, command.ClientOperationId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Record.CreationRequestHash != hash) throw Conflict();
            await transaction.CommitAsync(cancellationToken);
            return Map(existing, true);
        }
        var now = UtcNow();
        var person = Person.Create(first, last, display, now);
        var record = OrganizationPerson.CreateOnboarded(actor.OrganizationId.Value, person.Id, command.ClientOperationId, hash, now);
        store.Add(person, record);
        await store.AuditAsync(actor, "organization.person.create", "OrganizationPerson", record.Id, reason, now, null, "status=Active", cancellationToken);
        await store.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(new OrganizationPersonRecord(record, person, false, 1));
    }

    public async Task<OrganizationPeoplePage> ListAsync(Guid userId, Guid sessionId, string? search,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        SchoolOnboardingValidation.Page(page, pageSize);
        search = search?.Trim();
        if (search?.Length > 100) throw SchoolOnboardingValidation.Invalid("عبارت جست‌وجو بیش از حد مجاز است.");
        var now = UtcNow();
        var result = await store.ListAsync(actor.OrganizationId!.Value, search, page, pageSize, cancellationToken);
        return new(now, page, pageSize, result.Total, result.Items.Select(item => Map(item)).ToArray());
    }

    public async Task<OrganizationPersonDto> GetAsync(Guid userId, Guid sessionId, Guid id, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await RequiredAsync(actor, id, false, cancellationToken));
    }

    public async Task<OrganizationPersonDto> UpdateAsync(Guid userId, Guid sessionId, Guid id,
        UpdateOrganizationPersonNamesCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var first = SchoolOnboardingValidation.Name(command.FirstName);
        var last = SchoolOnboardingValidation.Name(command.LastName);
        var display = SchoolOnboardingValidation.DisplayName(command.DisplayName);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        await using var transaction = await store.BeginAsync(cancellationToken);
        var item = await RequiredAsync(actor, id, true, cancellationToken);
        SchoolOnboardingValidation.Version(item.Record.RowVersion, command.ExpectedRowVersion);
        SchoolOnboardingValidation.Version(item.Person.RowVersion, command.ExpectedPersonRowVersion);
        if (!item.Record.IsActive || item.IsAccountLinked || item.ReferenceCount != 1)
            throw new OrganizationException(SchoolOnboardingErrorCodes.NamesProtected, "نام شخص حساب‌دار یا مشترک از مسیر مدرسه قابل ویرایش نیست.");
        item.Person.Update(first, last, display, UtcNow());
        await store.AuditAsync(actor, "organization.person.names.update", "OrganizationPerson", id, reason, UtcNow(), "names=protected", "names=updated", cancellationToken);
        await store.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(item);
    }

    public async Task<OrganizationPersonDto> EndAsync(Guid userId, Guid sessionId, Guid id,
        OnboardingStateCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        await using var transaction = await store.BeginAsync(cancellationToken);
        var item = await RequiredAsync(actor, id, true, cancellationToken);
        if (!item.Record.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(item, true);
        }
        SchoolOnboardingValidation.Version(item.Record.RowVersion, command.ExpectedRowVersion);
        if (await store.HasActiveEnrollmentsAsync(actor.OrganizationId!.Value, id, cancellationToken))
            throw new OrganizationException(SchoolOnboardingErrorCodes.PersonInUse, "ابتدا ثبت‌نام فعال را از جریان آموزشی پایان دهید.");
        item.Record.End(UtcNow());
        await store.AuditAsync(actor, "organization.person.end", "OrganizationPerson", id, reason, UtcNow(), "status=Active", "status=Ended", cancellationToken);
        await store.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(item);
    }

    public async Task<PersonLinkInvitationDto> InviteAsync(Guid userId, Guid sessionId, Guid id,
        CreatePersonLinkInvitationCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        if (command.ClientOperationId == Guid.Empty || command.DestinationUserId == Guid.Empty || command.ExpiresAtUtc.Kind != DateTimeKind.Utc)
            throw SchoolOnboardingValidation.Invalid("مقصد، شناسه عملیات و زمان UTC معتبر لازم است.");
        await using var transaction = await store.BeginAsync(cancellationToken);
        var previous = await store.FindLinkOperationAsync(actor.OrganizationId!.Value, command.ClientOperationId, cancellationToken);
        if (previous is not null)
        {
            if (previous.OrganizationPersonId != id || previous.DestinationUserId != command.DestinationUserId || previous.ExpiresAtUtc != command.ExpiresAtUtc)
                throw Conflict();
            await transaction.CommitAsync(cancellationToken);
            return Map(previous, true);
        }
        if (command.ExpiresAtUtc <= UtcNow() || command.ExpiresAtUtc > UtcNow().AddDays(30))
            throw SchoolOnboardingValidation.Invalid("زمان انقضای دعوت باید در ۳۰ روز آینده باشد.");
        // ترتیب قفل Invitation سپس OrganizationPerson با مسیر پذیرش یکسان است.
        var pending = await store.GetPendingLinkAsync(actor.OrganizationId.Value, id, cancellationToken);
        var item = await RequiredAsync(actor, id, true, cancellationToken);
        if (!item.Record.IsActive || item.IsAccountLinked)
            throw new OrganizationException(SchoolOnboardingErrorCodes.AccountLinkConflict, "پرونده برای اتصال جدید مجاز نیست.");
        SchoolOnboardingValidation.Version(item.Record.RowVersion, command.ExpectedRowVersion);
        if (await store.GetAvailableUserAsync(command.DestinationUserId, cancellationToken) is null)
            throw SchoolOnboardingValidation.NotFound();
        if (pending is not null)
        {
            if (pending.GetEffectiveStatus(UtcNow()) == InvitationStatus.Expired)
            {
                pending.Accept(Guid.Empty, Guid.Empty, UtcNow());
                await store.SaveAsync(cancellationToken);
            }
            else if (pending.DestinationUserId == command.DestinationUserId)
            {
                await transaction.CommitAsync(cancellationToken);
                return Map(pending, true);
            }
            else throw Conflict();
        }
        var createdAtUtc = UtcNow();
        if (command.ExpiresAtUtc <= createdAtUtc)
            throw SchoolOnboardingValidation.Invalid("زمان انقضای دعوت گذشته است.");
        var invitation = Invitation.CreatePersonLink(actor.OrganizationId.Value, id, command.DestinationUserId,
            userId, command.ClientOperationId, createdAtUtc, command.ExpiresAtUtc);
        store.Add(invitation);
        await store.AuditAsync(actor, "organization.person.link.request", "Invitation", invitation.Id, reason, createdAtUtc, null, $"person={id:D};target={command.DestinationUserId:D}", cancellationToken);
        await store.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(invitation);
    }

    public async Task<PersonLinkInvitationDto> RevokeInvitationAsync(Guid userId, Guid sessionId, Guid id,
        Guid invitationId, OnboardingStateCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        await using var transaction = await store.BeginAsync(cancellationToken);
        var invitation = await store.GetLinkInvitationAsync(actor.OrganizationId!.Value, invitationId, cancellationToken);
        if (invitation?.OrganizationPersonId != id) throw SchoolOnboardingValidation.NotFound();
        if (invitation.Status == InvitationStatus.Revoked)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(invitation, true);
        }
        SchoolOnboardingValidation.Version(invitation.RowVersion, command.ExpectedRowVersion);
        if (invitation.Status != InvitationStatus.Pending) throw Conflict();
        invitation.Revoke(UtcNow());
        await store.AuditAsync(actor, "organization.person.link.revoke", "Invitation", invitation.Id, reason, UtcNow(), "status=Pending", "status=Revoked", cancellationToken);
        await store.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(invitation);
    }

    private Task<OrganizationPersonRecord> RequiredAsync(AccessContext actor, Guid id, bool forUpdate, CancellationToken cancellationToken) =>
        RequiredCoreAsync(actor.OrganizationId!.Value, id, forUpdate, cancellationToken);
    private async Task<OrganizationPersonRecord> RequiredCoreAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken cancellationToken) =>
        await store.GetAsync(organizationId, id, forUpdate, cancellationToken) ?? throw SchoolOnboardingValidation.NotFound();
    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;
    private static OrganizationException Conflict() => new(SchoolOnboardingErrorCodes.Conflict, "درخواست با وضعیت یا عملیات قبلی تعارض دارد.");
    private static OrganizationPersonDto Map(OrganizationPersonRecord item, bool replayed = false) =>
        new(item.Record.Id, item.Record.OrganizationId, item.Person.Id, item.Person.FirstName, item.Person.LastName,
            item.Person.DisplayName, item.Record.Status, item.IsAccountLinked,
            item.Record.IsActive && !item.IsAccountLinked && item.ReferenceCount == 1,
            Convert.ToBase64String(item.Record.RowVersion), Convert.ToBase64String(item.Person.RowVersion), replayed);
    private static PersonLinkInvitationDto Map(Invitation item, bool replayed = false) =>
        new(item.Id, item.OrganizationPersonId!.Value, item.DestinationUserId, item.Role, item.Status,
            item.ExpiresAtUtc, Convert.ToBase64String(item.RowVersion), replayed);
}
