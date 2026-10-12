using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Notification;
using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class NotificationEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 9700000;

    [Fact]
    public async Task NotificationCenterScopesOwnershipPaginationFiltersAndReadState()
    {
        using var ownerClient = factory.CreateClient();
        var owner = await CreateAuthenticatedUserAsync(ownerClient);
        UseBearerToken(ownerClient, owner.AccessToken);
        using var otherClient = factory.CreateClient();
        var other = await CreateAuthenticatedUserAsync(otherClient);
        UseBearerToken(otherClient, other.AccessToken);
        var organizationA = await factory.CreateOrganizationAsync($"Notification A {Guid.NewGuid():N}");
        var organizationB = await factory.CreateOrganizationAsync($"Notification B {Guid.NewGuid():N}");
        var nowUtc = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        var oldestId = await factory.CreateNotificationAsync(
            owner.User.Id, organizationA, nowUtc.AddMinutes(-3), read: true);
        var targetId = await factory.CreateNotificationAsync(
            owner.User.Id, organizationA, nowUtc.AddMinutes(-2));
        var newestId = await factory.CreateNotificationAsync(
            owner.User.Id, organizationB, nowUtc.AddMinutes(-1));
        _ = await factory.CreateNotificationAsync(other.User.Id, organizationA, nowUtc);

        var firstPage = await ownerClient.GetFromJsonAsync<NotificationPage>(
            "/api/v1/notifications?page=1&pageSize=2",
            JsonOptions);
        Assert.NotNull(firstPage);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([newestId, targetId], firstPage.Items.Select(item => item.Id));
        Assert.DoesNotContain(firstPage.Items, item => item.Id == oldestId);

        var organizationPage = await ownerClient.GetFromJsonAsync<NotificationPage>(
            $"/api/v1/notifications?organizationId={organizationA}&unreadOnly=true",
            JsonOptions);
        Assert.NotNull(organizationPage);
        Assert.Single(organizationPage.Items);
        Assert.Equal(targetId, organizationPage.Items.Single().Id);
        Assert.Equal(organizationA, organizationPage.Items.Single().OrganizationId);

        var unread = await ownerClient.GetFromJsonAsync<NotificationUnreadCount>(
            "/api/v1/notifications/unread-count",
            JsonOptions);
        Assert.Equal(2, unread!.Count);

        using var concealedResponse = await otherClient.PostAsync(
            $"/api/v1/notifications/{targetId}/read",
            null);
        Assert.Equal(HttpStatusCode.NotFound, concealedResponse.StatusCode);

        using var firstReadResponse = await ownerClient.PostAsync(
            $"/api/v1/notifications/{targetId}/read",
            null);
        firstReadResponse.EnsureSuccessStatusCode();
        var firstRead = await firstReadResponse.Content.ReadFromJsonAsync<NotificationItem>(JsonOptions);
        using var retryReadResponse = await ownerClient.PostAsync(
            $"/api/v1/notifications/{targetId}/read",
            null);
        retryReadResponse.EnsureSuccessStatusCode();
        var retryRead = await retryReadResponse.Content.ReadFromJsonAsync<NotificationItem>(JsonOptions);
        Assert.NotNull(firstRead!.ReadAtUtc);
        Assert.Equal(firstRead.ReadAtUtc, retryRead!.ReadAtUtc);
    }

    [Fact]
    public async Task DurableOutboxPersistsProcessesOnceAndConcurrentProcessorsDoNotDuplicate()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync($"Outbox {Guid.NewGuid():N}");
        var nowUtc = new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc);
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            var first = CreateIntent(user.User.Id, organizationId, nowUtc);
            var firstMessageId = Assert.Single(await factory.EnqueueNotificationAsync(first));
            Assert.True(await factory.CountPendingNotificationOutboxAsync() >= 1);

            Assert.True(await factory.DrainNotificationOutboxAsync() >= 1);
            var firstState = await factory.GetNotificationOutboxStateAsync(firstMessageId);
            Assert.True(firstState.Processed);
            Assert.Equal(1, await factory.CountNotificationsAsync(first.EventId, user.User.Id));
            _ = await factory.DrainNotificationOutboxAsync();
            Assert.Equal(1, await factory.CountNotificationsAsync(first.EventId, user.User.Id));

            var second = CreateIntent(user.User.Id, organizationId, nowUtc.AddMinutes(1));
            var secondMessageId = Assert.Single(await factory.EnqueueNotificationAsync(second));
            var concurrentResults = await factory.ProcessNotificationOutboxConcurrentlyAsync();
            Assert.True(concurrentResults.Sum() >= 1);
            Assert.Equal(1, await factory.CountNotificationsAsync(second.EventId, user.User.Id));
            Assert.True((await factory.GetNotificationOutboxStateAsync(secondMessageId)).Processed);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task FailedOutboxMessageRemainsRecoverableAndCanBeRetried()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync($"Retry {Guid.NewGuid():N}");
        var nowUtc = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var eventId = Guid.NewGuid();
        factory.Clock.SetUtcNow(nowUtc);
        try
        {
            await factory.AddMalformedNotificationOutboxAsync(eventId, nowUtc);
            Assert.True(await factory.DrainNotificationOutboxAsync() >= 1);
            var failed = await factory.GetNotificationOutboxStateAsync(eventId);
            Assert.False(failed.Processed);
            Assert.True(failed.RetryScheduled);
            Assert.Equal("notification_processing_failed", failed.ErrorCode);

            var repaired = CreateIntent(user.User.Id, organizationId, nowUtc) with { EventId = eventId };
            await factory.RepairNotificationOutboxAsync(eventId, repaired);
            Assert.True(await factory.DrainNotificationOutboxAsync() >= 1);
            var succeeded = await factory.GetNotificationOutboxStateAsync(eventId);
            Assert.True(succeeded.Processed);
            Assert.Equal(1, await factory.CountNotificationsAsync(eventId, user.User.Id));
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task NotificationEndpointsRequireAuthentication()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GuardianRecipientResolutionExcludesUnrelatedAndRevokedRelationsAndKeepsContentGeneric()
    {
        var organizationId = await factory.CreateOrganizationAsync($"Guardian notification {Guid.NewGuid():N}");
        var learners = await factory.CreateOrganizationPersonsAsync(organizationId, 2);
        using var validClient = factory.CreateClient();
        var validGuardian = await CreateAuthenticatedUserAsync(validClient);
        UseBearerToken(validClient, validGuardian.AccessToken);
        using var unrelatedClient = factory.CreateClient();
        var unrelatedGuardian = await CreateAuthenticatedUserAsync(unrelatedClient);
        using var revokedClient = factory.CreateClient();
        var revokedGuardian = await CreateAuthenticatedUserAsync(revokedClient);
        _ = await factory.CreateGuardianRelationAsync(
            validGuardian.User.Id,
            organizationId,
            learners[0]);
        _ = await factory.CreateGuardianRelationAsync(
            unrelatedGuardian.User.Id,
            organizationId,
            learners[1]);
        var revokedRelation = await factory.CreateGuardianRelationAsync(
            revokedGuardian.User.Id,
            organizationId,
            learners[0]);
        await factory.RevokeGuardianRelationAsync(revokedRelation);
        var nowUtc = DateTime.UtcNow;
        var recipients = await factory.ResolveActiveGuardianUserIdsAsync(
            organizationId,
            learners[0],
            nowUtc);
        Assert.Equal([validGuardian.User.Id], recipients);

        var intent = new NotificationIntent(
            Guid.NewGuid(),
            organizationId,
            NotificationCategory.Educational,
            NotificationType.AssignmentGradeReleased,
            NotificationResourceType.SubmissionAttempt,
            Guid.NewGuid(),
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            recipients.Select(userId => new NotificationRecipientIntent(
                userId,
                "نتیجه آموزشی منتشر شد",
                "نتیجه جدید آموزشی برای فرزند شما منتشر شده است.")).ToArray());
        _ = await factory.EnqueueNotificationAsync(intent);
        _ = await factory.DrainNotificationOutboxAsync();

        Assert.Equal(1, await factory.CountNotificationsAsync(intent.EventId, validGuardian.User.Id));
        Assert.Equal(0, await factory.CountNotificationsAsync(intent.EventId, unrelatedGuardian.User.Id));
        Assert.Equal(0, await factory.CountNotificationsAsync(intent.EventId, revokedGuardian.User.Id));
        var page = await validClient.GetFromJsonAsync<NotificationPage>(
            $"/api/v1/notifications?organizationId={organizationId}",
            JsonOptions);
        var notification = Assert.Single(page!.Items);
        Assert.DoesNotContain("7", notification.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("نمره", notification.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("پاسخ", notification.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishingAssignmentCommitsOutboxAndCreatesLearnerNotification()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateAuthenticatedUserAsync(managerClient);
        var organizationId = await factory.CreateOrganizationAsync($"Producer {Guid.NewGuid():N}");
        var managerMembership = await factory.CreateMembershipAsync(
            manager.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(managerClient, manager.AccessToken);
        using (var selectResponse = await managerClient.PostAsJsonAsync(
                   "/api/v1/workspaces/select",
                   new SelectWorkspaceCommand(
                       WorkspaceType.Organization,
                       managerMembership.MembershipId,
                       OrganizationRole.Manager),
                   JsonOptions))
        {
            selectResponse.EnsureSuccessStatusCode();
        }

        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        using var learnerClient = factory.CreateClient();
        var learner = await CreateAuthenticatedUserAsync(learnerClient);
        UseBearerToken(learnerClient, learner.AccessToken);
        using (var profileResponse = await learnerClient.PatchAsJsonAsync(
                   "/api/v1/me/profile",
                   new CompleteProfileCommand(
                       "دانش‌آموز",
                       "اعلان",
                       "دانش‌آموز اعلان",
                       $"nt.{Guid.NewGuid():N}"[..24]),
                   JsonOptions))
        {
            profileResponse.EnsureSuccessStatusCode();
        }
        var learnerOrganizationPersonId = await factory.CreateOrganizationPersonForUserAsync(
            organizationId,
            learner.User.Id);
        _ = await factory.CreateEnrollmentAsync(
            organizationId,
            academicClass.ClassId,
            learnerOrganizationPersonId);

        using var createResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            new CreateAssignmentDraftCommand(
                "تمرین اعلان",
                "صورت تمرین اعلان",
                DateTime.UtcNow.AddDays(2),
                false,
                1,
                20m),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var assignment = (await createResponse.Content.ReadFromJsonAsync<AssignmentResult>(JsonOptions))!;
        var pendingBefore = await factory.CountPendingNotificationOutboxAsync();

        using var publishResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/assignments/{assignment.Id}/publish",
            new PublishAssignmentCommand(
                assignment.AssignmentRowVersion,
                assignment.VersionRowVersion),
            JsonOptions);
        publishResponse.EnsureSuccessStatusCode();

        Assert.Equal(pendingBefore + 1, await factory.CountPendingNotificationOutboxAsync());
        _ = await factory.DrainNotificationOutboxAsync();
        var page = await learnerClient.GetFromJsonAsync<NotificationPage>(
            $"/api/v1/notifications?organizationId={organizationId}",
            JsonOptions);
        Assert.Contains(page!.Items, item =>
            item.Type == NotificationType.AssignmentPublished &&
            item.ResourceType == NotificationResourceType.Assignment &&
            item.ResourceId == assignment.Id);
    }

    private static NotificationIntent CreateIntent(
        Guid userId,
        Guid organizationId,
        DateTime occurredAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            NotificationCategory.Educational,
            NotificationType.AssignmentPublished,
            NotificationResourceType.Assignment,
            Guid.NewGuid(),
            occurredAtUtc,
            [new NotificationRecipientIntent(
                userId,
                "تکلیف جدید منتشر شد",
                "یک تکلیف جدید برای شما منتشر شده است.")]);

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
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

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
