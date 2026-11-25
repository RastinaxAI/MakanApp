using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Assessment;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedPolicyReopensTheSameDraftAndReadyPdfWithoutReupload(bool enableLateInsteadOfExtension)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(manager, student);
        var attempt = await CreateSubmissionDraftAsync(student, scenario.Assignment.Id);
        attempt = await SaveSubmissionDraftAsync(student, attempt, "متن محفوظ برای همان تلاش");
        var file = await UploadDeadlinePdfAsync(student);
        var now = DateTime.UtcNow.AddSeconds(1); var expiredDue = now.AddTicks(-1);
        await _factory.SetAssignmentDeadlineAsync(scenario.Assignment.VersionId, expiredDue, false);
        _factory.Clock.SetUtcNow(now);
        try
        {
            using (var denied = await student.PostAsJsonAsync($"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
                new AttachSubmissionFileCommand(file.Id, attempt.RowVersion), JsonOptions))
            {
                Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
                Assert.Equal(AssessmentErrorCodes.SubmissionDeadlinePassed, await ReadProblemCodeAsync(denied));
            }
            Assert.Equal(FileAssetStatus.Ready, (await _factory.GetFileAssetAsync(file.Id)).Status);
            Assert.Equal(0, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
            var current = await CurrentPolicyAsync(manager, scenario.Assignment.Id);
            Assert.Equal(DateTimeKind.Utc, current.DueAtUtc.Kind);
            var newDue = enableLateInsteadOfExtension ? current.DueAtUtc : now.AddHours(1);
            using var changed = await PatchPolicyAsync(manager, current, newDue, enableLateInsteadOfExtension);
            changed.EnsureSuccessStatusCode(); var updated = await ReadRequiredAsync<AssignmentResult>(changed);
            Assert.Equal(current.Id, updated.Id); Assert.Equal(current.VersionId, updated.VersionId);
            Assert.Equal(current.Title, updated.Title); Assert.Equal(current.Description, updated.Description);
            Assert.Equal(current.MaxScore, updated.MaxScore); Assert.Equal(current.MaxAttempts, updated.MaxAttempts);
            Assert.Equal(current.VersionNumber, updated.VersionNumber); Assert.Equal(current.PublishedAtUtc, updated.PublishedAtUtc);
            Assert.Equal(AssignmentStatus.Published, updated.Status);
            Assert.NotEqual(current.AssignmentRowVersion, updated.AssignmentRowVersion);
            Assert.NotEqual(current.VersionRowVersion, updated.VersionRowVersion);
            var storedDraft = await _factory.GetSubmissionAttemptAsync(attempt.Id);
            Assert.Equal(attempt.AnswerText, storedDraft.AnswerText);
            var resumed = await CreateSubmissionDraftAsync(student, current.Id);
            Assert.Equal(attempt.Id, resumed.Id); Assert.Equal(attempt.AnswerText, resumed.AnswerText);
            Assert.Equal(attempt.RowVersion, resumed.RowVersion);
            var attached = await AttachSubmissionFileAsync(student, resumed, file.Id);
            var receipt = await SubmitAsync(student, attached);
            Assert.Equal(attempt.Id, receipt.SubmissionAttemptId);
            Assert.Equal(enableLateInsteadOfExtension, receipt.IsLate);
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(current.Id));
            Assert.Equal(file.Id, Assert.Single(attached.Attachments).FileAssetId);

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var audit = await db.AdminAuditLogs.AsNoTracking().SingleAsync(a =>
                a.TargetId == current.Id.ToString() && a.Action == "organization.academic.SubmissionPolicyChanged");
            Assert.Equal(scenario.OrganizationId, audit.OrganizationId);
            Assert.NotNull(audit.ActorUserId); Assert.NotNull(audit.ActorSessionId); Assert.Equal(now, audit.OccurredAtUtc);
            using var before = JsonDocument.Parse(audit.BeforeSummary!); using var after = JsonDocument.Parse(audit.AfterSummary!);
            Assert.Equal(expiredDue, before.RootElement.GetProperty("dueAtUtc").GetDateTime());
            Assert.Equal(newDue, after.RootElement.GetProperty("dueAtUtc").GetDateTime());
            Assert.False(before.RootElement.GetProperty("allowLateSubmission").GetBoolean());
            Assert.Equal(enableLateInsteadOfExtension, after.RootElement.GetProperty("allowLateSubmission").GetBoolean());
            Assert.Equal(current.VersionId, after.RootElement.GetProperty("assignmentVersionId").GetGuid());
            Assert.False(await db.ModerationCases.AnyAsync(c => c.MediaFileAssetId == file.Id));
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedPolicyDoesNotRewriteSubmittedReceiptOrCreateAnotherAttempt(bool submittedLate)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student, allowLateSubmission: true);
        var attempt = await CreateSubmissionDraftAsync(student, s.Assignment.Id);
        attempt = await SaveSubmissionDraftAsync(student, attempt);
        var file = await UploadDeadlinePdfAsync(student);
        attempt = await AttachSubmissionFileAsync(student, attempt, file.Id);
        var now = DateTime.UtcNow.AddSeconds(1);
        await _factory.SetAssignmentDeadlineAsync(s.Assignment.VersionId, now.AddTicks(submittedLate ? -1 : 1), true);
        _factory.Clock.SetUtcNow(now);
        try
        {
            var receipt = await SubmitAsync(student, attempt); Assert.Equal(submittedLate, receipt.IsLate);
            var current = await CurrentPolicyAsync(manager, s.Assignment.Id);
            using var update = await PatchPolicyAsync(manager, current, now.AddDays(1), false); update.EnsureSuccessStatusCode();
            var replay = await SubmitAsync(student, attempt);
            Assert.Equal(receipt, replay);
            var stored = await _factory.GetSubmissionAttemptAsync(attempt.Id);
            Assert.Equal(submittedLate, stored.IsLate); Assert.Equal(receipt.SubmittedAtUtc, stored.SubmittedAtUtc);
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(s.Assignment.Id));
            Assert.Equal(1, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
            var retained = await _factory.GetFileAssetAsync(file.Id);
            Assert.Equal(FileAssetStatus.Ready, retained.Status); Assert.NotNull(retained.RetainedAtUtc);
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task PublishedPolicyRequiresBothCurrentStampsAndAdvancesAggregateAtTheSameClockTick()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        _factory.Clock.SetUtcNow(DateTime.UtcNow.AddSeconds(1));
        try
        {
            using var firstResponse = await PatchPolicyAsync(manager, s.Assignment, s.Assignment.DueAtUtc.AddHours(1), true);
            firstResponse.EnsureSuccessStatusCode(); var first = await ReadRequiredAsync<AssignmentResult>(firstResponse);
            using var stale = await PatchPolicyAsync(manager, s.Assignment, first.DueAtUtc.AddHours(1), false);
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(stale));
            using var lostResponseRetry = await PatchPolicyAsync(manager, s.Assignment, first.DueAtUtc, true);
            Assert.Equal(HttpStatusCode.PreconditionFailed, lostResponseRetry.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(lostResponseRetry));
            foreach (var pair in new[]
            {
                (s.Assignment.AssignmentRowVersion, first.VersionRowVersion),
                (first.AssignmentRowVersion, s.Assignment.VersionRowVersion)
            })
            {
                using var mixed = await manager.PatchAsJsonAsync($"/api/v1/academic/assignments/{first.Id}/submission-policy",
                    new UpdateAssignmentSubmissionPolicyCommand(first.DueAtUtc.AddHours(1), false, pair.Item1, pair.Item2), JsonOptions);
                Assert.Equal(HttpStatusCode.PreconditionFailed, mixed.StatusCode);
            }
            using var secondResponse = await PatchPolicyAsync(manager, first, first.DueAtUtc.AddHours(1), false);
            secondResponse.EnsureSuccessStatusCode(); var second = await ReadRequiredAsync<AssignmentResult>(secondResponse);
            Assert.Equal(first.UpdatedAtUtc, second.UpdatedAtUtc);
            Assert.NotEqual(first.AssignmentRowVersion, second.AssignmentRowVersion);
            Assert.NotEqual(first.VersionRowVersion, second.VersionRowVersion);
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task PublishedPolicyConcurrentWritersHaveOneWinnerAndOneVersionConflict()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        var responses = await Task.WhenAll(PatchPolicyAsync(manager, s.Assignment, s.Assignment.DueAtUtc.AddHours(1), true),
            PatchPolicyAsync(manager, s.Assignment, s.Assignment.DueAtUtc.AddHours(2), false));
        try
        {
            Assert.Single(responses, r => r.IsSuccessStatusCode);
            var conflict = Assert.Single(responses, r => !r.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(conflict));
            await using var scope = _factory.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MakanDbContext>().AdminAuditLogs.CountAsync(a =>
                a.TargetId == s.Assignment.Id.ToString() && a.Action == "organization.academic.SubmissionPolicyChanged"));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishedPolicyRequiresAnActiveAssignmentForTheActualClass(bool assignedToThisClass)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient(); using var teacher = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        var targetClassId = assignedToThisClass ? s.ClassId : (await _factory.CreateAcademicClassAsync(s.OrganizationId)).ClassId;
        var actor = await CreateTeacherAsync(teacher, s.OrganizationId, targetClassId, assigned: true);
        using var response = await PatchPolicyAsync(teacher, s.Assignment, s.Assignment.DueAtUtc.AddHours(1), true);
        if (assignedToThisClass)
        {
            response.EnsureSuccessStatusCode();
            await using var scope = _factory.Services.CreateAsyncScope();
            var audit = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().AdminAuditLogs.AsNoTracking()
                .SingleAsync(a => a.TargetId == s.Assignment.Id.ToString() && a.Action == "organization.academic.SubmissionPolicyChanged");
            Assert.Equal(actor.UserId, audit.ActorUserId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(AssessmentErrorCodes.TeacherNotAssigned, await ReadProblemCodeAsync(response));
        }
    }

    [Fact]
    public async Task PublishedPolicyRejectsStudentParentAndCrossOrganizationActor()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        using var parent = _factory.CreateClient(); using var foreignManager = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        await CreateParentForStudentAsync(parent, s); await CreateManagerAsync(foreignManager);
        foreach (var client in new[] { student, parent })
        {
            using var denied = await PatchPolicyAsync(client, s.Assignment, s.Assignment.DueAtUtc.AddHours(1), true);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(AssessmentErrorCodes.AssignmentNotAllowed, await ReadProblemCodeAsync(denied));
        }
        using var hidden = await PatchPolicyAsync(foreignManager, s.Assignment, s.Assignment.DueAtUtc.AddHours(1), true);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task PublishedPolicyRejectsShorteningAndPublishedContentFieldsWithoutMutation()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        using var shorter = await PatchPolicyAsync(manager, s.Assignment, s.Assignment.DueAtUtc.AddMinutes(-1), true);
        Assert.Equal(HttpStatusCode.BadRequest, shorter.StatusCode);
        Assert.Equal(AssessmentErrorCodes.AssignmentDueDateInvalid, await ReadProblemCodeAsync(shorter));
        using var grading = await manager.PatchAsJsonAsync($"/api/v1/academic/assignments/{s.Assignment.Id}/submission-policy", new
        {
            dueAtUtc = s.Assignment.DueAtUtc.AddHours(1), allowLateSubmission = true,
            expectedAssignmentRowVersion = s.Assignment.AssignmentRowVersion,
            expectedVersionRowVersion = s.Assignment.VersionRowVersion, maxScore = 99m
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, grading.StatusCode);
        var current = await CurrentPolicyAsync(manager, s.Assignment.Id);
        Assert.Equal(s.Assignment.DueAtUtc, current.DueAtUtc);
        Assert.Equal(s.Assignment.MaxScore, current.MaxScore);
        Assert.Equal(s.Assignment.AssignmentRowVersion, current.AssignmentRowVersion);
        Assert.Equal(s.Assignment.VersionRowVersion, current.VersionRowVersion);
    }

    [Fact]
    public async Task PublishedPolicyCannotReplaceTheDraftUpdateContract()
    {
        using var manager = _factory.CreateClient();
        var actor = await CreateManagerAsync(manager); var classroom = await _factory.CreateAcademicClassAsync(actor.OrganizationId);
        var draft = await CreateDraftAsync(manager, classroom.ClassId);
        using var response = await PatchPolicyAsync(manager, draft, draft.DueAtUtc.AddHours(1), true);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.AssignmentNotSubmittable, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task PublishedPolicyIdenticalCurrentRequestDoesNotCreateAnAuditOrRewriteStamps()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        using var response = await PatchPolicyAsync(manager, s.Assignment, s.Assignment.DueAtUtc, s.Assignment.AllowLateSubmission);
        response.EnsureSuccessStatusCode(); var result = await ReadRequiredAsync<AssignmentResult>(response);
        Assert.Equal(s.Assignment.AssignmentRowVersion, result.AssignmentRowVersion);
        Assert.Equal(s.Assignment.VersionRowVersion, result.VersionRowVersion);
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().AdminAuditLogs.AnyAsync(a =>
            a.TargetId == s.Assignment.Id.ToString() && a.Action == "organization.academic.SubmissionPolicyChanged"));
    }

    [Fact]
    public async Task PublishedPolicyLateCanBeEnabledAndDisabledWithoutChangingExpiredDeadlineOrLosingTheDraft()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        var attempt = await CreateSubmissionDraftAsync(student, s.Assignment.Id);
        attempt = await SaveSubmissionDraftAsync(student, attempt, "پاسخ محفوظ هنگام تغییر مستقل Late");
        var file = await UploadDeadlinePdfAsync(student);
        var otherFile = await UploadDeadlinePdfAsync(student);
        var now = DateTime.UtcNow.AddSeconds(1);
        await _factory.SetAssignmentDeadlineAsync(s.Assignment.VersionId, now.AddTicks(-1), false);
        _factory.Clock.SetUtcNow(now);
        try
        {
            var current = await CurrentPolicyAsync(manager, s.Assignment.Id);
            using var enabledResponse = await PatchPolicyAsync(manager, current, current.DueAtUtc, true);
            enabledResponse.EnsureSuccessStatusCode(); var enabled = await ReadRequiredAsync<AssignmentResult>(enabledResponse);
            Assert.Equal(current.DueAtUtc, enabled.DueAtUtc); Assert.True(enabled.AllowLateSubmission);
            attempt = await AttachSubmissionFileAsync(student, attempt, file.Id);
            using var disabledResponse = await PatchPolicyAsync(manager, enabled, enabled.DueAtUtc, false);
            disabledResponse.EnsureSuccessStatusCode(); var disabled = await ReadRequiredAsync<AssignmentResult>(disabledResponse);
            Assert.Equal(current.DueAtUtc, disabled.DueAtUtc); Assert.False(disabled.AllowLateSubmission);
            Assert.NotEqual(enabled.AssignmentRowVersion, disabled.AssignmentRowVersion);
            Assert.NotEqual(enabled.VersionRowVersion, disabled.VersionRowVersion);

            foreach (var operation in new[] { "Save", "Attach", "Remove", "Submit" })
            {
                using var denied = await DeadlineMutationAsync(student, operation, attempt, file.Id, otherFile.Id);
                Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
                Assert.Equal(AssessmentErrorCodes.SubmissionDeadlinePassed, await ReadProblemCodeAsync(denied));
            }
            var stored = await _factory.GetSubmissionAttemptAsync(attempt.Id);
            Assert.Equal(SubmissionAttemptStatus.Draft, stored.Status); Assert.Equal(attempt.AnswerText, stored.AnswerText);
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(s.Assignment.Id));
            Assert.Equal(1, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
            Assert.Equal(FileAssetStatus.Ready, (await _factory.GetFileAssetAsync(otherFile.Id)).Status);
            await using var scope = _factory.Services.CreateAsyncScope();
            var audits = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().AdminAuditLogs.AsNoTracking()
                .Where(a => a.TargetId == s.Assignment.Id.ToString() && a.Action == "organization.academic.SubmissionPolicyChanged").ToArrayAsync();
            Assert.Equal(2, audits.Length);
            foreach (var audit in audits)
            {
                using var before = JsonDocument.Parse(audit.BeforeSummary!); using var after = JsonDocument.Parse(audit.AfterSummary!);
                Assert.Equal(current.DueAtUtc, before.RootElement.GetProperty("dueAtUtc").GetDateTime());
                Assert.Equal(current.DueAtUtc, after.RootElement.GetProperty("dueAtUtc").GetDateTime());
                Assert.NotEqual(before.RootElement.GetProperty("allowLateSubmission").GetBoolean(), after.RootElement.GetProperty("allowLateSubmission").GetBoolean());
            }
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    [Theory]
    [InlineData("StillPast")]
    [InlineData("ExactNow")]
    [InlineData("UnspecifiedUtc")]
    public async Task PublishedPolicyRejectsAnExtendedDeadlineThatIsNotExplicitlyInTheUtcFuture(string invalidTime)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        var now = DateTime.UtcNow.AddSeconds(1);
        await _factory.SetAssignmentDeadlineAsync(s.Assignment.VersionId, now.AddHours(-2), false);
        _factory.Clock.SetUtcNow(now);
        try
        {
            var current = await CurrentPolicyAsync(manager, s.Assignment.Id);
            var due = invalidTime switch
            {
                "StillPast" => now.AddHours(-1), "ExactNow" => now,
                _ => DateTime.SpecifyKind(now.AddHours(1), DateTimeKind.Unspecified)
            };
            using var rejected = await PatchPolicyAsync(manager, current, due, true);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal(AssessmentErrorCodes.AssignmentDueDateInvalid, await ReadProblemCodeAsync(rejected));
            var unchanged = await CurrentPolicyAsync(manager, current.Id);
            Assert.Equal(current.DueAtUtc, unchanged.DueAtUtc); Assert.False(unchanged.AllowLateSubmission);
            Assert.Equal(current.AssignmentRowVersion, unchanged.AssignmentRowVersion);
            Assert.Equal(current.VersionRowVersion, unchanged.VersionRowVersion);
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    public static IEnumerable<object[]> PolicyInvalidStampCases()
    {
        foreach (var aggregateStamp in new[] { true, false })
        foreach (var stamp in new[] { "", "not-base64", Convert.ToBase64String(new byte[7]) })
            yield return [aggregateStamp, stamp];
    }

    [Theory]
    [MemberData(nameof(PolicyInvalidStampCases))]
    public async Task PublishedPolicyRejectsMalformedOrNonSqlRowVersionsWithoutMutation(bool aggregateStamp, string stamp)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        var command = new UpdateAssignmentSubmissionPolicyCommand(s.Assignment.DueAtUtc.AddHours(1), true,
            aggregateStamp ? stamp : s.Assignment.AssignmentRowVersion, aggregateStamp ? s.Assignment.VersionRowVersion : stamp);
        using var rejected = await manager.PatchAsJsonAsync($"/api/v1/academic/assignments/{s.Assignment.Id}/submission-policy", command, JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, rejected.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(rejected));
        var unchanged = await CurrentPolicyAsync(manager, s.Assignment.Id);
        Assert.Equal(s.Assignment.AssignmentRowVersion, unchanged.AssignmentRowVersion);
        Assert.Equal(s.Assignment.VersionRowVersion, unchanged.VersionRowVersion);
        Assert.Equal(s.Assignment.DueAtUtc, unchanged.DueAtUtc); Assert.False(unchanged.AllowLateSubmission);
    }

    [Theory]
    [InlineData("dueAtUtc")]
    [InlineData("allowLateSubmission")]
    [InlineData("expectedAssignmentRowVersion")]
    [InlineData("expectedVersionRowVersion")]
    public async Task PublishedPolicyHttpRequiresAllFourExplicitFields(string missingField)
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var s = await CreateSubmissionScenarioAsync(manager, student);
        JsonObject body = JsonSerializer.SerializeToNode(new UpdateAssignmentSubmissionPolicyCommand(s.Assignment.DueAtUtc.AddHours(1), false,
            s.Assignment.AssignmentRowVersion, s.Assignment.VersionRowVersion), JsonOptions)!.AsObject();
        Assert.True(body.Remove(missingField));
        using var rejected = await manager.PatchAsJsonAsync($"/api/v1/academic/assignments/{s.Assignment.Id}/submission-policy", body, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var unchanged = await CurrentPolicyAsync(manager, s.Assignment.Id);
        Assert.Equal(s.Assignment.AssignmentRowVersion, unchanged.AssignmentRowVersion);
        Assert.Equal(s.Assignment.VersionRowVersion, unchanged.VersionRowVersion);
    }

    private static Task<HttpResponseMessage> PatchPolicyAsync(HttpClient client, AssignmentResult current, DateTime due, bool allowLate) =>
        client.PatchAsJsonAsync($"/api/v1/academic/assignments/{current.Id}/submission-policy",
            new UpdateAssignmentSubmissionPolicyCommand(due, allowLate, current.AssignmentRowVersion, current.VersionRowVersion), JsonOptions);

    private static async Task<AssignmentResult> CurrentPolicyAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/v1/academic/assignments/{id}");
        response.EnsureSuccessStatusCode(); return await ReadRequiredAsync<AssignmentResult>(response);
    }

    private static async Task<FileAssetResult> UploadDeadlinePdfAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(StabilizationMediaFixtures.Bytes("application/pdf"));
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(part, "file", "پاسخ پس از تمدید.pdf");
        using var response = await client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode(); return await ReadRequiredAsync<FileAssetResult>(response);
    }
}
