using MakanApp.Application.Notification;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Notification;

public sealed partial class EfNotificationPreferenceStore(MakanDbContext dbContext) :
    INotificationPreferenceStore,
    IPushDeviceStore,
    INotificationDeliveryStore
{
    public Task<bool> HasActiveOrganizationMembershipAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships.AnyAsync(
            membership => membership.UserId == userId &&
                          membership.OrganizationId == organizationId &&
                          membership.Status == MembershipStatus.Active &&
                          membership.EndedAtUtc == null &&
                          dbContext.Organizations.Any(organization => organization.Id == organizationId &&
                              organization.Status == OrganizationStatus.Active),
            cancellationToken);

    public Task<NotificationPreference?> GetPreferenceForUpdateAsync(
        Guid userId,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences.SingleOrDefaultAsync(
            preference => preference.UserId == userId &&
                          preference.OrganizationId == organizationId &&
                          preference.Category == category,
            cancellationToken);

    public void Add(NotificationPreference preference) =>
        dbContext.NotificationPreferences.Add(preference);

    public void SetOriginalRowVersion(NotificationPreference preference, byte[] rowVersion) =>
        dbContext.Entry(preference).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public Task<bool> IsActiveSessionAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.AnyAsync(
            session => session.Id == sessionId &&
                       session.UserId == userId &&
                       session.RevokedAtUtc == null &&
                       session.ExpiresAtUtc > nowUtc && session.AbsoluteExpiresAtUtc > nowUtc &&
                       dbContext.Users.Any(user => user.Id == userId && user.AccountStatus == UserAccountStatus.Active),
            cancellationToken);

    public Task<PushDeviceRegistration?> GetForUpdateAsync(
        Guid registrationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.PushDeviceRegistrations.SingleOrDefaultAsync(
            registration => registration.Id == registrationId &&
                            registration.UserId == userId &&
                            registration.RevokedAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetActivePushDeviceIdsAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        await (
            from registration in dbContext.PushDeviceRegistrations.AsNoTracking()
            join session in dbContext.UserSessions.AsNoTracking()
                on new { Id = registration.UserSessionId, registration.UserId }
                equals new { session.Id, session.UserId }
            where registration.UserId == userId &&
                  registration.RevokedAtUtc == null &&
                  session.RevokedAtUtc == null &&
                  session.ExpiresAtUtc > nowUtc && session.AbsoluteExpiresAtUtc > nowUtc &&
                  dbContext.Users.Any(user => user.Id == userId && user.AccountStatus == UserAccountStatus.Active)
            select registration.Id)
            .ToArrayAsync(cancellationToken);

    public Task<Guid?> GetVerifiedMobileCredentialIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserCredentials
            .AsNoTracking()
            .Where(credential => credential.UserId == userId &&
                                 credential.Kind == CredentialKind.MobilePhone)
            .Select(credential => (Guid?)credential.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public void AddRange(IEnumerable<NotificationDeliveryAttempt> attempts) =>
        dbContext.NotificationDeliveryAttempts.AddRange(attempts);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.ConcurrencyConflict,
                "تنظیم اعلان هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("UX_PushDeviceRegistrations_ActiveToken", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.DeviceConflict,
                "این دستگاه در حال حاضر قابل ثبت نیست.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new NotificationPreferenceException(
                NotificationPreferenceErrorCodes.ConcurrencyConflict,
                "تنظیم اعلان هم‌زمان ایجاد یا تغییر کرده است.");
        }
    }
}
