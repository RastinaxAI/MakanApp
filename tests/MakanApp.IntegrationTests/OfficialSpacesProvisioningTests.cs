using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task OfficialSpacesConcurrentEnsureCreatesExactlyOnePairPerResource()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        async Task Ensure(Guid? classId)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(s.OrganizationId, classId, default));
        }
        await Task.WhenAll(Ensure(null), Ensure(null));
        await Task.WhenAll(Ensure(s.ClassId), Ensure(s.ClassId));
        await Ensure(s.ClassId);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var rows = await db.Conversations.Where(c => c.OrganizationId == s.OrganizationId && c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic).ToArrayAsync();
        Assert.Equal(4, rows.Length); Assert.Equal(2, rows.Count(c => c.IsOfficialOrganizationSpace));
        Assert.Equal(2, rows.Count(c => c.AcademicClassId == s.ClassId));
        Assert.Equal(4, await db.OfficialSpaceProvisions.CountAsync(p => p.OrganizationId == s.OrganizationId && p.State == OfficialSpaceState.Ready));
    }

    [Fact]
    public async Task OfficialSpacesLegacyDryRunAndPartialRepairKeepGroupHistoryAndPublishNoHistoricalCards()
    {
        using var s = await CreateOfficialScenarioAsync(false); Guid groupId; Guid messageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
            var old = Conversation.CreateSystemManagedAcademic(ConversationType.Group, s.OrganizationId, "گروه قدیمی معتبر", null, now); old.BindAcademicClass(s.ClassId);
            var participant = ConversationParticipant.CreateActive(old.Id, s.TeacherId, now);
            var history = Message.CreateText(old.Id, participant.Id, s.TeacherId, Guid.NewGuid(), old.AllocateNextMessageSequence(), "تاریخچهٔ آزمایشی حفظ می‌شود", 4000, now);
            var lesson = ClassLesson.Create(s.OrganizationId, s.ClassId, s.TeacherId, Guid.NewGuid(), new string('A', 64), "درس قدیمی", "متن منتشرشدهٔ قدیمی", 0, [], now); lesson.Publish(now);
            db.Conversations.Add(old); db.ConversationParticipants.Add(participant); db.Messages.Add(history); db.ClassLessons.Add(lesson); await db.SaveChangesAsync();
            groupId = old.Id; messageId = history.Id;
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var store = scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>();
            var before = await db.Conversations.CountAsync(c => c.OrganizationId == s.OrganizationId);
            var first = (await store.PlanAsync(default, s.OrganizationId)).Single(p => p.SourceId == s.ClassId);
            var second = (await store.PlanAsync(default, s.OrganizationId)).Single(p => p.SourceId == s.ClassId);
            Assert.Equal(first, second); Assert.Equal(groupId, first.GroupCandidateId); Assert.True(first.MissingChannel);
            Assert.Equal("ReuseAndCreateMissing", first.Action); Assert.Equal(before, await db.Conversations.CountAsync(c => c.OrganizationId == s.OrganizationId));
            using var read = await s.Manager.GetAsync($"/api/v1/organization/classes/{s.ClassId}/official-spaces"); read.EnsureSuccessStatusCode();
            Assert.Equal(before, await db.Conversations.CountAsync(c => c.OrganizationId == s.OrganizationId));
            Assert.True(await store.EnsurePairAsync(s.OrganizationId, s.ClassId, default));
            Assert.Equal(before + 1, await db.Conversations.CountAsync(c => c.OrganizationId == s.OrganizationId));
            Assert.True(await db.Messages.AnyAsync(m => m.Id == messageId && m.ConversationId == groupId));
            Assert.Equal(1, await db.Messages.CountAsync(m => m.ConversationId == groupId));
            Assert.False(await db.AcademicActivityPublications.AnyAsync(p => p.ClassId == s.ClassId));
            Assert.Equal(groupId, (await store.PlanAsync(default, s.OrganizationId)).Single(p => p.SourceId == s.ClassId).GroupCandidateId);
        }
    }

    [Theory]
    [InlineData(AcademicActivityKind.Lesson)]
    [InlineData(AcademicActivityKind.Assignment)]
    [InlineData(AcademicActivityKind.Exam)]
    public async Task OfficialSpacesPublicationUsesStructuredSafeCardsAndTwoWorkerDedupe(AcademicActivityKind kind)
    {
        using var s = await CreateOfficialScenarioAsync();
        var marker = "نباید-در-کارت-باشد-" + Guid.NewGuid().ToString("N");
        var publicationId = await PublishOfficialSourceAsync(s, kind, marker);
        await using (var pendingScope = factory.Services.CreateAsyncScope())
        {
            var publication = await pendingScope.ServiceProvider.GetRequiredService<MakanDbContext>().AcademicActivityPublications.SingleAsync(p => p.Id == publicationId);
            var versionId = publication.LessonId ?? publication.AssignmentVersionId ?? publication.ExamVersionId!.Value;
            var status = (await s.Teacher.GetFromJsonAsync<AcademicActivityDeliveryResult[]>(
                $"/api/v1/organization/classes/{s.ClassId}/official-spaces/activities?kind={kind}&sourceVersionId={versionId}&revision={publication.SourceRevision}", JsonOptions))!;
            Assert.Equal(2, status.Length);
            Assert.All(status, d => { Assert.Equal(publicationId, d.EventId); Assert.Equal(AcademicActivityDeliveryState.Pending, d.State); });
            Assert.False(await pendingScope.ServiceProvider.GetRequiredService<MakanDbContext>().Messages.AnyAsync(m => m.AcademicActivityPublicationId == publicationId));
        }
        async Task Deliver()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IOfficialSpacesWorkHandler>().HandleAsync(publicationId,
                OfficialSpaceEventWriter.PublicationType, "{}", default);
        }
        await Task.WhenAll(Deliver(), Deliver()); await Deliver();
        var pair = (await s.Student.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        Assert.Equal("Ready", pair.Group.State); Assert.Equal("Ready", pair.Channel.State);
        foreach (var id in new[] { pair.Group.ConversationId!.Value, pair.Channel.ConversationId!.Value })
        {
            var page = (await s.Student.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{id}/messages", JsonOptions))!;
            var card = Assert.Single(page.Messages);
            Assert.Equal("System", card.OriginKind); Assert.NotNull(card.AcademicActivity);
            Assert.Equal(publicationId, card.AcademicActivity!.EventId); Assert.Equal(kind, card.AcademicActivity.Kind);
            Assert.Equal(s.TeacherId, card.AcademicActivity.InitiatedByUserId); Assert.True(card.AcademicActivity.IsActionable);
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(page, JsonOptions));
            Assert.Empty(card.Attachments);
        }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var deliveries = await db.AcademicActivityDeliveries.Where(d => d.PublicationId == publicationId).ToArrayAsync();
        Assert.Equal(2, deliveries.Length); Assert.All(deliveries, d => Assert.Equal(AcademicActivityDeliveryState.Delivered, d.State));
        Assert.Equal(2, await db.Messages.CountAsync(m => m.AcademicActivityPublicationId == publicationId));
        Assert.Equal(2, await db.MessagingChangeEvents.CountAsync(e => deliveries.Select(d => d.MessageId).Contains(e.ResourceId) && e.ChangeType == MessagingChangeType.MessageCreated));
    }

    [Fact]
    public async Task OfficialSpacesPartialDeliveryRetriesOnlyMissingDestination()
    {
        using var s = await CreateOfficialScenarioAsync(false); Guid groupId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var group = Conversation.CreateSystemManagedAcademic(ConversationType.Group, s.OrganizationId, "گروه آماده", null, DateTime.UtcNow); group.BindAcademicClass(s.ClassId);
            db.Conversations.Add(group); await db.SaveChangesAsync(); groupId = group.Id;
        }
        var publication = await PublishOfficialSourceAsync(s, AcademicActivityKind.Lesson, "بدنهٔ غیرقابل‌انتقال");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IOfficialSpacesWorkHandler>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(publication, OfficialSpaceEventWriter.PublicationType, "{}", default));
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.Equal(1, await db.Messages.CountAsync(m => m.AcademicActivityPublicationId == publication));
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(s.OrganizationId, s.ClassId, default));
            await handler.HandleAsync(publication, OfficialSpaceEventWriter.PublicationType, "{}", default);
            Assert.Equal(2, await db.Messages.CountAsync(m => m.AcademicActivityPublicationId == publication));
            Assert.Single(await db.Messages.Where(m => m.ConversationId == groupId && m.AcademicActivityPublicationId == publication).ToArrayAsync());
        }
    }

    [Fact]
    public async Task OfficialSpacesReadConcealsCrossOrganizationAndDoesNotGrantParentOrUnassignedTeacher()
    {
        using var s = await CreateOfficialScenarioAsync();
        using var other = factory.CreateClient(); var unrelated = await CreateMessagingUserAsync(other);
        var teacher = await factory.CreateMembershipAsync(unrelated.UserId, s.OrganizationId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(other, teacher.MembershipId, OrganizationRole.Teacher);
        using (var response = await other.GetAsync($"/api/v1/organization/classes/{s.ClassId}/official-spaces")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await s.Teacher.GetAsync("/api/v1/organization/official-spaces")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var foreign = await factory.CreateOrganizationAsync(NewOrganizationName());
        var manager = await factory.CreateMembershipAsync(unrelated.UserId, foreign, OrganizationRole.Manager);
        await SelectOrganizationAsync(other, manager.MembershipId, OrganizationRole.Manager);
        using (var response = await other.GetAsync($"/api/v1/organization/classes/{s.ClassId}/official-spaces")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OfficialSpacesStudentCannotPublishChannelAndManagerUsesOnlySameOrganizationGrant()
    {
        using var s = await CreateOfficialScenarioAsync();
        var pair = (await s.Student.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        using var denied = await SendMessageAsync(s.Student, pair.Channel.ConversationId!.Value, Guid.NewGuid(), "دانش‌آموز ناشر کانال نیست");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        _ = await SendMessageAndReadAsync(s.Manager, pair.Group.ConversationId!.Value, Guid.NewGuid(), "ارسال مجاز مدیر همان سازمان");
        _ = await SendMessageAndReadAsync(s.Manager, pair.Channel.ConversationId!.Value, Guid.NewGuid(), "اعلان مجاز مدیر");
        await factory.EndEnrollmentAsync(s.EnrollmentId);
        using var revoked = await s.Student.GetAsync($"/api/v1/conversations/{pair.Group.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        var personal = await StartDirectAndReadAsync(s.Student, s.TeacherId, ConversationScope.Personal);
        _ = await SendMessageAndReadAsync(s.Student, personal.ConversationId, Guid.NewGuid(), "دادهٔ شخصی با پایان Enrollment حذف نمی‌شود");
    }

    [Fact]
    public async Task OfficialSpacesUnknownOrganizationManagerMediaStillRequiresSafetyAndIsNotPublished()
    {
        using var s = await CreateOfficialScenarioAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(s.OrganizationId, null, default));
        var pair = (await s.Manager.GetFromJsonAsync<OfficialSpacesPairResult>("/api/v1/organization/official-spaces", JsonOptions))!;
        var file = await UploadFileAsync(s.Manager, "image/png", "official-safety.png");
        await factory.SetCommunicationAgeCategoryAsync(s.ManagerId, MakanApp.Domain.Identity.CommunicationAgeCategory.Unknown);
        factory.ContentSafetyProvider.Configure(ContentSafetyDecision.ReviewRequired);
        try
        {
            using var denied = await s.Manager.PostAsJsonAsync($"/api/v1/conversations/{pair.Group.ConversationId}/messages",
                new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] }, JsonOptions);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
            Assert.Equal(MessagingErrorCodes.MessageSafetyReviewRequired, await ReadProblemCodeAsync(denied));
            Assert.Single(factory.ContentSafetyProvider.Requests);
            Assert.Empty(await factory.GetMessagesAsync(pair.Group.ConversationId!.Value));
        }
        finally { factory.ContentSafetyProvider.Configure(ContentSafetyDecision.Allowed); }
    }

    private async Task<Guid> PublishOfficialSourceAsync(OfficialScenario s, AcademicActivityKind kind, string marker)
    {
        var classbase = $"/api/v1/academic/classes/{s.ClassId}";
        if (kind == AcademicActivityKind.Lesson)
        {
            var lesson = await OfficialPostAsync(s.Teacher, classbase + "/lessons", new { clientOperationId = Guid.NewGuid(), title = "درس آزمایشی رسمی", body = marker, displayOrder = 0, fileAssetIds = Array.Empty<Guid>() });
            await AssertNoOfficialPublicationAsync(s.ClassId, kind);
            await OfficialPostAsync(s.Teacher, $"/api/v1/academic/lessons/{lesson["id"]!.GetValue<string>()}/publish", new { expectedVersion = lesson["version"]!.GetValue<string>() });
        }
        else if (kind == AcademicActivityKind.Assignment)
        {
            var assignment = await OfficialPostAsync(s.Teacher, classbase + "/assignments", new { title = "تکلیف آزمایشی رسمی", description = marker, dueAtUtc = DateTime.UtcNow.AddDays(1), allowLateSubmission = false, maxAttempts = 2, maxScore = 20m });
            await AssertNoOfficialPublicationAsync(s.ClassId, kind);
            await OfficialPostAsync(s.Teacher, $"/api/v1/academic/assignments/{assignment["id"]!.GetValue<string>()}/publish", new { expectedAssignmentRowVersion = assignment["assignmentRowVersion"]!.GetValue<string>(), expectedVersionRowVersion = assignment["versionRowVersion"]!.GetValue<string>() });
        }
        else
        {
            var exam = await OfficialPostAsync(s.Teacher, classbase + "/exams", new { title = "آزمون آزمایشی رسمی", description = marker, availableFromUtc = DateTime.UtcNow.AddMinutes(-5), availableUntilUtc = DateTime.UtcNow.AddHours(2), durationMinutes = 30, maxAttempts = 2, maxScore = 10m, randomizationPolicy = "None" });
            var path = $"/api/v1/academic/exams/{exam["id"]!.GetValue<string>()}";
            exam = await OfficialPostAsync(s.Teacher, path + "/questions", new { order = 1, type = "ObjectiveSingleChoice", prompt = marker,
                score = 10m, options = new[] { new { order = 1, text = marker, isCorrect = true }, new { order = 2, text = "گزینهٔ دیگر", isCorrect = false } }, expectedVersionRowVersion = exam["versionRowVersion"]!.GetValue<string>() });
            await AssertNoOfficialPublicationAsync(s.ClassId, kind);
            await OfficialPostAsync(s.Teacher, path + "/publish", new { expectedExamRowVersion = exam["examRowVersion"]!.GetValue<string>(), expectedVersionRowVersion = exam["versionRowVersion"]!.GetValue<string>() });
        }
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await db.AcademicActivityPublications.Where(p => p.ClassId == s.ClassId && p.Kind == kind).Select(p => p.Id).SingleAsync();
    }

    private async Task AssertNoOfficialPublicationAsync(Guid classId, AcademicActivityKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().AcademicActivityPublications.AnyAsync(p => p.ClassId == classId && p.Kind == kind));
    }

    private static async Task<JsonNode> OfficialPostAsync(HttpClient client, string path, object command)
    {
        using var response = await client.PostAsJsonAsync(path, command, JsonOptions); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions))!;
    }

    private async Task<OfficialScenario> CreateOfficialScenarioAsync(bool ensure = true)
    {
        var managerClient = factory.CreateClient(); var teacherClient = factory.CreateClient(); var studentClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient); var teacher = await CreateMessagingUserAsync(teacherClient); var student = await CreateMessagingUserAsync(studentClient);
        var org = await factory.CreateOrganizationAsync(NewOrganizationName()); var classroom = await factory.CreateAcademicClassAsync(org);
        var managerMember = await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        var teacherMember = await factory.CreateMembershipAsync(teacher.UserId, org, OrganizationRole.Teacher);
        var studentMember = await factory.CreateMembershipAsync(student.UserId, org, OrganizationRole.Student);
        var person = await factory.CreateOrganizationPersonForUserAsync(org, student.UserId);
        var enrollment = await factory.CreateEnrollmentAsync(org, classroom.ClassId, person);
        var assignment = await factory.CreateTeacherAssignmentAsync(org, classroom.ClassId, teacherMember.MembershipId);
        await SelectOrganizationAsync(managerClient, managerMember.MembershipId, OrganizationRole.Manager);
        await SelectOrganizationAsync(teacherClient, teacherMember.MembershipId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(studentClient, studentMember.MembershipId, OrganizationRole.Student);
        if (ensure)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePairAsync(org, classroom.ClassId, default));
        }
        return new(managerClient, teacherClient, studentClient, manager.UserId, teacher.UserId, student.UserId, org, classroom.ClassId, enrollment, assignment);
    }

    private sealed record OfficialScenario(HttpClient Manager, HttpClient Teacher, HttpClient Student, Guid ManagerId, Guid TeacherId,
        Guid StudentId, Guid OrganizationId, Guid ClassId, Guid EnrollmentId, Guid TeacherAssignmentId) : IDisposable
    {
        public void Dispose() { Manager.Dispose(); Teacher.Dispose(); Student.Dispose(); }
    }
}
