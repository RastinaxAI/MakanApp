using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Platform;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ProvisioningExactLookupRecoversLostResponseAndPreservesReplayConflict()
    {
        var admin = await AdminAsync();
        var command = new ProvisionOrganizationCommand("lookup-" + Guid.NewGuid().ToString("N"),
            "سازمان آزمایشی بازیابی پاسخ", null, null, null, null, "آزمون قطع پاسخ پس از commit");
        using (var lost = await admin.Client.PostAsJsonAsync("/api/admin/v1/provisioning", command, Json))
            Assert.Equal(HttpStatusCode.Created, lost.StatusCode);
        var lookup = await GetAsync<PlatformProvisioningPage>(admin.Client,
            "/api/admin/v1/provisioning?clientOperationId=" + command.ClientOperationId);
        var item = Assert.Single(lookup.Items);
        Assert.Equal(1, lookup.TotalCount); Assert.Equal(PlatformProvisioningStatus.Completed, item.Status);
        Assert.NotNull(item.OrganizationId); Assert.Equal(8, Convert.FromBase64String(item.Version).Length);
        var replay = await PostAsync<PlatformProvisioningResult>(admin.Client, "/api/admin/v1/provisioning", command);
        Assert.True(replay.Replayed); Assert.Equal(item.Id, replay.Request.Id); Assert.Equal(item.OrganizationId, replay.Request.OrganizationId);
        Assert.Equal(1, await WithDb(db => db.PlatformProvisioningRequests.CountAsync(r => r.ClientOperationId == command.ClientOperationId)));
        using var conflict = await admin.Client.PostAsJsonAsync("/api/admin/v1/provisioning", command with { OrganizationName = "قصد متفاوت" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ProvisioningExactLookupFindsOldIntentBeforePagingAndReportsCurrentSafeState()
    {
        var admin = await AdminAsync(); var prefix = "lookup-history-" + Guid.NewGuid().ToString("N");
        var organizationId = await factory.CreateOrganizationAsync("سازمان fixture خواندن");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var operationIds = await WithDb(async db =>
        {
            var rows = Enumerable.Range(0, 605).Select(i => PlatformProvisioningRequest.Create(prefix + "-" + i,
                admin.UserId, "قصد آزمایشی بدون ساخت سازمان", null, null, null, null, "fixture ایزوله", now.AddMinutes(-1000 + i))).ToArray();
            rows[1].MarkOrganizationCreated(organizationId, now);
            rows[2].MarkNeedsAttention("PROVIDER_UNAVAILABLE", now);
            rows[3].MarkOrganizationCreated(organizationId, now); rows[3].Complete(now);
            db.PlatformProvisioningRequests.AddRange(rows); await db.SaveChangesAsync();
            return rows.Take(4).Select(r => r.ClientOperationId).ToArray();
        });
        var statuses = new[] { PlatformProvisioningStatus.Requested, PlatformProvisioningStatus.OrganizationCreated,
            PlatformProvisioningStatus.NeedsAttention, PlatformProvisioningStatus.Completed };
        for (var i = 0; i < operationIds.Length; i++)
        {
            var url = "/api/admin/v1/provisioning?clientOperationId=" + Uri.EscapeDataString("  " + operationIds[i] + "  ") + "&pageSize=1";
            var result = await GetAsync<PlatformProvisioningPage>(admin.Client, url);
            var item = Assert.Single(result.Items); Assert.Equal(1, result.TotalCount); Assert.Equal(statuses[i], item.Status);
            Assert.Equal(operationIds[i], item.ClientOperationId);
            if (i == 2) Assert.Equal("PROVIDER_UNAVAILABLE", item.FailureCode);
        }
        var unknown = await GetAsync<PlatformProvisioningPage>(admin.Client, "/api/admin/v1/provisioning?clientOperationId=" + Guid.NewGuid());
        Assert.Empty(unknown.Items); Assert.Equal(0, unknown.TotalCount);
        var wrongStatus = await GetAsync<PlatformProvisioningPage>(admin.Client,
            "/api/admin/v1/provisioning?clientOperationId=" + operationIds[0] + "&status=Completed");
        Assert.Empty(wrongStatus.Items);
        var first = await GetAsync<PlatformProvisioningPage>(admin.Client, "/api/admin/v1/provisioning?pageSize=2");
        var second = await GetAsync<PlatformProvisioningPage>(admin.Client, "/api/admin/v1/provisioning?page=2&pageSize=2");
        Assert.Equal(first.TotalCount, second.TotalCount); Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(r => r.Id).Intersect(second.Items.Select(r => r.Id)));
    }

    [Fact]
    public async Task ProvisioningLookupValidatesFilterAndRequiresPlatformReadRatherThanOrganizationRole()
    {
        var admin = await AdminAsync(); var manager = await ActorAsync();
        var organizationId = await factory.CreateOrganizationAsync("سازمان مرز دسترسی lookup");
        await factory.CreateMembershipAsync(manager.UserId, organizationId, OrganizationRole.Manager);
        using var anonymous = factory.CreateClient();
        using (var denied = await anonymous.GetAsync("/api/admin/v1/provisioning?clientOperationId=test"))
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var denied = await manager.Client.GetAsync("/api/admin/v1/provisioning?clientOperationId=test"))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var query in new[] { "clientOperationId=", "clientOperationId=%20", "clientOperationId=" + new string('x', 101), "status=999", "pageSize=101" })
        { using var bad = await admin.Client.GetAsync("/api/admin/v1/provisioning?" + query); Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); }
        using var schema = JsonDocument.Parse(await admin.Client.GetStringAsync("/openapi/v1.json"));
        var operation = schema.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/provisioning").GetProperty("get");
        var filter = Assert.Single(operation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "clientOperationId");
        Assert.Equal("string", filter.GetProperty("schema").GetProperty("type").GetString());
        Assert.Contains("platform.provisioning.read", operation.GetProperty("x-platform-permissions").EnumerateArray().Select(p => p.GetString()));
    }
}
