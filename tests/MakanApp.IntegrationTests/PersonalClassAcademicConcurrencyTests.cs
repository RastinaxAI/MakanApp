using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalAcademicMembershipEndRacingWithDraftSavePreservesRecordsAndFencesFutureWrites()
    {
        var s = await PersonalAcademicAsync(); var assignment = await PersonalAssignmentAsync(s);
        var attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        var route = $"/api/v1/academic/submission-attempts/{attempt.Id}/draft";
        var member = await WithDb(db => db.PersonalClassParticipants.AsNoTracking().SingleAsync(m => m.PersonalClassId == s.Class.Id && m.StudentUserId == s.Learner.UserId && m.EndedAtUtc == null));
        using var end = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{s.Class.Id}/memberships/{member.Id}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(Convert.ToBase64String(member.RowVersion), "آزمون هم‌زمان پایان عضویت"), options: Json) };
        var responses = await Task.WhenAll(s.Manager.Client.SendAsync(end),
            s.Learner.Client.PatchAsJsonAsync(route, new SaveSubmissionDraftCommand("پاسخ هم‌زمان", attempt.RowVersion), Json));
        responses[0].EnsureSuccessStatusCode();
        Assert.True(responses[1].IsSuccessStatusCode || responses[1].StatusCode == HttpStatusCode.NotFound);
        foreach (var response in responses) response.Dispose();
        using var denied = await s.Learner.Client.PatchAsJsonAsync(route, new SaveSubmissionDraftCommand("پاسخ پس از لغو", attempt.RowVersion), Json);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await WithDb(async db => Assert.True(await db.SubmissionAttempts.AnyAsync(a => a.Id == attempt.Id)));
    }

    [Fact]
    public async Task PersonalAcademicAuthoritiesAndFilesCannotCrossIndependentClasses()
    {
        var first = await PersonalAcademicAsync(); var second = await PersonalAcademicAsync();
        var assignment = await PersonalAssignmentAsync(first); var exam = await PersonalExamAsync(first);
        var submission = await PostAsync<SubmissionAttemptResult>(first.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        var file = await PersonalFileAsync(first.Learner, first.Class.Id);
        var route = $"/api/v1/academic/submission-attempts/{submission.Id}";
        await PostAsync<SubmissionAttemptResult>(first.Learner.Client, route + "/attachments", new AttachSubmissionFileCommand(file.Id, submission.RowVersion));
        foreach (var actor in new[] { second.Manager, second.Learner })
        foreach (var path in new[] { $"/api/v1/academic/assignments/{assignment.Id}", $"/api/v1/academic/exams/{exam.Id}/editor", route, $"/api/v1/files/{file.Id}/content" })
        {
            using var denied = await actor.Client.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var copy = await second.Manager.Client.PostAsJsonAsync($"/api/v1/academic/personal-classes/{second.Class.Id}/assignments",
            AssignmentDraft() with { AttachmentFileIds = [file.Id] }, Json);
        Assert.False(copy.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PersonalAcademicConcurrentPublishCreatesOneSnapshotAndTwoIdempotentActivityCards()
    {
        var s = await PersonalAcademicAsync();
        var draft = await PostAsync<AssignmentResult>(s.Manager.Client, $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", AssignmentDraft());
        var command = new PublishAssignmentCommand(draft.AssignmentRowVersion, draft.VersionRowVersion);
        var results = await Task.WhenAll(
            PostAsync<PublishAssignmentResult>(s.Manager.Client, $"/api/v1/academic/assignments/{draft.Id}/publish", command),
            PostAsync<PublishAssignmentResult>(s.Manager.Client, $"/api/v1/academic/assignments/{draft.Id}/publish", command));
        Assert.Equal(results[0].PublicationReceipt!.EventId, results[1].PublicationReceipt!.EventId);
        var eventId = results[0].PublicationReceipt!.EventId;
        foreach (var destination in new[] { ConversationType.Group, ConversationType.Channel, ConversationType.Group, ConversationType.Channel })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Messaging.EfMessagingStore>().DeliverAcademicActivityAsync(eventId, destination, default));
        }
        await WithDb(async db =>
        {
            Assert.Equal(2, await db.Messages.CountAsync(m => m.AcademicActivityPublicationId == eventId));
            Assert.Equal(1, await db.AcademicActivityPublications.CountAsync(p => p.AssignmentVersionId == draft.VersionId));
            Assert.Equal(2, await db.AssignmentRecipients.CountAsync(r => r.AssignmentVersionId == draft.VersionId));
        });
    }

    [Fact]
    public async Task PersonalAcademicDraftReplayIsPrivateAndRevocationStopsHistoricalReplay()
    {
        var s = await PersonalAcademicAsync(); var assignment = await PersonalAssignmentAsync(s);
        var learnerCursor = (await GetAsync<AccountChangePageResult>(s.Learner.Client, "/api/v1/realtime/changes?scope=Personal")).NextCursor;
        var peerCursor = (await GetAsync<AccountChangePageResult>(s.Peer.Client, "/api/v1/realtime/changes?scope=Personal")).NextCursor;
        var attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        attempt = await AcademicWriteAsync<SubmissionAttemptResult>(s.Learner.Client, HttpMethod.Patch, $"/api/v1/academic/submission-attempts/{attempt.Id}/draft", new SaveSubmissionDraftCommand("PRIVATE-ANSWER-FOR-REPLAY", attempt.RowVersion));
        var own = await GetAsync<AccountChangePageResult>(s.Learner.Client, "/api/v1/realtime/changes?scope=Personal&cursor=" + Uri.EscapeDataString(learnerCursor));
        Assert.Contains(own.Changes, x => x.Invalidation?.ResourceId == attempt.Id);
        var peer = await GetAsync<AccountChangePageResult>(s.Peer.Client, "/api/v1/realtime/changes?scope=Personal&cursor=" + Uri.EscapeDataString(peerCursor));
        Assert.DoesNotContain(peer.Changes, x => x.Invalidation?.ResourceId == attempt.Id);
        Assert.DoesNotContain("PRIVATE-ANSWER-FOR-REPLAY", System.Text.Json.JsonSerializer.Serialize(own));
        await EndAcademicMemberAsync(s, s.Learner);
        using var revoked = await s.Learner.Client.GetAsync("/api/v1/realtime/changes?scope=Personal&cursor=" + Uri.EscapeDataString(learnerCursor));
        if (revoked.StatusCode == HttpStatusCode.Conflict)
            Assert.Equal(RealtimeErrorCodes.ResyncRequired, await ProblemAsync(revoked));
        else
        {
            revoked.EnsureSuccessStatusCode();
            var hidden = (await revoked.Content.ReadFromJsonAsync<AccountChangePageResult>(Json))!;
            Assert.DoesNotContain(hidden.Changes, x => x.Invalidation?.ResourceId == attempt.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonalAcademicOpenExamStopsSafelyAfterArchiveOrMembershipEnd(bool archive)
    {
        var s = await PersonalAcademicAsync(); var exam = await PersonalExamAsync(s, 1);
        var attempt = await PersonalExamStartAsync(s.Learner, exam.Id);
        var route = $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}";
        var lease = await PostAsync<ExamWriteLeaseDto>(s.Learner.Client, route + "/write-lease", null);
        var question = attempt.Questions.Single(q => q.QuestionType == ExamQuestionType.Descriptive);
        var file = await PersonalFileAsync(s.Learner, s.Class.Id);
        var saved = await PostAsync<ExamAnswerReceiptDto>(s.Learner.Client, route + $"/answers/{question.AttemptQuestionId}/attachments", new AttachExamAnswerFileCommand(file.Id, Guid.NewGuid(), lease.WriteLeaseVersion, null));
        if (archive)
        {
            var classroom = await GetAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}");
            await PostAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}/archive", new PersonalClassReasonCommand(classroom.Version, "بایگانی هنگام آزمون"));
        }
        else await EndAcademicMemberAsync(s, s.Learner);
        using var denied = await s.Learner.Client.PutAsJsonAsync(route + $"/answers/{question.AttemptQuestionId}", new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, 1, null, "تلاش پس از تغییر مجوز"), Json);
        Assert.False(denied.IsSuccessStatusCode);
        if (!archive)
        {
            using var download = await s.Learner.Client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
            await JoinAcademicAsync(s.Manager, s.Learner, s.Class.Id, PersonalClassLocalRoles.Learner);
            using var oldAttempt = await s.Learner.Client.GetAsync(route); Assert.Equal(HttpStatusCode.NotFound, oldAttempt.StatusCode);
        }
        else
        {
            factory.Clock.SetUtcNow(attempt.EffectiveDeadlineUtc);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<IExamDeadlineFinalizer>().FinalizeDueAttemptAsync(attempt.ExamAttemptId, default));
        }
        await WithDb(async db =>
        {
            Assert.True(await db.ExamAttempts.AnyAsync(a => a.Id == attempt.ExamAttemptId));
            Assert.True(await db.AnswerRevisions.AnyAsync(a => a.Id == saved.AnswerRevisionId));
        });
    }
}
