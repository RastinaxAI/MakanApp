using System.Net;
using System.Text.Json;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task OpenApiDescribesOpaqueSessionAndCurrentPlatformPermissionsWithoutRemovedFactors()
    {
        using var anonymous = _host.CreateClient();
        var json = await anonymous.GetStringAsync("/openapi/v1.json");
        // سند جاری نباید requirement حذف‌شده یا کد خطای تاریخی عامل دوم را advertise کند.
        Assert.DoesNotMatch(@"(?i)\bMFA\b|step[- _]?up|\bTOTP\b|Authenticator|Recovery[- _]?Codes?|PLATFORM_STEP_UP_REQUIRED", json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("Opaque", bearer.GetProperty("bearerFormat").GetString());
        var paths = root.GetProperty("paths");
        foreach (var route in new[] { "/api/v1/auth/otp/challenges", "/api/v1/auth/otp/verify", "/api/v1/auth/refresh" })
            Assert.Empty(Assert.Single(paths.GetProperty(route).GetProperty("post").GetProperty("security").EnumerateArray()).EnumerateObject());

        var methods = new[] { "get", "post", "put", "patch", "delete", "head", "options" };
        var admin = paths.EnumerateObject().Where(p => p.Name.StartsWith("/api/admin/v1/", StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Where(m => methods.Contains(m.Name))
                .Select(m => (Path: p.Name, Operation: m.Value))).ToArray();
        Assert.NotEmpty(admin);
        Assert.All(admin, operation => Assert.True(operation.Operation.GetProperty("security")[0].TryGetProperty("Bearer", out _)));
        foreach (var group in new[] { "/api/admin/v1/me", "/api/admin/v1/roles", "/api/admin/v1/personal-class-requests", "/api/admin/v1/commerce/", "/api/admin/v1/operations/" })
        {
            var target = admin.Where(o => group.EndsWith('/') || group.EndsWith("requests", StringComparison.Ordinal)
                ? o.Path.StartsWith(group, StringComparison.Ordinal) : o.Path == group).ToArray();
            Assert.NotEmpty(target);
            Assert.All(target, operation => Assert.NotEmpty(operation.Operation.GetProperty("x-platform-permissions").EnumerateArray()));
        }

        foreach (var route in new[] { "/api/v1/me", "/api/admin/v1/me", "/api/admin/v1/roles" })
        {
            using var denied = await anonymous.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var ordinary = _host.CreateClient();
        _ = await AuthenticateAsync(ordinary);
        using (var account = await ordinary.GetAsync("/api/v1/me")) Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        foreach (var route in new[] { "/api/admin/v1/me", "/api/admin/v1/roles" })
        {
            using var denied = await ordinary.GetAsync(route);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var owner = _host.CreateClient();
        _ = await OwnerAsync(owner);
        using (var me = await owner.GetAsync("/api/admin/v1/me"))
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            var result = JsonSerializer.Deserialize<PlatformMeResult>(await me.Content.ReadAsStringAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Contains(PlatformPermissionKeys.Access, result.Permissions);
            Assert.Contains(PlatformPermissionKeys.RolesRead, result.Permissions);
        }
        using var roles = await owner.GetAsync("/api/admin/v1/roles");
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
    }
}
