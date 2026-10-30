using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Data.Common;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(ConversationType.Group)]
    [InlineData(ConversationType.Channel)]
    public async Task OfficialSpacesPartialRepairPreservesEitherExistingPurposeAndCreatesOnlyItsMissingCounterpart(ConversationType existingPurpose)
    {
        using var s = await CreateOfficialScenarioAsync(false); Guid existingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var conversation = Conversation.CreateSystemManagedAcademic(existingPurpose, s.OrganizationId, "مقصد معتبر موجود", null, DateTime.UtcNow);
            conversation.BindAcademicClass(s.ClassId); existingId = conversation.Id; db.Conversations.Add(conversation); await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>();
            Assert.True(await store.EnsurePairAsync(s.OrganizationId, s.ClassId, default));
            Assert.True(await store.EnsurePairAsync(s.OrganizationId, s.ClassId, default));
            var rows = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Conversations.Where(c => c.AcademicClassId == s.ClassId).ToArrayAsync();
            Assert.Equal(2, rows.Length); Assert.Equal(existingId, Assert.Single(rows, c => c.Type == existingPurpose).Id);
            Assert.Single(rows, c => c.Type != existingPurpose);
        }
    }

    [Fact]
    public async Task OfficialSpacesCaptureAndPlannerDoNotTreatPersonalClassAsOrganizationClass()
    {
        using var client = factory.CreateClient(); var actor = await CreateMessagingUserAsync(client);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var before = await db.OfficialSpaceProvisions.CountAsync();
        var personal = PersonalClass.Create(actor.UserId, "کلاس مستقل شخصی", Guid.NewGuid(), DateTime.UtcNow);
        db.PersonalClasses.Add(personal);
        // همان capture تولیدی اجرا می‌شود؛ تشخیص نوع واقعی Class باید PersonalClass را کنار بگذارد.
        await scope.ServiceProvider.GetRequiredService<OfficialSpaceEventWriter>().CaptureSchoolAsync(default);
        await db.SaveChangesAsync();
        Assert.Equal(before, await db.OfficialSpaceProvisions.CountAsync());
        Assert.False(await db.Conversations.AnyAsync(c => c.AcademicClassId == personal.Id));
        Assert.DoesNotContain(await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().PlanAsync(default), row => row.SourceId == personal.Id);
    }

    [Fact]
    public async Task OfficialSpacesWorkerUsesAcademicClassThenCommercialOrganizationLockOrderDuringRevocation()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await using var holder = factory.Services.CreateAsyncScope(); var db = holder.ServiceProvider.GetRequiredService<MakanDbContext>();
        var academic = holder.ServiceProvider.GetRequiredService<IAcademicStore>();
        await using var tx = await academic.BeginSerializableTransactionAsync(default);
        Assert.NotNull(await academic.GetClassForUpdateAsync(s.OrganizationId, s.ClassId, default));
        var observer = new OfficialSourceLockObserver();
        await using var workerDb = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(db.Database.GetConnectionString()).AddInterceptors(observer).Options);
        var spaces = new EfOfficialSpacesStore(workerDb, new EfOutbox(workerDb), TimeProvider.System, NullLogger<EfOfficialSpacesStore>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var work = spaces.EnsurePairAsync(s.OrganizationId, s.ClassId, timeout.Token);
        try
        {
            Assert.Equal("Class", await observer.First.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            var assignment = await db.TeacherAssignments.SingleAsync(a => a.Id == s.TeacherAssignmentId);
            assignment.End(DateTime.UtcNow);
            // Save واقعی Academic، کنترل تجاری و قفل Organization موجود را اجرا می‌کند؛ worker هم‌زمان منتظر Class است.
            await academic.SaveChangesAsync(timeout.Token); await tx.CommitAsync(timeout.Token);
            Assert.True(await work);
            Assert.False(await workerDb.ConversationParticipants.AnyAsync(p => p.UserId == s.TeacherId && p.Status == ConversationParticipantStatus.Active &&
                workerDb.Conversations.Any(c => c.Id == p.ConversationId && c.AcademicClassId == s.ClassId)));
        }
        finally
        {
            timeout.Cancel();
            try { await work; } catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }
    }

    private sealed class OfficialSourceLockObserver : DbCommandInterceptor
    {
        public TaskCompletionSource<string> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK,HOLDLOCK", StringComparison.Ordinal))
            {
                if (command.CommandText.Contains("[academic].[Classes]", StringComparison.Ordinal)) First.TrySetResult("Class");
                else if (command.CommandText.Contains("[organization].[Organizations]", StringComparison.Ordinal)) First.TrySetResult("Organization");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task OfficialSpacesReadNeverClaimsReadyWithoutObservedDestinationBinding()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            foreach (var purpose in new[] { ConversationType.Group, ConversationType.Channel })
            {
                var intent = OfficialSpaceProvision.Request(s.OrganizationId, s.ClassId, purpose, DateTime.UtcNow);
                intent.Ready(); db.OfficialSpaceProvisions.Add(intent);
            }
            await db.SaveChangesAsync();
        }
        var pair = (await s.Manager.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        Assert.Equal("Pending", pair.Group.State); Assert.Null(pair.Group.ConversationId); Assert.False(pair.Group.Capabilities.CanRead);
        Assert.Equal("Pending", pair.Channel.State); Assert.Null(pair.Channel.ConversationId); Assert.False(pair.Channel.Capabilities.CanSend);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<MakanDbContext>().Conversations.AnyAsync(c => c.AcademicClassId == s.ClassId));
    }

    [Fact]
    public async Task OfficialSpacesCapabilitiesWaitForParticipantProjectionAndVersionChangesAfterSynchronization()
    {
        using var s = await CreateOfficialScenarioAsync(); using var teacherClient = factory.CreateClient();
        var teacher = await CreateMessagingUserAsync(teacherClient);
        var member = await factory.CreateMembershipAsync(teacher.UserId, s.OrganizationId, OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(s.OrganizationId, s.ClassId, member.MembershipId);
        await SelectOrganizationAsync(teacherClient, member.MembershipId, OrganizationRole.Teacher);
        var path = $"/api/v1/organization/classes/{s.ClassId}/official-spaces";
        var before = (await teacherClient.GetFromJsonAsync<OfficialSpacesPairResult>(path, JsonOptions))!;
        Assert.Equal("Ready", before.Group.State); Assert.False(before.Group.Capabilities.CanRead);
        using (var denied = await teacherClient.GetAsync($"/api/v1/conversations/{before.Group.ConversationId}/messages")) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().SynchronizeExistingAsync(s.OrganizationId, s.ClassId, default);
        var after = (await teacherClient.GetFromJsonAsync<OfficialSpacesPairResult>(path, JsonOptions))!;
        Assert.NotEqual(before.Version, after.Version); Assert.True(after.Group.Capabilities.CanRead); Assert.True(after.Channel.Capabilities.CanSend);
        using var allowed = await teacherClient.GetAsync($"/api/v1/conversations/{after.Group.ConversationId}/messages"); allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OfficialSpacesMigrationRefusesDestructiveDownAfterProvisioningWasUsed()
    {
        await using var scope = factory.Services.CreateAsyncScope(); var root = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(root.Database.GetConnectionString()) {
            InitialCatalog = "MakanApp_OfficialMigration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            await db.Database.MigrateAsync();
            var org = MakanApp.Domain.Organization.Organization.Create("isolated official rollback guard", DateTime.UtcNow);
            db.Organizations.Add(org); db.OfficialSpaceProvisions.Add(OfficialSpaceProvision.Request(org.Id, null, ConversationType.Group, DateTime.UtcNow));
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.GetService<IMigrator>().MigrateAsync("20261030060025_AddExamAnswerAttachments"));
            Assert.Equal(51131, error.Number);
            Assert.Contains("20261030093927_AddOfficialOrganizationAndClassSpaces", await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(1, await db.OfficialSpaceProvisions.CountAsync());
        }
        finally
        {
            // حذف فقط دیتابیس تصادفیِ تازه‌ساخته‌شدهٔ همین تست مجاز است.
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task OfficialSpacesPrivateSubmissionAttachmentAndExamAutosaveNeverCreatePublicActivity()
    {
        using var s = await CreateOfficialScenarioAsync();
        var assignmentEvent = await PublishOfficialSourceAsync(s, AcademicActivityKind.Assignment, "private assignment body");
        var examEvent = await PublishOfficialSourceAsync(s, AcademicActivityKind.Exam, "private exam question");
        Guid assignmentId; Guid examId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            assignmentId = await (from p in db.AcademicActivityPublications join v in db.AssignmentVersions on p.AssignmentVersionId equals v.Id where p.Id == assignmentEvent select v.AssignmentId).SingleAsync();
            examId = await (from p in db.AcademicActivityPublications join v in db.ExamVersions on p.ExamVersionId equals v.Id where p.Id == examEvent select v.ExamId).SingleAsync();
        }
        using var created = await s.Student.PostAsync($"/api/v1/academic/assignments/{assignmentId}/attempts", null); created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<SubmissionAttemptResult>(JsonOptions))!;
        using var saved = await s.Student.PatchAsJsonAsync($"/api/v1/academic/submission-attempts/{draft.Id}/draft",
            new SaveSubmissionDraftCommand("PRIVATE_STUDENT_SUBMISSION", draft.RowVersion), JsonOptions); saved.EnsureSuccessStatusCode();
        draft = (await saved.Content.ReadFromJsonAsync<SubmissionAttemptResult>(JsonOptions))!;
        using var multipart = new MultipartFormDataContent(); using var bytes = new ByteArrayContent(StabilizationMediaFixtures.Bytes("image/png"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png"); multipart.Add(bytes, "File", "private-student-answer.png");
        using var uploaded = await s.Student.PostAsync("/api/v1/files", multipart); uploaded.EnsureSuccessStatusCode();
        var file = (await uploaded.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
        using var attached = await s.Student.PostAsJsonAsync($"/api/v1/academic/submission-attempts/{draft.Id}/attachments",
            new AttachSubmissionFileCommand(file.Id, draft.RowVersion), JsonOptions); attached.EnsureSuccessStatusCode();
        using var started = await s.Student.PostAsJsonAsync($"/api/v1/academic/exams/{examId}/attempts/start", new StartExamCommand(Guid.NewGuid()), JsonOptions); started.EnsureSuccessStatusCode();
        var attempt = (await started.Content.ReadFromJsonAsync<StudentExamAttemptDto>(JsonOptions))!;
        using var acquired = await s.Student.PostAsync($"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease", null); acquired.EnsureSuccessStatusCode();
        var lease = (await acquired.Content.ReadFromJsonAsync<ExamWriteLeaseDto>(JsonOptions))!;
        var question = Assert.Single(attempt.Questions);
        using var answer = await s.Student.PutAsJsonAsync($"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/answers/{question.AttemptQuestionId}",
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, question.Options.First().Id, null), JsonOptions); answer.EnsureSuccessStatusCode();
        await using var check = factory.Services.CreateAsyncScope(); var database = check.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await database.AcademicActivityPublications.CountAsync(p => p.ClassId == s.ClassId));
        Assert.False(await database.Messages.AnyAsync(m => m.Text == "PRIVATE_STUDENT_SUBMISSION"));
        Assert.Equal(4, await database.AcademicActivityDeliveries.CountAsync(d => database.AcademicActivityPublications.Any(p => p.Id == d.PublicationId && p.ClassId == s.ClassId)));
    }

    [Fact]
    public async Task OfficialSpacesOldPublicationCannotRestoreNewVersionAndSystemCardsCannotBeMutatedByUserApi()
    {
        using var s = await CreateOfficialScenarioAsync();
        var oldId = await PublishOfficialSourceAsync(s, AcademicActivityKind.Exam, "private question marker");
        Guid examId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            examId = await (from p in db.AcademicActivityPublications join v in db.ExamVersions on p.ExamVersionId equals v.Id where p.Id == oldId select v.ExamId).SingleAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<EfMessagingStore>().DeliverAcademicActivityAsync(oldId, ConversationType.Group, default));
        }
        var editor = (await s.Teacher.GetFromJsonAsync<ExamEditorDto>($"/api/v1/academic/exams/{examId}/editor", JsonOptions))!;
        var next = await OfficialPostAsync(s.Teacher, $"/api/v1/academic/exams/{examId}/versions", new CreateNextExamVersionCommand(editor.ExamRowVersion, editor.VersionRowVersion));
        await OfficialPostAsync(s.Teacher, $"/api/v1/academic/exams/{examId}/publish", new {
            expectedExamRowVersion = next["examRowVersion"]!.GetValue<string>(), expectedVersionRowVersion = next["versionRowVersion"]!.GetValue<string>() });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var newest = await db.AcademicActivityPublications.Where(p => p.ClassId == s.ClassId && p.SourceRevision == 2).Select(p => p.Id).SingleAsync();
            var handler = scope.ServiceProvider.GetRequiredService<IOfficialSpacesWorkHandler>();
            await handler.HandleAsync(newest, OfficialSpaceEventWriter.PublicationType, "{}", default);
            await handler.HandleAsync(oldId, OfficialSpaceEventWriter.PublicationType, "{}", default);
            Assert.Equal(AcademicActivityDeliveryState.Superseded, (await db.AcademicActivityDeliveries.SingleAsync(d => d.PublicationId == oldId && d.Purpose == ConversationType.Channel)).State);
            Assert.Equal(3, await db.Messages.CountAsync(m => m.AcademicActivityPublicationId == oldId || m.AcademicActivityPublicationId == newest));
        }
        var pair = (await s.Teacher.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        var page = (await s.Teacher.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{pair.Group.ConversationId}/messages", JsonOptions))!;
        var old = Assert.Single(page.Messages, m => m.AcademicActivity?.EventId == oldId);
        Assert.False(old.AcademicActivity!.IsActionable); Assert.Equal("Superseded", old.AcademicActivity.State);
        var path = $"/api/v1/conversations/{old.ConversationId}/messages/{old.MessageId}";
        using var edit = await s.Teacher.PatchAsJsonAsync(path, new EditMessageCommand("cannot forge system activity", old.Version), JsonOptions);
        Assert.False(edit.IsSuccessStatusCode); Assert.Equal(MessagingErrorCodes.MessageNotEditable, await ReadProblemCodeAsync(edit));
        using var delete = await s.Teacher.DeleteAsync(path + "?expectedVersion=" + Uri.EscapeDataString(old.Version));
        Assert.False(delete.IsSuccessStatusCode); Assert.Equal(MessagingErrorCodes.MessageNotEditable, await ReadProblemCodeAsync(delete));
    }

    [Fact]
    public async Task OfficialSpacesClassCommitRecordsIntentAndReadWaitsForExistingOutboxWorker()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        Guid classId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var basis = await db.Classes.AsNoTracking().SingleAsync(c => c.Id == s.ClassId);
            var store = scope.ServiceProvider.GetRequiredService<IAcademicStore>();
            await using var tx = await store.BeginSerializableTransactionAsync(default);
            var classroom = MakanApp.Domain.Academic.Class.CreateDraft(s.OrganizationId, basis.AcademicPeriodId, basis.CourseId, "کلاس با آماده‌سازی پس از Commit", 10, DateTime.UtcNow);
            classroom.Activate(DateTime.UtcNow); classId = classroom.Id; store.Add(classroom);
            await store.SaveChangesAsync(default); await tx.CommitAsync(default);
            Assert.Equal(2, await db.OfficialSpaceProvisions.CountAsync(p => p.ClassId == classId && p.State == OfficialSpaceState.Pending));
            Assert.False(await db.Conversations.AnyAsync(c => c.AcademicClassId == classId));
        }
        var path = $"/api/v1/organization/classes/{classId}/official-spaces";
        var pending = (await s.Manager.GetFromJsonAsync<OfficialSpacesPairResult>(path, JsonOptions))!;
        Assert.Equal("Pending", pending.Group.State); Assert.Null(pending.Group.ConversationId);
        Assert.Equal("Pending", pending.Channel.State); Assert.Null(pending.Channel.ConversationId);
        await FlushReplayOutboxAsync();
        var ready = (await s.Manager.GetFromJsonAsync<OfficialSpacesPairResult>(path, JsonOptions))!;
        Assert.Equal("Ready", ready.Group.State); Assert.Equal("Ready", ready.Channel.State);
        await FlushReplayOutboxAsync();
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal(2, await check.ServiceProvider.GetRequiredService<MakanDbContext>().Conversations.CountAsync(c => c.AcademicClassId == classId));
        using var denied = await s.Teacher.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task OfficialSpacesOrganizationIntentRollsBackWithSourceAndCommittedPairUsesExistingWorker()
    {
        using var client = factory.CreateClient(); var actor = await CreateMessagingUserAsync(client);
        Guid abandoned;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var store = scope.ServiceProvider.GetRequiredService<IOrganizationStore>();
            await using var tx = await store.BeginSerializableTransactionAsync(default);
            var org = MakanApp.Domain.Organization.Organization.Create(NewOrganizationName(), DateTime.UtcNow); abandoned = org.Id;
            db.Organizations.Add(org); await store.SaveChangesAsync(default);
            Assert.Equal(2, await db.OfficialSpaceProvisions.CountAsync(p => p.OrganizationId == abandoned));
            // تراکنش بدون Commit بسته می‌شود؛ منبع و intent باید با هم rollback شوند.
        }
        Guid organizationId; Guid membershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.Organizations.AnyAsync(o => o.Id == abandoned));
            Assert.False(await db.OfficialSpaceProvisions.AnyAsync(p => p.OrganizationId == abandoned));
            var store = scope.ServiceProvider.GetRequiredService<IOrganizationStore>();
            await using var tx = await store.BeginSerializableTransactionAsync(default);
            var org = MakanApp.Domain.Organization.Organization.Create(NewOrganizationName(), DateTime.UtcNow); organizationId = org.Id;
            var member = Membership.CreateActive(actor.UserId, organizationId, DateTime.UtcNow); membershipId = member.Id;
            db.Organizations.Add(org); store.Add(member); store.Add(RoleAssignment.CreateActive(member.Id, OrganizationRole.Manager, DateTime.UtcNow));
            await store.SaveChangesAsync(default); await tx.CommitAsync(default);
            Assert.False(await db.Conversations.AnyAsync(c => c.OrganizationId == organizationId));
        }
        await SelectOrganizationAsync(client, membershipId, OrganizationRole.Manager);
        var pending = (await client.GetFromJsonAsync<OfficialSpacesPairResult>("/api/v1/organization/official-spaces", JsonOptions))!;
        Assert.Equal("Pending", pending.Group.State); Assert.Equal("Pending", pending.Channel.State);
        await FlushReplayOutboxAsync();
        var ready = (await client.GetFromJsonAsync<OfficialSpacesPairResult>("/api/v1/organization/official-spaces", JsonOptions))!;
        Assert.Equal("Ready", ready.Group.State); Assert.Equal("Ready", ready.Channel.State);
        Assert.NotEqual(ready.Group.ConversationId, ready.Channel.ConversationId);
        Assert.False(ready.Group.Capabilities.CanManage);
    }

    [Fact]
    public async Task OfficialSpacesGuardianGetsNoBroadAccessAndRemovingOneReasonPreservesAnotherCurrentGrant()
    {
        using var s = await CreateOfficialScenarioAsync(); using var parentClient = factory.CreateClient();
        var parent = await CreateMessagingUserAsync(parentClient);
        var membership = await factory.CreateMembershipAsync(parent.UserId, s.OrganizationId, OrganizationRole.Parent);
        Guid personId;
        await using (var scope = factory.Services.CreateAsyncScope())
            personId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Enrollments.Where(e => e.Id == s.EnrollmentId).Select(e => e.LearnerOrganizationPersonId).SingleAsync();
        await factory.CreateGuardianRelationAsync(parent.UserId, s.OrganizationId, personId);
        await SelectOrganizationAsync(parentClient, membership.MembershipId, OrganizationRole.Parent);
        var pair = (await s.Manager.GetFromJsonAsync<OfficialSpacesPairResult>($"/api/v1/organization/classes/{s.ClassId}/official-spaces", JsonOptions))!;
        foreach (var id in new[] { pair.Group.ConversationId, pair.Channel.ConversationId })
        {
            using var read = await parentClient.GetAsync($"/api/v1/conversations/{id}/messages"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var teacherMembership = await db.Memberships.SingleAsync(m => m.UserId == s.TeacherId && m.OrganizationId == s.OrganizationId);
            db.RoleAssignments.Add(RoleAssignment.CreateActive(teacherMembership.Id, OrganizationRole.Manager, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        await factory.EndTeacherAssignmentAsync(s.TeacherAssignmentId);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().SynchronizeExistingAsync(s.OrganizationId, s.ClassId, default);
        using var stillAllowed = await s.Teacher.GetAsync($"/api/v1/conversations/{pair.Channel.ConversationId}/messages"); stillAllowed.EnsureSuccessStatusCode();
        using var legacy = await s.Manager.PostAsync($"/api/v1/organization/classes/{s.ClassId}/conversation", null); legacy.EnsureSuccessStatusCode();
        Assert.Equal(pair.Group.ConversationId, (await legacy.Content.ReadFromJsonAsync<OfficialClassConversationResult>(JsonOptions))!.ConversationId);
    }

    [Fact]
    public async Task OfficialSpacesSqlUniquenessAndPublicCreationCannotForgeOfficialBinding()
    {
        using var s = await CreateOfficialScenarioAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var duplicate = Conversation.CreateSystemManagedAcademic(ConversationType.Group, s.OrganizationId, "duplicate official source", null, DateTime.UtcNow);
            duplicate.BindAcademicClass(s.ClassId); db.Conversations.Add(duplicate);
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 });
        }
        var fake = await OfficialPostAsync(s.Teacher, "/api/v1/conversations/groups", new {
            clientOperationId = Guid.NewGuid(), scope = "Personal", title = "Official Class Group", initialParticipantUserIds = Array.Empty<Guid>(),
            managementPolicy = "SystemManagedAcademic", academicClassId = s.ClassId, isOfficialOrganizationSpace = true });
        var id = Guid.Parse(fake["conversationId"]!.GetValue<string>());
        var summary = (await s.Teacher.GetFromJsonAsync<ConversationSummaryResult>($"/api/v1/conversations/{id}/summary", JsonOptions))!;
        Assert.False(summary.IsOfficial); Assert.Null(summary.Official); Assert.Null(summary.OrganizationId);
    }
}
