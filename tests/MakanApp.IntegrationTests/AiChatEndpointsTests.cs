using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AiChatEndpointsTests(MakanAppWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 9_100_000;

    [Fact]
    public async Task Chat_RequiresAuthenticatedSession()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Fact]
    public async Task Chat_Manager_UsesServerWorkspaceAndPropagatesCorrelation()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Manager"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "corr-ai-manager");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("  وضعیت چیست؟  ", null),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiChatResult>(JsonOptions);
        var request = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        Assert.Equal("پاسخ آزمایشی", result!.Answer.Text);
        Assert.Equal("corr-ai-manager", request.CorrelationId);
        Assert.Equal("Manager", request.Actor.Role);
        Assert.Equal(user.User.Id.ToString("D"), request.Actor.UserId);
        Assert.Equal(organizationId.ToString("D"), request.Scope.OrganizationId);
        Assert.Equal(organizationId.ToString("D"), request.Scope.SchoolId);
        Assert.Null(request.Scope.StudentId);
        Assert.Equal("وضعیت چیست؟", request.Message.Text);
    }

    [Fact]
    public async Task Chat_Student_UsesOrganizationPersonNotUserId()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profile = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "AI",
                "Student",
                "AI Student",
                $"ai.student.{Guid.NewGuid():N}"[..28]),
            JsonOptions);
        profile.EnsureSuccessStatusCode();
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Student Org"));
        var personId = await factory.GetUserPersonIdAsync(user.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("برنامه مطالعه", null),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var request = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        Assert.Equal(organizationPersonId.ToString("D"), request.Scope.StudentId);
        Assert.NotEqual(user.User.Id.ToString("D"), request.Scope.StudentId);
        Assert.Equal("Student", request.Actor.Role);
    }

    [Fact]
    public async Task Chat_PersonalOrRevokedWorkspace_IsDeniedWithoutCallingAi()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);

        using var personalResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, personalResponse.StatusCode);
        Assert.Equal(
            AiServiceErrorCodes.ContextNotAllowed,
            await ReadProblemCodeAsync(personalResponse));
        Assert.Null(factory.AiService.LastRequest);

        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Revoked"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        await factory.EndMembershipAsync(membership.MembershipId);
        using var revokedResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, revokedResponse.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Fact]
    public async Task Chat_MultiOrganizationScope_FollowsServerSelectedWorkspaceOnly()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationA = await factory.CreateOrganizationAsync(NewName("AI Scope A"));
        var organizationB = await factory.CreateOrganizationAsync(NewName("AI Scope B"));
        var membershipA = await factory.CreateMembershipAsync(
            user.User.Id, organizationA, OrganizationRole.Manager);
        var membershipB = await factory.CreateMembershipAsync(
            user.User.Id, organizationB, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);

        await SelectWorkspaceAsync(client, membershipA.MembershipId, OrganizationRole.Manager);
        using var responseA = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("سازمان جاری", null),
            JsonOptions);
        responseA.EnsureSuccessStatusCode();
        Assert.Equal(
            organizationA.ToString("D"),
            factory.AiService.LastRequest?.Scope.OrganizationId);

        await SelectWorkspaceAsync(client, membershipB.MembershipId, OrganizationRole.Manager);
        using var responseB = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("سازمان جاری", null),
            JsonOptions);
        responseB.EnsureSuccessStatusCode();
        Assert.Equal(
            organizationB.ToString("D"),
            factory.AiService.LastRequest?.Scope.OrganizationId);
    }

    [Fact]
    public async Task Chat_SuspendedUser_IsDeniedByAuthenticationBeforeAiCall()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Suspended"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        factory.AiService.Reset();
        await factory.SuspendUserAsync(user.User.Id);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.AiService.LastRequest);
    }

    [Theory]
    [InlineData(AiServiceErrorCodes.FeatureDisabled, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AiServiceErrorCodes.ServiceTimeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(AiServiceErrorCodes.ResponseInvalid, HttpStatusCode.BadGateway)]
    [InlineData(AiServiceErrorCodes.RateLimited, HttpStatusCode.TooManyRequests)]
    public async Task Chat_MapsDependencyFailuresToSafeProblemDetails(
        string code,
        HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewName("AI Failure"));
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        factory.AiService.FailNext(new AiServiceException(code, "خطای امن"));

        using var response = await client.PostAsJsonAsync(
            "/api/v1/ai/chat",
            new SendAiChatCommand("پرسش", null),
            JsonOptions);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(code, await ReadProblemCodeAsync(response));
        Assert.DoesNotContain("FastAPI", await response.Content.ReadAsStringAsync());
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge!.ChallengeId,
                phoneNumber,
                factory.GetLatestOtpCode(phoneNumber)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
    }

    private static async Task SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string NewName(string prefix) => $"{prefix}.{Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task InitializeAsync()
    {
        factory.AiService.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.AiService.Reset();
        return Task.CompletedTask;
    }
}
