using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MakanApp.Application.Assessment;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Theory]
    [InlineData("image/jpeg", "answer.jpg")]
    [InlineData("image/png", "answer.png")]
    [InlineData("application/pdf", "answer.pdf")]
    public async Task AcademicExamAttachmentOnlyReloadFinalizeAndTeacherDownloadsExactBytes(string mime, string name)
    {
        var evidence = new AcademicAttachmentEvidence("exam-" + mime.Replace('/', '-'));
        try
        {
            await WithAcademicExamAsync(async s =>
            {
                evidence.Stage = "UPLOAD";
                var file = await UploadAcademicFileAsync(s.Student, mime, name);
                Assert.Equal(FileAssetStatus.Ready, file.Status);
                evidence.Stage = "ATTACH";
                var command = new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), s.Lease.WriteLeaseVersion, null);
                var attached = await AttachAcademicAsync(s, command);
                var retry = await AttachAcademicAsync(s, command);
                Assert.Equal(attached, retry);
                Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(s.Attempt.ExamAttemptId));
                evidence.Stage = "RELOAD";
                var answer = Assert.Single((await AcademicAnswersAsync(s)).Answers);
                Assert.True(string.IsNullOrEmpty(answer.TextAnswer));
                var metadata = Assert.Single(answer.Attachments);
                Assert.Equal(file.Id, metadata.FileAssetId);
                Assert.True(metadata.CanDownload);
                evidence.Stage = "TEACHER_DRAFT_DENIED";
                using var draftDownload = await s.Teacher.GetAsync(metadata.DownloadPath);
                Assert.Equal(HttpStatusCode.NotFound, draftDownload.StatusCode);
                evidence.Stage = "FINALIZE";
                var finalize = new FinalizeExamCommand(Guid.NewGuid(), attached.AnswerSetVersion, s.Lease.WriteLeaseVersion);
                var receipt = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, finalize);
                Assert.Equal(1, receipt.AnsweredQuestionCount);
                Assert.Equal(receipt, await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, finalize));
                evidence.Stage = "TEACHER_GRADING";
                using var grading = await s.Teacher.PostAsync($"/api/v1/academic/exam-attempts/{s.Attempt.ExamAttemptId}/grading", null);
                grading.EnsureSuccessStatusCode();
                var grade = await ReadRequiredAsync<ExamGradeDto>(grading);
                var gradedQuestion = Assert.Single(grade.Questions);
                Assert.True(gradedQuestion.IsAnswered);
                Assert.False(gradedQuestion.IsReviewed);
                Assert.Equal(file.Id, Assert.Single(gradedQuestion.Attachments).FileAssetId);
                evidence.Stage = "TEACHER_DOWNLOAD";
                using var downloaded = await s.Teacher.GetAsync(metadata.DownloadPath);
                Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
                evidence.HashMatches = SHA256.HashData(StabilizationMediaFixtures.Bytes(mime))
                    .SequenceEqual(SHA256.HashData(await downloaded.Content.ReadAsByteArrayAsync()));
                Assert.True(evidence.HashMatches);
                using var late = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s),
                    new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), s.Lease.WriteLeaseVersion, 1), JsonOptions);
                Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
                evidence.Passed = true; evidence.Stage = "COMPLETE";
            }, evidence: evidence);
        }
        finally { evidence.Save(); }
    }

    [Fact]
    public async Task AcademicExamTextSavePreservesFilesAndDetachCreatesImmutableRevision()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var attached = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            var saved = await SaveAnswerAsync(s.Student, s.Attempt.ExamAttemptId, s.Question.AttemptQuestionId,
                new(Guid.NewGuid(), 1, attached.RevisionNumber, null, "پاسخ همراه تصویر"));
            Assert.Single(Assert.Single((await AcademicAnswersAsync(s)).Answers).Attachments);
            using var detached = await s.Student.DeleteAsync(AcademicAttachmentRoute(s) + $"/{file.Id}?clientOperationId={Guid.NewGuid()}&writeLeaseVersion=1&expectedRevisionNumber={saved.RevisionNumber}");
            detached.EnsureSuccessStatusCode();
            var after = Assert.Single((await AcademicAnswersAsync(s)).Answers);
            Assert.Empty(after.Attachments);
            Assert.Equal("پاسخ همراه تصویر", after.TextAnswer);
            using var removedDownload = await s.Student.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, removedDownload.StatusCode);
            await AcademicDbAsync(async db =>
            {
                Assert.Equal(2, await db.AnswerRevisionAttachments.CountAsync(x => x.ExamAttemptId == s.Attempt.ExamAttemptId));
                Assert.Equal(3, await db.AnswerRevisions.CountAsync(x => x.ExamAttemptId == s.Attempt.ExamAttemptId));
            });
            Assert.True(await _factory.StoredBytesExistAsync(file.Id));
        });
    }

    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    [InlineData(FileAssetStatus.Deleted)]
    public async Task AcademicExamNonReadyFileCannotAttach(FileAssetStatus status)
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            await SetAcademicFileStatusAsync(file.Id, status);
            using var response = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(StorageErrorCodes.FileNotReady, await ReadProblemCodeAsync(response));
            Assert.Empty((await AcademicAnswersAsync(s)).Answers);
        });
    }

    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    public async Task AcademicExamFinalizeRevalidatesFileStatus(FileAssetStatus status)
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var receipt = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            await SetAcademicFileStatusAsync(file.Id, status);
            var metadata = Assert.Single(Assert.Single((await AcademicAnswersAsync(s)).Answers).Attachments);
            Assert.False(metadata.CanDownload);
            Assert.Null(metadata.DownloadPath);
            using var result = await SendFinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), receipt.AnswerSetVersion, 1));
            Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
            Assert.Equal(StorageErrorCodes.FileNotReady, await ReadProblemCodeAsync(result));
            var state = await _factory.GetExamFinalizationStateAsync(s.Attempt.ExamAttemptId);
            Assert.Equal(ExamAttemptStatus.InProgress, state.Status);
            Assert.Empty(state.FinalAnswers);
        });
    }

    [Fact]
    public async Task AcademicExamForeignStudentCannotAttachReadMetadataOrDownload()
    {
        await WithAcademicExamAsync(async s =>
        {
            using var other = _factory.CreateClient();
            _ = await CreateStudentAsync(other, s.Manager.OrganizationId, s.ClassId);
            var file = await UploadAcademicFileAsync(other);
            using var attach = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.NotFound, attach.StatusCode);
            using var metadata = await s.Student.GetAsync($"/api/v1/files/{file.Id}");
            using var download = await s.Student.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        });
    }

    [Fact]
    public async Task AcademicExamDisabledQuestionRejectsAttachment()
    {
        await WithAcademicExamAsync(async s =>
        {
            Assert.False(s.Question.AttachmentPolicy!.AttachmentsAllowed);
            var file = await UploadAcademicFileAsync(s.Student);
            using var result = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAttachmentNotAllowed, await ReadProblemCodeAsync(result));
        }, new(false, false, null, null, null));
    }

    [Fact]
    public async Task AcademicExamCountTotalBytesAndMimePolicyAreEnforced()
    {
        await WithAcademicExamAsync(async s =>
        {
            var first = await UploadAcademicFileAsync(s.Student);
            _ = await AttachAcademicAsync(s, new(first.Id, Guid.NewGuid(), 1, null));
            var second = await UploadAcademicFileAsync(s.Student);
            using var limit = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(second.Id, Guid.NewGuid(), 1, 1), JsonOptions);
            Assert.Equal(AssessmentErrorCodes.ExamAttachmentLimitExceeded, await ReadProblemCodeAsync(limit));
            Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(s.Attempt.ExamAttemptId));
        }, new(true, false, ["image/png"], 1, 4096));
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            using var limit = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(AssessmentErrorCodes.ExamAttachmentLimitExceeded, await ReadProblemCodeAsync(limit));
        }, new(true, false, ["image/png"], 2, 1));
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student, "application/pdf", "answer.pdf");
            using var invalid = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(AssessmentErrorCodes.ExamAttachmentNotAllowed, await ReadProblemCodeAsync(invalid));
        }, new(true, false, ["image/png"], 2, 4096));
    }

    [Fact]
    public async Task AcademicExamConcurrentAttachAndTextSaveRejectsStaleRevisionWithoutLostUpdate()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var responses = await Task.WhenAll(
                s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions),
                SendSaveAsync(s.Student, s.Attempt.ExamAttemptId, s.Question.AttemptQuestionId, new(Guid.NewGuid(), 1, null, null, "متن هم‌زمان")));
            try
            {
                Assert.Single(responses, response => response.IsSuccessStatusCode);
                var conflict = Assert.Single(responses, response => !response.IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
                Assert.Equal(AssessmentErrorCodes.ExamAnswerVersionConflict, await ReadProblemCodeAsync(conflict));
                Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(s.Attempt.ExamAttemptId));
                var current = Assert.Single((await AcademicAnswersAsync(s)).Answers);
                if (current.Attachments.Count > 0)
                    _ = await SaveAnswerAsync(s.Student, s.Attempt.ExamAttemptId, s.Question.AttemptQuestionId, new(Guid.NewGuid(), 1, current.RevisionNumber, null, "متن هم‌زمان"));
                else _ = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, current.RevisionNumber));
                var recovered = Assert.Single((await AcademicAnswersAsync(s)).Answers);
                Assert.Single(recovered.Attachments);
                Assert.Equal("متن هم‌زمان", recovered.TextAnswer);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        });
    }

    [Fact]
    public async Task AcademicExamLeaseTransferRejectsOldWriterAttachment()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            using var otherSession = _factory.CreateClient();
            UseBearerToken(otherSession, await _factory.CreateAdditionalStudentSessionAsync(s.Learner.UserId, s.Learner.MembershipId));
            _ = await TransferLeaseAsync(otherSession, s.Attempt.ExamAttemptId);
            using var stale = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Contains(await ReadProblemCodeAsync(stale), new[] { AssessmentErrorCodes.ExamWriteLeaseStale, AssessmentErrorCodes.ExamWriteLeaseHeldByOtherSession });
            Assert.Empty((await AcademicAnswersAsync(s)).Answers);
        });
    }

    [Fact]
    public async Task AcademicExamUnrelatedTeacherParentAndRevokedReadersCannotDownload()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var answer = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            _ = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), answer.AnswerSetVersion, 1));
            using var unrelated = _factory.CreateClient();
            _ = await CreateTeacherAsync(unrelated, s.Manager.OrganizationId, s.ClassId, false);
            using var parent = _factory.CreateClient();
            _ = await CreateParentAsync(parent, s.Manager.OrganizationId, s.ClassId);
            foreach (var denied in new[] { unrelated, parent })
            {
                using var response = await denied.GetAsync($"/api/v1/files/{file.Id}/content");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            await AcademicDbAsync(async db =>
            {
                var assignment = await db.TeacherAssignments.SingleAsync(x => x.ClassId == s.ClassId && x.TeacherMembershipId == s.Evaluator.MembershipId);
                assignment.End(DateTime.UtcNow); await db.SaveChangesAsync();
            });
            using var revokedTeacher = await s.Teacher.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, revokedTeacher.StatusCode);
            await _factory.EndEnrollmentAsync(s.Learner.EnrollmentId!.Value);
            using var revokedStudent = await s.Student.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, revokedStudent.StatusCode);
        });
    }

    [Fact]
    public async Task AcademicExamDeadlineUsesServerClockWithoutUploadGrace()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            try
            {
                _factory.Clock.SetUtcNow(s.Attempt.EffectiveDeadlineUtc.AddTicks(-1));
                var attached = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
                _factory.Clock.SetUtcNow(s.Attempt.EffectiveDeadlineUtc);
                var another = await UploadAcademicFileAsync(s.Student);
                using var lateAttach = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(another.Id, Guid.NewGuid(), 1, attached.RevisionNumber), JsonOptions);
                Assert.Equal(AssessmentErrorCodes.ExamDeadlinePassed, await ReadProblemCodeAsync(lateAttach));
                using var lateFinalize = await SendFinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), attached.AnswerSetVersion, 1));
                Assert.Equal(AssessmentErrorCodes.ExamDeadlinePassed, await ReadProblemCodeAsync(lateFinalize));
                Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(s.Attempt.ExamAttemptId));
            }
            finally { _factory.Clock.UseSystemTime(); }
        });
    }

    [Fact]
    public async Task AcademicExamTextRequiredIsEnforcedOnlyAtFinalize()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var answer = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            using var missingText = await SendFinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), answer.AnswerSetVersion, 1));
            Assert.Equal(AssessmentErrorCodes.ExamAnswerTypeInvalid, await ReadProblemCodeAsync(missingText));
            var withText = await SaveAnswerAsync(s.Student, s.Attempt.ExamAttemptId, s.Question.AttemptQuestionId, new(Guid.NewGuid(), 1, 1, null, "توضیح پاسخ"));
            _ = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), withText.AnswerSetVersion, 1));
        }, new(true, true, ["image/png"], 2, 4096));
    }

    private async Task WithAcademicExamAsync(Func<AcademicExamScenario, Task> test,
        ExamQuestionAttachmentPolicyCommand? policy = null, AcademicAttachmentEvidence? evidence = null,
        ExamQuestionType type = ExamQuestionType.Descriptive)
    {
        using var manager = evidence is null ? _factory.CreateClient() : _factory.CreateDefaultClient(evidence.Handler());
        using var teacher = evidence is null ? _factory.CreateClient() : _factory.CreateDefaultClient(evidence.Handler());
        using var student = evidence is null ? _factory.CreateClient() : _factory.CreateDefaultClient(evidence.Handler());
        var organization = await CreateManagerAsync(manager);
        var academicClass = await _factory.CreateAcademicClassAsync(organization.OrganizationId);
        var evaluator = await CreateTeacherAsync(teacher, organization.OrganizationId, academicClass.ClassId, true);
        var draft = await CreateDraftAsync(teacher, academicClass.ClassId);
        policy ??= new(true, false, ["image/jpeg", "image/png", "application/pdf"], 3, 4096);
        using var added = await teacher.PostAsJsonAsync($"/api/v1/academic/exams/{draft.Id}/questions", new AddExamQuestionCommand(1,
            type, "حل مسئله را ارسال کنید.", 20m, type == ExamQuestionType.ObjectiveSingleChoice ? [new(1, "A", true), new(2, "B", false)] : null,
            draft.VersionRowVersion, policy), JsonOptions);
        added.EnsureSuccessStatusCode();
        var exam = await PublishAsync(teacher, await ReadRequiredAsync<ExamEditorDto>(added));
        var learner = await CreateStudentAsync(student, organization.OrganizationId, academicClass.ClassId);
        var attempt = await StartAttemptAsync(student, exam.Id);
        var lease = await AcquireLeaseAsync(student, attempt.ExamAttemptId);
        await test(new(manager, teacher, student, organization, evaluator, learner, academicClass.ClassId, exam, attempt, Assert.Single(attempt.Questions), lease));
    }

    private static string AcademicAttachmentRoute(AcademicExamScenario s) =>
        $"/api/v1/academic/exam-attempts/{s.Attempt.ExamAttemptId}/answers/{s.Question.AttemptQuestionId}/attachments";

    private static async Task<ExamAnswerReceiptDto> AttachAcademicAsync(AcademicExamScenario s, AttachExamAnswerFileCommand command)
    {
        using var response = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), command, JsonOptions);
        response.EnsureSuccessStatusCode(); return await ReadRequiredAsync<ExamAnswerReceiptDto>(response);
    }

    private static async Task<StudentExamAnswersDto> AcademicAnswersAsync(AcademicExamScenario s) =>
        (await s.Student.GetFromJsonAsync<StudentExamAnswersDto>($"/api/v1/academic/exam-attempts/{s.Attempt.ExamAttemptId}/answers", JsonOptions))!;

    private static async Task<FileAssetResult> UploadAcademicFileAsync(HttpClient client, string mime = "image/png", string name = "answer.png")
    {
        using var form = new MultipartFormDataContent(); using var part = new ByteArrayContent(StabilizationMediaFixtures.Bytes(mime));
        part.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(part, "File", name);
        using var response = await client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode(); return await ReadRequiredAsync<FileAssetResult>(response);
    }

    private async Task AcademicDbAsync(Func<MakanDbContext, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(MakanAppWebApplicationFactory.DatabaseName, db.Database.GetDbConnection().Database);
        await action(db);
    }

    private Task SetAcademicFileStatusAsync(Guid fileId, FileAssetStatus status) => AcademicDbAsync(async db =>
    {
        var at = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [storage].[FileAssets] SET [Status] = {(int)status}, [RetainedAtUtc] = NULL, [RejectedAtUtc] = {at}, [DeletedAtUtc] = {at} WHERE [Id] = {fileId}");
    });

    private sealed record AcademicExamScenario(HttpClient ManagerClient, HttpClient Teacher, HttpClient Student,
        ManagerContext Manager, TeacherContext Evaluator, StudentContext Learner, Guid ClassId, ExamEditorDto Exam,
        StudentExamAttemptDto Attempt, StudentExamAttemptQuestionDto Question, ExamWriteLeaseDto Lease);
}
