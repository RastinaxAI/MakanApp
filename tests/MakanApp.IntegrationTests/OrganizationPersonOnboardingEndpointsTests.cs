using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class OrganizationPersonOnboardingEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private const string People = "/api/v1/organization/people";
    private const string Relations = "/api/v1/organization/guardian-relations";
    private static int _phoneSequence = 9_300_000;
    private static readonly JsonSerializerOptions JsonOptions = Options();

    [Fact]
    public async Task ManagerOnboardsSearchesAndEnrollsLearnerWithoutImplicitAccountsOrRoles()
    {
        using var actor = await ManagerAsync();
        var before = await CountsAsync();
        var command = NewPerson("Operational");
        var person = await CreatePersonAsync(actor.Client, command);
        var after = await CountsAsync();
        Assert.Equal(before, after);
        Assert.False(person.IsAccountLinked);
        Assert.True(person.CanEditNames);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var audit = await database.AdminAuditLogs.AsNoTracking().SingleAsync(item =>
                item.Action == "organization.person.create" && item.TargetId == person.Id.ToString("D"));
            Assert.Equal(actor.User.User.Id, audit.ActorUserId);
            Assert.NotNull(audit.ActorPersonId);
            Assert.NotNull(audit.ActorSessionId);
            Assert.Equal(actor.OrganizationId, audit.OrganizationId);
        }
        var read = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{person.Id}", JsonOptions);
        Assert.Equal(person, read);
        var page = await actor.Client.GetFromJsonAsync<OrganizationPeoplePage>($"{People}?search=Operational&page=1&pageSize=1", JsonOptions);
        Assert.Equal(person.Id, Assert.Single(page!.Items).Id);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(actor.OrganizationId, page.Items.Single().OrganizationId);
        var classId = await CreateClassAsync(actor.Client);
        using var enrolled = await actor.Client.PostAsJsonAsync($"/api/v1/academic/classes/{classId}/enrollments", new EnrollLearnerCommand(person.Id), JsonOptions);
        enrolled.EnsureSuccessStatusCode();
        var enrollment = await enrolled.Content.ReadFromJsonAsync<EnrollmentResult>(JsonOptions);
        Assert.Equal(person.Id, enrollment!.LearnerOrganizationPersonId);
        using var prematureEnd = await actor.Client.PostAsJsonAsync($"{People}/{person.Id}/end", new OnboardingStateCommand(person.RowVersion, "آزمون پایان زودهنگام"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, prematureEnd.StatusCode);
    }

    [Fact]
    public async Task SameNameIsNotIdentityButOperationRetryAndPayloadConflictAreHandled()
    {
        using var actor = await ManagerAsync();
        var command = NewPerson("Same");
        var results = await Task.WhenAll(actor.Client.PostAsJsonAsync(People, command, JsonOptions), actor.Client.PostAsJsonAsync(People, command, JsonOptions));
        foreach (var response in results) response.EnsureSuccessStatusCode();
        var first = await results[0].Content.ReadFromJsonAsync<OrganizationPersonDto>(JsonOptions);
        var second = await results[1].Content.ReadFromJsonAsync<OrganizationPersonDto>(JsonOptions);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Contains(results, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Contains(results, item => item.StatusCode == HttpStatusCode.OK);
        foreach (var response in results) response.Dispose();
        using var conflict = await actor.Client.PostAsJsonAsync(People, command with { LastName = "Different" }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var distinct = await CreatePersonAsync(actor.Client, command with { ClientOperationId = Guid.NewGuid() });
        Assert.NotEqual(first.PersonId, distinct.PersonId);
        Assert.NotEqual(first.Id, distinct.Id);
        var pageOne = await actor.Client.GetFromJsonAsync<OrganizationPeoplePage>($"{People}?search=Same&pageSize=1&page=1", JsonOptions);
        var pageTwo = await actor.Client.GetFromJsonAsync<OrganizationPeoplePage>($"{People}?search=Same&pageSize=1&page=2", JsonOptions);
        Assert.Equal(2, pageOne!.TotalCount);
        Assert.NotEqual(Assert.Single(pageOne.Items).Id, Assert.Single(pageTwo!.Items).Id);
    }

    [Fact]
    public async Task NamesUseBothVersionsAndConcurrentEditsHaveOneExplicitWinner()
    {
        using var actor = await ManagerAsync();
        var person = await CreatePersonAsync(actor.Client);
        var command = Names(person, "Updated");
        var responses = await Task.WhenAll(actor.Client.PatchAsJsonAsync($"{People}/{person.Id}", command, JsonOptions),
            actor.Client.PatchAsJsonAsync($"{People}/{person.Id}", command with { FirstName = "Other" }, JsonOptions));
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.PreconditionFailed);
        foreach (var response in responses) response.Dispose();
        var fresh = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{person.Id}", JsonOptions);
        Assert.NotEqual(person.PersonRowVersion, fresh!.PersonRowVersion);
        using var stale = await actor.Client.PatchAsJsonAsync($"{People}/{person.Id}", command, JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var ended = await PostAsync<OrganizationPersonDto>(actor.Client, $"{People}/{person.Id}/end", new OnboardingStateCommand(fresh.RowVersion, "پایان پرونده بدون ثبت‌نام"));
        Assert.Equal(OrganizationPersonStatus.Ended, ended.Status);
        var replay = await PostAsync<OrganizationPersonDto>(actor.Client, $"{People}/{person.Id}/end", new OnboardingStateCommand(fresh.RowVersion, "تکرار پایان پرونده"));
        Assert.True(replay.Replayed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedPersonNamesAreProtectedIncludingEndedReferences(bool otherOrganization)
    {
        using var actor = await ManagerAsync();
        var person = await CreatePersonAsync(actor.Client);
        Guid editableId;
        if (otherOrganization)
        {
            var otherOrg = await factory.CreateOrganizationAsync($"Other {Guid.NewGuid():N}");
            _ = await factory.CreateOrganizationPersonAsync(otherOrg, person.PersonId);
            editableId = person.Id;
        }
        else
        {
            _ = await PostAsync<OrganizationPersonDto>(actor.Client, $"{People}/{person.Id}/end", new OnboardingStateCommand(person.RowVersion, "پایان سابقه نخست"));
            editableId = await factory.CreateOrganizationPersonAsync(actor.OrganizationId, person.PersonId);
        }
        var read = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{editableId}", JsonOptions);
        Assert.False(read!.CanEditNames);
        using var denied = await actor.Client.PatchAsJsonAsync($"{People}/{editableId}", Names(read, "Forbidden"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task AccountOwnedPersonNamesRemainInIdentityWorkflow()
    {
        using var actor = await ManagerAsync();
        var ownPersonId = await factory.GetUserPersonIdAsync(actor.User.User.Id);
        var recordId = await factory.CreateOrganizationPersonAsync(actor.OrganizationId, ownPersonId);
        var read = await actor.Client.GetFromJsonAsync<OrganizationPersonDto>($"{People}/{recordId}", JsonOptions);
        using var denied = await actor.Client.PatchAsJsonAsync($"{People}/{recordId}", Names(read!, "Forbidden"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var profile = await actor.Client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me", JsonOptions);
        Assert.Equal("Account", profile!.FirstName);
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Parent)]
    public async Task NonManagerCannotOnboardOrSelfGrantGuardianAccess(OrganizationRole role)
    {
        using var actor = await ManagerAsync(role);
        using var create = await actor.Client.PostAsJsonAsync(People, NewPerson(), JsonOptions);
        using var guardian = await actor.Client.PostAsJsonAsync(Relations,
            new CreateSchoolGuardianRelationCommand(actor.User.User.Id, Guid.NewGuid(), "تلاش غیرمجاز"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, guardian.StatusCode);
    }

    [Fact]
    public async Task CrossOrganizationIdentifiersCannotSelectScopeOrMutateRecords()
    {
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var foreign = await CreatePersonAsync(second.Client, NewPerson("Foreign"));
        using var read = await first.Client.GetAsync($"{People}/{foreign.Id}");
        using var update = await first.Client.PatchAsJsonAsync($"{People}/{foreign.Id}", Names(foreign, "Attack"), JsonOptions);
        using var relation = await first.Client.PostAsJsonAsync(Relations,
            new CreateSchoolGuardianRelationCommand(first.User.User.Id, foreign.Id, "آزمون جداسازی سازمان"), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, relation.StatusCode);
        var page = await first.Client.GetFromJsonAsync<OrganizationPeoplePage>($"{People}?search=Foreign&organizationId={second.OrganizationId}", JsonOptions);
        Assert.Empty(page!.Items);
        using var badSize = await first.Client.GetAsync($"{People}?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, badSize.StatusCode);
        using var injected = await first.Client.PostAsJsonAsync(People, new
        {
            clientOperationId = Guid.NewGuid(), firstName = "Scoped", lastName = "Record", reason = "آزمون selector",
            organizationId = second.OrganizationId, userId = second.User.User.Id, role = "Manager", verifiedPhone = "not-a-credential"
        }, JsonOptions);
        injected.EnsureSuccessStatusCode();
        var own = await injected.Content.ReadFromJsonAsync<OrganizationPersonDto>(JsonOptions);
        Assert.Equal(first.OrganizationId, own!.OrganizationId);
        Assert.False(own.IsAccountLinked);
    }

    private async Task<Actor> ManagerAsync(OrganizationRole role = OrganizationRole.Manager)
    {
        factory.Clock.SetUtcNow(DateTime.UtcNow.AddMinutes(1));
        var client = factory.CreateClient();
        var user = await UserAsync(client);
        var org = await factory.CreateOrganizationAsync($"Onboarding {Guid.NewGuid():N}");
        var membership = await factory.CreateMembershipAsync(user.User.Id, org, role);
        await SelectAsync(client, membership.MembershipId, role);
        return new Actor(client, user, org, membership.MembershipId);
    }

    private async Task<VerifyOtpResult> UserAsync(HttpClient client, bool completeProfile = true)
    {
        var phone = $"+98914{Interlocked.Increment(ref _phoneSequence):D7}";
        var challenge = await PostAsync<RequestOtpResult>(client, "/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var user = await PostAsync<VerifyOtpResult>(client, "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, factory.GetLatestOtpCode(phone)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        if (completeProfile)
        {
            using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile",
                new CompleteProfileCommand("Account", "Owner", "Account Owner", $"onboard.{Guid.NewGuid():N}"[..28]), JsonOptions);
            profile.EnsureSuccessStatusCode();
        }
        return user;
    }

    private static async Task SelectAsync(HttpClient client, Guid membershipId, OrganizationRole role) =>
        _ = await PostAsync<AccessContext>(client, "/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role));
    private static CreateOrganizationPersonCommand NewPerson(string firstName = "Learner") => new(Guid.NewGuid(), firstName, "School", $"{firstName} School", "ایجاد پرونده آموزشی");
    private static UpdateOrganizationPersonNamesCommand Names(OrganizationPersonDto person, string firstName) =>
        new(firstName, person.LastName, person.DisplayName, person.RowVersion, person.PersonRowVersion, "اصلاح نام پرونده");
    private static Task<OrganizationPersonDto> CreatePersonAsync(HttpClient client, CreateOrganizationPersonCommand? command = null) =>
        PostAsync<OrganizationPersonDto>(client, People, command ?? NewPerson());
    private static async Task<T> PostAsync<T>(HttpClient client, string path, object command)
    {
        using var response = await client.PostAsJsonAsync(path, command, JsonOptions);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path}: {(int)response.StatusCode}; {raw}");
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)!;
    }
    private static async Task<Guid> CreateClassAsync(HttpClient client)
    {
        var period = await PostAsync<AcademicPeriodResult>(client, "/api/v1/academic/periods",
            new CreateAcademicPeriodCommand("Operational period", new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31)));
        var course = await PostAsync<CourseResult>(client, "/api/v1/academic/courses", new CreateCourseCommand("Operational course"));
        var result = await PostAsync<ClassResult>(client, "/api/v1/academic/classes", new CreateClassCommand(period.Id, course.Id, "Operational class", 10, true));
        return result.Id;
    }
    private async Task<(int Users, int Credentials, int Memberships, int Roles, int Enrollments, int Teachers)> CountsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return (await database.Users.CountAsync(), await database.UserCredentials.CountAsync(), await database.Memberships.CountAsync(),
            await database.RoleAssignments.CountAsync(), await database.Enrollments.CountAsync(), await database.TeacherAssignments.CountAsync());
    }
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { factory.Clock.UseSystemTime(); return Task.CompletedTask; }
    private sealed record Actor(HttpClient Client, VerifyOtpResult User, Guid OrganizationId, Guid MembershipId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
