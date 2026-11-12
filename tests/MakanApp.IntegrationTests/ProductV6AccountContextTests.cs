using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private static async Task CompleteContextActor(TestActor actor)
    {
        using var response = await actor.Client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("کاربر", "تست زمینه", null,
            "ctx" + Guid.NewGuid().ToString("N")[..12], new DateOnly(1990, 1, 1)), Json);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ProductV6PersonalInboxContactsCloudBookAndClassesAreStableAcrossFourSchoolRoles()
    {
        var actor = await ActorAsync(); var peer = await ActorAsync(); var outsider = await ActorAsync();
        await CompleteContextActor(actor); await CompleteContextActor(peer); await CompleteContextActor(outsider);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var classroom = await ApprovedClassAsync(actor, "کلاس شخصی ثابت میان زمینه‌ها");
        var schoolA = await factory.CreateOrganizationAsync("مدرسه الف چندنقشی"); var schoolB = await factory.CreateOrganizationAsync("مدرسه ب چندنقشی");
        var a = await factory.CreateMembershipAsync(actor.UserId, schoolA, OrganizationRole.Teacher);
        var b = await factory.CreateMembershipAsync(actor.UserId, schoolB, OrganizationRole.Parent);
        await WithDb(async db => {
            db.RoleAssignments.Add(RoleAssignment.CreateActive(a.MembershipId, OrganizationRole.Manager, factory.Clock.GetUtcNow().UtcDateTime));
            db.RoleAssignments.Add(RoleAssignment.CreateActive(b.MembershipId, OrganizationRole.Student, factory.Clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync();
        });
        await PostAsync<UserContactResult>(actor.Client, "/api/v1/contacts", new SaveUserContactCommand(peer.UserId, "مخاطب ثابت"));
        await PostAsync<UserContactResult>(peer.Client, "/api/v1/contacts", new SaveUserContactCommand(actor.UserId, "اجازهٔ صریح تماس"));
        var directCommand = new StartDirectConversationCommand(peer.UserId, ConversationScope.Personal);
        var direct = await PostAsync<DirectConversationResult>(actor.Client, "/api/v1/conversations/direct", directCommand);
        var message = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام ثابت تست" };
        var receipt = await PostAsync<MessageReceiptResult>(actor.Client, $"/api/v1/conversations/{direct.ConversationId}/messages", message);
        var device = Guid.NewGuid(); var entry = Guid.NewGuid(); await ContactConsent(actor, device);
        await ContactSync(actor, device, 1, new ContactMutation(entry, ContactMutationKind.Upsert, null, "دفترچه ثابت", ["+989121234567"]));
        foreach (var selection in new[] { (a.MembershipId, OrganizationRole.Teacher), (a.MembershipId, OrganizationRole.Manager),
            (b.MembershipId, OrganizationRole.Parent), (b.MembershipId, OrganizationRole.Student) })
        {
            await SelectAsync(actor, selection.MembershipId, selection.Item2);
            var inbox = await GetAsync<AccountInboxPageResult>(actor.Client, "/api/v1/conversations/account");
            Assert.Equal("پیام ثابت تست", Assert.Single(inbox.Items, c => c.ConversationId == direct.ConversationId).LastMessagePreview);
            var contacts = await GetAsync<UserContactPageResult>(actor.Client, "/api/v1/contacts");
            Assert.Equal("مخاطب ثابت", Assert.Single(contacts.Items, c => c.ContactUserId == peer.UserId).SavedName);
            Assert.Equal(entry, Assert.Single((await ContactDelta(actor, device)).Upserts).EntryId);
            Assert.Contains(await GetAsync<PersonalClassDto[]>(actor.Client, "/api/v1/personal-classes"), c => c.Id == classroom.Id);
            var same = await PostAsync<DirectConversationResult>(actor.Client, "/api/v1/conversations/direct", directCommand);
            Assert.True(same.AlreadyExisted); Assert.Equal(direct.ConversationId, same.ConversationId);
            var repeated = await PostAsync<MessageReceiptResult>(actor.Client, $"/api/v1/conversations/{direct.ConversationId}/messages", message);
            Assert.Equal(receipt.MessageId, repeated.MessageId); Assert.Equal(receipt.Sequence, repeated.Sequence);
            var current = await GetAsync<AccessContext>(actor.Client, "/api/v1/workspaces/current");
            Assert.Equal(selection.MembershipId, current.MembershipId); Assert.Equal(selection.Item2, current.ActiveRole);
        }
        var received = await GetAsync<AccountInboxPageResult>(peer.Client, "/api/v1/conversations/account");
        Assert.Equal(1, Assert.Single(received.Items, c => c.ConversationId == direct.ConversationId).UnreadCount);
        using var concealed = await outsider.Client.GetAsync($"/api/v1/conversations/{direct.ConversationId}"); Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
        await WithDb(async db => Assert.Equal(1, await db.Messages.CountAsync(m => m.ConversationId == direct.ConversationId)));
    }

    [Fact]
    public async Task ProductV6CloudSyncCannotAutomaticallyLinkAccountFromPhoneUpsert()
    {
        var owner = await ActorAsync(); var target = await ActorAsync(); var device = Guid.NewGuid();
        await ContactConsent(owner, device);
        var id = Guid.NewGuid(); var result = await ContactSync(owner, device, 1,
            new ContactMutation(id, ContactMutationKind.Upsert, null, "reference تأییدنشده", ["+989121234567"], LinkedAccountUserId: target.UserId));
        Assert.Equal(AddressBookErrorCodes.Invalid, Assert.Single(result.Items).ErrorCode);
        await WithDb(async db => Assert.False(await db.AddressBookEntries.AnyAsync(e => e.OwnerUserId == owner.UserId && e.Id == id)));
    }

    [Fact]
    public async Task ProductV6AccountLinkIsExplicitPrivateReferenceAndDoesNotGrantContactsAudience()
    {
        var owner = await ActorAsync(); var target = await ActorAsync(); var device = Guid.NewGuid(); var entry = Guid.NewGuid();
        await CompleteContextActor(owner); await CompleteContextActor(target); await ContactConsent(owner, device);
        using var privacy = await target.Client.PutAsJsonAsync("/api/v1/messaging/privacy",
            new UpdateMessagingPrivacyCommand(PrivacyAudience.Contacts, PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Contacts, null), Json);
        privacy.EnsureSuccessStatusCode();
        var created = await ContactSync(owner, device, 1, new ContactMutation(entry, ContactMutationKind.Upsert, null, "نام خصوصی", ["+989121234567"]));
        var linked = await ContactSync(owner, device, 2, new ContactMutation(entry, ContactMutationKind.LinkAccount, Assert.Single(created.Items).ServerVersion,
            LinkedAccountUserId: target.UserId));
        Assert.Null(Assert.Single(linked.Items).ErrorCode);
        var result = Assert.Single((await ContactDelta(owner, device)).Upserts);
        Assert.Equal(target.UserId, result.LinkedAccountUserId); Assert.True(result.LinkCurrentlyVisible); Assert.Equal("نام خصوصی", result.SavedName);
        await WithDb(async db => {
            Assert.False(await db.UserContacts.AnyAsync(c => c.OwnerUserId == owner.UserId && c.ContactUserId == target.UserId));
            Assert.False(await db.UserContacts.AnyAsync(c => c.OwnerUserId == target.UserId && c.ContactUserId == owner.UserId));
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == target.UserId));
        });
        using var direct = await owner.Client.PostAsJsonAsync("/api/v1/conversations/direct", new StartDirectConversationCommand(target.UserId, ConversationScope.Personal), Json);
        Assert.False(direct.IsSuccessStatusCode);
        var updated = await ContactSync(owner, device, 3, new ContactMutation(entry, ContactMutationKind.Upsert, result.ServerVersion, "نام دستگاه", ["+989121234568"]));
        Assert.Equal(target.UserId, Assert.Single((await ContactDelta(owner, device)).Upserts).LinkedAccountUserId);
        using var block = await target.Client.PostAsync($"/api/v1/messaging/blocks/{owner.UserId}", null); block.EnsureSuccessStatusCode();
        var hidden = Assert.Single((await ContactDelta(owner, device)).Upserts);
        Assert.Null(hidden.LinkedAccountUserId); Assert.False(hidden.LinkCurrentlyVisible);
        var denied = await ContactSync(owner, device, 4, new ContactMutation(entry, ContactMutationKind.LinkAccount, Assert.Single(updated.Items).ServerVersion,
            LinkedAccountUserId: target.UserId));
        Assert.Equal(MessagingErrorCodes.PersonNotDiscoverable, Assert.Single(denied.Items).ErrorCode);
        var unlinked = await ContactSync(owner, device, 5, new ContactMutation(entry, ContactMutationKind.LinkAccount, Assert.Single(updated.Items).ServerVersion));
        Assert.Null(Assert.Single(unlinked.Items).ErrorCode);
        Assert.Null(Assert.Single((await ContactDelta(owner, device)).Upserts).LinkedAccountUserId);
    }
}
