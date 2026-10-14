using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class OrganizationPersonOnboardingEndpointsTests
{
    [Fact]
    public async Task ExplicitConsentLinksExistingAccountAndPreservesPersonsIdentityAndAcademicHistory()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        var classId = await CreateClassAsync(actor.Client);
        var enrollment = await PostAsync<EnrollmentResult>(actor.Client, $"/api/v1/academic/classes/{classId}/enrollments", new EnrollLearnerCommand(learner.Id));
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient);
        var accountPersonId = await factory.GetUserPersonIdAsync(target.User.Id);
        var before = await CountsAsync();
        var invitation = await InviteAsync(actor, learner, target.User.Id);
        var available = await targetClient.GetFromJsonAsync<InvitationResult[]>("/api/v1/invitations", JsonOptions);
        var ownedInvitation = Assert.Single(available!, item => item.Id == invitation.Id);
        Assert.True(ownedInvitation.RequiresPersonLinkConsent);
        Assert.Equal(learner.Id, ownedInvitation.OrganizationPersonId);
        Assert.Equal(learner.DisplayName, ownedInvitation.OrganizationPersonDisplayName);
        using var noConsent = await targetClient.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        using var wrongTarget = await actor.Client.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, wrongTarget.StatusCode);
        var acceptances = await Task.WhenAll(
            targetClient.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions),
            targetClient.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions));
        foreach (var response in acceptances) response.EnsureSuccessStatusCode();
        var accepted = (await acceptances[0].Content.ReadFromJsonAsync<AcceptInvitationResult>(JsonOptions))!;
        var replayed = (await acceptances[1].Content.ReadFromJsonAsync<AcceptInvitationResult>(JsonOptions))!;
        Assert.Equal(accepted.MembershipId, replayed.MembershipId);
        Assert.Equal(learner.Id, accepted.OrganizationPersonId);
        Assert.True(accepted.AlreadyAccepted || replayed.AlreadyAccepted);
        foreach (var response in acceptances) response.Dispose();
        var after = await CountsAsync();
        Assert.Equal(before.Users, after.Users);
        Assert.Equal(before.Credentials, after.Credentials);
        Assert.Equal(before.Enrollments, after.Enrollments);
        Assert.Equal(before.Teachers, after.Teachers);
        Assert.Equal(before.Memberships + 1, after.Memberships);
        Assert.Equal(before.Roles + 1, after.Roles);
        var linked = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{learner.Id}", JsonOptions);
        Assert.Equal(accountPersonId, linked!.PersonId);
        Assert.Equal(learner.Id, linked.Id);
        Assert.True(linked.IsAccountLinked);
        Assert.False(linked.CanEditNames);
        Assert.Equal(accountPersonId, await factory.GetUserPersonIdAsync(target.User.Id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.True(await database.Persons.AnyAsync(item => item.Id == learner.PersonId && item.FirstName == learner.FirstName));
            Assert.Equal(learner.Id, (await database.Enrollments.SingleAsync(item => item.Id == enrollment.Id)).LearnerOrganizationPersonId);
            Assert.Equal(1, await database.AdminAuditLogs.CountAsync(item => item.Action == "organization.person.link.accept" && item.TargetId == learner.Id.ToString("D")));
        }
        await SelectAsync(targetClient, accepted.MembershipId, OrganizationRole.Student);
        var classes = await targetClient.GetFromJsonAsync<ClassResult[]>("/api/v1/academic/classes", JsonOptions);
        Assert.Contains(classes!, item => item.Id == classId);
        using var dashboard = await targetClient.GetAsync("/api/v1/dashboards/student");
        dashboard.EnsureSuccessStatusCode();
        using var protectedNames = await actor.Client.PatchAsJsonAsync($"{People}/{learner.Id}", Names(linked, "Forbidden"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, protectedNames.StatusCode);
        using var profileUpdate = await targetClient.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("SelfUpdated", "Owner", "Self Updated", $"self.{Guid.NewGuid():N}"[..28]), JsonOptions);
        profileUpdate.EnsureSuccessStatusCode();
        var current = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{learner.Id}", JsonOptions);
        Assert.Equal("SelfUpdated", current!.FirstName);
    }

    [Fact]
    public async Task LinkInvitationRetryRevokeAndExpiryCannotCreateAnAssociation()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        using var targetClient = factory.CreateClient();
        var user = await UserAsync(targetClient);
        var command = LinkCommand(learner, user.User.Id);
        var invitation = await PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{learner.Id}/invitations", command);
        var retry = await PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{learner.Id}/invitations", command);
        Assert.Equal(invitation.Id, retry.Id);
        Assert.True(retry.Replayed);
        using var wrongPayload = await actor.Client.PostAsJsonAsync($"{People}/{learner.Id}/invitations",
            command with { DestinationUserId = actor.User.User.Id }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, wrongPayload.StatusCode);
        var revoked = await PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{learner.Id}/invitations/{invitation.Id}/revoke",
            new OnboardingStateCommand(invitation.RowVersion, "لغو درخواست اتصال"));
        Assert.Equal(InvitationStatus.Revoked, revoked.Status);
        var repeated = await PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{learner.Id}/invitations/{invitation.Id}/revoke",
            new OnboardingStateCommand(invitation.RowVersion, "تکرار لغو"));
        Assert.True(repeated.Replayed);
        using var denied = await targetClient.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.Gone, denied.StatusCode);
        var expiring = await PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{learner.Id}/invitations",
            command with { ClientOperationId = Guid.NewGuid(), ExpiresAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(1) });
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(2));
        using var expired = await targetClient.PostAsJsonAsync($"/api/v1/invitations/{expiring.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        var unchanged = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{learner.Id}", JsonOptions);
        Assert.Equal(learner.PersonId, unchanged!.PersonId);
        Assert.False(unchanged.IsAccountLinked);
    }

    [Fact]
    public async Task LinkRequiresOwnerProfileAndDoesNotCreateSecondUserOrCredential()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient, false);
        var invitation = await InviteAsync(actor, learner, target.User.Id);
        var before = await CountsAsync();
        using var incomplete = await targetClient.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);
        Assert.Equal(before, await CountsAsync());
        using var profile = await targetClient.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("Account", "Owner", null, $"late.{Guid.NewGuid():N}"[..28]), JsonOptions);
        profile.EnsureSuccessStatusCode();
        var accepted = await PostAsync<AcceptInvitationResult>(targetClient, $"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true));
        Assert.Equal(learner.Id, accepted.OrganizationPersonId);
        Assert.Equal(before.Users, (await CountsAsync()).Users);
        Assert.Equal(before.Credentials, (await CountsAsync()).Credentials);
    }

    [Fact]
    public async Task ExistingAccountRecordOrAnotherOwnerCannotBeSilentlyMerged()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient);
        var accountPersonId = await factory.GetUserPersonIdAsync(target.User.Id);
        var existingId = await factory.CreateOrganizationPersonAsync(actor.OrganizationId, accountPersonId);
        var invitation = await InviteAsync(actor, learner, target.User.Id);
        using var duplicate = await targetClient.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var read = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{learner.Id}", JsonOptions);
        Assert.Equal(learner.PersonId, read!.PersonId);
        using var otherUser = factory.CreateClient();
        var other = await UserAsync(otherUser);
        var existing = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{existingId}", JsonOptions);
        using var wrongOwner = await actor.Client.PostAsJsonAsync($"{People}/{existingId}/invitations", LinkCommand(existing!, other.User.Id), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, wrongOwner.StatusCode);
    }

    private CreatePersonLinkInvitationCommand LinkCommand(OrganizationPersonDto person, Guid targetUserId) =>
        new(Guid.NewGuid(), targetUserId, factory.Clock.GetUtcNow().UtcDateTime.AddDays(1), person.RowVersion, "درخواست اتصال با رضایت صاحب حساب");
    private Task<PersonLinkInvitationDto> InviteAsync(Actor actor, OrganizationPersonDto person, Guid targetUserId) =>
        PostAsync<PersonLinkInvitationDto>(actor.Client, $"{People}/{person.Id}/invitations", LinkCommand(person, targetUserId));
}
