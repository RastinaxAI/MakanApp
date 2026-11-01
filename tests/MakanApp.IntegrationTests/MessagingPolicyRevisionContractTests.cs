using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task Policy20261101EveryAccountUsesGeneralMessagingWithoutDobOrAgeVerification(OrganizationRole? role)
    {
        using var ownerClient = factory.CreateClient(); using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var target = await CreateMessagingUserAsync(targetClient);
        if (role.HasValue)
        {
            var organization = await factory.CreateOrganizationAsync(NewOrganizationName());
            var member = await factory.CreateMembershipAsync(owner.UserId, organization, role.Value);
            if (role != OrganizationRole.Parent) await SelectOrganizationAsync(ownerClient, member.MembershipId, role.Value);
        }
        await factory.SetCommunicationAgeCategoryAsync(owner.UserId, CommunicationAgeCategory.Unknown);
        await factory.SetCommunicationAgeCategoryAsync(target.UserId, CommunicationAgeCategory.Unknown);
        var status = (await ownerClient.GetFromJsonAsync<MessagingStatusResult>("/api/v1/me/messaging-status", JsonOptions))!;
        Assert.Equal(MessagingNextAction.None, status.NextAction);
        Assert.Equal(CommunicationAgeVerificationStatus.Unverified, status.VerificationStatus);
        Assert.Contains(PersonalMessagingAction.CreatePersonalGroup, status.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreatePersonalChannel, status.AllowedActions);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Null(await (from u in db.Users join p in db.Persons on u.PersonId equals p.Id where u.Id == owner.UserId select p.DateOfBirth).SingleAsync());
        }
        (await ownerClient.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(target.UserId, "مخاطب آزمایشی"), JsonOptions)).EnsureSuccessStatusCode();
        var found = (await ownerClient.GetFromJsonAsync<PeopleSearchPageResult>("/api/v1/people/search?q=" + Uri.EscapeDataString(target.Username), JsonOptions))!;
        Assert.Contains(found.Items, item => item.UserId == target.UserId);
        var direct = await StartDirectAndReadAsync(ownerClient, target.UserId, ConversationScope.Personal);
        foreach (var age in new[] { CommunicationAgeCategory.Unknown, CommunicationAgeCategory.Minor, CommunicationAgeCategory.Adult })
        {
            await factory.SetCommunicationAgeCategoryAsync(owner.UserId, age);
            await factory.SetCommunicationAgeCategoryAsync(target.UserId, age == CommunicationAgeCategory.Minor ? CommunicationAgeCategory.Unknown : CommunicationAgeCategory.Minor);
            (await SendMessageAsync(ownerClient, direct.ConversationId, Guid.NewGuid(), "پیام ساختگی برای بررسی سیاست")).EnsureSuccessStatusCode();
            (await SendMessageAsync(targetClient, direct.ConversationId, Guid.NewGuid(), "پاسخ ساختگی")).EnsureSuccessStatusCode();
        }
        await factory.MarkAgeConflictAsync(owner.UserId);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, target.UserId);
        using var created = await ownerClient.PostAsJsonAsync("/api/v1/conversations/groups", PersonalCommand("گروه بدون وابستگی سنی", target.UserId), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var group = (await created.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        Assert.Equal(owner.UserId, Assert.Single(group.Participants).User.UserId);
        var invitation = Assert.Single(group.InitialTargets);
        Assert.Equal(ConversationInitialTargetOutcome.InvitationPending, invitation.Outcome);
        (await targetClient.PostAsync($"/api/v1/messaging/invitations/{invitation.InvitationId}/accept", null)).EnsureSuccessStatusCode();
        using var channelResponse = await ownerClient.PostAsJsonAsync("/api/v1/conversations/channels", PersonalCommand("کانال بدون وابستگی سنی"), JsonOptions);
        channelResponse.EnsureSuccessStatusCode();
        var channel = (await channelResponse.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        Assert.True(channel.EffectiveCapabilities.CanPublish);
        (await SendMessageAsync(ownerClient, channel.ConversationId, Guid.NewGuid(), "انتشار مجاز صاحب کانال")).EnsureSuccessStatusCode();
        await SelectPersonalAsync(ownerClient);
        var file = await UploadPolicyImageAsync(ownerClient);
        var messageId = Guid.NewGuid();
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.ReviewRequired);
        try
        {
            using var fileMessage = await ownerClient.PostAsJsonAsync($"/api/v1/conversations/{direct.ConversationId}/messages",
                new SendMessageCommand { ClientMessageId = messageId, Kind = MessageKind.Image, AttachmentIds = [file.Id] }, JsonOptions);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, fileMessage.StatusCode);
            Assert.Equal(MessagingErrorCodes.MessageSafetyReviewRequired, await ReadProblemCodeAsync(fileMessage));
            using var notDelivered = await targetClient.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, notDelivered.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Messages.AnyAsync(m => m.ClientMessageId == messageId));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    [Fact]
    public async Task Policy20261101CreationReceiptHasPrivatePerTargetOutcomesAndRetryCreatesOneInvitation()
    {
        using var ownerClient = factory.CreateClient(); using var targetClient = factory.CreateClient(); using var deniedClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var target = await CreateMessagingUserAsync(targetClient); var denied = await CreateMessagingUserAsync(deniedClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, target.UserId);
        var command = PersonalCommand("رسید رضایت مستقل", target.UserId, denied.UserId, Guid.NewGuid());
        using var first = await ownerClient.PostAsJsonAsync("/api/v1/conversations/groups", command, JsonOptions); first.EnsureSuccessStatusCode();
        var result = (await first.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        var invitation = Assert.Single(result.InitialTargets, item => item.Outcome == ConversationInitialTargetOutcome.InvitationPending);
        Assert.Equal(2, result.InitialTargets.Count(item => item.Outcome == ConversationInitialTargetOutcome.NotAvailable)); Assert.Single(result.Participants);
        using var retry = await ownerClient.PostAsJsonAsync("/api/v1/conversations/groups", command, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var repeated = (await retry.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        Assert.Equal(result.ConversationId, repeated.ConversationId); Assert.Equal(result.InitialTargets.ToArray(), repeated.InitialTargets.ToArray());
        using (var changed = await ownerClient.PostAsJsonAsync("/api/v1/conversations/groups", command with { Title = "درخواست متفاوت" }, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var wrongActor = await deniedClient.PostAsync($"/api/v1/messaging/invitations/{invitation.InvitationId}/accept", null)) Assert.Equal(HttpStatusCode.NotFound, wrongActor.StatusCode);
        for (var attempt = 0; attempt < 2; attempt++) (await targetClient.PostAsync($"/api/v1/messaging/invitations/{invitation.InvitationId}/accept", null)).EnsureSuccessStatusCode();
        var memberView = (await targetClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{result.ConversationId}", JsonOptions))!;
        Assert.Equal(2, memberView.Participants.Count); Assert.Empty(memberView.InitialTargets);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var persisted = await db.ConversationInvitations.SingleAsync(i => i.ConversationId == result.ConversationId);
        Assert.Equal(TimeSpan.FromDays(30), persisted.ExpiresAtUtc - persisted.CreatedAtUtc);
        Assert.Equal(3, await db.ConversationInitialTargets.CountAsync(t => t.ConversationId == result.ConversationId));
        Assert.Equal(2, await db.ConversationParticipants.CountAsync(p => p.ConversationId == result.ConversationId && p.Status == ConversationParticipantStatus.Active));
    }

    [Fact]
    public async Task Policy20261101PhonePrivacyAndBlockRemainIndependentOfAge()
    {
        using var viewerClient = factory.CreateClient(); using var targetClient = factory.CreateClient();
        var viewer = await CreateMessagingUserAsync(viewerClient); var target = await CreateMessagingUserAsync(targetClient);
        await factory.SetCommunicationAgeCategoryAsync(viewer.UserId, CommunicationAgeCategory.Unknown);
        await factory.SetCommunicationAgeCategoryAsync(target.UserId, CommunicationAgeCategory.Unknown);
        var original = (await targetClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        async Task Set(PrivacyAudience audience, string? version)
        {
            using var update = await targetClient.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(
                PrivacyAudience.Everyone, audience, PrivacyAudience.Nobody, PrivacyAudience.Contacts, version), JsonOptions); update.EnsureSuccessStatusCode();
        }
        await Set(PrivacyAudience.Nobody, original.Version);
        Assert.Null((await viewerClient.GetFromJsonAsync<MessagingPublicProfileResult>($"/api/v1/people/{target.UserId}", JsonOptions))!.PhoneNumber);
        var current = (await targetClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
        await Set(PrivacyAudience.Everyone, current.Version);
        Assert.Equal(target.PhoneNumber, (await viewerClient.GetFromJsonAsync<MessagingPublicProfileResult>($"/api/v1/people/{target.UserId}", JsonOptions))!.PhoneNumber);
        using var undiscoverable = await viewerClient.PostAsJsonAsync("/api/v1/people/resolve", new ResolvePersonCommand(null, target.PhoneNumber), JsonOptions);
        Assert.False((await undiscoverable.Content.ReadFromJsonAsync<PeopleResolveResult>(JsonOptions))!.IsDiscoverable);
        (await viewerClient.PostAsync($"/api/v1/messaging/blocks/{target.UserId}", null)).EnsureSuccessStatusCode();
        using var blockedProfile = await viewerClient.GetAsync($"/api/v1/people/{target.UserId}"); Assert.Equal(HttpStatusCode.NotFound, blockedProfile.StatusCode);
        using var direct = await StartDirectAsync(viewerClient, target.UserId, ConversationScope.Personal); Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    [Fact]
    public async Task Policy20261101AccountInboxPaginatesAcrossMoreThanThirtyPerScopeAndRejectsForeignCursors()
    {
        using var ownerClient = factory.CreateClient(); using var otherClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); await CreateMessagingUserAsync(otherClient);
        var marker = "policy-page-" + Guid.NewGuid().ToString("N"); var expected = new HashSet<Guid>();
        for (var index = 0; index < 33; index++) expected.Add((await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand(marker + index))).ConversationId);
        var organizations = new List<(Guid OrganizationId, Guid MembershipId)>();
        for (var organizationIndex = 0; organizationIndex < 2; organizationIndex++)
        {
            var org = await factory.CreateOrganizationAsync(NewOrganizationName()); var membership = await factory.CreateMembershipAsync(owner.UserId, org, OrganizationRole.Manager);
            organizations.Add((org, membership.MembershipId)); await SelectOrganizationAsync(ownerClient, membership.MembershipId, OrganizationRole.Manager);
            for (var index = 0; index < 33; index++) expected.Add((await CreateManagedAndReadAsync(ownerClient, "groups",
                new(Guid.NewGuid(), ConversationScope.Organization, marker + organizationIndex + "-" + index, null, []))).ConversationId);
        }
        await SelectPersonalAsync(ownerClient); var prefix = "/api/v1/conversations/account?limit=17&search=" + marker;
        var first = (await ownerClient.GetFromJsonAsync<AccountInboxPageResult>(prefix, JsonOptions))!; Assert.True(first.HasMore); Assert.NotNull(first.NextCursor);
        using (var foreign = await otherClient.GetAsync(prefix + "&cursor=" + Uri.EscapeDataString(first.NextCursor!))) Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using (var changed = await ownerClient.GetAsync(prefix + "changed&cursor=" + Uri.EscapeDataString(first.NextCursor!))) Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        using (var tampered = await ownerClient.GetAsync(prefix + "&cursor=" + Uri.EscapeDataString("x" + first.NextCursor![1..]))) Assert.Equal(HttpStatusCode.BadRequest, tampered.StatusCode);
        var seen = new HashSet<Guid>(); var page = first;
        while (true)
        {
            foreach (var item in page.Items) Assert.True(seen.Add(item.ConversationId));
            if (!page.HasMore) break;
            page = (await ownerClient.GetFromJsonAsync<AccountInboxPageResult>(prefix + "&cursor=" + Uri.EscapeDataString(page.NextCursor!), JsonOptions))!;
        }
        Assert.Equal(99, seen.Count); Assert.True(expected.SetEquals(seen)); await factory.EndMembershipAsync(organizations[1].MembershipId);
        var after = (await ownerClient.GetFromJsonAsync<AccountInboxPageResult>(prefix + "&cursor=" + Uri.EscapeDataString(first.NextCursor!), JsonOptions))!;
        Assert.DoesNotContain(after.Items, item => item.OrganizationId == organizations[1].OrganizationId);
        Assert.All(after.Items, item => Assert.Contains(item.ConversationId, expected));
    }

    [Fact]
    public async Task Policy20261101ConversationPhotoUsesCurrentMembershipAndNeverBecomesPublicByAssetId()
    {
        using var ownerClient = factory.CreateClient(); using var targetClient = factory.CreateClient(); using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var target = await CreateMessagingUserAsync(targetClient); await CreateMessagingUserAsync(outsiderClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, target.UserId);
        using var create = await ownerClient.PostAsJsonAsync("/api/v1/conversations/groups", PersonalCommand("عکس گروه با رضایت", target.UserId), JsonOptions);
        create.EnsureSuccessStatusCode(); var group = (await create.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        // فایل تازهٔ Ready طبق Policy اختصاصی عکس گفتگو است؛ از attachment یا پروندهٔ Safety استفاده نمی‌شود.
        var file = await UploadPolicyImageAsync(ownerClient);
        using var put = await ownerClient.PutAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/photo", new SetConversationPhotoCommand(file.Id, group.Version), JsonOptions);
        put.EnsureSuccessStatusCode(); var photo = (await put.Content.ReadFromJsonAsync<ConversationPhotoMutationResult>(JsonOptions))!; Assert.Equal(file.Id, photo.Photo!.FileAssetId);
        using (var pending = await targetClient.GetAsync(photo.Photo.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, pending.StatusCode);
        (await targetClient.PostAsync($"/api/v1/messaging/invitations/{Assert.Single(group.InitialTargets).InvitationId}/accept", null)).EnsureSuccessStatusCode();
        using (var allowed = await targetClient.GetAsync(photo.Photo.DownloadUrl)) allowed.EnsureSuccessStatusCode();
        using (var denied = await outsiderClient.GetAsync(photo.Photo.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        (await ownerClient.DeleteAsync($"/api/v1/conversations/{group.ConversationId}/members/{target.UserId}")).EnsureSuccessStatusCode();
        using (var revoked = await targetClient.GetAsync(photo.Photo.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        var details = (await ownerClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{group.ConversationId}", JsonOptions))!; Assert.Equal(file.Id, details.Photo!.FileAssetId);
        (await ownerClient.DeleteAsync($"/api/v1/conversations/{group.ConversationId}/photo?expectedVersion={Uri.EscapeDataString(details.Version)}")).EnsureSuccessStatusCode();
        Assert.Null((await ownerClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{group.ConversationId}", JsonOptions))!.Photo);
    }

    [Fact]
    public async Task Policy20261101OpenApiExposesActualAdditiveContractsAndAnonymousAccessIsDenied()
    {
        using var anonymous = factory.CreateClient();
        foreach (var route in new[] { "/api/v1/conversations/account", "/api/v1/conversations/permissions", "/api/v1/realtime/account-inbox/changes" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        using var doc = JsonDocument.Parse(await anonymous.GetStringAsync("/openapi/v1.json")); var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/conversations/account").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/conversations/permissions").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/realtime/account-inbox/changes").TryGetProperty("get", out _));
        var photo = paths.GetProperty("/api/v1/conversations/{conversationId}/photo"); Assert.True(photo.TryGetProperty("put", out _)); Assert.True(photo.TryGetProperty("delete", out _));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty(nameof(ManagedConversationResult)).GetProperty("properties").TryGetProperty("effectiveCapabilities", out _));
        Assert.True(schemas.GetProperty(nameof(ManagedConversationResult)).GetProperty("properties").TryGetProperty("initialTargets", out _));
        Assert.True(schemas.GetProperty(nameof(AccountInboxPageResult)).GetProperty("properties").TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public async Task Policy20261101PhotoRejectsOtherOwnerAndStaleMutationWithoutReplacingCurrentBinding()
    {
        using var ownerClient = factory.CreateClient(); using var otherClient = factory.CreateClient();
        await CreateMessagingUserAsync(ownerClient); await CreateMessagingUserAsync(otherClient);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand("کنترل نسخهٔ عکس گفتگو"));
        var foreign = await UploadPolicyImageAsync(otherClient);
        using (var denied = await ownerClient.PutAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/photo", new SetConversationPhotoCommand(foreign.Id, group.Version), JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var first = await UploadPolicyImageAsync(ownerClient); var second = await UploadPolicyImageAsync(ownerClient);
        using var bound = await ownerClient.PutAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/photo", new SetConversationPhotoCommand(first.Id, group.Version), JsonOptions);
        bound.EnsureSuccessStatusCode();
        using (var stale = await ownerClient.PutAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/photo", new SetConversationPhotoCommand(second.Id, group.Version), JsonOptions))
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var current = (await ownerClient.GetFromJsonAsync<ManagedConversationResult>($"/api/v1/conversations/{group.ConversationId}", JsonOptions))!;
        Assert.Equal(first.Id, current.Photo!.FileAssetId);
        // تکرار همان Binding حتی با ACK گم‌شده اثر دوم نمی‌سازد.
        (await ownerClient.PutAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/photo", new SetConversationPhotoCommand(first.Id, group.Version), JsonOptions)).EnsureSuccessStatusCode();
    }

    private static async Task<FileAssetResult> UploadPolicyImageAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3xkAAAAASUVORK5CYII=");
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(content, "file", "policy-photo.png");
        using var response = await client.PostAsync("/api/v1/files", form); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
    }
}
