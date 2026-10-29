using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    private sealed record SchoolActor(HttpClient Client, Guid UserId, Guid MembershipId, Guid OrganizationPersonId, string Username);

    private async Task<SchoolActor> SchoolActorAsync(Guid organizationId, OrganizationRole role)
    {
        var client = _host.CreateClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var membership = await factory.CreateMembershipAsync(account.UserId, organizationId, role);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var personId = await db.Users.Where(u => u.Id == account.UserId).Select(u => u.PersonId!.Value).SingleAsync();
        var op = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        await SchoolSelectAsync(client, membership.MembershipId, role);
        return new(client, account.UserId, membership.MembershipId, op, account.Username);
    }

    private static async Task SchoolSelectAsync(HttpClient client, Guid membershipId, OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role), PolicyJson);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<T> SchoolReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>(PolicyJson))!;
        }
    }

    private static async Task<SchoolPage<T>> SchoolPageAsync<T>(HttpClient client, string route) =>
        await SchoolReadAsync<SchoolPage<T>>(await client.GetAsync(route));

    [Fact]
    public async Task SchoolDirectorySeparatesIdentitiesFiltersAndStablePagesWithoutLeakingOtherOrganizations()
    {
        var org = await factory.CreateOrganizationAsync($"School directory {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager);
        var teacher = await SchoolActorAsync(org, OrganizationRole.Teacher);
        var other = await SchoolActorAsync(await factory.CreateOrganizationAsync($"Other {Guid.NewGuid():N}"), OrganizationRole.Teacher);
        using var m = manager.Client; using var t = teacher.Client; using var o = other.Client;
        var full = await SchoolPageAsync<SchoolMemberResult>(m, "/api/v1/organization/members");
        Assert.Equal(2, full.TotalCount);
        var row = Assert.Single(full.Items, x => x.UserId == teacher.UserId);
        Assert.Equal(teacher.MembershipId, row.MembershipId);
        Assert.Equal(teacher.OrganizationPersonId, row.OrganizationPersonId);
        Assert.NotEqual(row.PersonId, row.OrganizationPersonId);
        Assert.NotEmpty(row.Version);
        Assert.Equal(OrganizationRole.Teacher, Assert.Single(row.ActiveRoles));
        Assert.NotEmpty(Assert.Single(row.RoleAssignments).Version);
        Assert.DoesNotContain(full.Items, x => x.UserId == other.UserId);
        var filtered = await SchoolPageAsync<SchoolMemberResult>(m, $"/api/v1/organization/members?role=Teacher&status=Active&search={teacher.Username}");
        Assert.Equal(teacher.UserId, Assert.Single(filtered.Items).UserId);
        var first = await SchoolPageAsync<SchoolMemberResult>(m, "/api/v1/organization/members?pageSize=1&page=1");
        var second = await SchoolPageAsync<SchoolMemberResult>(m, "/api/v1/organization/members?pageSize=1&page=2");
        Assert.Equal(2, first.TotalCount); Assert.NotEqual(first.Items.Single().MembershipId, second.Items.Single().MembershipId);
        Assert.Equal(first.Items.Single().MembershipId, (await SchoolPageAsync<SchoolMemberResult>(m, "/api/v1/organization/members?pageSize=1&page=1")).Items.Single().MembershipId);
        await ErrorAsync(await t.GetAsync("/api/v1/organization/members"), HttpStatusCode.Forbidden, "WORKSPACE_NOT_ALLOWED");
        using var invalid = await m.GetAsync("/api/v1/organization/members?page=0"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    public async Task SchoolDirectoryRejectsStudentAndParent(OrganizationRole role)
    {
        var actor = await SchoolActorAsync(await factory.CreateOrganizationAsync($"Directory deny {Guid.NewGuid():N}"), role);
        using var client = actor.Client;
        await ErrorAsync(await client.GetAsync("/api/v1/organization/members"), HttpStatusCode.Forbidden, "WORKSPACE_NOT_ALLOWED");
    }

    [Fact]
    public async Task SchoolCatalogsIncludeUnusedRowsAndValidatePaginationAndStatus()
    {
        var org = await factory.CreateOrganizationAsync($"Catalog {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        var course = await factory.CreateCourseAsync(org); var period = await factory.CreateAcademicPeriodAsync(org);
        Assert.Equal(course, Assert.Single((await SchoolPageAsync<CourseResult>(m, "/api/v1/academic/courses?status=Active")).Items).Id);
        Assert.Equal(period, Assert.Single((await SchoolPageAsync<AcademicPeriodResult>(m, "/api/v1/academic/periods")).Items).Id);
        Assert.Empty((await SchoolPageAsync<CourseResult>(m, "/api/v1/academic/courses?search=nonexistent")).Items);
        using var invalid = await m.GetAsync("/api/v1/academic/courses?status=999"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var size = await m.GetAsync("/api/v1/academic/periods?pageSize=101"); Assert.Equal(HttpStatusCode.BadRequest, size.StatusCode);
    }

    [Fact]
    public async Task SchoolRostersExistBeforeSessionsAndEndingOneTeacherPreservesOtherTeacherAndRole()
    {
        var org = await factory.CreateOrganizationAsync($"Rosters {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); var a = await SchoolActorAsync(org, OrganizationRole.Teacher); var b = await SchoolActorAsync(org, OrganizationRole.Teacher);
        var student = await SchoolActorAsync(org, OrganizationRole.Student);
        using var m = manager.Client; using var ta = a.Client; using var tb = b.Client; using var s = student.Client;
        var k = await factory.CreateAcademicClassAsync(org); var unassigned = await factory.CreateAcademicClassAsync(org);
        var enrollment = await SchoolReadAsync<EnrollmentResult>(await m.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/enrollments", new EnrollLearnerCommand(student.OrganizationPersonId)), HttpStatusCode.Created);
        var aa = await SchoolReadAsync<TeacherAssignmentResult>(await m.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/teachers", new AssignTeacherCommand(a.MembershipId)), HttpStatusCode.Created);
        _ = await SchoolReadAsync<TeacherAssignmentResult>(await m.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/teachers", new AssignTeacherCommand(b.MembershipId)), HttpStatusCode.Created);
        var learner = Assert.Single((await SchoolPageAsync<ClassLearnerResult>(ta, $"/api/v1/academic/classes/{k.ClassId}/learners")).Items);
        Assert.Equal(enrollment.Id, learner.EnrollmentId); Assert.Equal(student.UserId, learner.UserId); Assert.NotEmpty(learner.Version);
        Assert.Equal(2, (await SchoolPageAsync<ClassTeacherResult>(ta, $"/api/v1/academic/classes/{k.ClassId}/teachers")).TotalCount);
        await ErrorAsync(await ta.PostAsJsonAsync("/api/v1/academic/classes", new CreateClassCommand(k.PeriodId, k.CourseId, "Denied", 20, true)), HttpStatusCode.Forbidden, "MANAGER_ROLE_REQUIRED");
        var session = new CreateSessionCommand("جلسه", Now.AddDays(1), Now.AddDays(1).AddHours(1), "UTC", null);
        using var allowed = await ta.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/sessions", session); Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var unrelated = await ta.PostAsJsonAsync($"/api/v1/academic/classes/{unassigned.ClassId}/sessions", session); Assert.Equal(HttpStatusCode.Forbidden, unrelated.StatusCode);
        using var ended = await m.PostAsync($"/api/v1/academic/classes/{k.ClassId}/teachers/{aa.Id}/end", null); Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        using var denied = await ta.GetAsync($"/api/v1/academic/classes/{k.ClassId}/learners"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var deniedSession = await ta.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/sessions", session); Assert.Equal(HttpStatusCode.Forbidden, deniedSession.StatusCode);
        var remaining = Assert.Single((await SchoolPageAsync<ClassTeacherResult>(tb, $"/api/v1/academic/classes/{k.ClassId}/teachers")).Items);
        Assert.Equal(b.UserId, remaining.UserId);
        await SchoolSelectAsync(ta, a.MembershipId, OrganizationRole.Teacher);
        using var studentRoster = await s.GetAsync($"/api/v1/academic/classes/{k.ClassId}/learners"); Assert.Equal(HttpStatusCode.NotFound, studentRoster.StatusCode);
    }

    private static Task<HttpResponseMessage> SchoolDraftAsync(HttpClient client, Guid classId, Guid operationId, Guid[]? files = null) =>
        client.PostAsJsonAsync($"/api/v1/academic/classes/{classId}/lessons", new CreateClassLessonCommand(operationId, "درس اول", "متن آزمایشی", 0, files ?? []), PolicyJson);

    [Fact]
    public async Task SchoolLessonRequiresCurrentAssignmentAndEnrollmentAndConcealsDraftsAndCrossOrganization()
    {
        var org = await factory.CreateOrganizationAsync($"Lesson {Guid.NewGuid():N}");
        var m = await SchoolActorAsync(org, OrganizationRole.Manager); var t = await SchoolActorAsync(org, OrganizationRole.Teacher); var s = await SchoolActorAsync(org, OrganizationRole.Student);
        var stranger = await SchoolActorAsync(org, OrganizationRole.Student); var parent = await SchoolActorAsync(org, OrganizationRole.Parent);
        using var mc = m.Client; using var tc = t.Client; using var sc = s.Client; using var uc = stranger.Client; using var pc = parent.Client;
        var k = await factory.CreateAcademicClassAsync(org);
        using var assign = await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/teachers", new AssignTeacherCommand(t.MembershipId)); assign.EnsureSuccessStatusCode();
        var enrollment = await SchoolReadAsync<EnrollmentResult>(await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/enrollments", new EnrollLearnerCommand(s.OrganizationPersonId)), HttpStatusCode.Created);
        var operation = Guid.NewGuid();
        var draft = await SchoolReadAsync<ClassLessonResult>(await SchoolDraftAsync(tc, k.ClassId, operation), HttpStatusCode.Created);
        var replay = await SchoolReadAsync<ClassLessonResult>(await SchoolDraftAsync(tc, k.ClassId, operation)); Assert.True(replay.Replayed); Assert.Equal(draft.Id, replay.Id);
        await ErrorAsync(await tc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/lessons", new CreateClassLessonCommand(operation, "تغییر", "متن", 0, [])), HttpStatusCode.Conflict, SchoolErrorCodes.LessonConflict);
        Assert.Empty((await SchoolPageAsync<ClassLessonResult>(sc, $"/api/v1/academic/classes/{k.ClassId}/lessons")).Items);
        using var hidden = await sc.GetAsync($"/api/v1/academic/lessons/{draft.Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var changed = await SchoolReadAsync<ClassLessonResult>(await tc.PatchAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}", new UpdateClassLessonCommand("درس جدید", "متن", 1, [], draft.Version)));
        await ErrorAsync(await tc.PostAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}/publish", new PublishClassLessonCommand(draft.Version)), HttpStatusCode.PreconditionFailed, SchoolErrorCodes.LessonVersionConflict);
        var published = await SchoolReadAsync<ClassLessonResult>(await tc.PostAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}/publish", new PublishClassLessonCommand(changed.Version)));
        Assert.Equal(LessonStatus.Published, published.Status);
        Assert.Equal(published.Version, (await SchoolReadAsync<ClassLessonResult>(await tc.PostAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}/publish", new PublishClassLessonCommand(changed.Version)))).Version);
        var studentView = await SchoolReadAsync<ClassLessonResult>(await sc.GetAsync($"/api/v1/academic/lessons/{draft.Id}"));
        Assert.Equal(draft.Id, studentView.Id);
        Assert.Equal(DateTimeKind.Utc, studentView.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, studentView.PublishedAtUtc!.Value.Kind);
        await ErrorAsync(await tc.PatchAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}", new UpdateClassLessonCommand("تغییر", "متن", 1, [], published.Version)), HttpStatusCode.Conflict, SchoolErrorCodes.LessonConflict);
        foreach (var client in new[] { uc, pc }) { using var denied = await client.GetAsync($"/api/v1/academic/lessons/{draft.Id}"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        var other = await factory.CreateAcademicClassAsync(await factory.CreateOrganizationAsync($"Other lesson {Guid.NewGuid():N}"));
        using var cross = await SchoolDraftAsync(tc, other.ClassId, Guid.NewGuid()); Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using var end = await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/enrollments/{enrollment.Id}/end", new EndEnrollmentCommand(EnrollmentStatus.Withdrawn), PolicyJson); end.EnsureSuccessStatusCode();
        using var revoked = await sc.GetAsync($"/api/v1/academic/lessons/{draft.Id}"); Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    private static async Task<FileAssetResult> SchoolUploadAsync(HttpClient client)
    {
        using var multipart = new MultipartFormDataContent();
        using var file = new ByteArrayContent("school handout"u8.ToArray()); file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(file, "File", "handout.txt");
        return await SchoolReadAsync<FileAssetResult>(await client.PostAsync("/api/v1/files", multipart), HttpStatusCode.Created);
    }

    [Fact]
    public async Task SchoolFileOldLinkRechecksClassAuthorizationEvenForOriginalUploader()
    {
        var org = await factory.CreateOrganizationAsync($"Handout {Guid.NewGuid():N}");
        var m = await SchoolActorAsync(org, OrganizationRole.Manager); var t = await SchoolActorAsync(org, OrganizationRole.Teacher); var s = await SchoolActorAsync(org, OrganizationRole.Student); var p = await SchoolActorAsync(org, OrganizationRole.Parent);
        using var mc = m.Client; using var tc = t.Client; using var sc = s.Client; using var pc = p.Client;
        var k = await factory.CreateAcademicClassAsync(org);
        var assignment = await SchoolReadAsync<TeacherAssignmentResult>(await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/teachers", new AssignTeacherCommand(t.MembershipId)), HttpStatusCode.Created);
        var enrollment = await SchoolReadAsync<EnrollmentResult>(await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/enrollments", new EnrollLearnerCommand(s.OrganizationPersonId)), HttpStatusCode.Created);
        var file = await SchoolUploadAsync(tc);
        var lesson = await SchoolReadAsync<ClassLessonResult>(await SchoolDraftAsync(tc, k.ClassId, Guid.NewGuid(), [file.Id]), HttpStatusCode.Created);
        var attachment = Assert.Single(lesson.Attachments); Assert.Equal("handout.txt", attachment.FileName); Assert.True(attachment.IsAvailable); Assert.NotNull(attachment.DownloadUrl);
        using var draftDownload = await sc.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, draftDownload.StatusCode);
        _ = await SchoolReadAsync<ClassLessonResult>(await tc.PostAsJsonAsync($"/api/v1/academic/lessons/{lesson.Id}/publish", new PublishClassLessonCommand(lesson.Version)));
        using var download = await sc.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using var parentDownload = await pc.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, parentDownload.StatusCode);
        var metadata = await SchoolReadAsync<FileAssetResult>(await tc.GetAsync($"/api/v1/files/{file.Id}"));
        await ErrorAsync(await tc.DeleteAsync($"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(metadata.RowVersion)}"), HttpStatusCode.Conflict, "FILE_IN_USE");
        using var endEnrollment = await mc.PostAsJsonAsync($"/api/v1/academic/classes/{k.ClassId}/enrollments/{enrollment.Id}/end", new EndEnrollmentCommand(EnrollmentStatus.Withdrawn), PolicyJson); endEnrollment.EnsureSuccessStatusCode();
        using var endAssignment = await mc.PostAsync($"/api/v1/academic/classes/{k.ClassId}/teachers/{assignment.Id}/end", null); endAssignment.EnsureSuccessStatusCode();
        foreach (var client in new[] { sc, tc }) { using var denied = await client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        using var deniedDraft = await SchoolDraftAsync(tc, k.ClassId, Guid.NewGuid()); Assert.Equal(HttpStatusCode.NotFound, deniedDraft.StatusCode);
    }

    private async Task<SchoolGuardianInvitationResult> SchoolInviteAsync(SchoolActor manager, Guid childId, string? username, string? phone = null, Guid? operation = null, DateTime? expires = null) =>
        await SchoolReadAsync<SchoolGuardianInvitationResult>(await manager.Client.PostAsJsonAsync($"/api/v1/organization/people/{childId}/guardian-invitations",
            new CreateSchoolGuardianInvitationCommand(operation ?? Guid.NewGuid(), username, phone, expires ?? Now.AddDays(1), "دعوت کنترل‌شده والد")), HttpStatusCode.Created);

    private static async Task<AcceptInvitationResult> SchoolAcceptAsync(HttpClient client, Guid invitationId) =>
        await SchoolReadAsync<AcceptInvitationResult>(await client.PostAsJsonAsync($"/api/v1/invitations/{invitationId}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true)));

    [Fact]
    public async Task SchoolParentUsernameInvitationNeedsAuthenticatedConsentAndIndependentChildrenAndGuardians()
    {
        var org = await factory.CreateOrganizationAsync($"Parent consent {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        using var parent = _host.CreateClient(); using var secondParent = _host.CreateClient(); using var wrong = _host.CreateClient();
        var pa = await CreateP0PersonalAccountAsync(parent, null); var pb = await CreateP0PersonalAccountAsync(secondParent, null); _ = await CreateP0PersonalAccountAsync(wrong, null);
        var children = await factory.CreateOrganizationPersonsAsync(org, 2);
        var first = await SchoolInviteAsync(manager, children[0], pa.Username);
        var second = await SchoolInviteAsync(manager, children[1], pa.Username);
        var otherGuardian = await SchoolInviteAsync(manager, children[0], pb.Username);
        var inbox = (await parent.GetFromJsonAsync<InvitationResult[]>("/api/v1/invitations", PolicyJson))!;
        Assert.Equal(2, inbox.Length); Assert.All(inbox, x => { Assert.True(x.RequiresGuardianConsent); Assert.Equal("CONFIRM_GUARDIAN_RELATION", x.NextAction); Assert.Null(x.OrganizationPersonId); });
        await ErrorAsync(await parent.PostAsJsonAsync($"/api/v1/invitations/{first.Id}/accept", new AcceptInvitationCommand()), HttpStatusCode.BadRequest, SchoolErrorCodes.GuardianConsentRequired);
        await ErrorAsync(await wrong.PostAsJsonAsync($"/api/v1/invitations/{first.Id}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true)), HttpStatusCode.NotFound, "INVITATION_NOT_FOUND");
        var receipt = await SchoolAcceptAsync(parent, first.Id); var replay = await SchoolAcceptAsync(parent, first.Id);
        Assert.Equal(receipt.GuardianRelationId, replay.GuardianRelationId); Assert.True(replay.AlreadyAccepted);
        Assert.Equal(receipt.MembershipId, (await SchoolAcceptAsync(parent, second.Id)).MembershipId);
        _ = await SchoolAcceptAsync(secondParent, otherGuardian.Id);
        await SchoolSelectAsync(parent, receipt.MembershipId, OrganizationRole.Parent);
        var authorized = (await parent.GetFromJsonAsync<AuthorizedChildResult[]>("/api/v1/guardian/children", PolicyJson))!; Assert.Equal(2, authorized.Length);
        using var select = await parent.PostAsync($"/api/v1/guardian/children/{children[0]}/select", null); select.EnsureSuccessStatusCode();
        var relation = await SchoolReadAsync<SchoolGuardianRelationDto>(await m.GetAsync($"/api/v1/organization/guardian-relations/{receipt.GuardianRelationId}"));
        _ = await SchoolReadAsync<SchoolGuardianRelationDto>(await m.PostAsJsonAsync($"/api/v1/organization/guardian-relations/{relation.Id}/end", new OnboardingStateCommand(relation.RowVersion, "پایان رابطه اول")));
        _ = await SchoolAcceptAsync(parent, first.Id);
        var remaining = (await parent.GetFromJsonAsync<AuthorizedChildResult[]>("/api/v1/guardian/children", PolicyJson))!; Assert.Equal(children[1], Assert.Single(remaining).LearnerOrganizationPersonId);
        using var endedChild = await parent.PostAsync($"/api/v1/guardian/children/{children[0]}/select", null); Assert.False(endedChild.IsSuccessStatusCode);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(GuardianRelationStatus.Ended, (await db.GuardianRelations.SingleAsync(x => x.Id == receipt.GuardianRelationId)).Status);
        Assert.Equal(3, await db.GuardianRelations.CountAsync(x => x.OrganizationId == org));
        Assert.Equal(2, await db.Memberships.CountAsync(x => x.OrganizationId == org && (x.UserId == pa.UserId || x.UserId == pb.UserId)));
        Assert.Equal(0, await db.Enrollments.CountAsync(x => x.OrganizationId == org));
    }

    [Fact]
    public async Task SchoolParentPhoneInvitationIsExactAndRevokeDeclineExpiryAndReplayAreSafe()
    {
        var org = await factory.CreateOrganizationAsync($"Parent lifecycle {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        using var parent = _host.CreateClient(); var account = await CreateP0PersonalAccountAsync(parent, null);
        var children = await factory.CreateOrganizationPersonsAsync(org, 3);
        string phone;
        await using (var scope = _host.Services.CreateAsyncScope())
            phone = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UserCredentials.Where(c => c.UserId == account.UserId && c.Kind == CredentialKind.MobilePhone).Select(c => c.NormalizedIdentifier).SingleAsync();
        var op = Guid.NewGuid(); var expires = Now.AddDays(1);
        var first = await SchoolInviteAsync(manager, children[0], null, phone, op, expires);
        var command = new CreateSchoolGuardianInvitationCommand(op, null, phone, expires, "دعوت کنترل‌شده والد");
        var replay = await SchoolReadAsync<SchoolGuardianInvitationResult>(await m.PostAsJsonAsync($"/api/v1/organization/people/{children[0]}/guardian-invitations", command)); Assert.True(replay.Replayed);
        _ = await SchoolReadAsync<SchoolGuardianInvitationResult>(await m.PostAsJsonAsync($"/api/v1/organization/guardian-invitations/{first.Id}/revoke", new OnboardingStateCommand(first.Version, "لغو دعوت")));
        await ErrorAsync(await parent.PostAsJsonAsync($"/api/v1/invitations/{first.Id}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true)), HttpStatusCode.Gone, "INVITATION_REVOKED");
        var second = await SchoolInviteAsync(manager, children[1], account.Username);
        using var decline = await parent.PostAsync($"/api/v1/invitations/{second.Id}/decline", null); Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        var third = await SchoolInviteAsync(manager, children[2], account.Username, expires: Now.AddMinutes(1));
        factory.Clock.SetUtcNow(Now.AddMinutes(2));
        await ErrorAsync(await parent.PostAsJsonAsync($"/api/v1/invitations/{third.Id}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true)), HttpStatusCode.Gone, "INVITATION_EXPIRED");
        using var unknown = await m.PostAsJsonAsync($"/api/v1/organization/people/{children[0]}/guardian-invitations", command with { ClientOperationId = Guid.NewGuid(), PhoneNumber = "+989001234567", ExpiresAtUtc = Now.AddDays(1) });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
