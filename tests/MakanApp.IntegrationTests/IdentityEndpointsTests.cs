using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class IdentityEndpointsTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task RequestOtpProtectsProviderCodeAndNeverReturnsIt()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000001";
        const string providerCode = "opaque-provider-A7";
        var invocationCount = factory.OtpProvider.InvocationCount;
        factory.OtpProvider.OverrideVerificationCode = providerCode;
        try
        {
            using var response = await RequestOtpAsync(client, phoneNumber);
            var body = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync<RequestOtpResult>();

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ChallengeId);
            Assert.Equal(invocationCount + 1, factory.OtpProvider.InvocationCount);
            Assert.DoesNotContain(providerCode, body, StringComparison.Ordinal);
            Assert.DoesNotContain("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.True(await factory.IsChallengeProtectedWithCodeAsync(
                result.ChallengeId,
                providerCode));
        }
        finally
        {
            factory.OtpProvider.OverrideVerificationCode = null;
        }
    }

    [Fact]
    public async Task RequestOtpRejectsInvalidPhone()
    {
        using var client = factory.CreateClient();

        using var response = await RequestOtpAsync(client, "invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.PhoneInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RequestOtpProviderFailureDoesNotClaimSuccessOrCreateChallenge()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000002";
        var invocationCount = factory.OtpProvider.InvocationCount;
        var challengeCount = await factory.CountOtpChallengesAsync(phoneNumber);
        factory.OtpProvider.Fail = true;
        try
        {
            using var response = await RequestOtpAsync(client, phoneNumber);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(IdentityErrorCodes.SmsProviderUnavailable, await ReadProblemCodeAsync(response));
            Assert.DoesNotContain(phoneNumber, body, StringComparison.Ordinal);
            Assert.Equal(invocationCount + 1, factory.OtpProvider.InvocationCount);
            Assert.Equal(
                challengeCount,
                await factory.CountOtpChallengesAsync(phoneNumber));
        }
        finally
        {
            factory.OtpProvider.Fail = false;
        }
    }

    [Fact]
    public async Task RequestOtpRejectsUnusableProviderCodeWithoutCreatingChallenge()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000020";
        const string unusableProviderCode = "~";
        var challengeCount = await factory.CountOtpChallengesAsync(phoneNumber);
        factory.OtpProvider.OverrideVerificationCode = unusableProviderCode;
        try
        {
            using var response = await RequestOtpAsync(client, phoneNumber);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(
                IdentityErrorCodes.SmsProviderUnavailable,
                await ReadProblemCodeAsync(response));
            Assert.DoesNotContain(unusableProviderCode, body, StringComparison.Ordinal);
            Assert.Equal(
                challengeCount,
                await factory.CountOtpChallengesAsync(phoneNumber));
        }
        finally
        {
            factory.OtpProvider.OverrideVerificationCode = null;
        }
    }

    [Fact]
    public async Task VerifyOtpAcceptsValidCode()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000003";
        const string providerCode = "A7-provider-managed-code";
        factory.OtpProvider.OverrideVerificationCode = providerCode;
        try
        {
            var challenge = await CreateChallengeAsync(client, phoneNumber);
            using var response = await VerifyOtpAsync(
                client,
                challenge.ChallengeId,
                phoneNumber,
                providerCode);
            var verified = await response.Content.ReadFromJsonAsync<VerifyOtpResult>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(verified);
            Assert.False(string.IsNullOrWhiteSpace(verified.AccessToken));
            Assert.Equal("Bearer", verified.TokenType);
        }
        finally
        {
            factory.OtpProvider.OverrideVerificationCode = null;
        }
    }

    [Theory]
    [InlineData("۰۱۲۳۴۵۶۷۸۹")]
    [InlineData("٠١٢٣٤٥٦٧٨٩")]
    public async Task DevelopmentProviderOtpRoundTripAcceptsLocalizedDigits(
        string localizedDigits)
    {
        using var client = factory.CreateClient();
        const string localPhoneNumber = "۰۹۱۲۰۰۰۰۰۱۶";
        const string canonicalPhoneNumber = "+989120000016";
        var challenge = await CreateChallengeAsync(client, localPhoneNumber);
        var localizedCode = ToLocalizedDigits(
            factory.GetLatestOtpCode(localPhoneNumber),
            localizedDigits);

        using var response = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            canonicalPhoneNumber,
            localizedCode);
        var result = await response.Content.ReadFromJsonAsync<VerifyOtpResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(canonicalPhoneNumber, result.User.PhoneNumber);
    }

    [Fact]
    public async Task VerifyOtpRejectsInvalidCode()
    {
        using var client = factory.CreateClient();
        var challenge = await CreateChallengeAsync(client, "+989120000004");

        using var response = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            "+989120000004",
            "999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.OtpInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task VerifyOtpRejectsExpiredCode()
    {
        using var client = factory.CreateClient();
        var challenge = await CreateChallengeAsync(client, "+989120000005");
        await factory.ExpireChallengeAsync(challenge.ChallengeId);

        using var response = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            "+989120000005",
            factory.GetLatestOtpCode("+989120000005"));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.OtpExpired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ConsumedOtpCannotBeReused()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000006";
        var challenge = await CreateChallengeAsync(client, phoneNumber);
        var code = factory.GetLatestOtpCode(phoneNumber);

        using var firstResponse = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            phoneNumber,
            code);
        using var secondResponse = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            phoneNumber,
            code);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(IdentityErrorCodes.OtpAlreadyUsed, await ReadProblemCodeAsync(secondResponse));
    }

    [Fact]
    public async Task NewDevelopmentChallengeInvalidatesPreviousCodeButAcceptsLatestCode()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000017";
        var firstChallenge = await CreateChallengeAsync(client, phoneNumber);
        var firstCode = factory.GetLatestOtpCode(phoneNumber);
        var latestChallenge = await CreateChallengeAsync(client, phoneNumber);
        var latestCode = factory.GetLatestOtpCode(phoneNumber);

        using var supersededResponse = await VerifyOtpAsync(
            client,
            firstChallenge.ChallengeId,
            phoneNumber,
            firstCode);
        using var latestResponse = await VerifyOtpAsync(
            client,
            latestChallenge.ChallengeId,
            phoneNumber,
            latestCode);

        Assert.Equal(HttpStatusCode.Conflict, supersededResponse.StatusCode);
        Assert.Equal(
            IdentityErrorCodes.OtpAlreadyUsed,
            await ReadProblemCodeAsync(supersededResponse));
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);
    }

    [Fact]
    public async Task VerifyOtpLocksChallengeAtConfiguredAttemptLimit()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000018";
        const string providerCode = "correct-provider-code";
        factory.OtpProvider.OverrideVerificationCode = providerCode;
        try
        {
            var challenge = await CreateChallengeAsync(client, phoneNumber);
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                using var response = await VerifyOtpAsync(
                    client,
                    challenge.ChallengeId,
                    phoneNumber,
                    "wrong-provider-code");
                Assert.Equal(
                    attempt == 5 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest,
                    response.StatusCode);
            }

            using var correctResponse = await VerifyOtpAsync(
                client,
                challenge.ChallengeId,
                phoneNumber,
                providerCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, correctResponse.StatusCode);
            Assert.Equal(
                IdentityErrorCodes.OtpTooManyAttempts,
                await ReadProblemCodeAsync(correctResponse));
        }
        finally
        {
            factory.OtpProvider.OverrideVerificationCode = null;
        }
    }

    [Fact]
    public async Task RequestOtpKeepsPerPhoneRateLimitBeforeCallingProviderAgain()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000019";
        for (var request = 0; request < 5; request++)
        {
            using var acceptedResponse = await RequestOtpAsync(client, phoneNumber);
            Assert.Equal(HttpStatusCode.Accepted, acceptedResponse.StatusCode);
        }

        var invocationCount = factory.OtpProvider.InvocationCount;
        using var limitedResponse = await RequestOtpAsync(client, phoneNumber);

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal(
            IdentityErrorCodes.OtpRateLimited,
            await ReadProblemCodeAsync(limitedResponse));
        Assert.Equal(invocationCount, factory.OtpProvider.InvocationCount);
    }

    [Fact]
    public async Task NewChallengeForExistingPhoneDoesNotDuplicateUser()
    {
        using var client = factory.CreateClient();
        const string phoneNumber = "+989120000007";
        var firstUser = await CreateAuthenticatedUserAsync(client, phoneNumber);
        var secondUser = await CreateAuthenticatedUserAsync(client, phoneNumber);

        Assert.Equal(firstUser.User.Id, secondUser.User.Id);
        Assert.Equal(1, await factory.CountUsersForPhoneAsync(phoneNumber));
    }

    [Fact]
    public async Task NewUserHasIncompleteProfile()
    {
        using var client = factory.CreateClient();

        var verified = await CreateAuthenticatedUserAsync(client, "+989120000008");

        Assert.False(verified.User.IsProfileComplete);
        Assert.Null(verified.User.Username);
    }

    [Fact]
    public async Task ProfileCanBeCompleted()
    {
        using var client = factory.CreateClient();
        var verified = await CreateAuthenticatedUserAsync(client, "+989120000009");
        UseBearerToken(client, verified.AccessToken);

        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand("علی", "رضایی", "علی رضایی", "ali.rezaei"));
        var profile = await response.Content.ReadFromJsonAsync<CurrentUserResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.True(profile.IsProfileComplete);
        Assert.Equal("ali.rezaei", profile.Username);
    }

    [Fact]
    public async Task GetCurrentUserRequiresAuthentication()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.AuthRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task GetCurrentUserReturnsAuthenticatedUser()
    {
        using var client = factory.CreateClient();
        var verified = await CreateAuthenticatedUserAsync(client, "+989120000011");
        UseBearerToken(client, verified.AccessToken);

        var profile = await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me");

        Assert.NotNull(profile);
        Assert.Equal(verified.User.Id, profile.Id);
        Assert.Equal("+989120000011", profile.PhoneNumber);
    }

    [Fact]
    public async Task LogoutRevokesCurrentSession()
    {
        using var client = factory.CreateClient();
        var verified = await CreateAuthenticatedUserAsync(client, "+989120000012");
        UseBearerToken(client, verified.AccessToken);

        using var response = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await factory.IsSessionRevokedAsync(verified.AccessToken));
    }

    [Fact]
    public async Task RevokedSessionCannotAccessCurrentUser()
    {
        using var client = factory.CreateClient();
        var verified = await CreateAuthenticatedUserAsync(client, "+989120000013");
        UseBearerToken(client, verified.AccessToken);
        using var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.AuthRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RefreshRotatesBothTokensAndReplayRevokesTheSessionFamily()
    {
        factory.SensitiveLogs.Clear();
        using var client = factory.CreateClient();
        var verified = await CreateAuthenticatedUserAsync(client, "+989120000021", "Windows laptop");

        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshSessionCommand(verified.RefreshToken));
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<RefreshSessionResult>();

        Assert.True(
            refreshResponse.StatusCode == HttpStatusCode.OK,
            $"status={refreshResponse.StatusCode};body={await refreshResponse.Content.ReadAsStringAsync()};logs={string.Join(" | ", factory.SensitiveLogs.Snapshot().TakeLast(10))}");
        Assert.NotNull(rotated);
        Assert.NotEqual(verified.AccessToken, rotated.AccessToken);
        Assert.NotEqual(verified.RefreshToken, rotated.RefreshToken);
        Assert.True(rotated.RefreshExpiresAtUtc > rotated.ExpiresAtUtc);

        UseBearerToken(client, verified.AccessToken);
        using var oldAccess = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldAccess.StatusCode);

        UseBearerToken(client, rotated.AccessToken);
        using var activeAccess = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, activeAccess.StatusCode);

        using var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshSessionCommand(verified.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(IdentityErrorCodes.RefreshTokenReused, await ReadProblemCodeAsync(replay));

        using var revokedAccess = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedAccess.StatusCode);
    }

    [Fact]
    public async Task UserCanListOwnDevicesAndRevokeAllExceptCurrentSession()
    {
        const string phoneNumber = "+989120000022";
        using var firstClient = factory.CreateClient();
        var first = await CreateAuthenticatedUserAsync(firstClient, phoneNumber, "First device");
        UseBearerToken(firstClient, first.AccessToken);
        using var secondClient = factory.CreateClient();
        var second = await CreateAuthenticatedUserAsync(secondClient, phoneNumber, "Second device");
        UseBearerToken(secondClient, second.AccessToken);

        var sessions = await secondClient.GetFromJsonAsync<UserSessionResult[]>("/api/v1/auth/sessions");

        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Length);
        Assert.Contains(sessions, session => session.DeviceName == "First device" && !session.IsCurrent);
        Assert.Contains(sessions, session => session.DeviceName == "Second device" && session.IsCurrent);

        using var revoke = await secondClient.PostAsync("/api/v1/auth/sessions/revoke-others", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var firstProfile = await firstClient.GetAsync("/api/v1/me");
        using var secondProfile = await secondClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, firstProfile.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondProfile.StatusCode);
    }

    [Fact]
    public async Task UsernameMustBeUniqueIgnoringCase()
    {
        using var firstClient = factory.CreateClient();
        var firstUser = await CreateAuthenticatedUserAsync(firstClient, "+989120000014");
        UseBearerToken(firstClient, firstUser.AccessToken);
        using var firstResponse = await firstClient.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand("مریم", "احمدی", null, "unique.user"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var secondClient = factory.CreateClient();
        var secondUser = await CreateAuthenticatedUserAsync(secondClient, "+989120000015");
        UseBearerToken(secondClient, secondUser.AccessToken);
        using var secondResponse = await secondClient.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand("سارا", "محمدی", null, "UNIQUE.USER"));

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(
            IdentityErrorCodes.UsernameAlreadyExists,
            await ReadProblemCodeAsync(secondResponse));
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(
        HttpClient client,
        string phoneNumber,
        string? deviceName = null)
    {
        var challenge = await CreateChallengeAsync(client, phoneNumber);
        using var response = await VerifyOtpAsync(
            client,
            challenge.ChallengeId,
            phoneNumber,
            factory.GetLatestOtpCode(phoneNumber),
            deviceName);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
    }

    private static async Task<RequestOtpResult> CreateChallengeAsync(
        HttpClient client,
        string phoneNumber)
    {
        using var response = await RequestOtpAsync(client, phoneNumber);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RequestOtpResult>())!;
    }

    private static Task<HttpResponseMessage> RequestOtpAsync(
        HttpClient client,
        string phoneNumber) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber));

    private static Task<HttpResponseMessage> VerifyOtpAsync(
        HttpClient client,
        Guid challengeId,
        string phoneNumber,
        string code,
        string? deviceName = null) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challengeId, phoneNumber, code, deviceName));

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string ToLocalizedDigits(string value, string localizedDigits) =>
        string.Concat(value.Select(character =>
            character is >= '0' and <= '9'
                ? localizedDigits[character - '0']
                : character));

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
