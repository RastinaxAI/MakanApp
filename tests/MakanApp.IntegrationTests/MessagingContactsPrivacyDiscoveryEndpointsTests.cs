using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task PrivacyDefaultsUpdateAndRowVersionConflictAreEnforced()
    {
        using var client = factory.CreateClient();
        var user = await CreateMessagingUserAsync(client);

        var defaults = await client.GetFromJsonAsync<MessagingPrivacySettingsResult>(
            "/api/v1/messaging/privacy",
            JsonOptions);

        Assert.NotNull(defaults);
        Assert.Equal(PrivacyAudience.Everyone, defaults.DirectMessagesAudience);
        Assert.Equal(PrivacyAudience.Nobody, defaults.PhoneNumberVisibilityAudience);
        Assert.Equal(PrivacyAudience.Nobody, defaults.PhoneNumberDiscoveryAudience);
        Assert.Equal(PrivacyAudience.Contacts, defaults.GroupInvitesAudience);
        Assert.False(defaults.IsExplicit);
        Assert.Null(defaults.Version);
        Assert.Equal(0, await factory.CountMessagingPrivacySettingsAsync(user.UserId));

        var created = await UpdatePrivacyAsync(
            client,
            PrivacyAudience.Contacts,
            PrivacyAudience.Contacts,
            PrivacyAudience.Everyone,
            PrivacyAudience.Nobody,
            null);
        var updated = await UpdatePrivacyAsync(
            client,
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            PrivacyAudience.Contacts,
            created.Version);
        using var stale = await client.PutAsJsonAsync(
            "/api/v1/messaging/privacy",
            new UpdateMessagingPrivacyCommand(
                PrivacyAudience.Nobody,
                PrivacyAudience.Nobody,
                PrivacyAudience.Nobody,
                PrivacyAudience.Nobody,
                created.Version),
            JsonOptions);
        using var invalid = await client.PutAsJsonAsync(
            "/api/v1/messaging/privacy",
            new UpdateMessagingPrivacyCommand(
                (PrivacyAudience)99,
                PrivacyAudience.Nobody,
                PrivacyAudience.Nobody,
                PrivacyAudience.Contacts,
                updated.Version),
            JsonOptions);

        Assert.True(updated.IsExplicit);
        Assert.NotEqual(created.Version, updated.Version);
        Assert.Equal(1, await factory.CountMessagingPrivacySettingsAsync(user.UserId));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(MessagingErrorCodes.PrivacyConflict, await ReadProblemCodeAsync(stale));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(MessagingErrorCodes.PrivacyInvalid, await ReadProblemCodeAsync(invalid));
    }

    [Fact]
    public async Task ConcurrentFirstPrivacyUpdatesCreateOneRowWithoutSilentOverwrite()
    {
        using var ownerClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var clients = Enumerable.Range(0, 4).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, owner.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(clients.Select((client, index) =>
                client.PutAsJsonAsync(
                    "/api/v1/messaging/privacy",
                    new UpdateMessagingPrivacyCommand(
                        index % 2 == 0 ? PrivacyAudience.Everyone : PrivacyAudience.Nobody,
                        PrivacyAudience.Nobody,
                        PrivacyAudience.Nobody,
                        PrivacyAudience.Contacts,
                        null),
                    JsonOptions)));
            try
            {
                Assert.Single(
                    responses,
                    response => response.StatusCode == HttpStatusCode.OK);
                Assert.All(
                    responses.Where(response => response.StatusCode != HttpStatusCode.OK),
                    response => Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        Assert.Equal(1, await factory.CountMessagingPrivacySettingsAsync(owner.UserId));
    }

    [Fact]
    public async Task ContactCrudIsPrivateIdempotentAndDoesNotMutateTargetProfile()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        var originalDisplayName = await factory.GetPersonDisplayNameAsync(target.UserId);

        using var createdResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/contacts",
            new SaveUserContactCommand(target.UserId, "  هم‌کلاسی زبان  "),
            JsonOptions);
        var created = await createdResponse.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions);
        using var duplicateResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/contacts",
            new SaveUserContactCommand(target.UserId, "نباید جایگزین شود"),
            JsonOptions);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions);
        using var self = await ownerClient.PostAsJsonAsync(
            "/api/v1/contacts",
            new SaveUserContactCommand(owner.UserId, null),
            JsonOptions);
        using var updatedResponse = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{target.UserId}",
            new UpdateUserContactCommand("دوست نزدیک", true, created!.Version),
            JsonOptions);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions);
        using var unfavoriteResponse = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{target.UserId}",
            new UpdateUserContactCommand(updated!.SavedName, false, updated.Version),
            JsonOptions);
        var unfavorited = await unfavoriteResponse.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions);
        var outsiderList = await outsiderClient.GetFromJsonAsync<UserContactPageResult>(
            "/api/v1/contacts",
            JsonOptions);
        using var outsiderEdit = await outsiderClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{target.UserId}",
            new UpdateUserContactCommand("غیرمجاز", true, unfavorited!.Version),
            JsonOptions);
        using var outsiderDelete = await outsiderClient.DeleteAsync($"/api/v1/contacts/{target.UserId}");
        using var staleOwnerEdit = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{target.UserId}",
            new UpdateUserContactCommand("نسخه قدیمی", false, created.Version),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal("هم‌کلاسی زبان", created.SavedName);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.True(duplicate!.AlreadyExisted);
        Assert.Equal(created.SavedName, duplicate.SavedName);
        Assert.Equal(1, await factory.CountUserContactsAsync(owner.UserId, target.UserId));
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(MessagingErrorCodes.ContactSelfNotAllowed, await ReadProblemCodeAsync(self));
        Assert.True(updated.IsFavorite);
        Assert.Equal("دوست نزدیک", updated.SavedName);
        Assert.False(unfavorited.IsFavorite);
        Assert.Equal(originalDisplayName, await factory.GetPersonDisplayNameAsync(target.UserId));
        Assert.Empty(outsiderList!.Items);
        Assert.Equal(HttpStatusCode.NotFound, outsiderEdit.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, outsiderDelete.StatusCode);
        Assert.Equal(1, await factory.CountUserContactsAsync(owner.UserId, target.UserId));
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleOwnerEdit.StatusCode);
        Assert.Equal(MessagingErrorCodes.ContactConflict, await ReadProblemCodeAsync(staleOwnerEdit));

        using var deleted = await ownerClient.DeleteAsync($"/api/v1/contacts/{target.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await factory.CountUserContactsAsync(owner.UserId, target.UserId));
        Assert.Equal(0, await factory.CountActiveUserBlocksAsync(owner.UserId, target.UserId));
    }

    [Fact]
    public async Task ContactSearchFavoritesPaginationAndPhoneRedactionAreServerSide()
    {
        using var ownerClient = factory.CreateClient();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        using var thirdClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        var third = await CreateMessagingUserAsync(thirdClient);

        var firstContact = await SaveContactAsync(ownerClient, first.UserId, "استاد موسیقی");
        _ = await SaveContactAsync(ownerClient, second.UserId, null);
        _ = await SaveContactAsync(ownerClient, third.UserId, null);
        using var favorite = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/contacts/{first.UserId}",
            new UpdateUserContactCommand(firstContact.SavedName, true, firstContact.Version),
            JsonOptions);
        favorite.EnsureSuccessStatusCode();

        var bySavedName = await GetContactsAsync(ownerClient, "موسیقی");
        var byUsername = await GetContactsAsync(ownerClient, second.Username);
        var byDisplayName = await GetContactsAsync(ownerClient, third.DisplayName);
        var favorites = await ownerClient.GetFromJsonAsync<UserContactPageResult>(
            "/api/v1/contacts?favoritesOnly=true",
            JsonOptions);
        var firstPage = await ownerClient.GetFromJsonAsync<UserContactPageResult>(
            "/api/v1/contacts?page=1&pageSize=2",
            JsonOptions);
        var secondPage = await ownerClient.GetFromJsonAsync<UserContactPageResult>(
            "/api/v1/contacts?page=2&pageSize=2",
            JsonOptions);
        using var unboundedContacts = await ownerClient.GetAsync("/api/v1/contacts?pageSize=51");
        using var unboundedRecent = await ownerClient.GetAsync("/api/v1/contacts/recent?pageSize=51");

        Assert.Equal(first.UserId, Assert.Single(bySavedName.Items).ContactUserId);
        Assert.Equal(second.UserId, Assert.Single(byUsername.Items).ContactUserId);
        Assert.Equal(third.UserId, Assert.Single(byDisplayName.Items).ContactUserId);
        Assert.Equal(first.UserId, Assert.Single(favorites!.Items).ContactUserId);
        Assert.Equal(2, firstPage!.Items.Count);
        Assert.True(firstPage.HasMore);
        Assert.Single(secondPage!.Items);
        Assert.False(secondPage.HasMore);
        Assert.All(
            firstPage.Items.Concat(secondPage.Items),
            contact => Assert.Null(contact.Profile.PhoneNumber));
        Assert.Equal(HttpStatusCode.BadRequest, unboundedContacts.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unboundedRecent.StatusCode);
        Assert.Equal(3, await Task.FromResult(
            firstPage.Items.Concat(secondPage.Items).Select(item => item.ContactUserId).Distinct().Count()));
        Assert.NotEqual(Guid.Empty, owner.UserId);
    }

    [Fact]
    public async Task PhoneDiscoveryIsExactOneWayAndIndependentFromPhoneVisibility()
    {
        using var callerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var caller = await CreateMessagingUserAsync(callerClient);
        var target = await CreateMessagingUserAsync(targetClient);

        var privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Everyone,
            PrivacyAudience.Nobody,
            PrivacyAudience.Everyone,
            PrivacyAudience.Contacts,
            null);
        var localPhone = $"0{target.PhoneNumber[3..]}";
        factory.SensitiveLogs.Clear();
        var discoveredWithoutPhone = await ResolvePhoneAsync(callerClient, localPhone);
        Assert.True(discoveredWithoutPhone.IsDiscoverable);
        Assert.Equal(target.UserId, discoveredWithoutPhone.Profile!.UserId);
        Assert.Null(discoveredWithoutPhone.Profile.PhoneNumber);
        Assert.DoesNotContain(
            factory.SensitiveLogs.Snapshot(),
            message => message.Contains(localPhone, StringComparison.Ordinal) ||
                       message.Contains(target.PhoneNumber, StringComparison.Ordinal));

        privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Everyone,
            PrivacyAudience.Contacts,
            PrivacyAudience.Contacts,
            PrivacyAudience.Contacts,
            privacy.Version);
        var hiddenBeforeReciprocalContact = await ResolvePhoneAsync(callerClient, target.PhoneNumber);
        Assert.False(hiddenBeforeReciprocalContact.IsDiscoverable);
        Assert.Null(hiddenBeforeReciprocalContact.Profile);

        _ = await SaveContactAsync(targetClient, caller.UserId, "تماس مجاز");
        var discoveredForTargetContact = await ResolvePhoneAsync(callerClient, target.PhoneNumber);
        Assert.True(discoveredForTargetContact.IsDiscoverable);
        Assert.Equal(target.PhoneNumber, discoveredForTargetContact.Profile!.PhoneNumber);

        using var deleteReciprocal = await targetClient.DeleteAsync($"/api/v1/contacts/{caller.UserId}");
        deleteReciprocal.EnsureSuccessStatusCode();
        privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            PrivacyAudience.Contacts,
            privacy.Version);
        var visibleToEveryone = await ResolvePhoneAsync(callerClient, target.PhoneNumber);
        Assert.Equal(target.PhoneNumber, visibleToEveryone.Profile!.PhoneNumber);

        privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            PrivacyAudience.Nobody,
            PrivacyAudience.Contacts,
            privacy.Version);
        var hidden = await ResolvePhoneResponseAsync(callerClient, target.PhoneNumber);
        var nonexistent = await ResolvePhoneResponseAsync(callerClient, "+989991234567");
        Assert.Equal(await hidden.Content.ReadAsStringAsync(), await nonexistent.Content.ReadAsStringAsync());

        using var partial = await callerClient.PostAsJsonAsync(
            "/api/v1/people/resolve",
            new ResolvePersonCommand(null, "0912"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, partial.StatusCode);
        Assert.Equal(MessagingErrorCodes.PeopleResolveInvalid, await ReadProblemCodeAsync(partial));
    }

    [Fact]
    public async Task UsernameDiscoveryAndPublicProfileExposeOnlySafeBoundedFields()
    {
        using var callerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var caller = await CreateMessagingUserAsync(callerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(target.UserId, organizationId, OrganizationRole.Teacher);

        using var search = await callerClient.GetAsync(
            $"/api/v1/people/search?q={Uri.EscapeDataString(target.Username)}&pageSize=1");
        var searchJson = await search.Content.ReadAsStringAsync();
        var page = System.Text.Json.JsonSerializer.Deserialize<PeopleSearchPageResult>(searchJson, JsonOptions);
        using var resolve = await callerClient.PostAsJsonAsync(
            "/api/v1/people/resolve",
            new ResolvePersonCommand(target.Username, null),
            JsonOptions);
        var resolvedJson = await resolve.Content.ReadAsStringAsync();
        var resolved = System.Text.Json.JsonSerializer.Deserialize<PeopleResolveResult>(resolvedJson, JsonOptions);
        var profile = await callerClient.GetFromJsonAsync<MessagingPublicProfileResult>(
            $"/api/v1/people/{target.UserId}",
            JsonOptions);
        var displayNameSearch = await callerClient.GetFromJsonAsync<PeopleSearchPageResult>(
            $"/api/v1/people/search?q={Uri.EscapeDataString(target.DisplayName)}",
            JsonOptions);
        using var blank = await callerClient.GetAsync("/api/v1/people/search?q=%20");
        using var shortQuery = await callerClient.GetAsync("/api/v1/people/search?q=x");
        using var unbounded = await callerClient.GetAsync(
            $"/api/v1/people/search?q={Uri.EscapeDataString(target.Username)}&pageSize=51");

        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(target.UserId, Assert.Single(page!.Items).UserId);
        Assert.True(resolved!.IsDiscoverable);
        Assert.Equal(target.UserId, resolved.Profile!.UserId);
        Assert.Equal(target.UserId, profile!.UserId);
        Assert.Contains(displayNameSearch!.Items, item => item.UserId == target.UserId);
        Assert.Null(profile.PhoneNumber);
        Assert.True(profile.CanStartDirect);
        Assert.DoesNotContain("personId", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("membership", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("organization", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target.PhoneNumber, searchJson, StringComparison.Ordinal);
        Assert.DoesNotContain(target.PhoneNumber, resolvedJson, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortQuery.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unbounded.StatusCode);
        Assert.NotEqual(Guid.Empty, caller.UserId);
    }

    [Fact]
    public async Task GlobalPersonalDirectUsesCurrentPrivacyWithoutGrantOrAdultRequirement()
    {
        using var actorClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await factory.SetCommunicationAgeCategoryAsync(target.UserId, CommunicationAgeCategory.Minor);

        var conversation = await StartDirectAndReadAsync(
            actorClient,
            target.UserId,
            ConversationScope.Personal);
        using var reverse = await StartDirectAsync(
            targetClient,
            actor.UserId,
            ConversationScope.Personal);
        var reverseResult = await reverse.Content.ReadFromJsonAsync<DirectConversationResult>(JsonOptions);
        var firstMessage = await SendMessageAndReadAsync(
            actorClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "پیام قبل از محدودیت");
        var privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Contacts,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Contacts,
            null);
        using var deniedByPrivacy = await SendMessageAsync(
            actorClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "نباید ارسال شود");
        using var history = await actorClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages");

        _ = await SaveContactAsync(targetClient, actor.UserId, null);
        var allowedForTargetContact = await SendMessageAndReadAsync(
            actorClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "اکنون مجاز است");
        privacy = await UpdatePrivacyAsync(
            targetClient,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Contacts,
            privacy.Version);
        _ = await factory.CreatePersonalCommunicationGrantAsync(actor.UserId, target.UserId);
        using var grantCannotOverride = await SendMessageAsync(
            actorClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "grant نباید privacy را دور بزند");

        Assert.Equal(HttpStatusCode.OK, reverse.StatusCode);
        Assert.True(reverseResult!.AlreadyExisted);
        Assert.Equal(conversation.ConversationId, reverseResult.ConversationId);
        Assert.Equal(1, await factory.CountDirectConversationsAsync(
            actor.UserId,
            target.UserId,
            ConversationScope.Personal,
            null));
        Assert.Equal(CommunicationAgeCategory.Minor, await factory.GetCommunicationAgeCategoryAsync(target.UserId));
        Assert.Equal(HttpStatusCode.Forbidden, deniedByPrivacy.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotAllowed, await ReadProblemCodeAsync(deniedByPrivacy));
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(1, firstMessage.Sequence);
        Assert.Equal(2, allowedForTargetContact.Sequence);
        Assert.Equal(HttpStatusCode.Forbidden, grantCannotOverride.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotAllowed, await ReadProblemCodeAsync(grantCannotOverride));
    }

    [Fact]
    public async Task PersonalPrivacyDoesNotWeakenOrganizationDirectAndBlockReasonStaysGeneric()
    {
        using var managerClient = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var teacher = await CreateMessagingUserAsync(teacherClient);
        await UpdatePrivacyAsync(
            teacherClient,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            null);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(
            teacher.UserId,
            organizationId,
            OrganizationRole.Teacher);

        using var personalDenied = await StartDirectAsync(
            managerClient,
            teacher.UserId,
            ConversationScope.Personal);
        await SelectOrganizationAsync(
            managerClient,
            managerMembership.MembershipId,
            OrganizationRole.Manager);
        using var organizationAllowed = await StartDirectAsync(
            managerClient,
            teacher.UserId,
            ConversationScope.Organization);
        await SelectPersonalAsync(managerClient);
        using var block = await teacherClient.PostAsync(
            $"/api/v1/messaging/blocks/{manager.UserId}",
            null);
        block.EnsureSuccessStatusCode();
        using var blocked = await StartDirectAsync(
            managerClient,
            teacher.UserId,
            ConversationScope.Personal);

        Assert.Equal(HttpStatusCode.NotFound, personalDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Created, organizationAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);
        Assert.Equal(
            await ReadProblemCodeAsync(personalDenied),
            await ReadProblemCodeAsync(blocked));
    }

    [Fact]
    public async Task GroupInviteUsesTargetContactsButBlockStillDoesNotAffectGroup()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        var deniedCommand = new CreateManagedConversationCommand(
            Guid.NewGuid(),
            ConversationScope.Personal,
            "گروه ردشده",
            null,
            [member.UserId]);
        using var denied = await ownerClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            deniedCommand,
            JsonOptions);

        _ = await SaveContactAsync(memberClient, owner.UserId, null);
        using var block = await ownerClient.PostAsync(
            $"/api/v1/messaging/blocks/{member.UserId}",
            null);
        block.EnsureSuccessStatusCode();
        var allowed = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            deniedCommand with { ClientOperationId = Guid.NewGuid(), Title = "گروه مجاز" });
        var message = await SendMessageAndReadAsync(
            memberClient,
            allowed.ConversationId,
            Guid.NewGuid(),
            "Block روی Group اعمال نمی‌شود");

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(denied));
        Assert.Equal(1, message.Sequence);
    }

    [Fact]
    public async Task DeletingContactPreservesDirectHistoryAndUnblockDoesNotRestoreContact()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        var conversation = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        _ = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "تاریخچه مستقل از Contact");
        _ = await SaveContactAsync(firstClient, second.UserId, null);
        using var block = await firstClient.PostAsync(
            $"/api/v1/messaging/blocks/{second.UserId}",
            null);
        block.EnsureSuccessStatusCode();
        Assert.Equal(1, await factory.CountUserContactsAsync(first.UserId, second.UserId));
        using var delete = await firstClient.DeleteAsync($"/api/v1/contacts/{second.UserId}");
        using var unblock = await firstClient.DeleteAsync($"/api/v1/messaging/blocks/{second.UserId}");
        var contacts = await firstClient.GetFromJsonAsync<UserContactPageResult>(
            "/api/v1/contacts",
            JsonOptions);
        var history = await firstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{conversation.ConversationId}/messages",
            JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        Assert.Empty(contacts!.Items);
        Assert.Single(history!.Messages);
        Assert.Equal(1, await factory.CountDirectConversationsAsync(
            first.UserId,
            second.UserId,
            ConversationScope.Personal,
            null));
    }

    [Fact]
    public async Task RecentContactsComeFromPersonalDirectActivityAndAreBounded()
    {
        using var ownerClient = factory.CreateClient();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        var baseTime = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

        try
        {
            factory.Clock.SetUtcNow(baseTime);
            var firstConversation = await StartDirectAndReadAsync(
                ownerClient,
                first.UserId,
                ConversationScope.Personal);
            await SendMessageAndReadAsync(
                ownerClient,
                firstConversation.ConversationId,
                Guid.NewGuid(),
                "قدیمی‌تر");
            factory.Clock.SetUtcNow(baseTime.AddMinutes(1));
            var secondConversation = await StartDirectAndReadAsync(
                ownerClient,
                second.UserId,
                ConversationScope.Personal);
            await SendMessageAndReadAsync(
                ownerClient,
                secondConversation.ConversationId,
                Guid.NewGuid(),
                "جدیدتر");

            var firstPage = await ownerClient.GetFromJsonAsync<RecentContactPageResult>(
                "/api/v1/contacts/recent?page=1&pageSize=1",
                JsonOptions);
            var secondPage = await ownerClient.GetFromJsonAsync<RecentContactPageResult>(
                "/api/v1/contacts/recent?page=2&pageSize=1",
                JsonOptions);

            Assert.Equal(second.UserId, Assert.Single(firstPage!.Items).Profile.UserId);
            Assert.True(firstPage.HasMore);
            Assert.Equal(first.UserId, Assert.Single(secondPage!.Items).Profile.UserId);
            Assert.False(secondPage.HasMore);
            Assert.All(
                firstPage.Items.Concat(secondPage.Items),
                item => Assert.Null(item.Profile.PhoneNumber));
            Assert.NotEqual(Guid.Empty, owner.UserId);
        }
        finally
        {
            factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task ConcurrentContactSavesProduceExactlyOneRow()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        var clients = Enumerable.Range(0, 5).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, owner.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync(
                "/api/v1/contacts",
                new SaveUserContactCommand(target.UserId, "هم‌زمان"),
                JsonOptions)));
            try
            {
                Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        Assert.Equal(1, await factory.CountUserContactsAsync(owner.UserId, target.UserId));
    }

    private static async Task<MessagingPrivacySettingsResult> UpdatePrivacyAsync(
        HttpClient client,
        PrivacyAudience directMessages,
        PrivacyAudience phoneVisibility,
        PrivacyAudience phoneDiscovery,
        PrivacyAudience groupInvites,
        string? expectedVersion)
    {
        using var response = await client.PutAsJsonAsync(
            "/api/v1/messaging/privacy",
            new UpdateMessagingPrivacyCommand(
                directMessages,
                phoneVisibility,
                phoneDiscovery,
                groupInvites,
                expectedVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessagingPrivacySettingsResult>(JsonOptions))!;
    }

    private static async Task<UserContactResult> SaveContactAsync(
        HttpClient client,
        Guid contactUserId,
        string? savedName)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/contacts",
            new SaveUserContactCommand(contactUserId, savedName),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserContactResult>(JsonOptions))!;
    }

    private static async Task<UserContactPageResult> GetContactsAsync(
        HttpClient client,
        string query) =>
        (await client.GetFromJsonAsync<UserContactPageResult>(
            $"/api/v1/contacts?q={Uri.EscapeDataString(query)}",
            JsonOptions))!;

    private static async Task<PeopleResolveResult> ResolvePhoneAsync(
        HttpClient client,
        string phoneNumber)
    {
        using var response = await ResolvePhoneResponseAsync(client, phoneNumber);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PeopleResolveResult>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> ResolvePhoneResponseAsync(
        HttpClient client,
        string phoneNumber) =>
        client.PostAsJsonAsync(
            "/api/v1/people/resolve",
            new ResolvePersonCommand(null, phoneNumber),
            JsonOptions);
}
