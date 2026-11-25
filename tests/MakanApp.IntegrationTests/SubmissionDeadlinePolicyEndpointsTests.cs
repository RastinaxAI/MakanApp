using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    public static IEnumerable<object[]> DeadlineMutationCases()
    {
        foreach (var operation in new[] { "Save", "Attach", "Remove", "Submit" })
        foreach (var offset in new[] { -1, 0, 1 })
        foreach (var allowLate in new[] { false, true })
            yield return [operation, offset, allowLate];
    }

    [Theory]
    [MemberData(nameof(DeadlineMutationCases))]
    public async Task SubmissionDeadlineEveryMutationUsesTheSameInclusiveServerUtcBoundary(
        string operation, int requestOffsetTicks, bool allowLate)
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        attempt = await SaveSubmissionDraftAsync(studentClient, attempt, "پاسخ محفوظ پیش از تغییر مهلت");
        var attachedFile = await UploadSubmissionFileAsync(studentClient);
        attempt = await AttachSubmissionFileAsync(studentClient, attempt, attachedFile.Id);
        var unattachedFile = await UploadSubmissionFileAsync(studentClient, "فایل آماده برای اتصال.txt");
        var requestTime = DateTime.UtcNow.AddSeconds(1);
        var due = requestTime.AddTicks(-requestOffsetTicks);
        await _factory.SetAssignmentDeadlineAsync(scenario.Assignment.VersionId, due, allowLate);
        _factory.Clock.SetUtcNow(requestTime);
        try
        {
            using var response = await DeadlineMutationAsync(studentClient, operation, attempt, attachedFile.Id, unattachedFile.Id);
            var rejected = requestOffsetTicks > 0 && !allowLate;
            if (rejected)
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var problemJson = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain(attempt.AnswerText!, problemJson, StringComparison.Ordinal);
                Assert.DoesNotContain("پاسخ جدید", problemJson, StringComparison.Ordinal);
                using var problem = JsonDocument.Parse(problemJson);
                var body = problem.RootElement;
                Assert.False(body.TryGetProperty("answerText", out _));
                Assert.Equal(AssessmentErrorCodes.SubmissionDeadlinePassed, body.GetProperty("code").GetString());
                Assert.Equal("مهلت تحویل این تکلیف به پایان رسیده است.", body.GetProperty("detail").GetString());
                Assert.Equal(requestTime, body.GetProperty("requestTimeUtc").GetDateTime());
                Assert.Equal(due, body.GetProperty("dueAtUtc").GetDateTime());
                Assert.False(body.GetProperty("allowLateSubmission").GetBoolean());
                Assert.Equal(attempt.Id, body.GetProperty("attemptId").GetGuid());
                Assert.Equal(scenario.Assignment.Id, body.GetProperty("assignmentId").GetGuid());
                Assert.Equal(scenario.Assignment.VersionId, body.GetProperty("assignmentVersionId").GetGuid());
                Assert.Equal(scenario.ClassId, body.GetProperty("classId").GetGuid());
                Assert.Equal("OrganizationClass", body.GetProperty("classKind").GetString());
                Assert.Equal("Draft", body.GetProperty("attemptStatus").GetString());
                Assert.Equal("Published", body.GetProperty("assignmentStatus").GetString());
                Assert.True(body.GetProperty("assignmentVersionIsPublished").GetBoolean());
                Assert.Equal("REFETCH_ASSIGNMENT_AND_ATTEMPT", body.GetProperty("requiredAction").GetString());
                Assert.Equal("NotApplied", body.GetProperty("operationState").GetString());
                Assert.False(body.GetProperty("retryable").GetBoolean());
                Assert.True(body.TryGetProperty("correlationId", out _));
                Assert.True(body.TryGetProperty("traceId", out _));
                Assert.Equal(1, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
                var stored = await _factory.GetSubmissionAttemptAsync(attempt.Id);
                Assert.Equal(attempt.AnswerText, stored.AnswerText);
                Assert.Equal(SubmissionAttemptStatus.Draft, stored.Status);
            }
            else
            {
                response.EnsureSuccessStatusCode();
                if (operation == "Submit")
                {
                    var receipt = await ReadRequiredAsync<SubmissionReceipt>(response);
                    Assert.Equal(requestTime, receipt.SubmittedAtUtc);
                    Assert.Equal(requestOffsetTicks > 0, receipt.IsLate);
                }
            }
            Assert.Equal(FileAssetStatus.Ready, (await _factory.GetFileAssetAsync(unattachedFile.Id)).Status);
        }
        finally { _factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task SubmissionDeadlineClassResourceUrlIsDistinctFromAuthorizedClassCollection()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        using var resource = await studentClient.GetAsync($"/api/v1/academic/classes/{scenario.ClassId}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resource.StatusCode);
        using var malformed = await studentClient.GetAsync("/api/v1/academic/classes/undefined");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, malformed.StatusCode);
        using var missingAssignment = await studentClient.GetAsync($"/api/v1/academic/assignments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingAssignment.StatusCode);
        Assert.Equal(AssessmentErrorCodes.AssignmentNotFound, await ReadProblemCodeAsync(missingAssignment));
        using var collection = await studentClient.GetAsync("/api/v1/academic/classes");
        collection.EnsureSuccessStatusCode();
        using var assignment = await studentClient.GetAsync($"/api/v1/academic/assignments/{scenario.Assignment.Id}");
        assignment.EnsureSuccessStatusCode();
        var result = await ReadRequiredAsync<AssignmentResult>(assignment);
        Assert.Equal(scenario.ClassId, result.ClassId);
        Assert.Equal(scenario.Assignment.DueAtUtc, result.DueAtUtc);
        Assert.Equal(scenario.Assignment.AllowLateSubmission, result.AllowLateSubmission);
    }

    [Fact]
    public async Task SubmissionDeadlineClassCollectionRejectsPersonalWorkspaceWithoutChangingIts403Contract()
    {
        using var client = _factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var response = await client.GetAsync("/api/v1/academic/classes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AcademicErrorCodes.AcademicReadNotAllowed, await ReadProblemCodeAsync(response));
    }

    private static Task<HttpResponseMessage> DeadlineMutationAsync(HttpClient client, string operation,
        SubmissionAttemptResult attempt, Guid attachedFileId, Guid unattachedFileId) => operation switch
    {
        "Save" => client.PatchAsJsonAsync($"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new SaveSubmissionDraftCommand("پاسخ جدید", attempt.RowVersion), JsonOptions),
        "Attach" => client.PostAsJsonAsync($"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
            new AttachSubmissionFileCommand(unattachedFileId, attempt.RowVersion), JsonOptions),
        "Remove" => client.DeleteAsync($"/api/v1/academic/submission-attempts/{attempt.Id}/attachments/{attachedFileId}?expectedRowVersion={Uri.EscapeDataString(attempt.RowVersion)}"),
        "Submit" => client.PostAsJsonAsync($"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion), JsonOptions),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
