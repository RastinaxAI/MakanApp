using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MakanApp.Application.Assessment;
using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Theory]
    [InlineData("image/jpeg", "written-answer.jpg")]
    [InlineData("image/png", "written-answer.png")]
    [InlineData("application/pdf", "written-answer.pdf")]
    public async Task AcademicAssignmentAttachmentOnlySurvivesReloadAndAuthorizedTeacherDownloadsExactBytes(string mime, string name)
    {
        var evidence = new AcademicAttachmentEvidence("assignment-" + mime.Replace('/', '-'));
        try
        {
            using var manager = _factory.CreateDefaultClient(evidence.Handler());
            using var student = _factory.CreateDefaultClient(evidence.Handler());
            using var teacher = _factory.CreateDefaultClient(evidence.Handler());
            var scenario = await CreateSubmissionScenarioAsync(manager, student);
            _ = await CreateTeacherAsync(teacher, scenario.OrganizationId, scenario.ClassId, true);
            evidence.Stage = "OPEN_ASSIGNMENT";
            using var assignment = await student.GetAsync($"/api/v1/academic/assignments/{scenario.Assignment.Id}");
            assignment.EnsureSuccessStatusCode();
            evidence.Stage = "CREATE_ATTEMPT";
            var draft = await CreateSubmissionDraftAsync(student, scenario.Assignment.Id);
            evidence.Stage = "UPLOAD";
            var bytes = StabilizationMediaFixtures.Bytes(mime);
            using var form = new MultipartFormDataContent(); using var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(part, "File", name);
            using var uploaded = await student.PostAsync("/api/v1/files", form);
            uploaded.EnsureSuccessStatusCode();
            var file = await ReadRequiredAsync<FileAssetResult>(uploaded);
            evidence.Stage = "READY";
            var metadata = (await student.GetFromJsonAsync<FileAssetResult>($"/api/v1/files/{file.Id}", JsonOptions))!;
            Assert.Equal(FileAssetStatus.Ready, metadata.Status);
            evidence.Stage = "ATTACH";
            var attached = await AttachSubmissionFileAsync(student, draft, file.Id);
            var retried = await AttachSubmissionFileAsync(student, draft, file.Id);
            Assert.Equal(attached.RowVersion, retried.RowVersion);
            evidence.Stage = "RELOAD";
            var reloaded = (await student.GetFromJsonAsync<SubmissionAttemptResult>($"/api/v1/academic/submission-attempts/{draft.Id}", JsonOptions))!;
            Assert.Equal(file.Id, Assert.Single(reloaded.Attachments).FileAssetId);
            Assert.True(string.IsNullOrEmpty(reloaded.AnswerText));
            evidence.Stage = "FINAL_SUBMIT";
            _ = await SubmitAsync(student, reloaded);
            evidence.Stage = "TEACHER_READ";
            var official = (await teacher.GetFromJsonAsync<SubmissionAttemptResult>($"/api/v1/academic/submission-attempts/{draft.Id}", JsonOptions))!;
            Assert.True(official.ContentVisible);
            Assert.Equal(file.Id, Assert.Single(official.Attachments).FileAssetId);
            evidence.Stage = "TEACHER_DOWNLOAD";
            using var download = await teacher.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            var actual = await download.Content.ReadAsByteArrayAsync();
            evidence.HashMatches = SHA256.HashData(bytes).SequenceEqual(SHA256.HashData(actual));
            Assert.True(evidence.HashMatches);
            Assert.Equal(mime, download.Content.Headers.ContentType!.MediaType);
            evidence.Passed = true; evidence.Stage = "COMPLETE";
        }
        finally { evidence.Save(); }
    }

    [Fact]
    public async Task AcademicAssignmentRevokedEnrollmentAndGuardianCannotDownloadBoundFile()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        using var parent = _factory.CreateClient(); using var unrelated = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(manager, student);
        _ = await CreateParentForStudentAsync(parent, scenario);
        _ = await CreateTeacherAsync(unrelated, scenario.OrganizationId, scenario.ClassId, false);
        var draft = await CreateSubmissionDraftAsync(student, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(student);
        draft = await AttachSubmissionFileAsync(student, draft, file.Id);
        _ = await SubmitAsync(student, draft);
        foreach (var denied in new[] { parent, unrelated })
        {
            using var response = await denied.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var allowed = await student.GetAsync($"/api/v1/files/{file.Id}/content");
        allowed.EnsureSuccessStatusCode();
        await _factory.EndEnrollmentAsync(scenario.Student.EnrollmentId);
        using var revoked = await student.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    [Fact]
    public async Task AcademicAssignmentPendingFileRejectedBeforeBinding()
    {
        using var manager = _factory.CreateClient(); using var student = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(manager, student);
        var draft = await CreateSubmissionDraftAsync(student, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(student);
        await _factory.SetFileAssetStatusAsync(file.Id, FileAssetStatus.Pending);
        using var response = await student.PostAsJsonAsync($"/api/v1/academic/submission-attempts/{draft.Id}/attachments",
            new AttachSubmissionFileCommand(file.Id, draft.RowVersion), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.SubmissionFileNotReady, await ReadProblemCodeAsync(response));
        Assert.Equal(0, await _factory.CountSubmissionAttachmentsAsync(draft.Id));
    }
}
