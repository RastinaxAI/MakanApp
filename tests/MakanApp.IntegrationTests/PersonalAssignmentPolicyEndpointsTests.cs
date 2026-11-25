using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalPublishedPolicyRequiresCurrentExplicitAuthorityAndRevokesDelegatedAuthority()
    {
        var s = await PersonalAcademicAsync();
        var assignment = await PersonalAssignmentAsync(s);
        var teacher = await ActorAsync();
        await JoinAcademicAsync(s.Manager, teacher, s.Class.Id, PersonalClassLocalRoles.Teacher);
        var route = $"/api/v1/academic/assignments/{assignment.Id}/submission-policy";
        var command = new UpdateAssignmentSubmissionPolicyCommand(assignment.DueAtUtc.AddHours(1), true,
            assignment.AssignmentRowVersion, assignment.VersionRowVersion);
        foreach (var actor in new[] { teacher, s.Learner, s.Peer, s.Admin })
        {
            using var denied = await actor.Client.PatchAsJsonAsync(route, command, Json);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }

        var member = await WithDb(db => db.PersonalClassParticipants.AsNoTracking().SingleAsync(m =>
            m.PersonalClassId == s.Class.Id && m.StudentUserId == teacher.UserId && m.EndedAtUtc == null));
        await AcademicWriteAsync<PersonalClassAcademicPermissionDto>(s.Manager.Client, HttpMethod.Put,
            $"/api/v1/personal-classes/{s.Class.Id}/academic-permissions/{member.Id}",
            new SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities.AuthorAssignments,
                Guid.NewGuid(), Convert.ToBase64String(member.RowVersion), null, "تفویض اختیار تغییر مهلت تکلیف"));
        var updated = await AcademicWriteAsync<AssignmentResult>(teacher.Client, HttpMethod.Patch, route, command);
        Assert.Equal(assignment.VersionId, updated.VersionId);
        Assert.Equal(assignment.MaxScore, updated.MaxScore);
        Assert.Null(updated.OrganizationId);
        Assert.Equal(s.Class.Id, updated.ClassRef!.ClassId);

        await GrantAcademicAsync(s.Admin, s.Class.Id, s.Manager, PersonalClassAcademicCapabilities.None, s.Grant.Version);
        var currentCommand = command with
        {
            DueAtUtc = updated.DueAtUtc.AddHours(1),
            ExpectedAssignmentRowVersion = updated.AssignmentRowVersion,
            ExpectedVersionRowVersion = updated.VersionRowVersion
        };
        foreach (var actor in new[] { s.Manager, teacher })
        {
            using var revoked = await actor.Client.PatchAsJsonAsync(route, currentCommand, Json);
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        }
        await WithDb(async db =>
        {
            var audit = await db.AdminAuditLogs.AsNoTracking().SingleAsync(a =>
                a.TargetId == assignment.Id.ToString() && a.Action == "personal_class.academic.SubmissionPolicyChanged");
            Assert.Equal(teacher.UserId, audit.ActorUserId);
            Assert.Null(audit.OrganizationId);
            using var after = JsonDocument.Parse(audit.AfterSummary!);
            Assert.Equal(s.Class.Id, after.RootElement.GetProperty("classId").GetGuid());
            Assert.Equal(command.DueAtUtc, after.RootElement.GetProperty("dueAtUtc").GetDateTime());
        });
    }

    [Fact]
    public async Task PersonalPublishedPolicyExtensionPreservesLocalDraftAndItsAlreadyUploadedFile()
    {
        var s = await PersonalAcademicAsync();
        var assignment = await PersonalAssignmentAsync(s, AssignmentDraft() with
        { DueAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5) });
        var attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client,
            $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        var attemptRoute = $"/api/v1/academic/submission-attempts/{attempt.Id}";
        attempt = await AcademicWriteAsync<SubmissionAttemptResult>(s.Learner.Client, HttpMethod.Patch,
            attemptRoute + "/draft", new SaveSubmissionDraftCommand("پاسخ محفوظ کلاس مستقل", attempt.RowVersion));
        var file = await PersonalFileAsync(s.Learner, s.Class.Id);
        var now = assignment.DueAtUtc.AddTicks(1);
        factory.Clock.SetUtcNow(now);
        try
        {
            using (var expired = await s.Learner.Client.PostAsJsonAsync(attemptRoute + "/attachments",
                new AttachSubmissionFileCommand(file.Id, attempt.RowVersion), Json))
            {
                Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
                Assert.Equal(AssessmentErrorCodes.SubmissionDeadlinePassed, await ProblemAsync(expired));
            }
            var updated = await AcademicWriteAsync<AssignmentResult>(s.Manager.Client, HttpMethod.Patch,
                $"/api/v1/academic/assignments/{assignment.Id}/submission-policy",
                new UpdateAssignmentSubmissionPolicyCommand(now.AddHours(1), false,
                    assignment.AssignmentRowVersion, assignment.VersionRowVersion));
            Assert.Equal(assignment.VersionId, updated.VersionId);
            var attached = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, attemptRoute + "/attachments",
                new AttachSubmissionFileCommand(file.Id, attempt.RowVersion));
            Assert.Equal(attempt.Id, attached.Id);
            Assert.Equal(attempt.AnswerText, attached.AnswerText);
            Assert.Equal(file.Id, Assert.Single(attached.Attachments).FileAssetId);
            var receipt = await PostAsync<SubmissionReceipt>(s.Learner.Client, attemptRoute + "/submit",
                new FinalSubmitAssignmentCommand(attached.RowVersion));
            Assert.False(receipt.IsLate);
            Assert.Equal(attempt.Id, receipt.SubmissionAttemptId);
            await WithDb(async db =>
            {
                Assert.Equal(1, await db.SubmissionAttempts.CountAsync(a => a.AssignmentId == assignment.Id));
                Assert.False(await db.ModerationCases.AnyAsync(c => c.MediaFileAssetId == file.Id));
            });
        }
        finally { factory.Clock.UseSystemTime(); }
    }
}
