using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task AcademicExamObjectiveAllowsOptionWithFileOnlyWhenExplicitlyEnabled()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            using var noOption = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(AssessmentErrorCodes.ExamAnswerTypeInvalid, await ReadProblemCodeAsync(noOption));
            var saved = await SaveAnswerAsync(s.Student, s.Attempt.ExamAttemptId, s.Question.AttemptQuestionId,
                new(Guid.NewGuid(), 1, null, s.Question.Options.First().Id, null));
            var attached = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, saved.RevisionNumber));
            Assert.Equal(s.Question.Options.First().Id, Assert.Single((await AcademicAnswersAsync(s)).Answers).SelectedOptionId);
            _ = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), attached.AnswerSetVersion, 1));
        }, type: ExamQuestionType.ObjectiveSingleChoice);
    }

    [Fact]
    public async Task AcademicExamConcurrentDuplicateAttachAndChangedPayloadHaveStableReceipts()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var command = new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null);
            var receipts = await Task.WhenAll(AttachAcademicAsync(s, command), AttachAcademicAsync(s, command));
            Assert.Equal(receipts[0], receipts[1]);
            Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(s.Attempt.ExamAttemptId));
            using var changed = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), command with { FileAssetId = Guid.NewGuid() }, JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAnswerIdempotencyConflict, await ReadProblemCodeAsync(changed));
            await AcademicDbAsync(async db => Assert.Equal(1, await db.AnswerRevisionAttachments.CountAsync(x => x.ExamAttemptId == s.Attempt.ExamAttemptId)));
        });
    }

    [Fact]
    public async Task AcademicExamConcurrentFinalizeAndAttachFreezeOneAnswerSet()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var responses = await Task.WhenAll(
                s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions),
                SendFinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), 0, 1)));
            try
            {
                Assert.Single(responses, x => x.IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.Conflict, Assert.Single(responses, x => !x.IsSuccessStatusCode).StatusCode);
                var state = await _factory.GetExamFinalizationStateAsync(s.Attempt.ExamAttemptId);
                if (state.Status == ExamAttemptStatus.InProgress)
                {
                    var current = await AcademicAnswersAsync(s);
                    _ = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), current.AnswerSetVersion, 1));
                }
                await AcademicDbAsync(async db =>
                {
                    var frozen = await db.ExamFinalAnswers.Where(x => x.ExamAttemptId == s.Attempt.ExamAttemptId).ToArrayAsync();
                    var revisions = await db.AnswerRevisions.Where(x => x.ExamAttemptId == s.Attempt.ExamAttemptId).ToArrayAsync();
                    Assert.Equal(revisions.Length, frozen.Length);
                    Assert.Equal(revisions.Select(x => x.Id), frozen.Select(x => x.AnswerRevisionId));
                });
            }
            finally { foreach (var response in responses) response.Dispose(); }
        });
    }

    [Fact]
    public async Task AcademicExamCurrentGuardianCannotReadRawAttachment()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var saved = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            _ = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), saved.AnswerSetVersion, 1));
            using var guardian = _factory.CreateClient();
            _ = await CreateGradingParentAsync(guardian, s.Manager.OrganizationId, s.Learner.OrganizationPersonId);
            using var metadata = await guardian.GetAsync($"/api/v1/files/{file.Id}");
            using var download = await guardian.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        });
    }

    [Fact]
    public async Task AcademicExamFileCannotMoveBetweenAttemptsOrAcrossOrganizations()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            _ = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            // تلاش دومِ دارای سیاست فعال در همان سازمان ساخته می‌شود.
            var draft = await CreateDraftAsync(s.Teacher, s.ClassId);
            using var added = await s.Teacher.PostAsJsonAsync($"/api/v1/academic/exams/{draft.Id}/questions",
                new AddExamQuestionCommand(1, ExamQuestionType.Descriptive, "حل", 20m, null, draft.VersionRowVersion,
                    new(true, false, ["image/png"], 2, 4096)), JsonOptions);
            added.EnsureSuccessStatusCode();
            var published = await PublishAsync(s.Teacher, await ReadRequiredAsync<ExamEditorDto>(added));
            var second = await StartAttemptAsync(s.Student, published.Id);
            _ = await AcquireLeaseAsync(s.Student, second.ExamAttemptId);
            using var reuse = await s.Student.PostAsJsonAsync($"/api/v1/academic/exam-attempts/{second.ExamAttemptId}/answers/{Assert.Single(second.Questions).AttemptQuestionId}/attachments",
                new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.NotFound, reuse.StatusCode);
            using var foreignManager = _factory.CreateClient();
            _ = await CreateManagerAsync(foreignManager);
            using var foreignDownload = await foreignManager.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, foreignDownload.StatusCode);
        });
    }

    [Fact]
    public async Task AcademicExamDetachedFinalHistoryStaysRetainedWithoutDownloadGrant()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            _ = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            using var detached = await s.Student.DeleteAsync(AcademicAttachmentRoute(s) + $"/{file.Id}?clientOperationId={Guid.NewGuid()}&writeLeaseVersion=1&expectedRevisionNumber=1");
            detached.EnsureSuccessStatusCode();
            var receipt = await ReadRequiredAsync<ExamAnswerReceiptDto>(detached);
            var final = await FinalizeAsync(s.Student, s.Attempt.ExamAttemptId, new(Guid.NewGuid(), receipt.AnswerSetVersion, 1));
            Assert.Equal(0, final.AnsweredQuestionCount);
            using var removed = await s.Teacher.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
            Assert.NotNull((await _factory.GetFileAssetAsync(file.Id)).RetainedAtUtc);
            Assert.True(await _factory.StoredBytesExistAsync(file.Id));
        });
    }

    [Fact]
    public async Task AcademicExamQuestionPolicyIsFrozenWhenNewVersionChangesPolicy()
    {
        await WithAcademicExamAsync(async s =>
        {
            using var nextResponse = await s.Teacher.PostAsJsonAsync($"/api/v1/academic/exams/{s.Exam.Id}/versions",
                new CreateNextExamVersionCommand(s.Exam.ExamRowVersion, s.Exam.VersionRowVersion), JsonOptions);
            nextResponse.EnsureSuccessStatusCode();
            var next = await ReadRequiredAsync<ExamEditorDto>(nextResponse);
            var q = Assert.Single(next.Questions);
            Assert.Equal(3, q.AttachmentPolicy!.MaximumCount);
            using var changed = await s.Teacher.PatchAsJsonAsync($"/api/v1/academic/exams/{next.Id}/questions/{q.Id}",
                new UpdateExamQuestionCommand(q.Order, q.Type, q.Prompt, q.Score, null, next.VersionRowVersion, q.RowVersion,
                    new(false, false, null, null, null)), JsonOptions);
            changed.EnsureSuccessStatusCode();
            _ = await PublishAsync(s.Teacher, await ReadRequiredAsync<ExamEditorDto>(changed));
            var snapshot = (await s.Student.GetFromJsonAsync<StudentExamAttemptDto>($"/api/v1/academic/exam-attempts/{s.Attempt.ExamAttemptId}", JsonOptions))!;
            Assert.True(Assert.Single(snapshot.Questions).AttachmentPolicy!.AttachmentsAllowed);
            var file = await UploadAcademicFileAsync(s.Student);
            _ = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
        });
    }

    [Fact]
    public async Task AcademicExamAttachmentInvalidationReusesExistingAccountStreamWithoutFilePayload()
    {
        await WithAcademicExamAsync(async s =>
        {
            var boundary = (await AccountReplayTestClient.ReadAsync(s.Student, JsonOptions)).NextCursor;
            var file = await UploadAcademicFileAsync(s.Student);
            _ = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            var changes = await AccountReplayTestClient.DrainAsync(s.Student, JsonOptions, boundary);
            var change = Assert.Single(changes.Values, x => x.Invalidation?.Key == "assessment.exam-attempt" && x.Invalidation.ResourceId == s.Attempt.ExamAttemptId);
            var json = JsonSerializer.Serialize(change);
            Assert.DoesNotContain(file.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(file.OriginalFileName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TextAnswer", json, StringComparison.OrdinalIgnoreCase);
            await _factory.EndEnrollmentAsync(s.Learner.EnrollmentId!.Value);
            Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(s.Student, JsonOptions, boundary)).Values, x => x.Invalidation?.ResourceId == s.Attempt.ExamAttemptId);
        });
    }

    [Fact]
    public async Task AcademicExamModelMatchesMigrationAndDatabaseRejectsCrossTenantAttachment()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            var revision = await AttachAcademicAsync(s, new(file.Id, Guid.NewGuid(), 1, null));
            var unbound = await UploadAcademicFileAsync(s.Student);
            await AcademicDbAsync(async db =>
            {
                Assert.False(db.Database.HasPendingModelChanges());
                Assert.Contains((await db.Database.GetAppliedMigrationsAsync()), id => id.EndsWith("_AddExamAnswerAttachments", StringComparison.Ordinal));
                var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [assessment].[AnswerRevisionAttachments] ([AnswerRevisionId],[FileAssetId],[OrganizationId],[ExamAttemptId],[ExamAttemptQuestionId],[AttachedAtUtc]) VALUES ({revision.AnswerRevisionId},{unbound.Id},{Guid.NewGuid()},{s.Attempt.ExamAttemptId},{s.Question.AttemptQuestionId},{DateTime.UtcNow})"));
                Assert.Equal(547, exception.Number);
                var invalidPolicy = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(async () =>
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [assessment].[QuestionVersions] SET [MaximumAnswerAttachmentCount] = NULL WHERE [Id] = {s.Question.QuestionVersionId}"));
                Assert.Equal(547, invalidPolicy.Number);
            });
        });
    }

    [Fact]
    public async Task AcademicExamAttachmentOpenApiHasRealRoutesAndProblemDetails()
    {
        using var client = _factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths");
        var attach = paths.GetProperty("/api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments").GetProperty("post");
        Assert.Contains("ExpectedRevisionNumber", attach.GetProperty("description").GetString());
        foreach (var status in new[] { "400", "401", "403", "404", "409" })
            Assert.True(attach.GetProperty("responses").GetProperty(status).GetProperty("content").TryGetProperty("application/problem+json", out _));
        var detach = paths.GetProperty("/api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments/{fileAssetId}").GetProperty("delete");
        Assert.Contains(detach.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "ClientOperationId" || p.GetProperty("name").GetString() == "clientOperationId");
    }

    [Fact]
    public async Task AcademicExamExpiredUnattachedFileAndRevokedWriterAreDenied()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            await AcademicDbAsync(async db =>
            {
                var created = DateTime.UtcNow.AddHours(-3); var expires = DateTime.UtcNow.AddHours(-2);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [storage].[FileAssets] SET [CreatedAtUtc] = {created}, [UnattachedExpiresAtUtc] = {expires} WHERE [Id] = {file.Id}");
            });
            using var expired = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
            await _factory.RevokeSessionByTokenAsync(s.Learner.AccessToken);
            using var revoked = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        });
    }

    [Fact]
    public async Task AcademicExamSameAccountForeignOrganizationFileCannotAttach()
    {
        await WithAcademicExamAsync(async s =>
        {
            var file = await UploadAcademicFileAsync(s.Student);
            using var manager = _factory.CreateClient(); var org = await CreateManagerAsync(manager);
            // فایل متعلق به همین User ولی scope دیگری است؛ selector سازمان مجوز ایجاد نمی‌کند.
            await AcademicDbAsync(async db =>
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [storage].[FileAssets] SET [OrganizationId] = {org.OrganizationId} WHERE [Id] = {file.Id}"));
            using var denied = await s.Student.PostAsJsonAsync(AcademicAttachmentRoute(s), new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), 1, null), JsonOptions);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        });
    }

    [Theory]
    [InlineData("missing-count")]
    [InlineData("zero-count")]
    [InlineData("missing-bytes")]
    [InlineData("unsupported-webp")]
    [InlineData("disabled-with-limits")]
    public async Task AcademicExamRejectsInvalidQuestionAttachmentPolicy(string invalid)
    {
        using var manager = _factory.CreateClient(); var org = await CreateManagerAsync(manager);
        var classroom = await _factory.CreateAcademicClassAsync(org.OrganizationId);
        var draft = await CreateDraftAsync(manager, classroom.ClassId);
        var policy = invalid switch
        {
            "missing-count" => new ExamQuestionAttachmentPolicyCommand(true, false, ["image/png"], null, 1024),
            "zero-count" => new(true, false, ["image/png"], 0, 1024),
            "missing-bytes" => new(true, false, ["image/png"], 1, null),
            "unsupported-webp" => new(true, false, ["image/webp"], 1, 1024),
            _ => new(false, false, ["image/png"], 1, 1024)
        };
        using var response = await manager.PostAsJsonAsync($"/api/v1/academic/exams/{draft.Id}/questions",
            new AddExamQuestionCommand(1, ExamQuestionType.Descriptive, "حل", 20m, null, draft.VersionRowVersion, policy), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
