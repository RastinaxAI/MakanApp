using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class OrganizationPersonOnboardingEndpointsTests
{
    [Theory]
    [InlineData("revoke", GuardianRelationStatus.Revoked)]
    [InlineData("end", GuardianRelationStatus.Ended)]
    public async Task OperationalGuardianActivationAndTerminationImmediatelyControlChildReads(string operation, GuardianRelationStatus expected)
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        var classId = await CreateClassAsync(actor.Client);
        var enrollment = await PostAsync<EnrollmentResult>(actor.Client, $"/api/v1/academic/classes/{classId}/enrollments", new EnrollLearnerCommand(learner.Id));
        using var parentClient = factory.CreateClient();
        var parent = await UserAsync(parentClient);
        var parentMembership = await factory.CreateMembershipAsync(parent.User.Id, actor.OrganizationId, OrganizationRole.Parent);
        await SelectAsync(parentClient, parentMembership.MembershipId, OrganizationRole.Parent);
        var command = new CreateSchoolGuardianRelationCommand(parent.User.Id, learner.Id, "ثبت رابطه با بررسی مدرسه");
        var relation = await PostAsync<SchoolGuardianRelationDto>(actor.Client, Relations, command);
        Assert.Equal(GuardianRelationStatus.Pending, relation.Status);
        var duplicate = await PostAsync<SchoolGuardianRelationDto>(actor.Client, Relations, command);
        Assert.Equal(relation.Id, duplicate.Id);
        Assert.True(duplicate.Replayed);
        using var pending = await parentClient.PostAsync($"/api/v1/guardian/children/{learner.Id}/select", null);
        Assert.Equal(HttpStatusCode.NotFound, pending.StatusCode);
        var activation = new OnboardingStateCommand(relation.RowVersion, "تأیید رابطه توسط مدیر مجاز");
        var results = await Task.WhenAll(actor.Client.PostAsJsonAsync($"{Relations}/{relation.Id}/activate", activation, JsonOptions),
            actor.Client.PostAsJsonAsync($"{Relations}/{relation.Id}/activate", activation, JsonOptions));
        foreach (var response in results) response.EnsureSuccessStatusCode();
        var active = (await results[0].Content.ReadFromJsonAsync<SchoolGuardianRelationDto>(JsonOptions))!;
        Assert.Equal(GuardianRelationStatus.Active, active.Status);
        foreach (var response in results) response.Dispose();
        using var select = await parentClient.PostAsync($"/api/v1/guardian/children/{learner.Id}/select", null);
        select.EnsureSuccessStatusCode();
        var dashboard = await parentClient.GetFromJsonAsync<ParentDashboardDto>("/api/v1/dashboards/parent", JsonOptions);
        Assert.Equal(learner.Id, dashboard!.SelectedLearner!.LearnerOrganizationPersonId);
        using var report = await parentClient.GetAsync("/api/v1/reports/parent/attendance");
        report.EnsureSuccessStatusCode();
        var before = await CountsAsync();
        var terminal = await PostAsync<SchoolGuardianRelationDto>(actor.Client, $"{Relations}/{relation.Id}/{operation}",
            new OnboardingStateCommand(active.RowVersion, "پایان دسترسی آینده والد"));
        Assert.Equal(expected, terminal.Status);
        var replayed = await PostAsync<SchoolGuardianRelationDto>(actor.Client, $"{Relations}/{relation.Id}/{operation}",
            new OnboardingStateCommand(active.RowVersion, "تکرار عملیات پایان دسترسی"));
        Assert.True(replayed.Replayed);
        using var deniedDashboard = await parentClient.GetAsync("/api/v1/dashboards/parent");
        using var deniedReport = await parentClient.GetAsync("/api/v1/reports/parent/attendance");
        using var deniedSummary = await parentClient.GetAsync($"/api/v1/guardian/relations/{learner.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deniedDashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedReport.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedSummary.StatusCode);
        Assert.Equal(before, await CountsAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await database.OrganizationPersons.AnyAsync(item => item.Id == learner.Id));
        Assert.True(await database.Enrollments.AnyAsync(item => item.Id == enrollment.Id && item.Status == EnrollmentStatus.Active));
    }

    [Fact]
    public async Task MultipleChildrenAndOrganizationsRemainIsolatedAndParentsCannotManageRelations()
    {
        using var actor = await ManagerAsync();
        using var otherOrg = await ManagerAsync();
        var first = await CreatePersonAsync(actor.Client, NewPerson("First"));
        var second = await CreatePersonAsync(actor.Client, NewPerson("Second"));
        var foreign = await CreatePersonAsync(otherOrg.Client, NewPerson("Foreign"));
        var firstClass = await CreateClassAsync(actor.Client);
        var secondClass = await CreateClassAsync(actor.Client);
        _ = await PostAsync<EnrollmentResult>(actor.Client, $"/api/v1/academic/classes/{firstClass}/enrollments", new EnrollLearnerCommand(first.Id));
        _ = await PostAsync<EnrollmentResult>(actor.Client, $"/api/v1/academic/classes/{secondClass}/enrollments", new EnrollLearnerCommand(second.Id));
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        _ = await factory.CreateAcademicSessionAsync(actor.OrganizationId, firstClass, now.AddHours(1), now.AddHours(2));
        _ = await factory.CreateAcademicSessionAsync(actor.OrganizationId, secondClass, now.AddHours(3), now.AddHours(4));
        using var parentClient = factory.CreateClient();
        var parent = await UserAsync(parentClient);
        var membership = await factory.CreateMembershipAsync(parent.User.Id, actor.OrganizationId, OrganizationRole.Parent);
        await SelectAsync(parentClient, membership.MembershipId, OrganizationRole.Parent);
        var firstRelation = await ActiveRelationAsync(actor, parent.User.Id, first.Id);
        _ = await ActiveRelationAsync(actor, parent.User.Id, second.Id);
        using var selectFirst = await parentClient.PostAsync($"/api/v1/guardian/children/{first.Id}/select", null);
        selectFirst.EnsureSuccessStatusCode();
        var firstDashboard = await parentClient.GetFromJsonAsync<ParentDashboardDto>("/api/v1/dashboards/parent", JsonOptions);
        Assert.Equal(firstClass, Assert.Single(firstDashboard!.UpcomingSessions).ClassId);
        using var selectSecond = await parentClient.PostAsync($"/api/v1/guardian/children/{second.Id}/select", null);
        selectSecond.EnsureSuccessStatusCode();
        var secondDashboard = await parentClient.GetFromJsonAsync<ParentDashboardDto>("/api/v1/dashboards/parent", JsonOptions);
        Assert.Equal(secondClass, Assert.Single(secondDashboard!.UpcomingSessions).ClassId);
        using var schoolList = await parentClient.GetAsync(Relations);
        using var parentRevoke = await parentClient.PostAsJsonAsync($"{Relations}/{firstRelation.Id}/revoke",
            new OnboardingStateCommand(firstRelation.RowVersion, "تلاش والد برای مدیریت مدرسه"), JsonOptions);
        using var foreignRelation = await actor.Client.PostAsJsonAsync(Relations, new CreateSchoolGuardianRelationCommand(parent.User.Id, foreign.Id, "آزمون سازمان دیگر"), JsonOptions);
        using var foreignRead = await otherOrg.Client.GetAsync($"{Relations}/{firstRelation.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, schoolList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentRevoke.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRelation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using var unrelatedClient = factory.CreateClient();
        var unrelated = await UserAsync(unrelatedClient);
        var unrelatedMembership = await factory.CreateMembershipAsync(unrelated.User.Id, actor.OrganizationId, OrganizationRole.Parent);
        await SelectAsync(unrelatedClient, unrelatedMembership.MembershipId, OrganizationRole.Parent);
        using var unrelatedSummary = await unrelatedClient.GetAsync($"/api/v1/guardian/relations/{first.Id}");
        Assert.Equal(HttpStatusCode.NotFound, unrelatedSummary.StatusCode);
        var ownRelations = await actor.Client.GetFromJsonAsync<SchoolGuardianRelationsPage>($"{Relations}?learnerId={first.Id}&pageSize=1", JsonOptions);
        Assert.Equal(firstRelation.Id, Assert.Single(ownRelations!.Items).Id);
    }

    [Fact]
    public async Task GuardianActivationRevalidatesParentRoleAndRejectsResurrection()
    {
        using var actor = await ManagerAsync();
        var learner = await CreatePersonAsync(actor.Client);
        using var parentClient = factory.CreateClient();
        var parent = await UserAsync(parentClient);
        using var noMembership = await actor.Client.PostAsJsonAsync(Relations,
            new CreateSchoolGuardianRelationCommand(parent.User.Id, learner.Id, "نبود عضویت والد"), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, noMembership.StatusCode);
        _ = await factory.CreateMembershipAsync(parent.User.Id, actor.OrganizationId, OrganizationRole.Parent);
        var pending = await PostAsync<SchoolGuardianRelationDto>(actor.Client, Relations, new CreateSchoolGuardianRelationCommand(parent.User.Id, learner.Id, "ثبت رابطه"));
        var revoked = await PostAsync<SchoolGuardianRelationDto>(actor.Client, $"{Relations}/{pending.Id}/revoke", new OnboardingStateCommand(pending.RowVersion, "لغو درخواست پیش از تأیید"));
        using var resurrection = await actor.Client.PostAsJsonAsync($"{Relations}/{pending.Id}/activate", new OnboardingStateCommand(revoked.RowVersion, "فعال‌سازی غیرمجاز"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, resurrection.StatusCode);
        var next = await PostAsync<SchoolGuardianRelationDto>(actor.Client, Relations, new CreateSchoolGuardianRelationCommand(parent.User.Id, learner.Id, "درخواست تازه پس از لغو"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var membership = await database.Memberships.SingleAsync(item => item.UserId == parent.User.Id && item.OrganizationId == actor.OrganizationId);
            membership.End(factory.Clock.GetUtcNow().UtcDateTime);
            await database.SaveChangesAsync();
        }
        using var endedMembership = await actor.Client.PostAsJsonAsync($"{Relations}/{next.Id}/activate", new OnboardingStateCommand(next.RowVersion, "تأیید پس از پایان عضویت"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, endedMembership.StatusCode);
    }

    private async Task<SchoolGuardianRelationDto> ActiveRelationAsync(Actor actor, Guid parentId, Guid learnerId)
    {
        var pending = await PostAsync<SchoolGuardianRelationDto>(actor.Client, Relations,
            new CreateSchoolGuardianRelationCommand(parentId, learnerId, "ثبت رابطه تأییدشده مدرسه"));
        return await PostAsync<SchoolGuardianRelationDto>(actor.Client, $"{Relations}/{pending.Id}/activate",
            new OnboardingStateCommand(pending.RowVersion, "تأیید رابطه"));
    }
}
