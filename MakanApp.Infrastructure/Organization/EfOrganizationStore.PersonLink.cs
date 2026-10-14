using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed partial class EfOrganizationStore
{
    public async Task CompletePersonLinkAsync(Invitation invitation, Guid userId, Guid sessionId,
        DateTime nowUtc, bool replayed, CancellationToken cancellationToken)
    {
        var record = await dbContext.OrganizationPersons
            .FromSqlInterpolated($"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {invitation.OrganizationId} AND [Id] = {invitation.OrganizationPersonId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (record is null || !record.IsActive || invitation.DestinationUserId != userId)
            throw SchoolOnboardingValidation.NotFound();
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId &&
            item.AccountStatus == UserAccountStatus.Active, cancellationToken);
        if (user is null || !user.IsProfileComplete || !user.PersonId.HasValue)
            throw new OrganizationException(SchoolOnboardingErrorCodes.AccountProfileRequired, "ابتدا پروفایل حساب خود را تکمیل کنید.");
        // قفل Person با ویرایش نام مشترک است؛ ثبت User و داده هویتی تغییری نمی‌کند.
        _ = await dbContext.Persons.FromSqlInterpolated($"SELECT * FROM [identity].[Persons] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {record.PersonId}")
            .SingleAsync(cancellationToken);
        if (await dbContext.Users.AnyAsync(item => item.PersonId == record.PersonId && item.Id != userId, cancellationToken))
            throw LinkConflict();
        if (replayed)
        {
            if (record.PersonId != user.PersonId.Value) throw LinkConflict();
            return;
        }
        var duplicate = await dbContext.OrganizationPersons
            .FromSqlInterpolated($"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {record.OrganizationId} AND [PersonId] = {user.PersonId.Value} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            .AnyAsync(item => item.Id != record.Id, cancellationToken);
        if (duplicate) throw LinkConflict();
        var originalPersonId = record.PersonId;
        record.LinkConsentingAccountPerson(user.PersonId.Value);
        dbContext.AdminAuditLogs.Add(AdminAuditLog.Create(userId, user.PersonId, sessionId,
            "organization.person.link.accept", "OrganizationPerson", record.Id.ToString("D"), record.OrganizationId,
            "پذیرش صریح دعوت اتصال پرونده توسط صاحب حساب", nowUtc, $"school-{Guid.NewGuid():N}",
            AdminAuditResult.Succeeded, $"person={originalPersonId:D}", $"person={record.PersonId:D};invitation={invitation.Id:D}"));
    }

    private static OrganizationException LinkConflict() => new(SchoolOnboardingErrorCodes.AccountLinkConflict,
        "پرونده به حساب دیگری متصل است یا پرونده فعال دیگری برای این حساب وجود دارد.");
}
