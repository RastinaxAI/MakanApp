using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    private static string SchoolMembershipRoute(Guid id) => $"/api/v1/organization/members/{id}";
    private async Task<SchoolMemberResult> SchoolMembershipAsync(SchoolActor manager, SchoolActor member) =>
        Assert.Single((await SchoolPageAsync<SchoolMemberResult>(manager.Client,
            "/api/v1/organization/members?search=" + member.Username)).Items, m => m.MembershipId == member.MembershipId);
    private static async Task<HttpResponseMessage> DeleteSchoolAsync(HttpClient client, string route, object command)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route) { Content = JsonContent.Create(command, options: PolicyJson) };
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SchoolRoleCommandsReplayWithoutDuplicateEffectsAndNeverResurrectEndedRoles()
    {
        var org = await factory.CreateOrganizationAsync("مدیریت نقش " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var target = await SchoolActorAsync(org, OrganizationRole.Student); using var tc = target.Client;
        var member = await SchoolMembershipAsync(manager, target); var route = SchoolMembershipRoute(member.MembershipId);
        var grant = new GrantSchoolRoleCommand(OrganizationRole.Teacher, Guid.NewGuid(), member.Version, "اعطای صریح نقش");
        var responses = await Task.WhenAll(mc.PostAsJsonAsync(route + "/roles", grant, PolicyJson), mc.PostAsJsonAsync(route + "/roles", grant, PolicyJson));
        var results = new List<SchoolMembershipMutationResult>();
        foreach (var response in responses) results.Add(await SchoolReadAsync<SchoolMembershipMutationResult>(response));
        Assert.Equal(results[0].ResultRoleAssignmentId, results[1].ResultRoleAssignmentId);
        Assert.Single(results, r => r.Replayed); Assert.Equal(results[0].OperationCompletedAtUtc, results[1].OperationCompletedAtUtc);
        using var mismatch = await mc.PostAsJsonAsync(route + "/roles", grant with { Role = OrganizationRole.Manager }, PolicyJson);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        await ErrorAsync(mismatch, HttpStatusCode.Conflict, SchoolMembershipErrorCodes.OperationConflict);
        var teacher = Assert.Single(results[0].ActiveRoleAssignments, r => r.Role == OrganizationRole.Teacher);
        var change = new ChangeSchoolRoleCommand(OrganizationRole.Parent, Guid.NewGuid(), member.Version, teacher.Version, "تغییر نقش با حفظ تاریخچه");
        var changed = await SchoolReadAsync<SchoolMembershipMutationResult>(await mc.PutAsJsonAsync(route + "/roles/" + teacher.RoleAssignmentId, change, PolicyJson));
        Assert.DoesNotContain(changed.ActiveRoleAssignments, r => r.Role == OrganizationRole.Teacher);
        var replay = await SchoolReadAsync<SchoolMembershipMutationResult>(await mc.PostAsJsonAsync(route + "/roles", grant, PolicyJson));
        Assert.True(replay.Replayed); Assert.DoesNotContain(replay.ActiveRoleAssignments, r => r.Role == OrganizationRole.Teacher);
        using var stale = await mc.PutAsJsonAsync(route + "/roles/" + teacher.RoleAssignmentId, change with { ClientOperationId = Guid.NewGuid() }, PolicyJson);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var parent = Assert.Single(changed.ActiveRoleAssignments, r => r.Role == OrganizationRole.Parent);
        var end = new EndSchoolRoleCommand(Guid.NewGuid(), member.Version, parent.Version, "پایان نقش والد");
        var ended = await SchoolReadAsync<SchoolMembershipMutationResult>(await DeleteSchoolAsync(mc, route + "/roles/" + parent.RoleAssignmentId, end));
        Assert.Equal(OrganizationRole.Student, Assert.Single(ended.ActiveRoleAssignments).Role);
        Assert.True((await SchoolReadAsync<SchoolMembershipMutationResult>(await DeleteSchoolAsync(mc, route + "/roles/" + parent.RoleAssignmentId, end))).Replayed);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(3, await db.OrganizationMembershipOperations.CountAsync(o => o.OrganizationId == org));
        Assert.Equal(3, await db.AdminAuditLogs.CountAsync(a => a.OrganizationId == org && a.Action.StartsWith("organization.membership.")));
        Assert.Single(await db.RoleAssignments.Where(r => r.MembershipId == member.MembershipId && r.Status == RoleAssignmentStatus.Active).ToArrayAsync());
        Assert.True(await db.Users.AnyAsync(u => u.Id == target.UserId));
        Assert.True(await db.OutboxMessages.AnyAsync(o => o.MessageType == "platform.realtime-invalidation.v1" && o.Payload.Contains(target.UserId.ToString())));
    }

    [Fact]
    public async Task SchoolManagerCanGrantAndManageAnotherManagerWithoutPlatformPrivileges()
    {
        var org = await factory.CreateOrganizationAsync("مدیر همکار " + Guid.NewGuid());
        var a = await SchoolActorAsync(org, OrganizationRole.Manager); using var ac = a.Client;
        var b = await SchoolActorAsync(org, OrganizationRole.Teacher); using var bc = b.Client;
        var member = await SchoolMembershipAsync(a, b); var route = SchoolMembershipRoute(b.MembershipId);
        var grant = await SchoolReadAsync<SchoolMembershipMutationResult>(await ac.PostAsJsonAsync(route + "/roles",
            new GrantSchoolRoleCommand(OrganizationRole.Manager, Guid.NewGuid(), member.Version, "مدیر جدید سازمان"), PolicyJson));
        await SchoolSelectAsync(bc, b.MembershipId, OrganizationRole.Manager);
        Assert.NotEmpty((await SchoolPageAsync<SchoolMemberResult>(bc, "/api/v1/organization/members")).Items);
        var role = Assert.Single(grant.ActiveRoleAssignments, r => r.Role == OrganizationRole.Manager);
        var ended = await SchoolReadAsync<SchoolMembershipMutationResult>(await ac.PutAsJsonAsync(route + "/roles/" + role.RoleAssignmentId,
            new ChangeSchoolRoleCommand(OrganizationRole.Parent, Guid.NewGuid(), member.Version, role.Version, "تغییر مدیر همکار"), PolicyJson));
        Assert.DoesNotContain(ended.ActiveRoleAssignments, r => r.Role == OrganizationRole.Manager);
        using var revoked = await bc.GetAsync("/api/v1/organization/members"); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var platform = await ac.GetAsync("/api/admin/v1/me"); Assert.Equal(HttpStatusCode.Forbidden, platform.StatusCode);
        var end = new EndSchoolMembershipCommand(Guid.NewGuid(), member.Version, "پایان عضویت با حفظ حساب");
        var result = await SchoolReadAsync<SchoolMembershipMutationResult>(await DeleteSchoolAsync(ac, route, end));
        Assert.Equal(MembershipStatus.Ended, result.Status); Assert.Empty(result.ActiveRoleAssignments);
        Assert.NotEqual(member.Version, result.MembershipVersion);
        Assert.True((await SchoolReadAsync<SchoolMembershipMutationResult>(await DeleteSchoolAsync(ac, route, end))).Replayed);
        using var identity = await bc.GetAsync("/api/v1/me"); Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationMembershipOperationKind.EndRole)]
    [InlineData(OrganizationMembershipOperationKind.ChangeRole)]
    [InlineData(OrganizationMembershipOperationKind.EndMembership)]
    public async Task SchoolLastManagerCannotBeRemovedAndSuspendedAccountDoesNotCountAsReplacement(OrganizationMembershipOperationKind kind)
    {
        var org = await factory.CreateOrganizationAsync("آخرین مدیر " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var client = manager.Client;
        var suspended = await SchoolActorAsync(org, OrganizationRole.Manager); using var suspendedClient = suspended.Client;
        await using (var setup = _host.Services.CreateAsyncScope())
        { var db = setup.ServiceProvider.GetRequiredService<MakanDbContext>(); (await db.Users.SingleAsync(u => u.Id == suspended.UserId)).Suspend(Now); await db.SaveChangesAsync(); }
        var member = await SchoolMembershipAsync(manager, manager); var role = Assert.Single(member.RoleAssignments, r => r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active);
        var route = SchoolMembershipRoute(member.MembershipId);
        using var response = kind switch
        {
            OrganizationMembershipOperationKind.EndRole => await DeleteSchoolAsync(client, route + "/roles/" + role.RoleAssignmentId,
                new EndSchoolRoleCommand(Guid.NewGuid(), member.Version, role.Version, "آزمون محافظ مدیر")),
            OrganizationMembershipOperationKind.ChangeRole => await client.PutAsJsonAsync(route + "/roles/" + role.RoleAssignmentId,
                new ChangeSchoolRoleCommand(OrganizationRole.Teacher, Guid.NewGuid(), member.Version, role.Version, "آزمون محافظ مدیر"), PolicyJson),
            _ => await DeleteSchoolAsync(client, route, new EndSchoolMembershipCommand(Guid.NewGuid(), member.Version, "آزمون محافظ مدیر"))
        };
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ErrorAsync(response, HttpStatusCode.Conflict, SchoolMembershipErrorCodes.LastManagerRequired);
        await using var scope = _host.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().OrganizationMembershipOperations.AnyAsync(o => o.OrganizationId == org));
    }

    [Fact]
    public async Task ConcurrentManagerSelfRemovalLeavesExactlyOneActiveManager()
    {
        var org = await factory.CreateOrganizationAsync("رقابت مدیران " + Guid.NewGuid());
        var a = await SchoolActorAsync(org, OrganizationRole.Manager); using var ac = a.Client;
        var b = await SchoolActorAsync(org, OrganizationRole.Manager); using var bc = b.Client;
        var am = await SchoolMembershipAsync(a, a); var bm = await SchoolMembershipAsync(a, b);
        var ar = Assert.Single(am.RoleAssignments); var br = Assert.Single(bm.RoleAssignments);
        var responses = await Task.WhenAll(
            DeleteSchoolAsync(ac, SchoolMembershipRoute(a.MembershipId) + "/roles/" + ar.RoleAssignmentId, new EndSchoolRoleCommand(Guid.NewGuid(), am.Version, ar.Version, "پایان خود با جانشین")),
            DeleteSchoolAsync(bc, SchoolMembershipRoute(b.MembershipId) + "/roles/" + br.RoleAssignmentId, new EndSchoolRoleCommand(Guid.NewGuid(), bm.Version, br.Version, "پایان خود با جانشین")));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await AssertOneSchoolManagerAsync(org);
    }

    [Fact]
    public async Task ConcurrentManagersCannotRemoveEachOtherAfterLosingAuthority()
    {
        var org = await factory.CreateOrganizationAsync("رقابت پایان عضویت مدیر " + Guid.NewGuid());
        var a = await SchoolActorAsync(org, OrganizationRole.Manager); using var ac = a.Client;
        var b = await SchoolActorAsync(org, OrganizationRole.Manager); using var bc = b.Client;
        var am = await SchoolMembershipAsync(a, a); var bm = await SchoolMembershipAsync(a, b);
        var responses = await Task.WhenAll(
            DeleteSchoolAsync(ac, SchoolMembershipRoute(b.MembershipId), new EndSchoolMembershipCommand(Guid.NewGuid(), bm.Version, "پایان مدیر دیگر")),
            DeleteSchoolAsync(bc, SchoolMembershipRoute(a.MembershipId), new EndSchoolMembershipCommand(Guid.NewGuid(), am.Version, "پایان مدیر دیگر")));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Forbidden);
        foreach (var response in responses) response.Dispose();
        await AssertOneSchoolManagerAsync(org);
    }

    [Fact]
    public async Task PlatformAndSchoolMutationsShareLastManagerSerialization()
    {
        var org = await factory.CreateOrganizationAsync("قفل مشترک Platform و School " + Guid.NewGuid());
        var a = await SchoolActorAsync(org, OrganizationRole.Manager); using var ac = a.Client;
        var b = await SchoolActorAsync(org, OrganizationRole.Manager); using var bc = b.Client;
        using var owner = _host.CreateClient(); _ = await OwnerAsync(owner);
        var bm = await SchoolMembershipAsync(a, b);
        var responses = await Task.WhenAll(
            DeleteSchoolAsync(owner, $"/api/admin/v1/organizations/{org}/memberships/{a.MembershipId}", new EndPlatformMembershipCommand("پایان مدیر با Platform")),
            DeleteSchoolAsync(bc, SchoolMembershipRoute(b.MembershipId), new EndSchoolMembershipCommand(Guid.NewGuid(), bm.Version, "پایان مدیر با School")));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var schoolOperations = responses[1].StatusCode == HttpStatusCode.OK ? 1 : 0;
        foreach (var response in responses) response.Dispose();
        await AssertOneSchoolManagerAsync(org, schoolOperations);
    }

    private async Task AssertOneSchoolManagerAsync(Guid org, int expectedOperations = 1)
    {
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var count = await (from m in db.Memberships join r in db.RoleAssignments on m.Id equals r.MembershipId
            join u in db.Users on m.UserId equals u.Id where m.OrganizationId == org && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null && u.AccountStatus == UserAccountStatus.Active select m.Id).CountAsync();
        Assert.Equal(1, count); Assert.Equal(expectedOperations, await db.OrganizationMembershipOperations.CountAsync(o => o.OrganizationId == org));
    }

    [Theory]
    [InlineData("role")]
    [InlineData("membership")]
    [InlineData("session")]
    [InlineData("account")]
    [InlineData("organization")]
    public async Task SchoolRoleStoreRechecksAuthorityEvenWithPreviouslyResolvedContext(string invalidated)
    {
        var org = await factory.CreateOrganizationAsync("بازبینی مجوز مدیر " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var target = await SchoolActorAsync(org, OrganizationRole.Student); using var tc = target.Client;
        var member = await SchoolMembershipAsync(manager, target);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var session = await db.UserSessions.SingleAsync(s => s.UserId == manager.UserId && s.RevokedAtUtc == null);
        var actor = await scope.ServiceProvider.GetRequiredService<IAccessContextResolver>().ResolveAsync(manager.UserId, session.Id, default);
        switch (invalidated)
        {
            case "role": (await db.RoleAssignments.SingleAsync(r => r.MembershipId == manager.MembershipId && r.Role == OrganizationRole.Manager)).End(Now); break;
            case "membership": (await db.Memberships.SingleAsync(m => m.Id == manager.MembershipId)).End(Now); break;
            case "session": session.Revoke(Now); break;
            case "account": (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Suspend(Now); break;
            case "organization": (await db.Organizations.SingleAsync(o => o.Id == org)).Suspend(); break;
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var command = new SchoolMembershipMutation(OrganizationMembershipOperationKind.GrantRole, Guid.NewGuid(), member.Version,
            null, null, OrganizationRole.Teacher, "زمینهٔ قدیمی مجوز نیست", "role-authority-regression");
        var denied = await Assert.ThrowsAsync<OrganizationException>(() => scope.ServiceProvider.GetRequiredService<ISchoolMembershipManagementStore>()
            .ExecuteAsync(actor, target.MembershipId, command, default));
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, denied.Code);
        Assert.False(await db.OrganizationMembershipOperations.AnyAsync(o => o.OrganizationId == org));
    }

    [Fact]
    public async Task SchoolRoleCommandsRejectStaleMembershipRoleAndForgedRolePeriod()
    {
        var org = await factory.CreateOrganizationAsync("نسخهٔ مستقل دوره‌ها " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var teacher = await SchoolActorAsync(org, OrganizationRole.Teacher); using var tc = teacher.Client;
        var member = await SchoolMembershipAsync(manager, teacher); var role = Assert.Single(member.RoleAssignments);
        var route = SchoolMembershipRoute(teacher.MembershipId) + "/roles/";
        var zero = Convert.ToBase64String(new byte[8]);
        var command = new EndSchoolRoleCommand(Guid.NewGuid(), member.Version, role.Version, "نسخهٔ مستقل نقش");
        using var staleMember = await DeleteSchoolAsync(mc, route + role.RoleAssignmentId, command with { ExpectedMembershipVersion = zero });
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleMember.StatusCode);
        using var staleRole = await DeleteSchoolAsync(mc, route + role.RoleAssignmentId, command with { ExpectedRoleVersion = zero });
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleRole.StatusCode);
        var managerRole = Assert.Single((await SchoolMembershipAsync(manager, manager)).RoleAssignments);
        using var forged = await DeleteSchoolAsync(mc, route + managerRole.RoleAssignmentId, command with { ExpectedRoleVersion = managerRole.Version });
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        using var missing = await mc.PostAsJsonAsync(SchoolMembershipRoute(Guid.NewGuid()) + "/roles",
            new GrantSchoolRoleCommand(OrganizationRole.Parent, Guid.NewGuid(), member.Version, "عضویت ناموجود"), PolicyJson);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var sameRole = await mc.PutAsJsonAsync(route + role.RoleAssignmentId,
            new ChangeSchoolRoleCommand(OrganizationRole.Teacher, Guid.NewGuid(), member.Version, role.Version, "نقش یکسان"), PolicyJson);
        Assert.Equal(HttpStatusCode.BadRequest, sameRole.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    public async Task SchoolRoleManagementRejectsNonManagerAndCrossOrganizationSelectors(OrganizationRole role)
    {
        var org = await factory.CreateOrganizationAsync("مرز نقش " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var actor = await SchoolActorAsync(org, role); using var client = actor.Client;
        var member = await SchoolMembershipAsync(manager, actor);
        var command = new GrantSchoolRoleCommand(OrganizationRole.Manager, Guid.NewGuid(), member.Version, "آزمون مرز مجوز");
        var route = SchoolMembershipRoute(member.MembershipId) + "/roles";
        using var denied = await client.PostAsJsonAsync(route, command, PolicyJson); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var other = await SchoolActorAsync(await factory.CreateOrganizationAsync("سازمان دیگر " + Guid.NewGuid()), OrganizationRole.Manager); using var oc = other.Client;
        using var hidden = await oc.PostAsJsonAsync(route, command, PolicyJson); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var anonymous = _host.CreateClient(); using var auth = await anonymous.PostAsJsonAsync(route, command, PolicyJson); Assert.Equal(HttpStatusCode.Unauthorized, auth.StatusCode);
        foreach (var invalid in new[] { command with { ClientOperationId = Guid.Empty }, command with { Role = (OrganizationRole)999 },
            command with { ExpectedMembershipVersion = "invalid" }, command with { Reason = " " } })
        { using var bad = await mc.PostAsJsonAsync(route, invalid, PolicyJson); Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); }
    }

    [Fact]
    public async Task EndingTeacherRoleRevokesCurrentAccessWithoutDeletingAssignmentsEnrollmentsOrAccounts()
    {
        var org = await factory.CreateOrganizationAsync("لغو دسترسی جاری مدرس " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var teacher = await SchoolActorAsync(org, OrganizationRole.Teacher); using var tc = teacher.Client;
        var source = await factory.CreateAcademicClassAsync(org); var member = await SchoolMembershipAsync(manager, teacher);
        var assignment = await SchoolReadAsync<TeacherAssignmentResult>(await mc.PostAsJsonAsync($"/api/v1/academic/classes/{source.ClassId}/teachers",
            new AssignTeacherCommand(teacher.MembershipId), PolicyJson), HttpStatusCode.Created);
        using var before = await tc.GetAsync($"/api/v1/academic/classes/{source.ClassId}/learners"); Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var role = Assert.Single(member.RoleAssignments);
        await SchoolReadAsync<SchoolMembershipMutationResult>(await DeleteSchoolAsync(mc, SchoolMembershipRoute(teacher.MembershipId) + "/roles/" + role.RoleAssignmentId,
            new EndSchoolRoleCommand(Guid.NewGuid(), member.Version, role.Version, "لغو مجوز مدرس")));
        using var after = await tc.GetAsync($"/api/v1/academic/classes/{source.ClassId}/learners"); Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        using var identity = await tc.GetAsync("/api/v1/me"); Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(TeacherAssignmentStatus.Active, (await db.TeacherAssignments.SingleAsync(t => t.Id == assignment.Id)).Status);
        Assert.True((await db.Memberships.SingleAsync(m => m.Id == teacher.MembershipId)).IsActive);
        Assert.True(await db.Users.AnyAsync(u => u.Id == teacher.UserId));
    }

    [Fact]
    public async Task SchoolRoleReceiptHasUniqueIntentScopedForeignKeyAndAppendOnlyProtection()
    {
        var org = await factory.CreateOrganizationAsync("رسید نقش " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var mc = manager.Client;
        var target = await SchoolActorAsync(org, OrganizationRole.Student); using var tc = target.Client;
        var member = await SchoolMembershipAsync(manager, target); var key = Guid.NewGuid();
        await SchoolReadAsync<SchoolMembershipMutationResult>(await mc.PostAsJsonAsync(SchoolMembershipRoute(target.MembershipId) + "/roles",
            new GrantSchoolRoleCommand(OrganizationRole.Teacher, key, member.Version, "ایجاد رسید"), PolicyJson));
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var receipt = await db.OrganizationMembershipOperations.SingleAsync(o => o.OrganizationId == org);
        db.Entry(receipt).Property(o => o.RequestHash).CurrentValue = new string('B', 64);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.OrganizationMembershipOperations.Add(OrganizationMembershipOperation.Record(org, manager.UserId, target.MembershipId,
            key, OrganizationMembershipOperationKind.EndMembership, new string('C', 64), null, Now));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(((SqlException)duplicate.InnerException!).Number, new[] { 2601, 2627 }); db.ChangeTracker.Clear();
        var foreignOrg = await factory.CreateOrganizationAsync("رسید مرز سازمان " + Guid.NewGuid());
        db.OrganizationMembershipOperations.Add(OrganizationMembershipOperation.Record(foreignOrg, manager.UserId, target.MembershipId,
            Guid.NewGuid(), OrganizationMembershipOperationKind.EndMembership, new string('D', 64), null, Now));
        var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(547, ((SqlException)foreign.InnerException!).Number);
    }

    [Fact]
    public async Task SchoolMembershipOpenApiHasNamedContractsAndIndependentVersionRequirements()
    {
        using var client = _host.CreateClient(); using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths"); var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var routes = new[] { ("/api/v1/organization/members/{membershipId}/roles", "post"),
            ("/api/v1/organization/members/{membershipId}/roles/{roleAssignmentId}", "put"),
            ("/api/v1/organization/members/{membershipId}/roles/{roleAssignmentId}", "delete"), ("/api/v1/organization/members/{membershipId}", "delete") };
        foreach (var (route, method) in routes)
        {
            var operation = paths.GetProperty(route).GetProperty(method); Assert.True(operation.TryGetProperty("summary", out _));
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "412" }) Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        var required = schemas.GetProperty("ChangeSchoolRoleCommand").GetProperty("required").EnumerateArray().Select(v => v.GetString()).ToArray();
        Assert.Contains("expectedMembershipVersion", required); Assert.Contains("expectedRoleVersion", required); Assert.Contains("clientOperationId", required);
        Assert.True(schemas.GetProperty("SchoolMembershipMutationResult").GetProperty("properties").GetProperty("activeRoleAssignments").GetProperty("items").TryGetProperty("$ref", out _));
    }
}
