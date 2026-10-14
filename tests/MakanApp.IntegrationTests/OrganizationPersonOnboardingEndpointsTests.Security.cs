using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class OrganizationPersonOnboardingEndpointsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SqlServerRejectsCrossOrganizationLinkAndNonStudentLinkRole(bool crossOrganization)
    {
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var own = await CreatePersonAsync(first.Client);
        var foreign = await CreatePersonAsync(second.Client);
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient);
        var invitation = await InviteAsync(first, own, target.User.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var exception = await Assert.ThrowsAsync<SqlException>(async () =>
        {
            if (crossOrganization)
                await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[Invitations] SET [OrganizationPersonId] = {foreign.Id} WHERE [Id] = {invitation.Id}");
            else
                await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[Invitations] SET [Role] = {(int)OrganizationRole.Manager} WHERE [Id] = {invitation.Id}");
        });
        Assert.Equal(547, exception.Number);
        var unchanged = await database.Invitations.AsNoTracking().SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal(own.Id, unchanged.OrganizationPersonId);
        Assert.Equal(OrganizationRole.Student, unchanged.Role);
    }

    [Fact]
    public async Task LinkCreationIsScopedAndDoesNotExposeForeignSchoolRecord()
    {
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var foreign = await CreatePersonAsync(second.Client);
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient);
        using var create = await first.Client.PostAsJsonAsync($"{People}/{foreign.Id}/invitations", LinkCommand(foreign, target.User.Id), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        var invitation = await InviteAsync(second, foreign, target.User.Id);
        using var revoke = await first.Client.PostAsJsonAsync($"{People}/{foreign.Id}/invitations/{invitation.Id}/revoke",
            new OnboardingStateCommand(invitation.RowVersion, "تلاش سازمان دیگر"), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
        using var unauthenticated = factory.CreateClient();
        using var accept = await unauthenticated.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(true), JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, accept.StatusCode);
    }

    [Fact]
    public async Task SameExistingAccountCanConsentToDifferentOrganizationsWithoutMergingRecords()
    {
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var firstRecord = await CreatePersonAsync(first.Client);
        var secondRecord = await CreatePersonAsync(second.Client);
        using var targetClient = factory.CreateClient();
        var target = await UserAsync(targetClient);
        var firstInvitation = await InviteAsync(first, firstRecord, target.User.Id);
        var secondInvitation = await InviteAsync(second, secondRecord, target.User.Id);
        var firstAcceptance = await PostAsync<AcceptInvitationResult>(targetClient, $"/api/v1/invitations/{firstInvitation.Id}/accept", new AcceptInvitationCommand(true));
        var secondAcceptance = await PostAsync<AcceptInvitationResult>(targetClient, $"/api/v1/invitations/{secondInvitation.Id}/accept", new AcceptInvitationCommand(true));
        Assert.NotEqual(firstAcceptance.MembershipId, secondAcceptance.MembershipId);
        var firstRead = await first.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{firstRecord.Id}", JsonOptions);
        var secondRead = await second.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{secondRecord.Id}", JsonOptions);
        Assert.Equal(firstRead!.PersonId, secondRead!.PersonId);
        Assert.NotEqual(firstRead.Id, secondRead.Id);
        Assert.False(firstRead.CanEditNames);
        Assert.False(secondRead.CanEditNames);
    }

    [Fact]
    public async Task EndingSchoolRecordBlocksSelectedGuardianWithoutDeletingRelation()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        using var parentClient = factory.CreateClient();
        var parent = await UserAsync(parentClient);
        var membership = await factory.CreateMembershipAsync(parent.User.Id, actor.OrganizationId, OrganizationRole.Parent);
        await SelectAsync(parentClient, membership.MembershipId, OrganizationRole.Parent);
        var relation = await ActiveRelationAsync(actor, parent.User.Id, learner.Id);
        using var selected = await parentClient.PostAsync($"/api/v1/guardian/children/{learner.Id}/select", null);
        selected.EnsureSuccessStatusCode();
        _ = await PostAsync<OrganizationPersonDto>(actor.Client, $"{People}/{learner.Id}/end", new OnboardingStateCommand(learner.RowVersion, "پایان استفاده عملیاتی پرونده"));
        using var denied = await parentClient.GetAsync("/api/v1/dashboards/parent");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var preserved = await actor.Client.GetFromJsonAsync<SchoolGuardianRelationDto>($"{Relations}/{relation.Id}", JsonOptions);
        Assert.Equal(GuardianRelationStatus.Active, preserved!.Status);
    }

    [Fact]
    public async Task PublicOnboardingContractsContainNoIdentityOrPermissionMutationFields()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var schema in new[] { nameof(CreateOrganizationPersonCommand), nameof(UpdateOrganizationPersonNamesCommand) })
        {
            var properties = schemas.GetProperty(schema).GetProperty("properties");
            foreach (var forbidden in new[] { "userId", "personId", "organizationId", "username", "phoneNumber", "verifiedPhone", "credentials", "role", "membershipId", "enrollmentId" })
                Assert.False(properties.TryGetProperty(forbidden, out _));
        }
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(People, out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(Relations, out _));
    }
}
