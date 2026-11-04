using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PlatformMembershipReadKeepsTeacherDelegationPolicyAndEndedPeriodFencesRacingGrant()
    {
        var s = await PersonalAcademicAsync(); var teacher = await ActorAsync();
        await JoinAcademicAsync(s.Manager, teacher, s.Class.Id, PersonalClassLocalRoles.Teacher);
        var member = (await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client,
            $"/api/admin/v1/personal-classes/{s.Class.Id}/memberships")).Items.Single(m => m.UserId == teacher.UserId);
        var command = new SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities.AuthorAssignments,
            Guid.NewGuid(), member.MembershipVersion, null, "تفویض با دورهٔ عضویت مشخص");
        using (var platformDenied = await s.Admin.Client.PutAsJsonAsync($"/api/admin/v1/personal-classes/{s.Class.Id}/academic-permissions/{member.MembershipId}", command, Json))
            Assert.Equal(HttpStatusCode.NotFound, platformDenied.StatusCode);
        using var end = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{s.Class.Id}/memberships/{member.MembershipId}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(member.MembershipVersion, "پایان هم‌زمان دوره"), options: Json) };
        var grantRoute = $"/api/v1/personal-classes/{s.Class.Id}/academic-permissions/{member.MembershipId}";
        var results = await Task.WhenAll(s.Manager.Client.SendAsync(end), s.Manager.Client.PutAsJsonAsync(grantRoute, command, Json));
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, results[0].StatusCode);
            Assert.True(results[1].IsSuccessStatusCode || results[1].StatusCode == HttpStatusCode.NotFound);
        }
        finally { foreach (var result in results) result.Dispose(); }
        var grants = await GetAsync<PersonalClassPage<PersonalClassAcademicPermissionDto>>(s.Manager.Client,
            $"/api/v1/personal-classes/{s.Class.Id}/academic-permissions");
        Assert.All(grants.Items.Where(g => g.MembershipId == member.MembershipId), g => Assert.False(g.IsEffective));
        using var future = await s.Manager.Client.PutAsJsonAsync(grantRoute, command with { ClientOperationId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.NotFound, future.StatusCode);
    }
    [Fact]
    public async Task PlatformMembershipReadReturnsScopedSafeIdentityAndCurrentMembershipPeriodVersion()
    {
        var s = await PersonalAcademicAsync(); var teacher = await ActorAsync();
        using (var profile = await teacher.Client.PatchAsJsonAsync("/api/v1/me/profile", new
        { firstName = "Visible", lastName = "Teacher", displayName = "Visible Teacher", username = "gap." + Guid.NewGuid().ToString("N")[..20] }, Json))
            profile.EnsureSuccessStatusCode();
        await JoinAcademicAsync(s.Manager, teacher, s.Class.Id, PersonalClassLocalRoles.Teacher);
        var route = $"/api/admin/v1/personal-classes/{s.Class.Id}/memberships";
        var members = await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route);
        Assert.Equal(4, members.TotalCount);
        var manager = Assert.Single(members.Items, m => m.UserId == s.Manager.UserId);
        Assert.True(manager.CanReceivePlatformAcademicGrant); Assert.Equal(s.Class.Id, manager.PersonalClassId);
        foreach (var member in members.Items)
        {
            Assert.Equal(8, Convert.FromBase64String(member.MembershipVersion).Length);
            Assert.Equal(await WithDb(db => db.PersonalClassParticipants.AsNoTracking().Where(m => m.Id == member.MembershipId)
                .Select(m => m.RowVersion).SingleAsync()), Convert.FromBase64String(member.MembershipVersion));
        }
        var selected = await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "?role=Teacher&search=Visible&pageSize=1");
        var row = Assert.Single(selected.Items); Assert.Equal(teacher.UserId, row.UserId); Assert.Equal("Visible Teacher", row.DisplayName);
        Assert.False(row.CanReceivePlatformAcademicGrant);
        var serialized = await s.Admin.Client.GetStringAsync(route);
        Assert.DoesNotContain(teacher.Phone, serialized); Assert.DoesNotContain("endReason", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teacherPrivateNote", serialized, StringComparison.OrdinalIgnoreCase);
        var first = await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "?pageSize=2");
        var second = await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "?page=2&pageSize=2");
        Assert.Equal(members.Items.Select(m => m.MembershipId), first.Items.Concat(second.Items).Select(m => m.MembershipId));
        using var end = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{s.Class.Id}/memberships/{row.MembershipId}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(row.MembershipVersion, "پایان عضویت برای آزمون read"), options: Json) };
        using (var ended = await s.Manager.Client.SendAsync(end)) ended.EnsureSuccessStatusCode();
        var history = await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route);
        var endedMember = Assert.Single(history.Items, m => m.MembershipId == row.MembershipId);
        Assert.Equal("Ended", endedMember.Status); Assert.NotNull(endedMember.EndedAtUtc); Assert.False(endedMember.CanReceivePlatformAcademicGrant);
        Assert.NotEqual(row.MembershipVersion, endedMember.MembershipVersion);
        Assert.DoesNotContain((await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "?activeOnly=true")).Items,
            m => m.MembershipId == row.MembershipId);
    }

    [Fact]
    public async Task PlatformMembershipReadDrivesFirstUpdateAndRevokeGrantWithIndependentVersions()
    {
        var s = await PersonalAcademicAsync();
        var classroom = await ApprovedClassAsync(s.Manager, "کلاس دوم برای اعطای نخست", reviewer: s.Admin);
        var route = $"/api/admin/v1/personal-classes/{classroom.Id}";
        var member = Assert.Single((await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "/memberships")).Items);
        var command = new SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities.AuthorAssignments,
            Guid.NewGuid(), member.MembershipVersion, null, "اعطای نخست با نسخهٔ خوانده‌شده");
        var grantRoute = route + "/academic-permissions/" + member.MembershipId;
        var first = await AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Admin.Client, HttpMethod.Put, grantRoute, command);
        var replay = await AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Admin.Client, HttpMethod.Put, grantRoute, command);
        Assert.Equal(first.Id, replay.Id); Assert.True(first.IsEffective);
        using (var mismatch = await s.Admin.Client.PutAsJsonAsync(grantRoute, command with { Capabilities = AllAcademic }, Json))
            Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        var latest = Assert.Single((await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route + "/memberships")).Items);
        var changed = await AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Admin.Client, HttpMethod.Put, grantRoute,
            command with { ClientOperationId = Guid.NewGuid(), ExpectedMembershipVersion = latest.MembershipVersion, ExpectedGrantVersion = first.Version, Capabilities = AllAcademic });
        using (var stale = await s.Admin.Client.PutAsJsonAsync(grantRoute,
            command with { ClientOperationId = Guid.NewGuid(), ExpectedGrantVersion = first.Version }, Json))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var revoked = await AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Admin.Client, HttpMethod.Put, grantRoute,
            command with { ClientOperationId = Guid.NewGuid(), ExpectedMembershipVersion = latest.MembershipVersion,
                ExpectedGrantVersion = changed.Version, Capabilities = PersonalClassAcademicCapabilities.None, Reason = "لغو صریح اختیار" });
        Assert.False(revoked.IsEffective);
        var otherMember = (await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client,
            $"/api/admin/v1/personal-classes/{s.Class.Id}/memberships")).Items.Single(m => m.UserId == s.Manager.UserId);
        using var crossClass = await s.Admin.Client.PutAsJsonAsync(route + "/academic-permissions/" + otherMember.MembershipId,
            command with { ClientOperationId = Guid.NewGuid(), ExpectedMembershipVersion = otherMember.MembershipVersion }, Json);
        Assert.Equal(HttpStatusCode.NotFound, crossClass.StatusCode);
    }

    [Fact]
    public async Task PlatformMembershipReadAndGrantRejectStalePeriodAndMissingPermission()
    {
        var s = await PersonalAcademicAsync(); var route = $"/api/admin/v1/personal-classes/{s.Class.Id}/memberships";
        using var anonymous = factory.CreateClient();
        using (var noSession = await anonymous.GetAsync(route)) Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
        foreach (var client in new[] { s.Manager.Client, s.Learner.Client, s.Peer.Client })
        { using var denied = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
        using (var missing = await s.Admin.Client.GetAsync($"/api/admin/v1/personal-classes/{Guid.NewGuid()}/memberships"))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var manager = (await GetAsync<PersonalClassPage<PlatformPersonalClassMembershipDto>>(s.Admin.Client, route)).Items.Single(m => m.UserId == s.Manager.UserId);
        await WithDb(async db =>
        {
            var member = await db.PersonalClassParticipants.SingleAsync(m => m.Id == manager.MembershipId);
            member.ChangeRoles(PersonalClassLocalRoles.Manager | PersonalClassLocalRoles.Teacher, PersonalClassDelegatedCapabilities.None);
            await db.SaveChangesAsync();
        });
        using var stale = await s.Admin.Client.PutAsJsonAsync($"/api/admin/v1/personal-classes/{s.Class.Id}/academic-permissions/{manager.MembershipId}",
            new SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities.Grade, Guid.NewGuid(), manager.MembershipVersion, s.Grant.Version, "نسخهٔ عضویت کهنه"), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var badQuery = await s.Admin.Client.GetAsync(route + "?pageSize=101"); Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);
        using var openapi = JsonDocument.Parse(await s.Admin.Client.GetStringAsync("/openapi/v1.json"));
        var operation = openapi.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/personal-classes/{classId}/memberships").GetProperty("get");
        Assert.Contains("platform.personal_classes.read", operation.GetProperty("x-platform-permissions").EnumerateArray().Select(p => p.GetString()));
    }
}
