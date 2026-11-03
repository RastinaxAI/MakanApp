using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private sealed record PersonalAcademicScenario(TestActor Manager, TestActor Admin, TestActor Learner,
        TestActor Peer, PersonalClassDto Class, PersonalClassAcademicPermissionDto Grant);

    private async Task<PersonalAcademicScenario> PersonalAcademicAsync()
    {
        var manager = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(manager.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var classroom = await ApprovedClassAsync(manager, "کلاس پذیرش آموزشی");
        var admin = await AdminAsync();
        var grant = await GrantAcademicAsync(admin, classroom.Id, manager, AllAcademic);
        var learner = await ActorAsync(); var peer = await ActorAsync();
        await JoinAcademicAsync(manager, learner, classroom.Id, PersonalClassLocalRoles.Learner);
        await JoinAcademicAsync(manager, peer, classroom.Id, PersonalClassLocalRoles.Learner);
        return new(manager, admin, learner, peer, classroom, grant);
    }

    private async Task<AssignmentResult> PersonalAssignmentAsync(PersonalAcademicScenario s, CreateAssignmentDraftCommand? command = null)
    {
        var draft = await PostAsync<AssignmentResult>(s.Manager.Client, $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", command ?? AssignmentDraft());
        return (await PostAsync<PublishAssignmentResult>(s.Manager.Client, $"/api/v1/academic/assignments/{draft.Id}/publish",
            new PublishAssignmentCommand(draft.AssignmentRowVersion, draft.VersionRowVersion))).Assignment;
    }

    private static async Task<T> AcademicWriteAsync<T>(HttpClient client, HttpMethod method, string route, object body)
    {
        using var request = new HttpRequestMessage(method, route) { Content = JsonContent.Create(body, options: Json) };
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<FileAssetResult> PersonalFileAsync(TestActor actor, Guid classId)
    {
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(StabilizationMediaFixtures.Bytes("image/png"));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(part, "File", "answer.png"); form.Add(new StringContent(classId.ToString()), "PersonalClassId");
        using var response = await actor.Client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<FileAssetResult>(Json))!;
    }

    private async Task EndAcademicMemberAsync(PersonalAcademicScenario s, TestActor target)
    {
        var member = await WithDb(db => db.PersonalClassParticipants.AsNoTracking().SingleAsync(m => m.PersonalClassId == s.Class.Id && m.StudentUserId == target.UserId && m.EndedAtUtc == null));
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{s.Class.Id}/memberships/{member.Id}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(Convert.ToBase64String(member.RowVersion), "پایان عضویت آزمون"), options: Json) };
        using var response = await s.Manager.Client.SendAsync(request); response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PersonalAcademicAssignmentAttachmentSubmissionReleaseCorrectionAndPrivateAcl()
    {
        var s = await PersonalAcademicAsync();
        var authorFile = await PersonalFileAsync(s.Manager, s.Class.Id);
        var assignment = await PersonalAssignmentAsync(s, AssignmentDraft() with { AttachmentFileIds = [authorFile.Id] });
        using (var authorDownload = await s.Learner.Client.GetAsync($"/api/v1/files/{authorFile.Id}/content")) authorDownload.EnsureSuccessStatusCode();
        var attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        var route = $"/api/v1/academic/submission-attempts/{attempt.Id}";
        var oldVersion = attempt.RowVersion;
        var saves = await Task.WhenAll(
            s.Learner.Client.PatchAsJsonAsync(route + "/draft", new SaveSubmissionDraftCommand("پاسخ نخست هنرجو", oldVersion), Json),
            s.Learner.Client.PatchAsJsonAsync(route + "/draft", new SaveSubmissionDraftCommand("پاسخ دوم هنرجو", oldVersion), Json));
        var saved = Assert.Single(saves, r => r.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, Assert.Single(saves, r => !r.IsSuccessStatusCode).StatusCode);
        attempt = (await saved.Content.ReadFromJsonAsync<SubmissionAttemptResult>(Json))!;
        foreach (var response in saves) response.Dispose();
        var foreignFile = await PersonalFileAsync(s.Peer, s.Class.Id);
        using (var foreign = await s.Learner.Client.PostAsJsonAsync(route + "/attachments", new AttachSubmissionFileCommand(foreignFile.Id, attempt.RowVersion), Json))
            Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        var pendingFileId = await WithDb(async db =>
        {
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            var membership = await db.PersonalClassParticipants.SingleAsync(m => m.PersonalClassId == s.Class.Id && m.StudentUserId == s.Learner.UserId && m.EndedAtUtc == null);
            var pending = FileAsset.CreatePending(Guid.NewGuid(), null, s.Learner.UserId, "pending.png", Guid.NewGuid().ToString("N"), "image/png", now, now.AddHours(1));
            pending.BindPersonalClass(s.Class.Id, membership.Id);
            db.FileAssets.Add(pending); await db.SaveChangesAsync(); return pending.Id;
        });
        using (var pending = await s.Learner.Client.PostAsJsonAsync(route + "/attachments", new AttachSubmissionFileCommand(pendingFileId, attempt.RowVersion), Json))
            Assert.Equal(AssessmentErrorCodes.SubmissionFileNotReady, await ProblemAsync(pending));
        var file = await PersonalFileAsync(s.Learner, s.Class.Id);
        attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, route + "/attachments", new AttachSubmissionFileCommand(file.Id, attempt.RowVersion));
        using (var draftContent = await s.Manager.Client.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, draftContent.StatusCode);
        var submit = new FinalSubmitAssignmentCommand(attempt.RowVersion);
        var receipt = await PostAsync<SubmissionReceipt>(s.Learner.Client, route + "/submit", submit);
        Assert.Equal(receipt, await PostAsync<SubmissionReceipt>(s.Learner.Client, route + "/submit", submit));
        using (var evaluatorFile = await s.Manager.Client.GetAsync($"/api/v1/files/{file.Id}/content")) evaluatorFile.EnsureSuccessStatusCode();
        var queue = await GetAsync<EvaluationQueueItemResult[]>(s.Manager.Client, $"/api/v1/academic/evaluations/queue?classId={s.Class.Id}&classKind=PersonalClass&page=1&pageSize=1");
        Assert.Equal(attempt.Id, Assert.Single(queue).SubmissionAttemptId);
        var evaluation = await AcademicWriteAsync<EvaluatorEvaluationResult>(s.Manager.Client, HttpMethod.Put, route + "/evaluation",
            new SaveEvaluationDraftCommand(17.25m, "بازخورد هنرجو", "بازخورد ولی", "PRIVATE-EVALUATOR-NOTE", null));
        using (var unreleased = await s.Learner.Client.GetAsync(route + "/result")) Assert.Equal(HttpStatusCode.NotFound, unreleased.StatusCode);
        var release = await PostAsync<GradeReleaseResult>(s.Manager.Client, route + "/evaluation/release", new ReleaseEvaluationCommand(evaluation.RowVersion));
        var result = await GetAsync<StudentReleasedResult>(s.Learner.Client, route + "/result");
        Assert.Equal(17.25m, result.Score);
        var rawResult = await s.Learner.Client.GetStringAsync(route + "/result");
        Assert.DoesNotContain("PRIVATE-EVALUATOR-NOTE", rawResult); Assert.DoesNotContain("guardianVisibleFeedback", rawResult);
        foreach (var actor in new[] { s.Peer, s.Admin })
        {
            using var hidden = await actor.Client.GetAsync(route + "/result"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            using var hiddenFile = await actor.Client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.NotFound, hiddenFile.StatusCode);
        }
        var correctionCommand = new CorrectReleasedEvaluationCommand(18m, "اصلاح بازخورد", null, "PRIVATE-CORRECTION", "بازبینی مستند", release.EvaluationRowVersion);
        var corrections = await Task.WhenAll(
            s.Manager.Client.PostAsJsonAsync(route + "/evaluation/corrections", correctionCommand, Json),
            s.Manager.Client.PostAsJsonAsync(route + "/evaluation/corrections", correctionCommand, Json));
        var accepted = Assert.Single(corrections, r => r.IsSuccessStatusCode);
        var conflict = Assert.Single(corrections, r => !r.IsSuccessStatusCode);
        Assert.Equal(AssessmentErrorCodes.GradeReleaseConflict, await ProblemAsync(conflict));
        var correction = (await accepted.Content.ReadFromJsonAsync<EvaluatorEvaluationResult>(Json))!;
        foreach (var response in corrections) response.Dispose();
        Assert.Equal(17.25m, (await GetAsync<StudentReleasedResult>(s.Learner.Client, route + "/result")).Score);
        await PostAsync<GradeReleaseResult>(s.Manager.Client, route + "/evaluation/release", new ReleaseEvaluationCommand(correction.RowVersion));
        Assert.Equal(18m, (await GetAsync<StudentReleasedResult>(s.Learner.Client, route + "/result")).Score);
        await WithDb(async db => Assert.Equal(2, await db.EvaluationRevisions.CountAsync(r => r.SubmissionAttemptId == attempt.Id)));
        await EndAcademicMemberAsync(s, s.Learner);
        using (var removedFile = await s.Learner.Client.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, removedFile.StatusCode);
        await JoinAcademicAsync(s.Manager, s.Learner, s.Class.Id, PersonalClassLocalRoles.Learner);
        using (var oldAttempt = await s.Learner.Client.GetAsync(route)) Assert.Equal(HttpStatusCode.NotFound, oldAttempt.StatusCode);
        using (var oldResult = await s.Learner.Client.GetAsync(route + "/result")) Assert.Equal(HttpStatusCode.NotFound, oldResult.StatusCode);
        using (var oldRecipient = await s.Learner.Client.PostAsJsonAsync($"/api/v1/academic/assignments/{assignment.Id}/attempts", (object?)null, Json)) Assert.Equal(HttpStatusCode.NotFound, oldRecipient.StatusCode);
    }

    [Fact]
    public async Task PersonalAcademicAssignmentServerDeadlineAttemptLimitAndArchivePreserveHistory()
    {
        var s = await PersonalAcademicAsync();
        var assignment = await PersonalAssignmentAsync(s, AssignmentDraft() with { MaxAttempts = 1 });
        var attempt = await PostAsync<SubmissionAttemptResult>(s.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
        attempt = await AcademicWriteAsync<SubmissionAttemptResult>(s.Learner.Client, HttpMethod.Patch, $"/api/v1/academic/submission-attempts/{attempt.Id}/draft", new SaveSubmissionDraftCommand("پاسخ", attempt.RowVersion));
        await PostAsync<SubmissionReceipt>(s.Learner.Client, $"/api/v1/academic/submission-attempts/{attempt.Id}/submit", new FinalSubmitAssignmentCommand(attempt.RowVersion));
        using (var limit = await s.Learner.Client.PostAsJsonAsync($"/api/v1/academic/assignments/{assignment.Id}/attempts", (object?)null, Json))
            Assert.Equal(AssessmentErrorCodes.SubmissionAttemptsExhausted, await ProblemAsync(limit));
        var expiring = await PersonalAssignmentAsync(s, AssignmentDraft() with { DueAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(1) });
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(2));
        using (var late = await s.Learner.Client.PostAsJsonAsync($"/api/v1/academic/assignments/{expiring.Id}/attempts", (object?)null, Json))
            Assert.Equal(AssessmentErrorCodes.SubmissionDeadlinePassed, await ProblemAsync(late));
        var classroom = await GetAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}");
        await PostAsync<PersonalClassDto>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}/archive", new PersonalClassReasonCommand(classroom.Version, "بایگانی آزمون"));
        using var archived = await s.Manager.Client.PostAsJsonAsync($"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", AssignmentDraft(), Json);
        Assert.Equal(AcademicErrorCodes.ClassNotActive, await ProblemAsync(archived));
        await WithDb(async db => Assert.True(await db.SubmissionAttempts.AnyAsync(a => a.Id == attempt.Id && a.Status == SubmissionAttemptStatus.Submitted)));
    }
}
