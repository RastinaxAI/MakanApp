using System.Text.Json;
using MakanApp.Application.Realtime;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task AccountRealtimeAssignmentDraftIsPrivateButPublishedAssignmentInvalidatesEligibleStudent()
    {
        using var staff = _factory.CreateClient(); using var student = _factory.CreateClient();
        var manager = await CreateManagerAsync(staff);
        var classroom = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        _ = await CreateStudentAsync(student, manager.OrganizationId, classroom.ClassId);
        var boundary = await AccountReplayTestClient.ReadAsync(student, JsonOptions);
        var draft = await CreateDraftAsync(staff, classroom.ClassId);
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(student, JsonOptions, boundary.NextCursor)).Values,
            x => x.Invalidation?.ResourceId == draft.Id);
        _ = await PublishAsync(staff, draft);
        var replay = await AccountReplayTestClient.DrainAsync(student, JsonOptions, boundary.NextCursor, 1);
        Assert.Single(replay.Values, x => x.Invalidation?.ResourceId == draft.Id && x.Invalidation.Key == "assessment.assignment");
    }

    [Fact]
    public async Task AccountRealtimeSubmissionDraftIsOwnerOnlyAndFinalSubmitInvalidatesAuthorizedEvaluator()
    {
        using var staff = _factory.CreateClient(); using var student = _factory.CreateClient(); using var peer = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(staff, student);
        _ = await CreateStudentAsync(peer, scenario.OrganizationId, scenario.ClassId);
        var staffCursor = (await AccountReplayTestClient.ReadAsync(staff, JsonOptions)).NextCursor;
        var studentCursor = (await AccountReplayTestClient.ReadAsync(student, JsonOptions)).NextCursor;
        var peerCursor = (await AccountReplayTestClient.ReadAsync(peer, JsonOptions)).NextCursor;
        var draft = await CreateSubmissionDraftAsync(student, scenario.Assignment.Id);
        var saved = await SaveSubmissionDraftAsync(student, draft, "پاسخ خصوصی ساختگی آزمون");
        Assert.Contains((await AccountReplayTestClient.DrainAsync(student, JsonOptions, studentCursor)).Values, x => x.Invalidation?.ResourceId == draft.Id);
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(staff, JsonOptions, staffCursor)).Values, x => x.Invalidation?.ResourceId == draft.Id);
        _ = await SubmitAsync(student, saved);
        Assert.Contains((await AccountReplayTestClient.DrainAsync(staff, JsonOptions, staffCursor)).Values, x => x.Invalidation?.ResourceId == draft.Id);
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(peer, JsonOptions, peerCursor)).Values, x => x.Invalidation?.ResourceId == draft.Id);
    }

    [Fact]
    public async Task AccountRealtimeGradeIsHiddenBeforeExplicitReleaseAndContainsNoPrivateGradePayloadAfterRelease()
    {
        using var staff = _factory.CreateClient(); using var student = _factory.CreateClient(); using var parent = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(staff, student);
        _ = await CreateParentForStudentAsync(parent, scenario.Submission);
        var studentCursor = (await AccountReplayTestClient.ReadAsync(student, JsonOptions)).NextCursor;
        var parentCursor = (await AccountReplayTestClient.ReadAsync(parent, JsonOptions)).NextCursor;
        var grade = await SaveEvaluationAsync(staff, scenario.Attempt.Id);
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(student, JsonOptions, studentCursor)).Values, x => x.Invalidation?.Key == "assessment.grade");
        Assert.DoesNotContain((await AccountReplayTestClient.DrainAsync(parent, JsonOptions, parentCursor)).Values, x => x.Invalidation?.Key == "assessment.grade");
        _ = await ReleaseEvaluationAsync(staff, scenario.Attempt.Id, grade.RowVersion);
        foreach (var actor in new[] { (student, studentCursor), (parent, parentCursor) })
        {
            var replay = await AccountReplayTestClient.DrainAsync(actor.Item1, JsonOptions, actor.Item2, 1);
            var published = Assert.Single(replay.Values, x => x.Invalidation?.Key == "assessment.grade");
            Assert.Equal(scenario.Attempt.Id, published.Invalidation!.ResourceId);
            var json = JsonSerializer.Serialize(published);
            Assert.DoesNotContain("score", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PRIVATE-DISTINCTIVE-NOTE", json);
        }
    }
}
