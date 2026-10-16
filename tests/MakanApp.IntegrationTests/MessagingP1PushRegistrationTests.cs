using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using MakanApp.Application.Notification;
using MakanApp.Application.Identity;
using MakanApp.Domain.Notification;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task WebSubscriptionCrudIsPrivateVersionedIdempotentAndSafeAcrossSessions()
    {
        using var ownerClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        await CreateMessagingUserAsync(otherClient);
        var command = P1WebSubscription();
        using var createdResponse = await ownerClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", command, JsonOptions);
        createdResponse.EnsureSuccessStatusCode();
        var raw = await createdResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<PushDeviceRegistrationResult>(raw, JsonOptions)!;
        Assert.DoesNotContain(command.Endpoint, raw);
        Assert.DoesNotContain(command.Auth, raw);
        Assert.DoesNotContain(command.P256dh, raw);
        using var retryResponse = await ownerClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", command, JsonOptions);
        retryResponse.EnsureSuccessStatusCode();
        Assert.Equal(created.Id, (await retryResponse.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!.Id);
        var list = (await ownerClient.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!;
        var item = Assert.Single(list.Items);
        Assert.Equal(PushDevicePlatform.Web, item.Platform);
        Assert.True(item.IsActive);
        var replacement = P1WebSubscription() with { Endpoint = command.Endpoint, ExpectedVersion = item.Version };
        using var rotatedResponse = await ownerClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", replacement, JsonOptions);
        rotatedResponse.EnsureSuccessStatusCode();
        var rotated = (await rotatedResponse.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!;
        Assert.Equal(created.Id, rotated.Id);
        Assert.NotEqual(item.Version, rotated.Version);
        using var stale = await ownerClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push",
            command with { ExpectedVersion = item.Version }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var duplicateOwner = await otherClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", command, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, duplicateOwner.StatusCode);
        using var denied = await otherClient.DeleteAsync($"/api/v1/notifications/devices/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Empty((await otherClient.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.Items);
        using var ownerSecondSession = factory.CreateClient();
        var secondSession = await AuthenticateP1SessionAsync(ownerSecondSession, owner.PhoneNumber, factory.Services);
        UseBearerToken(ownerSecondSession, secondSession.AccessToken);
        Assert.Equal(created.Id, Assert.Single((await ownerSecondSession.GetFromJsonAsync<PushDevicePageResult>(
            "/api/v1/notifications/devices", JsonOptions))!.Items).Id);
        using var revoked = await ownerSecondSession.DeleteAsync($"/api/v1/notifications/devices/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Empty((await ownerClient.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.Items);
        var history = Assert.Single((await ownerClient.GetFromJsonAsync<PushDevicePageResult>(
            "/api/v1/notifications/devices?includeRevoked=true", JsonOptions))!.Items);
        Assert.False(history.IsActive);
        Assert.NotNull(history.RevokedAtUtc);
    }

    [Theory]
    [InlineData(PushDevicePlatform.Android)]
    [InlineData(PushDevicePlatform.Ios)]
    public async Task ConcurrentMobileRegistrationsHaveOneStableIdentity(PushDevicePlatform platform)
    {
        using var client = factory.CreateClient();
        await CreateMessagingUserAsync(client);
        var command = new RegisterPushDeviceCommand(platform, $"p1-concurrent-mobile-{Guid.NewGuid():N}");
        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync(
            "/api/v1/notifications/devices", command, JsonOptions)));
        try
        {
            var ids = new List<Guid>();
            foreach (var reply in replies)
            {
                reply.EnsureSuccessStatusCode();
                ids.Add((await reply.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!.Id);
            }
            Assert.Single(ids.Distinct());
            Assert.Equal(platform, Assert.Single((await client.GetFromJsonAsync<PushDevicePageResult>(
                "/api/v1/notifications/devices", JsonOptions))!.Items).Platform);
            using var revoked = await client.DeleteAsync($"/api/v1/notifications/devices/{ids[0]}");
            revoked.EnsureSuccessStatusCode();
        }
        finally { foreach (var reply in replies) reply.Dispose(); }
    }

    [Fact]
    public async Task SubscriptionRejectsUnapprovedEndpointBadKeyAndAnonymousAccess()
    {
        using var client = factory.CreateClient();
        var command = P1WebSubscription();
        using var anonymous = await client.PostAsJsonAsync("/api/v1/notifications/devices/web-push", command, JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await CreateMessagingUserAsync(client);
        var invalidPoint = Convert.ToBase64String(new byte[] { 4 }.Concat(new byte[64]).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        foreach (var invalid in new[] { command with { Endpoint = "http://127.0.0.1/private" },
            command with { Endpoint = "https://not-approved.test/secret" }, command with { Auth = "invalid" },
            command with { ExpectedVersion = "not-rowversion" }, command with { P256dh = invalidPoint } })
        {
            using var response = await client.PostAsJsonAsync("/api/v1/notifications/devices/web-push", invalid, JsonOptions);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, (await client.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.TotalCount);
    }

    [Fact]
    public async Task WebSubscriptionWithoutConfiguredAllowlistFailsUnavailableNotDelivered()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PushDeviceOptions>();
            services.AddSingleton(new PushDeviceOptions());
        }));
        using var client = host.CreateClient();
        _ = await AuthenticateP1SessionAsync(client, $"+9891{Interlocked.Increment(ref _phoneSequence):D8}", host.Services);
        using var response = await client.PostAsJsonAsync("/api/v1/notifications/devices/web-push", P1WebSubscription(), JsonOptions);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(NotificationPreferenceErrorCodes.PushUnavailable, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task PushDevicePaginationHasStableTieBreakersAndNoSensitiveResponseFields()
    {
        using var client = factory.CreateClient();
        await CreateMessagingUserAsync(client);
        for (var index = 0; index < 11; index++) await RegisterP1NotificationDeviceAsync(client);
        var ids = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            using var response = await client.GetAsync($"/api/v1/notifications/devices?page={page}&pageSize=3");
            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("pushToken", raw);
            Assert.DoesNotContain("p256dh", raw);
            Assert.DoesNotContain("auth", raw);
            using var json = JsonDocument.Parse(raw);
            foreach (var row in json.RootElement.GetProperty("items").EnumerateArray())
            {
                Assert.EndsWith("Z", row.GetProperty("createdAtUtc").GetString());
                Assert.EndsWith("Z", row.GetProperty("lastSeenAtUtc").GetString());
            }
            var body = (await response.Content.ReadFromJsonAsync<PushDevicePageResult>(JsonOptions))!;
            Assert.Equal(11, body.TotalCount);
            ids.AddRange(body.Items.Select(item => item.Id));
        }
        Assert.Equal(11, ids.Distinct().Count());
        foreach (var id in ids) (await client.DeleteAsync($"/api/v1/notifications/devices/{id}")).EnsureSuccessStatusCode();
    }

    private static RegisterWebPushCommand P1WebSubscription()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var p256dh = Convert.ToBase64String(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var auth = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new($"https://push.example.test/subscriptions/{Guid.NewGuid():N}", p256dh, auth);
    }

    [Fact]
    public async Task ConcurrentWebSubscriptionRetryDoesNotDuplicateOrOverwriteKeys()
    {
        using var client = factory.CreateClient();
        await CreateMessagingUserAsync(client);
        var command = P1WebSubscription();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync(
            "/api/v1/notifications/devices/web-push", command, JsonOptions)));
        try
        {
            var ids = new List<Guid>();
            foreach (var response in responses)
            {
                response.EnsureSuccessStatusCode();
                ids.Add((await response.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!.Id);
            }
            Assert.Single(ids.Distinct());
            var registration = Assert.Single((await client.GetFromJsonAsync<PushDevicePageResult>("/api/v1/notifications/devices", JsonOptions))!.Items);
            using var rotationWithoutVersion = await client.PostAsJsonAsync("/api/v1/notifications/devices/web-push",
                P1WebSubscription() with { Endpoint = command.Endpoint }, JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, rotationWithoutVersion.StatusCode);
            using var revoke = await client.DeleteAsync($"/api/v1/notifications/devices/{registration.Id}");
            revoke.EnsureSuccessStatusCode();
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task PushRegistrationOpenApiSeparatesInputSecretsFromSafeResponseModels()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/notifications/devices").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/notifications/devices/web-push").TryGetProperty("post", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var input = schemas.GetProperty(nameof(RegisterWebPushCommand)).GetProperty("properties");
        Assert.True(input.TryGetProperty("endpoint", out _));
        Assert.True(input.TryGetProperty("p256dh", out _));
        Assert.True(input.TryGetProperty("auth", out _));
        Assert.Equal("string", schemas.GetProperty(nameof(PushDevicePlatform)).GetProperty("type").GetString());
        foreach (var response in new[] { nameof(PushDeviceRegistrationResult), nameof(PushDeviceListItem) })
        {
            var properties = schemas.GetProperty(response).GetProperty("properties");
            Assert.False(properties.TryGetProperty("pushToken", out _));
            Assert.False(properties.TryGetProperty("endpoint", out _));
            Assert.False(properties.TryGetProperty("auth", out _));
            Assert.False(properties.TryGetProperty("p256dh", out _));
            Assert.True(properties.TryGetProperty("version", out _));
        }
    }

    private static async Task<VerifyOtpResult> AuthenticateP1SessionAsync(HttpClient client, string phone, IServiceProvider services)
    {
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone), JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
        using var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone)), JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        var login = (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
        UseBearerToken(client, login.AccessToken);
        return login;
    }

    [Fact]
    public async Task PushRegistrationThrottlingReturnsRetryAfterWithoutLeakingDeviceData()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api/v1/notifications/devices")
                    ? RateLimitPartition.GetFixedWindowLimiter("p1-registration", _ => new FixedWindowRateLimiterOptions
                        { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("other")));
        }));
        using var client = host.CreateClient();
        await AuthenticateP1SessionAsync(client, $"+9891{Interlocked.Increment(ref _phoneSequence):D8}", host.Services);
        client.DefaultRequestHeaders.Add("Origin", "https://messenger-client.test");
        var command = new RegisterPushDeviceCommand(PushDevicePlatform.Android, $"p1-throttle-{Guid.NewGuid():N}");
        using var accepted = await client.PostAsJsonAsync("/api/v1/notifications/devices", command, JsonOptions);
        accepted.EnsureSuccessStatusCode();
        using var rejected = await client.PostAsJsonAsync("/api/v1/notifications/devices", command, JsonOptions);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        Assert.Contains("Retry-After", string.Join(',', rejected.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(command.PushToken, await rejected.Content.ReadAsStringAsync());
    }
}
