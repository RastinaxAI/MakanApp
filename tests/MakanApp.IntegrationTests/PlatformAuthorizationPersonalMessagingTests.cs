using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Theory]
    [InlineData(1990, CommunicationAgeCategory.Adult)]
    [InlineData(2015, CommunicationAgeCategory.Minor)]
    public async Task PersonalInitialAgeVerificationUsesPlatformPermissionWithoutCreatingSchoolRelationships(
        int birthYear, CommunicationAgeCategory category)
    {
        using var ownerClient = _host.CreateClient();
        var owner = await OwnerAsync(ownerClient);
        using var personalClient = _host.CreateClient();
        var personal = await AuthenticateAsync(personalClient);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        using var profile = await personalClient.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("کاربر", "شخصی", "پروفایل شخصی", $"p0.{suffix}"));
        profile.EnsureSuccessStatusCode();
        var before = await personalClient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/me/messaging-status");
        Assert.Equal("Unverified", before.GetProperty("verificationStatus").GetString());
        Assert.Equal("Unknown", before.GetProperty("effectiveAgeCategory").GetString());
        Assert.Equal("VerificationRequired", before.GetProperty("nextAction").GetString());

        var route = $"/api/admin/v1/users/{personal.User.Id}/communication-age";
        var state = (await ownerClient.GetFromJsonAsync<CommunicationAgeResult>(route, PolicyJson))!;
        var command = new VerifyCommunicationAgeCommand(new DateOnly(birthYear, 1, 1), state.Version, "مرجع شاهد بررسی‌شدهٔ آزمون");
        using var response = await ownerClient.PostAsJsonAsync(route + "/verifications", command);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<CommunicationAgeResult>(PolicyJson))!;
        Assert.Equal(category, result.Classification);
        Assert.Equal(CommunicationAgeVerificationStatus.Verified, result.VerificationStatus);
        Assert.NotEqual(state.Version, result.Version);
        var after = await personalClient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/me/messaging-status");
        Assert.Equal(category.ToString(), after.GetProperty("effectiveAgeCategory").GetString());
        Assert.Equal("Verified", after.GetProperty("verificationStatus").GetString());
        Assert.Equal(result.Version, after.GetProperty("version").GetString());
        Assert.DoesNotContain("birthDate", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var personId = await db.Users.Where(u => u.Id == personal.User.Id).Select(u => u.PersonId).SingleAsync();
        Assert.False(await db.OrganizationPersons.AnyAsync(p => p.PersonId == personId));
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == personal.User.Id));
        Assert.False(await db.Enrollments.AnyAsync(e => db.OrganizationPersons.Any(p => p.Id == e.LearnerOrganizationPersonId && p.PersonId == personId)));
        var evidence = await db.CommunicationAgeVerifications.SingleAsync(e => e.UserId == personal.User.Id);
        Assert.Equal(owner.User.Id, evidence.ActorUserId);
        Assert.Equal("PlatformVerification", evidence.Source.ToString());
        Assert.Null(evidence.OrganizationId);
        var audit = await db.AdminAuditLogs.SingleAsync(a => a.Action == "messaging.age.initial.verified" && a.TargetId == personal.User.Id.ToString("D"));
        Assert.DoesNotContain(birthYear.ToString(), audit.AfterSummary ?? string.Empty);
    }

    [Fact]
    public async Task PersonalInitialVerificationRequiresCurrentUsersManage()
    {
        using var ownerClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        using var targetClient = _host.CreateClient();
        var target = await AuthenticateAsync(targetClient);
        var route = $"/api/admin/v1/users/{target.User.Id}/communication-age";
        var version = (await factory.ReadAgeStateAsync(target.User.Id)).Version;
        var command = new VerifyCommunicationAgeCommand(new DateOnly(1990, 1, 1), version, "شاهد اولیه");
        using var deniedRead = await targetClient.GetAsync(route);
        using var deniedWrite = await targetClient.PostAsJsonAsync(route + "/verifications", command);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        Assert.Equal(0, await factory.CountAgeEvidenceAsync(target.User.Id));
        using var verified = await ownerClient.PostAsJsonAsync(route + "/verifications", command);
        verified.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PersonalInitialVerificationCannotOverwriteVerifiedOrConflictOrStaleVersion()
    {
        using var ownerClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        using var targetClient = _host.CreateClient();
        var target = await AuthenticateAsync(targetClient);
        var route = $"/api/admin/v1/users/{target.User.Id}/communication-age";
        var state = (await ownerClient.GetFromJsonAsync<CommunicationAgeResult>(route, PolicyJson))!;
        var command = new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "شاهد اولیه کودک");
        using var first = await ownerClient.PostAsJsonAsync(route + "/verifications", command);
        first.EnsureSuccessStatusCode();
        var verified = (await first.Content.ReadFromJsonAsync<CommunicationAgeResult>(PolicyJson))!;
        using var stale = await ownerClient.PostAsJsonAsync(route + "/verifications", command);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route + "/verifications",
            command with { ExpectedVersion = verified.Version, VerifiedBirthDate = new DateOnly(1990, 1, 1) }),
            HttpStatusCode.Conflict, "AGE_VERIFICATION_CONFLICT");
        Assert.Equal(CommunicationAgeCategory.Minor, (await factory.ReadAgeStateAsync(target.User.Id)).Classification);
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(target.User.Id));
        await factory.MarkAgeConflictAsync(target.User.Id);
        state = await factory.ReadAgeStateAsync(target.User.Id);
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route + "/verifications", command with { ExpectedVersion = state.Version }),
            HttpStatusCode.Conflict, "AGE_VERIFICATION_CONFLICT");
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(target.User.Id));
    }

    [Fact]
    public async Task ConcurrentPersonalInitialVerificationAppendsExactlyOneEvidence()
    {
        using var ownerClient = _host.CreateClient();
        _ = await OwnerAsync(ownerClient);
        using var targetClient = _host.CreateClient();
        var target = await AuthenticateAsync(targetClient);
        var route = $"/api/admin/v1/users/{target.User.Id}/communication-age";
        var state = (await ownerClient.GetFromJsonAsync<CommunicationAgeResult>(route, PolicyJson))!;
        var command = new VerifyCommunicationAgeCommand(new DateOnly(1990, 1, 1), state.Version, "شاهد هم‌زمان");
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => ownerClient.PostAsJsonAsync(route + "/verifications", command)));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(1, await factory.CountAgeEvidenceAsync(target.User.Id));
    }

    [Fact]
    public async Task MessagingStatusIsSelfOnlyAndRevokedSessionCannotReadIt()
    {
        using var anonymous = _host.CreateClient();
        using var firstClient = _host.CreateClient();
        using var secondClient = _host.CreateClient();
        var first = await AuthenticateAsync(firstClient);
        var second = await AuthenticateAsync(secondClient);
        await factory.MarkAgeConflictAsync(second.User.Id);
        var state = (await factory.ReadAgeStateAsync(first.User.Id)).Version;
        using var response = await firstClient.GetAsync($"/api/v1/me/messaging-status?userId={second.User.Id}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = (await response.Content.ReadFromJsonAsync<MessagingStatusResult>(PolicyJson))!;
        Assert.Equal(state, result.Version);
        Assert.Equal(CommunicationAgeVerificationStatus.Unverified, result.VerificationStatus);
        Assert.DoesNotContain(second.User.Id.ToString(), body);
        Assert.DoesNotContain(second.User.PhoneNumber, body);
        Assert.DoesNotContain("birthDate", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/me/messaging-status")).StatusCode);
        using var logout = await firstClient.PostAsync("/api/v1/auth/logout", null);
        logout.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.GetAsync("/api/v1/me/messaging-status")).StatusCode);
    }
}
