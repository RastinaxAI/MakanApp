using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task RolelessPersonalMessagingSupportsDirectSendInboxGroupsChannelsAndPrivacy()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        var receipt = await SendP0MessageAsync(first, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام شخصی بدون نقش مدرسه" });
        var inbox = (await first.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", PolicyJson))!;
        Assert.Contains(inbox, c => c.ConversationId == direct.ConversationId);
        var history = (await second.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{direct.ConversationId}/messages", PolicyJson))!;
        Assert.Equal(receipt.MessageId, Assert.Single(history.Messages).MessageId);
        _ = await CreateEpicPersonalManagedAsync(first, "groups");
        _ = await CreateEpicPersonalManagedAsync(first, "channels");
        var status = (await first.GetFromJsonAsync<MessagingStatusResult>("/api/v1/me/messaging-status", PolicyJson))!;
        Assert.Contains(PersonalMessagingAction.CreateDirect, status.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreatePersonalChannel, status.AllowedActions);
        Assert.Equal(MessagingNextAction.None, status.NextAction);
        using var privacy = await first.GetAsync("/api/v1/messaging/privacy");
        privacy.EnsureSuccessStatusCode();
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == a.UserId || m.UserId == b.UserId));
        Assert.False(await db.OrganizationPersons.AnyAsync(p => db.Users.Any(u =>
            (u.Id == a.UserId || u.Id == b.UserId) && u.PersonId == p.PersonId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonalOwnershipSurvivesRoleAdditionAndSelectedRoleOrMembershipRemoval(bool endMembership)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        var group = await CreateEpicPersonalManagedAsync(first, "groups");
        var channel = await CreateEpicPersonalManagedAsync(first, "channels");
        var originalProfile = (await first.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        var organization = await factory.CreateOrganizationAsync($"Epic {Guid.NewGuid():N}");
        var teacher = await factory.CreateMembershipAsync(a.UserId, organization, OrganizationRole.Teacher);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.RoleAssignments.Add(RoleAssignment.CreateActive(teacher.MembershipId, OrganizationRole.Parent, Now));
            await db.SaveChangesAsync();
        }
        _ = await SendP0MessageAsync(first, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "حساب شخصی پس از دریافت نقش" });
        using var select = await first.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, teacher.MembershipId, OrganizationRole.Teacher), PolicyJson);
        select.EnsureSuccessStatusCode();
        if (endMembership) await factory.EndMembershipAsync(teacher.MembershipId);
        else await factory.EndRoleAssignmentAsync(teacher.RoleAssignmentId);

        // تغییر Workspace صریح همان قرارداد موجود است؛ Context سازمانی نامعتبر به مجوز رسمی تبدیل نمی‌شود.
        using var personal = await first.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Personal, null, null), PolicyJson);
        personal.EnsureSuccessStatusCode();
        _ = await SendP0MessageAsync(first, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "حساب شخصی پس از پایان نقش" });
        foreach (var conversationId in new[] { group.ConversationId, channel.ConversationId })
        {
            _ = await SendP0MessageAsync(first, $"/api/v1/conversations/{conversationId}/messages",
                new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "مالکیت شخصی پابرجاست" });
        }
        var inbox = (await first.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", PolicyJson))!;
        Assert.All(new[] { direct.ConversationId, group.ConversationId, channel.ConversationId },
            id => Assert.Contains(inbox, c => c.ConversationId == id));
        Assert.Equal(originalProfile, await first.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"));
        await using var verification = _host.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await context.Users.AnyAsync(u => u.Id == a.UserId));
        Assert.Equal(2, await context.ConversationParticipants.CountAsync(p => p.UserId == a.UserId &&
            (p.ConversationId == group.ConversationId || p.ConversationId == channel.ConversationId) &&
            p.Role == ConversationParticipantRole.Owner && p.Status == ConversationParticipantStatus.Active));
        Assert.True(await context.RoleAssignments.AnyAsync(r => r.MembershipId == teacher.MembershipId &&
            r.Role == OrganizationRole.Parent && r.Status == RoleAssignmentStatus.Active));
        if (!endMembership)
        {
            using var parent = await first.PostAsJsonAsync("/api/v1/workspaces/select",
                new SelectWorkspaceCommand(WorkspaceType.Organization, teacher.MembershipId, OrganizationRole.Parent), PolicyJson);
            parent.EnsureSuccessStatusCode();
        }
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Manager)]
    public async Task RecipientSchoolRoleDoesNotRestrictPersonalDirect(OrganizationRole role)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        _ = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        var organizationId = await factory.CreateOrganizationAsync($"Recipient {Guid.NewGuid():N}");
        await factory.CreateMembershipAsync(b.UserId, organizationId, role);
        _ = await CreateEpicPersonalDirectAsync(first, b.UserId);
    }

    [Theory]
    [InlineData(PrivacyAudience.Nobody)]
    [InlineData(PrivacyAudience.Contacts)]
    public async Task RolelessDirectKeepsReceiverPrivacyAndContactDirection(PrivacyAudience audience)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        using var privacy = await second.PutAsJsonAsync("/api/v1/messaging/privacy",
            new UpdateMessagingPrivacyCommand(audience, PrivacyAudience.Nobody, PrivacyAudience.Nobody,
                PrivacyAudience.Contacts, null), PolicyJson);
        privacy.EnsureSuccessStatusCode();
        using var oneWay = await first.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, null));
        oneWay.EnsureSuccessStatusCode();
        using var denied = await first.PostAsJsonAsync("/api/v1/conversations/direct",
            new StartDirectConversationCommand(b.UserId, ConversationScope.Personal), PolicyJson);
        await ErrorAsync(denied, HttpStatusCode.NotFound, MessagingErrorCodes.DirectRecipientNotAvailable);
        using var receiverContact = await second.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(a.UserId, null));
        receiverContact.EnsureSuccessStatusCode();
        if (audience == PrivacyAudience.Contacts) _ = await CreateEpicPersonalDirectAsync(first, b.UserId);
        else
        {
            using var stillDenied = await first.PostAsJsonAsync("/api/v1/conversations/direct",
                new StartDirectConversationCommand(b.UserId, ConversationScope.Personal), PolicyJson);
            Assert.Equal(HttpStatusCode.NotFound, stillDenied.StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RolelessDirectBlockInEitherDirectionStopsNewSendAndPreservesHistory(bool receiverBlocks)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        var route = $"/api/v1/conversations/{direct.ConversationId}/messages";
        _ = await SendP0MessageAsync(first, route,
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "تاریخچه پیش از مسدودسازی" });
        using var block = await (receiverBlocks ? second : first).PostAsync(
            $"/api/v1/messaging/blocks/{(receiverBlocks ? a.UserId : b.UserId)}", null);
        block.EnsureSuccessStatusCode();
        using var denied = await first.PostAsJsonAsync(route,
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "ارسال غیرمجاز پس از مسدودسازی" }, PolicyJson);
        await ErrorAsync(denied, HttpStatusCode.Forbidden, MessagingErrorCodes.MessageNotAllowed);
        Assert.Single(await factory.GetMessagesAsync(direct.ConversationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RolelessPersonalMessagingStillRequiresActiveAccountAndSession(bool suspend)
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            if (suspend) (await db.Users.SingleAsync(u => u.Id == account.UserId)).Suspend(Now);
            else await db.UserSessions.Where(s => s.UserId == account.UserId)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, Now));
            await db.SaveChangesAsync();
        }
        foreach (var route in new[] { "/api/v1/conversations", "/api/v1/contacts",
            "/api/v1/messaging/privacy", "/api/v1/me/messaging-status" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelfDeclaredAdultBirthDateDoesNotBypassProtectedRolelessMessaging(bool verifiedMinor)
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var child = _host.CreateClient();
        using var adult = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(child, verifiedMinor ? owner : null, 2015);
        var b = await CreateP0PersonalAccountAsync(adult, owner);
        using var declaration = await child.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, a.Username, new DateOnly(1990, 1, 1)));
        declaration.EnsureSuccessStatusCode();
        using var denied = await child.PostAsJsonAsync("/api/v1/conversations/direct",
            new StartDirectConversationCommand(b.UserId, ConversationScope.Personal), PolicyJson);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var command = new CreateManagedConversationCommand(Guid.NewGuid(), ConversationScope.Personal,
            "گروه شخصی محافظت‌شده", null, []);
        using var channel = await child.PostAsJsonAsync("/api/v1/conversations/channels", command, PolicyJson);
        Assert.Equal(HttpStatusCode.Forbidden, channel.StatusCode);
        _ = await CreateEpicPersonalManagedAsync(child, "groups");
        var status = (await child.GetFromJsonAsync<MessagingStatusResult>("/api/v1/me/messaging-status", PolicyJson))!;
        Assert.Equal(verifiedMinor ? CommunicationAgeCategory.Minor : CommunicationAgeCategory.Unknown, status.EffectiveAgeCategory);
        Assert.DoesNotContain(PersonalMessagingAction.CreateDirect, status.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreateDirectWithAuthorizedRelationship, status.AllowedActions);
    }

    [Fact]
    public async Task IncompleteBasicProfileDoesNotBlanketDenyPrivateMessagingSettingsAndInbox()
    {
        using var client = _host.CreateClient();
        _ = await AuthenticateAsync(client);
        foreach (var route in new[] { "/api/v1/messaging/privacy", "/api/v1/contacts", "/api/v1/conversations" })
        {
            using var response = await client.GetAsync(route);
            response.EnsureSuccessStatusCode();
        }
        var status = (await client.GetFromJsonAsync<MessagingStatusResult>("/api/v1/me/messaging-status", PolicyJson))!;
        Assert.Equal(MessagingNextAction.ProfileCompletionRequired, status.NextAction);
        Assert.Equal(PersonalMessagingAction.ReadExistingConversations, Assert.Single(status.AllowedActions));
    }

    [Fact]
    public async Task RolelessSignalRPersonalSubscriptionWorksButExistingOfficialClassRemainsProtected()
    {
        // Hub میزبان اصلی و REST میزبان کنترل‌شده از همان security تست استفاده می‌کنند.
        _host.Dispose();
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentitySecurity>();
            services.AddSingleton(factory.Services.GetRequiredService<IIdentitySecurity>());
            services.RemoveAll<IHostedService>();
        }));
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        using var managerClient = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        var manager = await CreateP0PersonalAccountAsync(managerClient, owner);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        var organizationId = await factory.CreateOrganizationAsync($"Protected {Guid.NewGuid():N}");
        var membership = await factory.CreateMembershipAsync(manager.UserId, organizationId, OrganizationRole.Manager);
        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        using var selected = await managerClient.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membership.MembershipId, OrganizationRole.Manager), PolicyJson);
        selected.EnsureSuccessStatusCode();
        var route = $"/api/v1/organization/classes/{academicClass.ClassId}/conversation";
        using var provisioned = await managerClient.PostAsync(route, null);
        provisioned.EnsureSuccessStatusCode();
        var official = (await provisioned.Content.ReadFromJsonAsync<OfficialClassConversationResult>())!;
        using var deniedRest = await first.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.NotFound, deniedRest.StatusCode);
        using var deniedHistory = await first.GetAsync($"/api/v1/conversations/{official.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, deniedHistory.StatusCode);
        await using var connection = CreateP0HubConnection(a.Authentication.AccessToken);
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeConversation", direct.ConversationId);
        var deniedHub = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SubscribeConversation", official.ConversationId));
        Assert.Contains(MessagingErrorCodes.RealtimeSubscriptionNotAllowed, deniedHub.Message, StringComparison.Ordinal);
        using var presence = await first.GetAsync($"/api/v1/people/{b.UserId}/presence");
        presence.EnsureSuccessStatusCode();
    }

    private static async Task<DirectConversationResult> CreateEpicPersonalDirectAsync(HttpClient client, Guid targetId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/conversations/direct",
            new StartDirectConversationCommand(targetId, ConversationScope.Personal), PolicyJson);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DirectConversationResult>(PolicyJson))!;
    }

    private static async Task<ManagedConversationResult> CreateEpicPersonalManagedAsync(HttpClient client, string resource)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{resource}",
            new CreateManagedConversationCommand(Guid.NewGuid(), ConversationScope.Personal, "فضای شخصی", null, []), PolicyJson);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<ManagedConversationResult>(PolicyJson))!;
        Assert.Equal(ConversationScope.Personal, result.Scope);
        Assert.Null(result.OrganizationId);
        return result;
    }
}
