using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class NotificationPreferenceEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 9800000;

    [Fact]
    public async Task DefaultsUpdatesAndScopesRemainIsolatedPerUserAndOrganization()
    {
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        UseBearerToken(ownerClient, owner.AccessToken);
        var organizationA = await factory.CreateOrganizationAsync($"Preference A {Guid.NewGuid():N}");
        var organizationB = await factory.CreateOrganizationAsync($"Preference B {Guid.NewGuid():N}");
        _ = await factory.CreateMembershipAsync(owner.User.Id, organizationA, OrganizationRole.Student);
        _ = await factory.CreateMembershipAsync(owner.User.Id, organizationB, OrganizationRole.Student);

        var defaults = await GetPreferencesAsync(ownerClient, null);
        Assert.Equal(7, defaults.Count);
        var assignmentDefault = Assert.Single(defaults, item =>
            item.Category == NotificationPreferenceCategory.Assignments);
        Assert.True(assignmentDefault.InAppEnabled);
        Assert.True(assignmentDefault.PushEnabled);
        Assert.False(assignmentDefault.SmsEnabled);
        Assert.False(assignmentDefault.IsExplicit);

        _ = await PutPreferenceAsync(ownerClient, null, NotificationPreferenceCategory.Assignments,
            Command(push: false, sms: false));
        _ = await PutPreferenceAsync(ownerClient, organizationA, NotificationPreferenceCategory.Assignments,
            Command(push: true, sms: true));
        _ = await PutPreferenceAsync(ownerClient, organizationB, NotificationPreferenceCategory.Assignments,
            Command(push: false, sms: false));

        Assert.False(Assert.Single(await GetPreferencesAsync(ownerClient, null), item =>
            item.Category == NotificationPreferenceCategory.Assignments).PushEnabled);
        var a = Assert.Single(await GetPreferencesAsync(ownerClient, organizationA), item =>
            item.Category == NotificationPreferenceCategory.Assignments);
        Assert.True(a.PushEnabled);
        Assert.True(a.SmsEnabled);
        Assert.False(Assert.Single(await GetPreferencesAsync(ownerClient, organizationB), item =>
            item.Category == NotificationPreferenceCategory.Assignments).PushEnabled);

        using var otherClient = factory.CreateClient();
        var other = await CreateAuthenticatedUserAsync(otherClient);
        UseBearerToken(otherClient, other.AccessToken);
        var otherPersonal = Assert.Single(await GetPreferencesAsync(otherClient, null), item =>
            item.Category == NotificationPreferenceCategory.Assignments);
        Assert.True(otherPersonal.PushEnabled);
        Assert.False(otherPersonal.IsExplicit);
        using var forbiddenScope = await otherClient.GetAsync(
            $"/api/v1/notification-preferences?organizationId={organizationA}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenScope.StatusCode);
    }

    [Fact]
    public async Task MandatoryValidationInvalidTimeCategoryAndConcurrencyAreEnforced()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);

        using var mandatory = await client.PutAsJsonAsync(
            $"/api/v1/notification-preferences/{NotificationPreferenceCategory.AccountSecurity}",
            Command(push: false, sms: false),
            JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mandatory.StatusCode);

        using var invalidCategory = await client.PutAsJsonAsync(
            "/api/v1/notification-preferences/999",
            Command(push: true, sms: false),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);

        using var invalidTime = await client.PutAsJsonAsync(
            $"/api/v1/notification-preferences/{NotificationPreferenceCategory.Results}",
            Command(
                push: true,
                sms: false,
                quiet: true,
                start: new TimeOnly(22, 0),
                end: new TimeOnly(22, 0),
                timeZoneId: "UTC"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTime.StatusCode);

        var created = await PutPreferenceAsync(
            client, null, NotificationPreferenceCategory.Results, Command(push: true, sms: false));
        var updated = await PutPreferenceAsync(
            client, null, NotificationPreferenceCategory.Results,
            Command(push: false, sms: false, expectedRowVersion: created.RowVersion));
        Assert.NotEqual(created.RowVersion, updated.RowVersion);
        using var stale = await client.PutAsJsonAsync(
            $"/api/v1/notification-preferences/{NotificationPreferenceCategory.Results}",
            Command(push: true, sms: false, expectedRowVersion: created.RowVersion),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task PreferenceAndDeviceEndpointsRequireAuthentication()
    {
        using var client = factory.CreateClient();

        using var preference = await client.GetAsync("/api/v1/notification-preferences");
        using var device = await client.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, Token()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, preference.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, device.StatusCode);
    }

    [Fact]
    public async Task PushRegistrationIsOwnedIdempotentRevocableAndDoesNotSerializeToken()
    {
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        UseBearerToken(ownerClient, owner.AccessToken);
        using var otherClient = factory.CreateClient();
        var other = await CreateAuthenticatedUserAsync(otherClient);
        UseBearerToken(otherClient, other.AccessToken);
        var token = Token();

        using var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, token),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var rawJson = await response.Content.ReadAsStringAsync();
        var registered = JsonSerializer.Deserialize<PushDeviceRegistrationResult>(rawJson, JsonOptions)!;
        Assert.DoesNotContain(token, rawJson, StringComparison.Ordinal);
        Assert.True(await factory.IsPushDeviceTokenStoredAsync(registered.Id, token));

        using var retryResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, token),
            JsonOptions);
        retryResponse.EnsureSuccessStatusCode();
        var retry = (await retryResponse.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!;
        Assert.Equal(registered.Id, retry.Id);

        using var conflict = await otherClient.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, token),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var concealed = await otherClient.DeleteAsync($"/api/v1/notifications/devices/{registered.Id}");
        Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
        using var revoked = await ownerClient.DeleteAsync($"/api/v1/notifications/devices/{registered.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
    }

    [Fact]
    public async Task RevokedSessionStopsFuturePushAndInternalNotificationStillExists()
    {
        factory.NotificationDeliveryProvider.Reset();
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        _ = await RegisterDeviceAsync(client, Token());
        using (var logout = await client.PostAsync("/api/v1/auth/logout", null))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        var nowUtc = Utc(2026, 10, 12, 12);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            var intent = CreateIntent(user.User.Id, null, NotificationType.AssignmentPublished, nowUtc);
            _ = await factory.EnqueueNotificationAsync(intent);
            _ = await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(intent.EventId, user.User.Id);
            var attempts = await factory.GetDeliveryAttemptsAsync(notificationId);

            Assert.Equal(1, await factory.CountNotificationsAsync(intent.EventId, user.User.Id));
            var push = Assert.Single(attempts, item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Suppressed, push.Status);
            Assert.Equal(NotificationDeliveryFailureReason.NoDevice, push.FailureReason);
            Assert.Empty(factory.NotificationDeliveryProvider.PushDeliveries);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task EnabledSmsWithoutVerifiedDestinationIsSafelySuppressed()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        _ = await PutPreferenceAsync(client, null, NotificationPreferenceCategory.Results,
            Command(push: false, sms: true));
        await factory.RemoveVerifiedMobileCredentialAsync(user.User.Id);
        var nowUtc = Utc(2026, 10, 12, 12);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            var intent = CreateIntent(
                user.User.Id, null, NotificationType.AssignmentGradeReleased, nowUtc);
            _ = await factory.EnqueueNotificationAsync(intent);
            _ = await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(intent.EventId, user.User.Id);
            var sms = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(notificationId),
                item => item.Channel == NotificationDeliveryChannel.Sms);

            Assert.Equal(NotificationDeliveryStatus.Suppressed, sms.Status);
            Assert.Equal(NotificationDeliveryFailureReason.NoSmsDestination, sms.FailureReason);
            Assert.Equal(1, await factory.CountNotificationsAsync(intent.EventId, user.User.Id));
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task EnabledChannelsDeliverSafePayloadAndUseCorrectOrganizationScope()
    {
        factory.NotificationDeliveryProvider.Reset();
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        var organizationA = await factory.CreateOrganizationAsync($"Delivery A {Guid.NewGuid():N}");
        var organizationB = await factory.CreateOrganizationAsync($"Delivery B {Guid.NewGuid():N}");
        _ = await factory.CreateMembershipAsync(user.User.Id, organizationA, OrganizationRole.Student);
        _ = await factory.CreateMembershipAsync(user.User.Id, organizationB, OrganizationRole.Student);
        _ = await RegisterDeviceAsync(client, Token());
        _ = await PutPreferenceAsync(client, organizationA, NotificationPreferenceCategory.Results,
            Command(push: false, sms: false));
        _ = await PutPreferenceAsync(client, organizationB, NotificationPreferenceCategory.Results,
            Command(push: true, sms: true));

        var nowUtc = Utc(2026, 10, 12, 13);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            var aIntent = CreateIntent(user.User.Id, organizationA, NotificationType.ExamGradeReleased, nowUtc);
            var bIntent = CreateIntent(user.User.Id, organizationB, NotificationType.ExamGradeReleased, nowUtc);
            _ = await factory.EnqueueNotificationAsync(aIntent);
            _ = await factory.EnqueueNotificationAsync(bIntent);
            _ = await factory.DrainNotificationOutboxAsync();
            var aNotificationId = await factory.GetNotificationIdAsync(aIntent.EventId, user.User.Id);
            var bNotificationId = await factory.GetNotificationIdAsync(bIntent.EventId, user.User.Id);

            var aAttempts = await factory.GetDeliveryAttemptsAsync(aNotificationId);
            Assert.All(aAttempts, attempt =>
                Assert.Equal(NotificationDeliveryStatus.Suppressed, attempt.Status));
            var concurrent = await factory.ProcessNotificationDeliveriesConcurrentlyAsync();
            Assert.True(concurrent.Sum() >= 1);
            var bAttempts = await factory.GetDeliveryAttemptsAsync(bNotificationId);
            Assert.All(bAttempts, attempt =>
                Assert.Equal(NotificationDeliveryStatus.Delivered, attempt.Status));
            var push = Assert.Single(factory.NotificationDeliveryProvider.PushDeliveries);
            var sms = Assert.Single(factory.NotificationDeliveryProvider.SmsDeliveries);
            Assert.Equal(bNotificationId, push.NotificationId);
            Assert.Equal(bNotificationId, sms.NotificationId);
            Assert.DoesNotContain("نمره", push.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("پاسخ", push.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("grader", JsonSerializer.Serialize(push), StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("makan://notifications/", push.DeepLink, StringComparison.Ordinal);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task CrossMidnightQuietHoursDefersExternalDeliveryButNotInternalNotification()
    {
        factory.NotificationDeliveryProvider.Reset();
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        _ = await RegisterDeviceAsync(client, Token());
        _ = await PutPreferenceAsync(client, null, NotificationPreferenceCategory.Results,
            Command(
                push: true,
                sms: false,
                quiet: true,
                start: new TimeOnly(22, 0),
                end: new TimeOnly(7, 0),
                timeZoneId: "UTC"));
        var nowUtc = Utc(2026, 10, 12, 23);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            var intent = CreateIntent(user.User.Id, null, NotificationType.AssignmentGradeReleased, nowUtc);
            _ = await factory.EnqueueNotificationAsync(intent);
            _ = await factory.DrainNotificationOutboxAsync();
            var notificationId = await factory.GetNotificationIdAsync(intent.EventId, user.User.Id);
            var push = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(notificationId),
                item => item.Channel == NotificationDeliveryChannel.Push);

            Assert.Equal(1, await factory.CountNotificationsAsync(intent.EventId, user.User.Id));
            Assert.Equal(NotificationDeliveryStatus.Deferred, push.Status);
            Assert.Equal(Utc(2026, 10, 13, 7), push.NextAttemptAtUtc);
            _ = await factory.ProcessNotificationDeliveriesAsync();
            push = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(notificationId),
                item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Deferred, push.Status);
            Assert.Equal(0, push.AttemptCount);

            factory.Clock.SetUtcNow(Utc(2026, 10, 13, 7));
            Assert.Equal(1, await factory.ProcessNotificationDeliveriesAsync());
            push = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(notificationId),
                item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Delivered, push.Status);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task TransientPermanentAndTimeoutOutcomesHaveBoundedRetry()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        _ = await RegisterDeviceAsync(client, Token());
        var nowUtc = Utc(2026, 10, 12, 14);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            factory.NotificationDeliveryProvider.Reset();
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderUnavailable);
            var transient = CreateIntent(user.User.Id, null, NotificationType.ExamPublished, nowUtc);
            _ = await factory.EnqueueNotificationAsync(transient);
            _ = await factory.DrainNotificationOutboxAsync();
            var transientId = await factory.GetNotificationIdAsync(transient.EventId, user.User.Id);
            for (var attemptNumber = 1; attemptNumber <= 3; attemptNumber++)
            {
                _ = await factory.ProcessNotificationDeliveriesAsync();
                factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(5));
            }

            var transientAttempt = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(transientId),
                item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Failed, transientAttempt.Status);
            Assert.Equal(3, transientAttempt.AttemptCount);

            factory.NotificationDeliveryProvider.Reset();
            factory.NotificationDeliveryProvider.PushResult = DeliveryProviderResult.Permanent(
                NotificationDeliveryFailureReason.ProviderRejected);
            var permanent = CreateIntent(
                user.User.Id, null, NotificationType.ExamPublished,
                factory.Clock.GetUtcNow().UtcDateTime);
            _ = await factory.EnqueueNotificationAsync(permanent);
            _ = await factory.DrainNotificationOutboxAsync();
            var permanentId = await factory.GetNotificationIdAsync(permanent.EventId, user.User.Id);
            _ = await factory.ProcessNotificationDeliveriesAsync();
            var permanentAttempt = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(permanentId),
                item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Failed, permanentAttempt.Status);
            Assert.Equal(1, permanentAttempt.AttemptCount);

            factory.NotificationDeliveryProvider.Reset();
            factory.NotificationDeliveryProvider.PushWaitsForCancellation = true;
            var timeout = CreateIntent(
                user.User.Id, null, NotificationType.ExamPublished,
                factory.Clock.GetUtcNow().UtcDateTime);
            _ = await factory.EnqueueNotificationAsync(timeout);
            _ = await factory.DrainNotificationOutboxAsync();
            var timeoutId = await factory.GetNotificationIdAsync(timeout.EventId, user.User.Id);
            _ = await factory.ProcessNotificationDeliveriesAsync();
            var timeoutAttempt = Assert.Single(
                await factory.GetDeliveryAttemptsAsync(timeoutId),
                item => item.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Deferred, timeoutAttempt.Status);
            Assert.Equal(NotificationDeliveryFailureReason.ProviderTimeout, timeoutAttempt.FailureReason);
            Assert.Equal(1, await factory.CountNotificationsAsync(timeout.EventId, user.User.Id));
        }
        finally
        {
            factory.NotificationDeliveryProvider.Reset();
            factory.Clock.UseSystemTime();
        }
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98912{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
    }

    private static async Task<IReadOnlyCollection<NotificationPreferenceItem>> GetPreferencesAsync(
        HttpClient client,
        Guid? organizationId)
    {
        var url = organizationId.HasValue
            ? $"/api/v1/notification-preferences?organizationId={organizationId}"
            : "/api/v1/notification-preferences";
        return (await client.GetFromJsonAsync<NotificationPreferenceItem[]>(url, JsonOptions))!;
    }

    private static async Task<NotificationPreferenceItem> PutPreferenceAsync(
        HttpClient client,
        Guid? organizationId,
        NotificationPreferenceCategory category,
        UpdateNotificationPreferenceCommand command)
    {
        var url = $"/api/v1/notification-preferences/{category}" +
                  (organizationId.HasValue ? $"?organizationId={organizationId}" : string.Empty);
        using var response = await client.PutAsJsonAsync(url, command, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotificationPreferenceItem>(JsonOptions))!;
    }

    private static async Task<PushDeviceRegistrationResult> RegisterDeviceAsync(
        HttpClient client,
        string token)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/notifications/devices",
            new RegisterPushDeviceCommand(PushDevicePlatform.Android, token),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!;
    }

    private static UpdateNotificationPreferenceCommand Command(
        bool push,
        bool sms,
        bool quiet = false,
        TimeOnly? start = null,
        TimeOnly? end = null,
        string? timeZoneId = null,
        string? expectedRowVersion = null) =>
        new(push, sms, quiet, start, end, timeZoneId, expectedRowVersion);

    private static NotificationIntent CreateIntent(
        Guid userId,
        Guid? organizationId,
        NotificationType type,
        DateTime occurredAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            NotificationCategory.Educational,
            type,
            type is NotificationType.AssignmentPublished
                ? NotificationResourceType.Assignment
                : type is NotificationType.ExamPublished
                    ? NotificationResourceType.Exam
                    : type is NotificationType.AssignmentGradeReleased
                        ? NotificationResourceType.SubmissionAttempt
                        : NotificationResourceType.ExamAttempt,
            Guid.NewGuid(),
            occurredAtUtc,
            [new NotificationRecipientIntent(
                userId,
                "اعلان آموزشی داخلی",
                "بدنه داخلی شامل جزئیات خصوصی آزمون و نمره نیست.")]);

    private static string Token() => $"test-device-token-{Guid.NewGuid():N}";

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
