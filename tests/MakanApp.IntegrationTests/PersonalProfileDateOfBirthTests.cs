using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task PersonalProfileWithoutDateOfBirthRemainsCompatibleWithLoginAndCompletion()
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var profile = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.True(profile.IsProfileComplete);
        Assert.Null(profile.DateOfBirth);
        Assert.Null(account.Authentication.User.DateOfBirth);

        var login = await AuthenticateAsync(client, profile.PhoneNumber);
        Assert.Equal(profile.Id, login.User.Id);
        Assert.True(login.User.IsProfileComplete);
        Assert.Null(login.User.DateOfBirth);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/v1/me"));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("dateOfBirth").ValueKind);
    }

    [Theory]
    [InlineData("2005-08-21")]
    [InlineData("2000-02-29")]
    [InlineData("1800-01-01")]
    public async Task PersonalProfileDateOfBirthRoundTripsAsDateWithoutTimezone(string input)
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var date = DateOnly.ParseExact(input, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var update = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", "پروفایل شخصی", account.Username, date));
        update.EnsureSuccessStatusCode();
        using var updated = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal(input, updated.RootElement.GetProperty("dateOfBirth").GetString());
        using var current = JsonDocument.Parse(await client.GetStringAsync("/api/v1/me"));
        Assert.Equal(input, current.RootElement.GetProperty("dateOfBirth").GetString());

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var person = await (from user in db.Users.AsNoTracking()
            join value in db.Persons.AsNoTracking() on user.PersonId equals value.Id
            where user.Id == account.UserId select value).SingleAsync();
        Assert.Equal(date, person.DateOfBirth);
        var userRecord = await db.Users.SingleAsync(u => u.Id == account.UserId);
        Assert.Equal(CommunicationAgeVerificationStatus.Unverified, userRecord.CommunicationAgeVerificationStatus);
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == account.UserId));
    }

    [Fact]
    public async Task PersonalProfileAcceptsTodayButRejectsFutureDateWithoutPersistingIt()
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var today = DateOnly.FromDateTime(Now);
        using var accepted = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, account.Username, today));
        accepted.EnsureSuccessStatusCode();
        using var future = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نباید ذخیره شود", "نام خانوادگی", null, account.Username, today.AddDays(1)));
        await ErrorAsync(future, HttpStatusCode.BadRequest, IdentityErrorCodes.ProfileValidationFailed);
        var after = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.Equal(today, after.DateOfBirth);
        Assert.Equal("نام", after.FirstName);
    }

    [Theory]
    [InlineData("2005-02-30")]
    [InlineData("21/08/2005")]
    [InlineData("2005-8-21")]
    [InlineData("2005-08-21T00:00:00Z")]
    public async Task PersonalProfileInvalidDateUsesExistingModelBindingFailure(string invalidDate)
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        using var response = await client.PatchAsJsonAsync("/api/v1/me/profile", new
        {
            firstName = "نام", lastName = "نام خانوادگی", displayName = (string?)null,
            username = account.Username, dateOfBirth = invalidDate
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null((await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.DateOfBirth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonalProfileOldClientOmissionOrNullDoesNotEraseExistingDate(bool sendNull)
    {
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var declared = new DateOnly(2005, 8, 21);
        using var initial = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, account.Username, declared));
        initial.EnsureSuccessStatusCode();
        var command = new Dictionary<string, object?>
        {
            ["firstName"] = "نام جدید", ["lastName"] = "نام خانوادگی",
            ["displayName"] = null, ["username"] = account.Username
        };
        if (sendNull) command["dateOfBirth"] = null;
        using var legacy = await client.PatchAsJsonAsync("/api/v1/me/profile", command);
        legacy.EnsureSuccessStatusCode();
        var after = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.Equal(declared, after.DateOfBirth);
        Assert.Equal("نام جدید", after.FirstName);
    }

    [Fact]
    public async Task PersonalProfileSelfDeclarationNeverChangesVerifiedBirthDateOrVerifiedAge()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, owner);
        using var update = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, account.Username, new DateOnly(2015, 4, 3)));
        update.EnsureSuccessStatusCode();
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var evidence = Assert.Single(await db.CommunicationAgeVerifications.AsNoTracking()
            .Where(v => v.UserId == account.UserId).ToArrayAsync());
        Assert.Equal(new DateOnly(1990, 1, 1), evidence.VerifiedBirthDate);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == account.UserId);
        Assert.Equal(CommunicationAgeCategory.Adult, user.CommunicationAgeCategory);
        Assert.Equal(CommunicationAgeVerificationStatus.Verified, user.CommunicationAgeVerificationStatus);
    }

    [Fact]
    public async Task PersonalAgeVerificationDoesNotBackfillOrOverwriteSelfDeclaredDate()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var declared = new DateOnly(2005, 8, 21);
        using var update = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, account.Username, declared));
        update.EnsureSuccessStatusCode();
        var route = $"/api/admin/v1/users/{account.UserId}/communication-age";
        var state = (await owner.GetFromJsonAsync<CommunicationAgeResult>(route, PolicyJson))!;
        using var verified = await owner.PostAsJsonAsync(route + "/verifications",
            new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "تأیید شاهد مستقل از خوداظهاری پروفایل"));
        verified.EnsureSuccessStatusCode();
        Assert.Equal(declared, (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.DateOfBirth);
        var status = (await client.GetFromJsonAsync<MessagingStatusResult>("/api/v1/me/messaging-status", PolicyJson))!;
        Assert.Equal(CommunicationAgeCategory.Minor, status.EffectiveAgeCategory);
    }

    [Fact]
    public async Task PersonalDateOfBirthNeverAppearsInDiscoveryContactsOrMessagingIdentity()
    {
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using var first = _host.CreateClient();
        using var second = _host.CreateClient();
        var a = await CreateP0PersonalAccountAsync(first, owner);
        var b = await CreateP0PersonalAccountAsync(second, owner);
        using var update = await second.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, b.Username, new DateOnly(2005, 8, 21)));
        update.EnsureSuccessStatusCode();
        using var contact = await first.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, "مخاطب"));
        contact.EnsureSuccessStatusCode();
        using var directResponse = await first.PostAsJsonAsync("/api/v1/conversations/direct",
            new StartDirectConversationCommand(b.UserId, MakanApp.Domain.Messaging.ConversationScope.Personal), PolicyJson);
        directResponse.EnsureSuccessStatusCode();
        var direct = (await directResponse.Content.ReadFromJsonAsync<DirectConversationResult>(PolicyJson))!;
        _ = await SendP0MessageAsync(second, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام آزمون حریم خصوصی" });
        foreach (var route in new[] { $"/api/v1/people/search?q={b.Username}", $"/api/v1/people/{b.UserId}",
            "/api/v1/contacts", "/api/v1/conversations", $"/api/v1/conversations/{direct.ConversationId}/messages" })
        {
            using var response = await first.GetAsync(route);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("dateOfBirth", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verifiedBirthDate", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2005-08-21", body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain(true, _host.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot()
            .Select(message => message.Contains("2005-08-21", StringComparison.Ordinal)));
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var audit = JsonSerializer.Serialize(await db.AdminAuditLogs.AsNoTracking()
            .Where(entry => entry.ActorUserId == b.UserId).ToArrayAsync());
        Assert.DoesNotContain("2005-08-21", audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersonalProfileOpenApiDescribesNullableDateOnlyWithoutChangingPublicSchemas()
    {
        using var client = _host.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { nameof(CompleteProfileCommand), nameof(CurrentUserResult) })
        {
            var date = schemas.GetProperty(name).GetProperty("properties").GetProperty("dateOfBirth");
            Assert.Equal("string", date.GetProperty("type").GetString());
            Assert.Equal("date", date.GetProperty("format").GetString());
            Assert.True(date.GetProperty("nullable").GetBoolean());
        }
        var request = schemas.GetProperty(nameof(CompleteProfileCommand));
        if (request.TryGetProperty("required", out var required))
            Assert.DoesNotContain(required.EnumerateArray(), v => v.GetString() == "dateOfBirth");
        foreach (var name in new[] { nameof(SafeMessagingIdentityResult), nameof(MessagingPublicProfileResult) })
            Assert.False(schemas.GetProperty(name).GetProperty("properties").TryGetProperty("dateOfBirth", out _));
    }
}
