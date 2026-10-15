using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(2015, CommunicationAgeCategory.Minor)]
    [InlineData(1990, CommunicationAgeCategory.Adult)]
    public async Task AuthorizedSchoolVerificationClassifiesAndAuditsWithoutPublicBirthDate(int year, CommunicationAgeCategory expected)
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient);
        await factory.SetCommunicationAgeCategoryAsync(child.UserId, CommunicationAgeCategory.Unknown);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        var state = await managerClient.GetFromJsonAsync<CommunicationAgeResult>($"/api/v1/organization/people/{school.PersonId}/communication-age", JsonOptions);
        Assert.Equal(CommunicationAgeCategory.Unknown, state!.Classification);
        using var verify = await managerClient.PostAsJsonAsync($"/api/v1/organization/people/{school.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(year, 1, 1), state.Version, "بررسی مدرک توسط مدرسه"), JsonOptions);
        verify.EnsureSuccessStatusCode();
        var result = (await verify.Content.ReadFromJsonAsync<CommunicationAgeResult>(JsonOptions))!;
        Assert.Equal(expected, result.Classification); Assert.Equal(CommunicationAgeVerificationStatus.Verified, result.VerificationStatus);
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(child.UserId));
        var json = await verify.Content.ReadAsStringAsync();
        Assert.DoesNotContain("birthDate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(child.PhoneNumber, json);
        using var unauthorized = await childClient.PostAsJsonAsync($"/api/v1/organization/people/{school.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(1990, 1, 1), result.Version, "ادعای خود کاربر"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
    }

    [Fact]
    public async Task SchoolCannotVerifyForeignPersonAndConflictingSourcesRequireOwnerResolution()
    {
        await factory.ResetPlatformIamAsync();
        using var managerAClient = factory.CreateClient(); using var managerBClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var managerA = await CreateMessagingUserAsync(managerAClient); var managerB = await CreateMessagingUserAsync(managerBClient);
        var child = await CreateMessagingUserAsync(childClient);
        var a = await CreatePolicySchoolAsync(managerAClient, managerA, child);
        var b = await CreatePolicySchoolAsync(managerBClient, managerB, child);
        var state = await factory.ReadAgeStateAsync(child.UserId);
        using var foreign = await managerAClient.PostAsJsonAsync($"/api/v1/organization/people/{b.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "شناسه سازمان دیگر"), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var first = await managerAClient.PostAsJsonAsync($"/api/v1/organization/people/{a.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "شاهد اول"), JsonOptions);
        first.EnsureSuccessStatusCode(); state = await factory.ReadAgeStateAsync(child.UserId);
        using var conflict = await managerBClient.PostAsJsonAsync($"/api/v1/organization/people/{b.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(1990, 1, 1), state.Version, "شاهد متعارض"), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        state = await factory.ReadAgeStateAsync(child.UserId);
        Assert.Equal(CommunicationAgeVerificationStatus.Conflict, state.VerificationStatus);
        Assert.Equal(CommunicationAgeCategory.Unknown, state.Classification); Assert.Equal(2, await factory.CountAgeEvidenceAsync(child.UserId));
        using var schoolResolve = await managerAClient.PostAsJsonAsync($"/api/admin/v1/users/{child.UserId}/communication-age/resolve",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "نقش مدرسه کافی نیست"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, schoolResolve.StatusCode);
        using var ownerClient = factory.CreateClient(); var owner = await CreateMessagingUserAsync(ownerClient);
        await factory.BootstrapPlatformOwnerAsync(owner.UserId);
        factory.PlatformStepUp.SetSatisfied(false);
        using var noStepUp = await ownerClient.PostAsJsonAsync($"/api/admin/v1/users/{child.UserId}/communication-age/resolve",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "حل با مدرک معتبر"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, noStepUp.StatusCode);
        factory.PlatformStepUp.SetSatisfied(true);
        using var resolved = await ownerClient.PostAsJsonAsync($"/api/admin/v1/users/{child.UserId}/communication-age/resolve",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "حل با مدرک معتبر"), JsonOptions);
        resolved.EnsureSuccessStatusCode();
        Assert.Equal(CommunicationAgeVerificationStatus.Verified, (await factory.ReadAgeStateAsync(child.UserId)).VerificationStatus);
        Assert.Equal(3, await factory.CountAgeEvidenceAsync(child.UserId));
    }

    [Fact]
    public async Task StaleAgeVersionAndClaimedProfileAgeCannotOverwriteVerification()
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        var version = (await factory.ReadAgeStateAsync(child.UserId)).Version;
        using var first = await managerClient.PostAsJsonAsync($"/api/v1/organization/people/{school.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), version, "تأیید معتبر"), JsonOptions);
        first.EnsureSuccessStatusCode();
        using var stale = await managerClient.PostAsJsonAsync($"/api/v1/organization/people/{school.PersonId}/communication-age/verify",
            new VerifyCommunicationAgeCommand(new DateOnly(1990, 1, 1), version, "نسخه قدیمی"), JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var claim = await childClient.PatchAsJsonAsync("/api/v1/me/profile", new { firstName = "نام", lastName = "کاربر", displayName = "نام", username = child.Username,
            communicationAgeCategory = "Adult", birthDate = "1990-01-01" }, JsonOptions);
        Assert.Equal(CommunicationAgeCategory.Minor, (await factory.ReadAgeStateAsync(child.UserId)).Classification);
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(child.UserId));
    }

    private async Task<PolicySchool> CreatePolicySchoolAsync(HttpClient managerClient, MessagingUser manager, MessagingUser learner)
    {
        var org = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        var studentMembership = await factory.CreateMembershipAsync(learner.UserId, org, OrganizationRole.Student);
        var person = await factory.CreateOrganizationPersonAsync(org, await factory.GetUserPersonIdAsync(learner.UserId));
        var academic = await factory.CreateAcademicClassAsync(org);
        var enrollment = await factory.CreateEnrollmentAsync(org, academic.ClassId, person);
        await SelectOrganizationAsync(managerClient, membership.MembershipId, OrganizationRole.Manager);
        return new PolicySchool(org, person, academic.ClassId, enrollment, studentMembership.MembershipId);
    }
    [Fact]
    public async Task ConcurrentAgeVerificationCannotSilentlyOverwriteAnotherDecision()
    {
        using var managerClient = factory.CreateClient(); using var childClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var child = await CreateMessagingUserAsync(childClient);
        var school = await CreatePolicySchoolAsync(managerClient, manager, child);
        var state = await factory.ReadAgeStateAsync(child.UserId);
        var command = new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "بررسی هم‌زمان شاهد");
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => managerClient.PostAsJsonAsync(
            $"/api/v1/organization/people/{school.PersonId}/communication-age/verify", command, JsonOptions)));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict);
        }
        finally { foreach (var result in results) result.Dispose(); }
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(child.UserId));
    }
    private sealed record PolicySchool(Guid OrganizationId, Guid PersonId, Guid ClassId, Guid EnrollmentId, Guid StudentMembershipId);
}
