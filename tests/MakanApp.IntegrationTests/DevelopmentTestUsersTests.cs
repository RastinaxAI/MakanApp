using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class DevelopmentTestUsersTests : IAsyncLifetime
{
    private const string PhoneUpdateScript = "update-development-test-user-phones-to-iranian.sql";
    private readonly MakanAppWebApplicationFactory _factory = new("MakanApp_DevelopmentTestUsers_" + Guid.NewGuid().ToString("N"));
    private readonly string _code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
    private WebApplicationFactory<Program> _bypass = null!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static Guid UserId(int number) => Guid.Parse($"d3700000-0000-0000-0000-{number:D12}");
    private static string Phone(int number) => "+989120000" + (100 + number).ToString("D3");

    public async Task InitializeAsync()
    {
        await ((IAsyncLifetime)_factory).InitializeAsync();
        await ExecuteSeedAsync();
        _bypass = CreateApplication();
    }

    public async Task DisposeAsync()
    {
        _bypass.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    private WebApplicationFactory<Program> CreateApplication(Dictionary<string, string?>? overrides = null) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Identity:Otp:Mode"] = "DevelopmentBypass",
                    ["Identity:Otp:DevelopmentCode"] = _code
                };
                for (var number = 1; number <= 11; number++)
                {
                    values[$"Identity:Otp:TestUsers:{number - 1}:PhoneNumber"] = Phone(number);
                    values[$"Identity:Otp:TestUsers:{number - 1}:UserId"] = UserId(number).ToString();
                }
                if (overrides is not null) foreach (var item in overrides) values[item.Key] = item.Value;
                configuration.AddInMemoryCollection(values);
            });
            // factory عادی provider پیامک را fault-injection می‌کند؛ این میزبان adapter واقعی bypass را می‌سنجد.
            builder.ConfigureTestServices(services =>
            {
                // Options پروژه هنگام composition ساخته می‌شوند؛ override دیرهنگام WAF باید همان Load واقعی را اجرا کند.
                var values = new ConfigurationManager
                {
                    ["Identity:Otp:Mode"] = "DevelopmentBypass",
                    ["Identity:Otp:DevelopmentCode"] = _code
                };
                for (var number = 1; number <= 11; number++)
                {
                    values[$"Identity:Otp:TestUsers:{number - 1}:PhoneNumber"] = Phone(number);
                    values[$"Identity:Otp:TestUsers:{number - 1}:UserId"] = UserId(number).ToString();
                }
                if (overrides is not null) foreach (var item in overrides) values[item.Key] = item.Value;
                var bypassOptions = DevelopmentOtpBypassOptions.Load(values, "Testing");
                services.RemoveAll<DevelopmentOtpBypassOptions>();
                services.AddSingleton(bypassOptions);
                services.RemoveAll<OtpOptions>();
                services.AddSingleton(new OtpOptions
                {
                    Mode = bypassOptions.Mode, DevelopmentTestUsers = bypassOptions.TestUsers,
                    ResendDelay = TimeSpan.Zero
                });
                services.RemoveAll<IOtpDeliveryProvider>();
                services.AddSingleton<IOtpDeliveryProvider, DevelopmentBypassOtpProvider>();
                services.RemoveAll<IPlatformMfaAssurance>();
                services.AddScoped<IPlatformMfaAssurance>(provider => provider.GetRequiredService<EfPlatformMfaAssurance>());
                services.RemoveAll<IPlatformStepUpAuthorization>();
                services.AddScoped<IPlatformStepUpAuthorization>(provider => provider.GetRequiredService<EfPlatformMfaAssurance>());
            });
        });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public async Task EveryRealRole_UsesNormalSessionRefreshAndCurrentAuthorization(int number)
    {
        using var client = _bypass.CreateClient();
        var result = await LoginAsync(client, number);
        Assert.Equal(UserId(number), result.User.Id);
        Assert.True(result.User.IsProfileComplete);
        Assert.False(result.AccessToken.Contains('.'));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        await using var scope = _bypass.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.True(await database.UserSessions.AnyAsync(session => session.UserId == UserId(number) &&
            session.AssuranceLevel == AuthenticationAssuranceLevel.Primary));
        Assert.False(await database.PlatformMfaCredentials.AnyAsync(factor => factor.UserId == UserId(number)));
        if (number <= 4)
        {
            var membership = await database.Memberships.SingleAsync(item => item.UserId == UserId(number));
            using var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select",
                new SelectWorkspaceCommand(WorkspaceType.Organization, membership.Id, (OrganizationRole)number), Json);
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
            var context = await selected.Content.ReadFromJsonAsync<AccessContext>(Json);
            Assert.NotNull(context);
            Assert.Equal((OrganizationRole)number, context.ActiveRole);
            Assert.Equal(UserId(number), context.UserId);
            using var forbidden = await client.GetAsync("/api/admin/v1/me");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        else
        {
            var assigned = await database.PlatformRoleAssignments.SingleAsync(item => item.UserId == UserId(number));
            Assert.True(assigned.IsActive);
            var role = await database.PlatformRoles.SingleAsync(item => item.Id == assigned.RoleId);
            Assert.Equal(PlatformRoleKeys.All.ElementAt(number - 5), role.Key);
            using var requiresMfa = await client.GetAsync("/api/admin/v1/me");
            Assert.Equal(HttpStatusCode.Forbidden, requiresMfa.StatusCode);
            using var problem = JsonDocument.Parse(await requiresMfa.Content.ReadAsStringAsync());
            Assert.Equal(PlatformErrorCodes.MfaRequired, problem.RootElement.GetProperty("code").GetString());
        }
        using var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(result.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(0, _factory.OtpProvider.InvocationCount);
    }

    [Fact]
    public async Task Seed_IsIdempotent_AndCreatesRequiredRelationships()
    {
        await ExecuteSeedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(11, await db.Users.CountAsync());
        Assert.Equal(11, await db.UserCredentials.CountAsync());
        Assert.Equal(4, await db.Memberships.CountAsync());
        Assert.Equal(4, await db.RoleAssignments.CountAsync());
        Assert.Equal(7, await db.PlatformRoleAssignments.CountAsync());
        Assert.Equal(4, await db.OrganizationPersons.CountAsync());
        Assert.Single(await db.GuardianRelations.ToListAsync());
        Assert.Single(await db.Enrollments.ToListAsync());
        Assert.Single(await db.TeacherAssignments.ToListAsync());
        for (var number = 1; number <= 11; number++)
        {
            var credential = await db.UserCredentials.SingleAsync(item => item.UserId == UserId(number));
            Assert.Equal(Phone(number), credential.NormalizedIdentifier);
        }
    }

    [Theory]
    [InlineData("09120000101")]
    [InlineData("+989120000101")]
    [InlineData("۰۹۱۲۰۰۰۰۱۰۱")]
    public async Task IranianSeedPhone_InMockMode_ResolvesExistingAccountWithoutSms(string phone)
    {
        using var app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<OtpOptions>();
                services.AddSingleton(new OtpOptions { Enabled = false });
                services.RemoveAll<IOtpDeliveryProvider>();
                services.AddSingleton<IOtpDeliveryProvider, UnavailableOtpDeliveryProvider>();
            }));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new VerifyOtpCommand(Guid.Empty, phone, "not-a-real-otp"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<VerifyOtpResult>();
        Assert.NotNull(result);
        Assert.Equal(UserId(1), result.User.Id);
        Assert.NotEmpty(result.AccessToken);
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(11, await database.Users.CountAsync());
        Assert.Empty(await database.OtpChallenges.ToListAsync());
        Assert.Equal(0, _factory.OtpProvider.InvocationCount);
    }

    [Fact]
    public async Task WrongCode_IsRejectedAndConsumesFailedAttempt()
    {
        using var client = _bypass.CreateClient();
        var challenge = await ChallengeAsync(client, Phone(1));
        var wrong = (_code[0] == '0' ? "1" : "0") + _code[1..];
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(1), wrong));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OtpChallenges.SingleAsync(item => item.Id == challenge.ChallengeId);
        Assert.Equal(1, row.FailedAttempts);
    }

    [Fact]
    public async Task ExistingNonTestAccount_AndMissingAllowedAccount_CannotUseBypass()
    {
        var ordinary = User.Create(DateTime.UtcNow);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.Users.Add(ordinary);
            db.UserCredentials.Add(UserCredential.CreateMobile(ordinary.Id, Phone(98), DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        try
        {
            using var app = CreateApplication(new()
            {
                ["Identity:Otp:TestUsers:11:PhoneNumber"] = Phone(99),
                ["Identity:Otp:TestUsers:11:UserId"] = Guid.NewGuid().ToString()
            });
            using var client = app.CreateClient();
            foreach (var phone in new[] { Phone(98), Phone(99) })
            {
                using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.Users.Remove(await db.Users.SingleAsync(item => item.Id == ordinary.Id));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BindingChangeAfterChallenge_RejectsVerifyWithoutCreatingAccount()
    {
        using var client = _bypass.CreateClient();
        var challenge = await ChallengeAsync(client, Phone(1));
        using var changed = CreateApplication(new() { ["Identity:Otp:TestUsers:0:UserId"] = Guid.NewGuid().ToString() });
        using var changedClient = changed.CreateClient();
        using var response = await changedClient.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(1), _code));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuspensionAfterChallenge_StillRejectsLogin()
    {
        using var client = _bypass.CreateClient();
        var challenge = await ChallengeAsync(client, Phone(11));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(item => item.Id == UserId(11))).Suspend(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        try
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(11), _code));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(item => item.Id == UserId(11))).Reactivate(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task DevelopmentChallenge_CannotBeRedeemedInRealMode()
    {
        using var bypassClient = _bypass.CreateClient();
        var challenge = await ChallengeAsync(bypassClient, Phone(2));
        using var realClient = _factory.CreateClient();
        foreach (var candidate in new[] { _code, "\u001fMakanApp.DevelopmentOtpBypass\u001f" + _code })
        {
            using var response = await realClient.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(2), candidate));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Challenge_RemainsHashedSingleUseAndExpiring()
    {
        using var client = _bypass.CreateClient();
        var challenge = await ChallengeAsync(client, Phone(3));
        await using (var scope = _bypass.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var row = await db.OtpChallenges.SingleAsync(item => item.Id == challenge.ChallengeId);
            var security = scope.ServiceProvider.GetRequiredService<IIdentitySecurity>();
            Assert.True(row.CodeHash.SequenceEqual(security.HashOtp("\u001fMakanApp.DevelopmentOtpBypass\u001f" + _code, row.Salt)));
            Assert.False(row.CodeHash.SequenceEqual(security.HashOtp(_code, row.Salt)));
        }
        using var first = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(3), _code));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(3), _code));
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        var expired = await ChallengeAsync(client, Phone(3));
        await _factory.ExpireChallengeAsync(expired.ChallengeId);
        using var expiry = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(expired.ChallengeId, Phone(3), _code));
        Assert.Equal(HttpStatusCode.Gone, expiry.StatusCode);
        Assert.All(_bypass.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot(), log =>
            Assert.False(log.Contains(_code, StringComparison.Ordinal)));
    }

    private async Task<RequestOtpResult> ChallengeAsync(HttpClient client, string phone)
    {
        var activeOptions = _bypass.Services.GetRequiredService<OtpOptions>();
        Assert.Equal(OtpMode.DevelopmentBypass, activeOptions.Mode);
        Assert.True(activeOptions.DevelopmentTestUsers.TryGetValue(phone, out var expectedUser));
        await using (var diagnosticScope = _bypass.Services.CreateAsyncScope())
        {
            var db = diagnosticScope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.True(await db.UserCredentials.AnyAsync(item => item.NormalizedIdentifier == phone && item.UserId == expectedUser));
            Assert.True(await db.Users.AnyAsync(item => item.Id == expectedUser && item.AccountStatus == UserAccountStatus.Active));
        }
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var responseBody = await response.Content.ReadAsStringAsync();
        using var responseDocument = JsonDocument.Parse(responseBody);
        var safeCode = responseDocument.RootElement.TryGetProperty("code", out var errorCode) ? errorCode.GetString() : null;
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, "OTP challenge status=" + (int)response.StatusCode + "; code=" + safeCode);
        Assert.False((await response.Content.ReadAsStringAsync()).Contains(_code, StringComparison.Ordinal));
        return (await response.Content.ReadFromJsonAsync<RequestOtpResult>())!;
    }

    private async Task<VerifyOtpResult> LoginAsync(HttpClient client, int number)
    {
        var challenge = await ChallengeAsync(client, Phone(number));
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(number), _code));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
    }

    [Fact]
    public async Task SqlGuard_RequiresExplicitApproval_AndCleanupPreservesRoleCatalog()
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteScriptAsync("create-development-test-users.sql", false));
        Assert.Equal(51001, failure.Number);
        await ExecuteScriptAsync("cleanup-development-test-users.sql");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(0, await db.Users.CountAsync());
            Assert.Equal(7, await db.PlatformRoles.CountAsync());
        }
        await ExecuteSeedAsync();
    }

    [Theory]
    [InlineData("AccountStatus")]
    [InlineData("CommunicationAgeCategory")]
    [InlineData("CommunicationAgeVerificationStatus")]
    public async Task MissingSecurityColumn_ReportsSchemaPrerequisiteInsteadOfCompileError(string columnName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await using var connection = new SqlConnection(
            scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedApproved', @value=N'DevelopmentOnly';";
        await command.ExecuteNonQueryAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "DevelopmentSeed", "create-development-test-users.sql"));
        // خطای ستون ناموجود به متن همان اسکریپت تزریق می‌شود؛ Schema و constraintها تغییر نمی‌کنند.
        // در صورت compile زودهنگام بدنه، این تست به‌جای پیام guard خطای واقعی 207 می‌گیرد.
        command.CommandText = script.Replace(columnName, "SeedGuardMissingColumn", StringComparison.Ordinal);

        var failure = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(51016, failure.Number);
        Assert.Contains("migration", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private Task ExecuteSeedAsync() => ExecuteScriptAsync("create-development-test-users.sql");

    [Fact]
    public async Task CredentialRemovalAfterChallenge_CannotFallThroughToAutomaticRegistration()
    {
        using var client = _bypass.CreateClient();
        var challenge = await ChallengeAsync(client, Phone(4));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.UserCredentials.Remove(await db.UserCredentials.SingleAsync(item => item.NormalizedIdentifier == Phone(4)));
            await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, Phone(4), _code));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var resultScope = _factory.Services.CreateAsyncScope();
        var resultDb = resultScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(11, await resultDb.Users.CountAsync());
        Assert.Equal(0, await resultDb.UserSessions.CountAsync());
    }

    [Fact]
    public async Task PhoneCollision_AbortsWithoutMergingAnExistingAccount()
    {
        var other = User.Create(DateTime.UtcNow);
        UserCredential original;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            original = await db.UserCredentials.SingleAsync(item => item.NormalizedIdentifier == Phone(1));
            db.UserCredentials.Remove(original);
            await db.SaveChangesAsync();
            db.Users.Add(other);
            db.UserCredentials.Add(UserCredential.CreateMobile(other.Id, Phone(1), DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        var failure = await Assert.ThrowsAsync<SqlException>(ExecuteSeedAsync);
        Assert.Equal(51003, failure.Number);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(other.Id, (await database.UserCredentials.SingleAsync(item => item.NormalizedIdentifier == Phone(1))).UserId);
        Assert.Equal(12, await database.Users.CountAsync());
    }

    [Fact]
    public async Task IranianPhoneUpdate_PreservesOtherAccountsRolesAndCredentialMetadata_AndIsIdempotent()
    {
        await UsePreviousFixturePhonesAsync();
        var ordinary = User.Create(DateTime.UtcNow);
        var ordinaryCredential = UserCredential.CreateMobile(ordinary.Id, Phone(99), DateTime.UtcNow);
        string metadata;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.Users.Add(ordinary);
            db.UserCredentials.Add(ordinaryCredential);
            await db.SaveChangesAsync();
            metadata = await CredentialMetadataAsync(db);
        }
        var protectedRows = await ProtectedRowsAsync();
        var ordinaryVersion = ordinaryCredential.RowVersion.ToArray();

        await ExecuteScriptAsync(PhoneUpdateScript);

        Assert.Equal(protectedRows, await ProtectedRowsAsync());
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(metadata, await CredentialMetadataAsync(db));
            Assert.Equal(12, await db.Users.CountAsync());
            for (var number = 1; number <= 11; number++)
            {
                var credential = await db.UserCredentials.SingleAsync(item => item.UserId == UserId(number));
                Assert.Equal(Phone(number), credential.NormalizedIdentifier);
            }
            var untouched = await db.UserCredentials.SingleAsync(item => item.Id == ordinaryCredential.Id);
            Assert.Equal(Phone(99), untouched.NormalizedIdentifier);
            Assert.Equal(ordinaryVersion, untouched.RowVersion);
        }
        var afterFirstUpdate = await CredentialVersionsAsync();
        await ExecuteScriptAsync(PhoneUpdateScript);
        Assert.Equal(afterFirstUpdate, await CredentialVersionsAsync());
    }

    [Theory]
    [InlineData(false, true, true, 51201)]
    [InlineData(true, false, true, 51202)]
    [InlineData(true, true, false, 51203)]
    public async Task IranianPhoneUpdate_RequiresEveryExplicitConnectionApproval(
        bool approved, bool databaseApproved, bool phoneUpdateApproved, int expectedCode)
    {
        await UsePreviousFixturePhonesAsync();
        var before = await CredentialVersionsAsync();
        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteScriptAsync(
            PhoneUpdateScript, approved, databaseApproved, phoneUpdateApproved));
        Assert.Equal(expectedCode, failure.Number);
        Assert.Equal(before, await CredentialVersionsAsync());
    }

    [Fact]
    public async Task IranianPhoneUpdate_CollisionAbortsAllChangesWithoutMerging()
    {
        await UsePreviousFixturePhonesAsync();
        var ordinary = User.Create(DateTime.UtcNow);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            db.Users.Add(ordinary);
            db.UserCredentials.Add(UserCredential.CreateMobile(ordinary.Id, Phone(11), DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        var before = await CredentialVersionsAsync();
        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteScriptAsync(PhoneUpdateScript));
        Assert.Equal(51206, failure.Number);
        Assert.Equal(before, await CredentialVersionsAsync());
    }

    [Fact]
    public async Task IranianPhoneUpdate_RejectsAnUnexpectedExistingPhoneWithoutOverwritingIt()
    {
        await UsePreviousFixturePhonesAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [identity].UserCredentials SET NormalizedIdentifier={Phone(99)} WHERE UserId={UserId(1)}");
        }
        var before = await CredentialVersionsAsync();
        var failure = await Assert.ThrowsAsync<SqlException>(() => ExecuteScriptAsync(PhoneUpdateScript));
        Assert.Equal(51205, failure.Number);
        Assert.Equal(before, await CredentialVersionsAsync());
    }

    [Fact]
    public async Task IranianPhoneUpdate_ConcurrentExecutionsRemainIdempotent()
    {
        await UsePreviousFixturePhonesAsync();
        await Task.WhenAll(ExecuteScriptAsync(PhoneUpdateScript), ExecuteScriptAsync(PhoneUpdateScript));
        var beforeRetry = await CredentialVersionsAsync();
        await ExecuteScriptAsync(PhoneUpdateScript);
        Assert.Equal(beforeRetry, await CredentialVersionsAsync());
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(11, await db.Users.CountAsync());
        Assert.Equal(11, await db.UserCredentials.CountAsync(item => item.NormalizedIdentifier.StartsWith("+9891200001")));
    }

    private async Task UsePreviousFixturePhonesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        for (var number = 1; number <= 11; number++)
        {
            var phone = "+1202555" + (100 + number).ToString("D4");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [identity].UserCredentials SET NormalizedIdentifier={phone} WHERE UserId={UserId(number)}");
        }
    }

    private static async Task<string> CredentialMetadataAsync(MakanDbContext db) => JsonSerializer.Serialize(
        await db.UserCredentials.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.UserId, item.Kind, item.CreatedAtUtc, item.VerifiedAtUtc }).ToArrayAsync());

    private async Task<string> CredentialVersionsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return JsonSerializer.Serialize(await db.UserCredentials.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.RowVersion }).ToArrayAsync());
    }

    private async Task<string> ProtectedRowsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return JsonSerializer.Serialize(new
        {
            Users = await db.Users.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.RowVersion }).ToArrayAsync(),
            Persons = await db.Persons.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.RowVersion }).ToArrayAsync(),
            Memberships = await db.Memberships.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.RowVersion }).ToArrayAsync(),
            Roles = await db.RoleAssignments.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.RowVersion }).ToArrayAsync(),
            PlatformRoles = await db.PlatformRoleAssignments.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.RowVersion }).ToArrayAsync()
        });
    }

    private async Task ExecuteScriptAsync(string fileName, bool approved = true,
        bool databaseApproved = true, bool phoneUpdateApproved = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = approved
            ? "EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedApproved', @value=N'DevelopmentOnly';"
            : "EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedApproved', @value=NULL;";
        await command.ExecuteNonQueryAsync();
        if (fileName == PhoneUpdateScript)
        {
            command.CommandText = "EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedDatabase', @value=@ApprovedDatabase; " +
                "EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedPhoneUpdateApproved', @value=@PhoneApproval;";
            command.Parameters.Add("@ApprovedDatabase", System.Data.SqlDbType.NVarChar, 128).Value =
                databaseApproved ? connection.Database : DBNull.Value;
            command.Parameters.Add("@PhoneApproval", System.Data.SqlDbType.NVarChar, 50).Value =
                phoneUpdateApproved ? "IranianFixturePhones" : DBNull.Value;
            await command.ExecuteNonQueryAsync();
            command.Parameters.Clear();
        }
        command.CommandText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "DevelopmentSeed", fileName));
        await command.ExecuteNonQueryAsync();
    }
}
