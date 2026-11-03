using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalAcademicDelegationIsExplicitBoundedAndRevokedWithManagerGrant()
    {
        var s = await PersonalAcademicAsync(); var teacher = await ActorAsync();
        await JoinAcademicAsync(s.Manager, teacher, s.Class.Id, PersonalClassLocalRoles.Teacher);
        var create = $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments";
        using (var roleOnly = await teacher.Client.PostAsJsonAsync(create, AssignmentDraft(), Json)) Assert.Equal(HttpStatusCode.NotFound, roleOnly.StatusCode);
        var member = await WithDb(db => db.PersonalClassParticipants.AsNoTracking().SingleAsync(m => m.PersonalClassId == s.Class.Id && m.StudentUserId == teacher.UserId && m.EndedAtUtc == null));
        var command = new SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities.AuthorAssignments, Guid.NewGuid(), Convert.ToBase64String(member.RowVersion), null, "تفویض صریح آزمون");
        var route = $"/api/v1/personal-classes/{s.Class.Id}/academic-permissions/{member.Id}";
        var concurrent = await Task.WhenAll(
            AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Manager.Client, HttpMethod.Put, route, command),
            AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Manager.Client, HttpMethod.Put, route, command));
        var delegated = concurrent[0]; var replay = concurrent[1];
        Assert.Equal(delegated.Id, replay.Id); Assert.Equal(s.Grant.Id, delegated.ParentGrantId); Assert.True(delegated.IsEffective);
        _ = await PostAsync<AssignmentResult>(teacher.Client, create, AssignmentDraft());
        var classroom = await GetAsync<PersonalClassDto>(teacher.Client, $"/api/v1/personal-classes/{s.Class.Id}");
        Assert.True(classroom.Capabilities.CanAuthorAssignments); Assert.False(classroom.Capabilities.CanGrade); Assert.False(classroom.Capabilities.CanAuthorExams);
        using (var teacherEscalation = await teacher.Client.PutAsJsonAsync(route, command with { Capabilities = AllAcademic, ClientOperationId = Guid.NewGuid(), ExpectedGrantVersion = delegated.Version }, Json))
            Assert.Equal(HttpStatusCode.NotFound, teacherEscalation.StatusCode);
        await GrantAcademicAsync(s.Admin, s.Class.Id, s.Manager, PersonalClassAcademicCapabilities.None, s.Grant.Version);
        using (var revoked = await teacher.Client.PostAsJsonAsync(create, AssignmentDraft(), Json)) Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        var restricted = await GrantAcademicAsync(s.Admin, s.Class.Id, s.Manager, PersonalClassAcademicCapabilities.AuthorExams);
        using (var exceedsParent = await s.Manager.Client.PutAsJsonAsync(route, command with { ClientOperationId = Guid.NewGuid() }, Json))
            Assert.Equal(HttpStatusCode.NotFound, exceedsParent.StatusCode);
        await GrantAcademicAsync(s.Admin, s.Class.Id, s.Manager, AllAcademic, restricted.Version);
        using (var notRevived = await teacher.Client.PostAsJsonAsync(create, AssignmentDraft(), Json)) Assert.Equal(HttpStatusCode.NotFound, notRevived.StatusCode);
        await WithDb(async db =>
        {
            Assert.NotNull((await db.PersonalClassAcademicGrants.SingleAsync(g => g.Id == delegated.Id)).RevokedAtUtc);
            Assert.Equal(1, await db.PersonalClassAcademicGrants.CountAsync(g => g.GrantedByUserId == s.Manager.UserId && g.ClientOperationId == command.ClientOperationId));
        });
    }

    [Fact]
    public async Task PersonalAcademicManagerTransferDoesNotTransferAuthorityAndRevokesDependents()
    {
        var s = await PersonalAcademicAsync(); var next = await ActorAsync();
        await JoinAcademicAsync(s.Manager, next, s.Class.Id, PersonalClassLocalRoles.Teacher);
        var c = await GetAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}");
        var transfer = await PostAsync<PersonalClassTransferDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}/manager-transfers",
            new CreatePersonalClassTransferCommand(c.Version, next.UserId, Guid.NewGuid(), "انتقال مدیریت برای آزمون مجوز"));
        await PostAsync<PersonalClassTransferDto>(next.Client, $"/api/v1/personal-classes/manager-transfers/{transfer.Id}/decision",
            new DecidePersonalClassTransferCommand(transfer.Version, PersonalClassManagerTransferStatus.Accepted));
        foreach (var actor in new[] { s.Manager, next })
        {
            using var denied = await actor.Client.PostAsJsonAsync($"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", AssignmentDraft(), Json);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        var grant = await GrantAcademicAsync(s.Admin, s.Class.Id, next, PersonalClassAcademicCapabilities.AuthorAssignments);
        Assert.True(grant.IsEffective);
        _ = await PostAsync<AssignmentResult>(next.Client, $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", AssignmentDraft());
        await WithDb(async db => Assert.NotNull((await db.PersonalClassAcademicGrants.SingleAsync(g => g.Id == s.Grant.Id)).RevokedAtUtc));
    }
}
