using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AccountDeletionAcceptanceTests
{
    private async Task<Guid> SourceAsync(Actor owner, EducationalSourceScope scope = EducationalSourceScope.Personal, Guid? org = null, Guid? classroom = null)
    {
        return await Db(async db =>
        {
            var file = FileAsset.CreatePending(Guid.NewGuid(), org, owner.Id, "synthetic.txt", "synthetic/" + Guid.NewGuid().ToString("N"), "text/plain", Now, Now.AddDays(1));
            file.MarkReady(1, new string('a', 64), Now);
            var source = EducationalSource.Create(scope, owner.Id, org, classroom, Guid.NewGuid(), new string('a', 64), Now);
            db.FileAssets.Add(file); db.EducationalSources.Add(source);
            db.EducationalSourceVersions.Add(EducationalSourceVersion.Create(source.Id, 1, file.Id, owner.Id, "محتوای ساختگی آزمون", null, EducationalSourceType.Document, Now));
            await db.SaveChangesAsync(); return source.Id;
        });
    }

    [Fact]
    public async Task DeletionPurgesOwnerAddressBookAndOnlyUnlinksVersionedOtherOwnerEntries()
    {
        var owner = await ActorAsync(); var other = await ActorAsync();
        var ownDevice = Guid.NewGuid(); var otherDevice = Guid.NewGuid(); var ownEntry = Guid.NewGuid(); var otherEntry = Guid.NewGuid();
        foreach (var pair in new[] { (owner, ownDevice), (other, otherDevice) })
        {
            using var consent = await pair.Item1.Client.PutAsJsonAsync("/api/v1/address-book/devices/consent", new SetContactConsentCommand(pair.Item2, "v1", "ContactsLimited", true), Json);
            Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        }
        var oldSync = new ContactSyncCommand(ownDevice, Guid.NewGuid(), 1, [new(ownEntry, ContactMutationKind.Upsert, null, "دفترچهٔ خصوصی حذف‌شونده", ["+989121234567"])]);
        await Post<ContactSyncResult>(owner.Client, "/api/v1/address-book/sync", oldSync);
        await Post<ContactSyncResult>(other.Client, "/api/v1/address-book/sync", new ContactSyncCommand(otherDevice, Guid.NewGuid(), 1,
            [new(otherEntry, ContactMutationKind.Upsert, null, "نام شخصیِ صاحب دفترچه", [owner.Phone], ContactNameOrigin.Manual)]));
        await Db(async db =>
        {
            var entry = await db.AddressBookEntries.SingleAsync(e => e.OwnerUserId == other.Id && e.Id == otherEntry);
            var state = await db.ContactSyncStates.SingleAsync(s => s.OwnerUserId == other.Id);
            entry.Update(entry.ProtectedContent, entry.HasManualName, owner.Id, state.NextVersion(), Now);
            db.UserContacts.Add(UserContact.Create(owner.Id, other.Id, "یادداشت حذف‌شونده", Now));
            db.UserContacts.Add(UserContact.Create(other.Id, owner.Id, "یادداشت متعلق به دیگری", Now));
            await db.SaveChangesAsync();
        });
        using var beforeResponse = await other.Client.GetAsync($"/api/v1/address-book/delta?deviceId={otherDevice}");
        var before = (await beforeResponse.Content.ReadFromJsonAsync<ContactDeltaResult>(Json))!;
        var encrypted = await Db(db => db.AddressBookEntries.Where(e => e.OwnerUserId == other.Id).Select(e => e.ProtectedContent).SingleAsync());
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        using var afterResponse = await other.Client.GetAsync($"/api/v1/address-book/delta?deviceId={otherDevice}&cursor={Uri.EscapeDataString(before.NextCursor)}");
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
        var after = (await afterResponse.Content.ReadFromJsonAsync<ContactDeltaResult>(Json))!; var changed = Assert.Single(after.Upserts);
        Assert.Null(changed.LinkedAccountUserId); Assert.False(changed.LinkCurrentlyVisible); Assert.Equal("نام شخصیِ صاحب دفترچه", changed.SavedName);
        Assert.Contains(owner.Phone, changed.Phones); Assert.True(changed.ServerVersion > Assert.Single(before.Upserts).ServerVersion);
        await Db(async db =>
        {
            Assert.False(await db.AddressBookEntries.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.False(await db.ContactPhones.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.False(await db.ContactSyncDevices.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.False(await db.ContactDeviceBindings.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.False(await db.ContactSyncOperations.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.False(await db.ContactSyncStates.AnyAsync(e => e.OwnerUserId == owner.Id));
            Assert.Equal(encrypted, await db.AddressBookEntries.Where(e => e.OwnerUserId == other.Id).Select(e => e.ProtectedContent).SingleAsync());
            Assert.Equal("یادداشت متعلق به دیگری", await db.UserContacts.Where(c => c.OwnerUserId == other.Id).Select(c => c.SavedName).SingleAsync());
        });
        using var stale = await owner.Client.PostAsJsonAsync("/api/v1/address-book/sync", oldSync, Json); Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.False(await Db(db => db.AddressBookEntries.AnyAsync(e => e.OwnerUserId == owner.Id)));
    }

    [Fact]
    public async Task DeletionKeepsSharedMessageRevisionAndPeerHistoryWithNeutralSenderIdentity()
    {
        var owner = await ActorAsync(); var peer = await ActorAsync();
        var conversation = await Db(async db =>
        {
            var c = Conversation.CreateDirect(ConversationScope.Personal, null, owner.Id, peer.Id, Now);
            var p = ConversationParticipant.CreateActive(c.Id, owner.Id, Now); db.Conversations.Add(c);
            db.ConversationParticipants.AddRange(p, ConversationParticipant.CreateActive(c.Id, peer.Id, Now));
            var message = Message.CreateText(c.Id, p.Id, owner.Id, Guid.NewGuid(), c.AllocateNextMessageSequence(), "پیام مشترک باقی می‌ماند", 4000, Now);
            db.Messages.Add(message); db.MessageRevisions.Add(MessageRevision.Create(message.Id, 1, message.Text, owner.Id, Now));
            await db.SaveChangesAsync();
            var report = AbuseReport.Create(peer.Id, owner.Id, c.Id, message.Id, Guid.NewGuid(), AbuseReportReason.Spam,
                "شاهد ساختگی", MessageKind.Text, message.Text, message.RowVersion, new byte[32], Now);
            db.AbuseReports.Add(report); db.ModerationCases.Add(ModerationCase.CreateForReport(c.Id, report.Id, ModerationQueueScope.PlatformSafety, ModerationPriority.Normal, Now));
            db.UserBlocks.Add(UserBlock.Create(peer.Id, owner.Id, Now));
            var ticket = MakanApp.Domain.Support.SupportTicket.Create(owner.Id, "تاریخچهٔ پشتیبانی", "Technical", Now);
            db.SupportTickets.Add(ticket); db.SupportTicketMessages.Add(MakanApp.Domain.Support.SupportTicketMessage.Create(ticket.Id, owner.Id, false, "پیام پشتیبانی حفظ‌شده", 1, Now));
            var folder = ChatFolder.Create(Guid.NewGuid(), owner.Id); folder.Update("پوشهٔ خصوصی", 1, FolderConversationTypes.All, FolderConversationTypes.None, false);
            db.ChatFolders.Add(folder); db.FolderConversationOverrides.Add(FolderConversationOverride.Create(folder.Id, c.Id, true, null));
            db.ConversationUserStates.Add(ConversationUserState.Create(c.Id, owner.Id, Now));
            var saved = Conversation.CreateSaved(owner.Id, Now); var self = ConversationParticipant.CreateActive(saved.Id, owner.Id, Now);
            db.Conversations.Add(saved); db.ConversationParticipants.Add(self);
            var privateMessage = Message.CreateText(saved.Id, self.Id, owner.Id, Guid.NewGuid(), saved.AllocateNextMessageSequence(), "ذخیره‌شدهٔ خصوصی", 4000, Now);
            db.Messages.Add(privateMessage); var organization = SavedMessageOrganization.Create(privateMessage.Id, owner.Id); organization.Touch();
            db.SavedMessageOrganizations.Add(organization); db.SavedMessageLabels.Add(SavedMessageLabel.Create(privateMessage.Id, "خصوصی"));
            await db.SaveChangesAsync(); return c.Id;
        });
        var original = await Db(db => db.Messages.Where(m => m.ConversationId == conversation).Select(m => m.RowVersion).SingleAsync());
        var request = await RequestAsync(owner); await OneAsync();
        using (var during = await peer.Client.GetAsync($"/api/v1/conversations/{conversation}/messages"))
        {
            Assert.Equal(HttpStatusCode.OK, during.StatusCode);
            var frozen = (await during.Content.ReadFromJsonAsync<ConversationMessagePageResult>(Json))!;
            Assert.Equal(Person.DeletedDisplayName, Assert.Single(frozen.Messages).Sender.DisplayName);
            Assert.NotEqual(owner.Username, Assert.Single(frozen.Messages).Sender.Username);
        }
        await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        using var history = await peer.Client.GetAsync($"/api/v1/conversations/{conversation}/messages"); Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var result = (await history.Content.ReadFromJsonAsync<ConversationMessagePageResult>(Json))!; var messageResult = Assert.Single(result.Messages);
        Assert.Equal("پیام مشترک باقی می‌ماند", messageResult.Text); Assert.Equal(Person.DeletedDisplayName, messageResult.Sender.DisplayName);
        Assert.NotEqual(owner.Username, messageResult.Sender.Username); Assert.Null(messageResult.Sender.ProfilePhoto);
        Assert.Equal(original, await Db(db => db.Messages.Where(m => m.ConversationId == conversation).Select(m => m.RowVersion).SingleAsync()));
        Assert.Equal(1, await Db(db => db.MessageRevisions.CountAsync())); Assert.Equal(3, await Db(db => db.ConversationParticipants.CountAsync()));
        Assert.Equal(2, await Db(db => db.Messages.CountAsync())); Assert.Equal(1, await Db(db => db.AbuseReports.CountAsync()));
        Assert.Equal(1, await Db(db => db.ModerationCases.CountAsync())); Assert.Equal(1, await Db(db => db.SupportTickets.CountAsync()));
        Assert.Equal(1, await Db(db => db.SupportTicketMessages.CountAsync()));
        Assert.Equal(0, await Db(db => db.ChatFolders.CountAsync())); Assert.Equal(0, await Db(db => db.FolderConversationOverrides.CountAsync()));
        Assert.Equal(0, await Db(db => db.ConversationUserStates.CountAsync())); Assert.Equal(0, await Db(db => db.SavedMessageLabels.CountAsync()));
        using var blocks = await peer.Client.GetAsync("/api/v1/messaging/blocks"); Assert.Equal(HttpStatusCode.OK, blocks.StatusCode);
        var block = Assert.Single((await blocks.Content.ReadFromJsonAsync<BlockedUserResult[]>(Json))!);
        Assert.Equal(Person.DeletedDisplayName, block.User.DisplayName); Assert.NotEqual(owner.Username, block.User.Username);
        Assert.Equal(1, await Db(db => db.UserBlocks.CountAsync()));
    }

    [Fact]
    public async Task DeletionEndsOrganizationGuardianAndPersonalClassAccessButRetainsAcademicRows()
    {
        var owner = await ActorAsync(); var peer = await ActorAsync(); var org = await _factory.CreateOrganizationAsync("سازمان پذیرش حذف");
        var member = await _factory.CreateMembershipAsync(owner.Id, org, OrganizationRole.Teacher);
        var person = (await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.PersonId).SingleAsync()))!.Value;
        var peerPerson = (await Db(db => db.Users.Where(u => u.Id == peer.Id).Select(u => u.PersonId).SingleAsync()))!.Value;
        var learner = await _factory.CreateOrganizationPersonAsync(org, person); var otherLearner = await _factory.CreateOrganizationPersonAsync(org, peerPerson);
        var classIds = await _factory.CreateAcademicClassAsync(org);
        var enrollment = await _factory.CreateEnrollmentAsync(org, classIds.ClassId, learner);
        var teaching = await _factory.CreateTeacherAssignmentAsync(org, classIds.ClassId, member.MembershipId);
        var history = await Db(async db =>
        {
            var scope = AcademicScope.Organization(org); var participation = new AcademicParticipantRef(AcademicClassKind.OrganizationClass, enrollment);
            var assignment = Assignment.CreateDraft(scope, classIds.ClassId, member.MembershipId, Now);
            var version = AssignmentVersion.CreateDraft(scope, classIds.ClassId, assignment.Id, 1, "تکلیف تاریخی", "شرح ساختگی", Now.AddDays(1), true, 1, 20, member.MembershipId, Now);
            version.Publish(Now); assignment.Publish(Now);
            var recipient = AssignmentRecipient.Create(scope, classIds.ClassId, assignment.Id, version.Id, participation, Now);
            var submission = SubmissionAttempt.CreateDraft(scope, assignment.Id, version.Id, recipient.Id, participation, 1, Now);
            submission.SaveAnswer("پاسخ تاریخی باقی می‌ماند", Now); submission.Submit(Now, Now.AddDays(1), true, false);
            db.Assignments.Add(assignment); db.AssignmentVersions.Add(version); db.AssignmentRecipients.Add(recipient); db.SubmissionAttempts.Add(submission);
            var exam = Exam.CreateDraft(scope, classIds.ClassId, member.MembershipId, Now);
            var examVersion = ExamVersion.CreateDraft(scope, exam.Id, 1, "آزمون تاریخی", null, Now.AddMinutes(-1), Now.AddHours(1), 30, 1, 20, ExamRandomizationPolicy.None, member.MembershipId, Now);
            var question = QuestionVersion.CreateDraft(scope, examVersion.Id, 1, ExamQuestionType.Descriptive, "پرسش ساختگی", 20, null, Now);
            examVersion.Publish([question], Now); exam.PublishCurrentVersion(1);
            var attempt = ExamAttempt.Start(scope, exam.Id, examVersion.Id, classIds.ClassId, participation, Guid.NewGuid(), 1, 1, Now.AddMinutes(-1), Now.AddHours(1), 30, Now);
            db.Exams.Add(exam); db.ExamVersions.Add(examVersion); db.QuestionVersions.Add(question); db.ExamAttempts.Add(attempt);
            await db.SaveChangesAsync(); return (Submission: submission.Id, ExamAttempt: attempt.Id, SubmissionVersion: submission.RowVersion.ToArray(), AttemptVersion: attempt.RowVersion.ToArray());
        });
        await _factory.CreateGuardianRelationAsync(owner.Id, org, otherLearner); await _factory.CreateGuardianRelationAsync(peer.Id, org, learner);
        var personal = await Db(async db =>
        {
            var c = PersonalClass.Create(peer.Id, "کلاس مستقل متعلق به دیگری", Guid.NewGuid(), Now); db.PersonalClasses.Add(c);
            db.PersonalClassParticipants.Add(PersonalClassParticipant.Admit(c.Id, owner.Id, PersonalClassLocalRoles.Learner, Now));
            await db.SaveChangesAsync(); return c.Id;
        });
        var before = await Db(async db => (await db.Enrollments.SingleAsync(e => e.Id == enrollment)).RowVersion.ToArray());
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            Assert.NotNull((await db.Memberships.SingleAsync(m => m.Id == member.MembershipId)).EndedAtUtc);
            Assert.All(await db.RoleAssignments.Where(r => r.MembershipId == member.MembershipId).ToArrayAsync(), r => Assert.NotNull(r.EndedAtUtc));
            Assert.All(await db.GuardianRelations.ToArrayAsync(), r => Assert.NotNull(r.EndedAtUtc));
            Assert.NotNull((await db.PersonalClassParticipants.SingleAsync(p => p.PersonalClassId == personal)).EndedAtUtc);
            Assert.Equal(before, (await db.Enrollments.SingleAsync(e => e.Id == enrollment)).RowVersion);
            Assert.Null((await db.TeacherAssignments.SingleAsync(t => t.Id == teaching)).EndedAtUtc);
            Assert.Equal(Person.DeletedDisplayName, (await db.Persons.SingleAsync(p => p.Id == person)).DisplayName);
            Assert.True(await db.OrganizationPersons.AnyAsync(p => p.Id == learner));
            Assert.Equal(history.SubmissionVersion, (await db.SubmissionAttempts.SingleAsync(s => s.Id == history.Submission)).RowVersion);
            Assert.Equal(history.AttemptVersion, (await db.ExamAttempts.SingleAsync(a => a.Id == history.ExamAttempt)).RowVersion);
        });
    }

    [Fact]
    public async Task DeletionLastOrganizationManagerNeedsAttentionBeforeFreezeAndCannotBeForcedComplete()
    {
        var owner = await ActorAsync(); var org = await _factory.CreateOrganizationAsync("آخرین مدیر");
        await _factory.CreateMembershipAsync(owner.Id, org, OrganizationRole.Manager);
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id));
        Assert.Null(await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.DeletionStartedAtUtc).SingleAsync()));
        Assert.Equal(AccountDeletionErrorCodes.LastOrganizationManager, await Db(db => db.AccountDeletionRequests.Where(r => r.Id == request.Id).Select(r => r.FailureCode).SingleAsync()));
        using var complete = await owner.Client.PostAsync($"/api/admin/v1/privacy/deletion-requests/{request.Id}/complete", null);
        Assert.False(complete.IsSuccessStatusCode); Assert.False(await Db(db => db.AccountDeletionSteps.AnyAsync(s => s.RequestId == request.Id && s.Status == AccountDeletionStepStatus.Completed)));
    }

    [Fact]
    public async Task DeletionConcurrentManagersCannotFreezeTheLastActiveManager()
    {
        var first = await ActorAsync(); var second = await ActorAsync(); var org = await _factory.CreateOrganizationAsync("رقابت مدیران");
        await _factory.CreateMembershipAsync(first.Id, org, OrganizationRole.Manager); await _factory.CreateMembershipAsync(second.Id, org, OrganizationRole.Manager);
        var a = await RequestAsync(first); var b = await RequestAsync(second); var claims = await Task.WhenAll(ClaimAsync(), ClaimAsync());
        // READPAST مجاز است موقتاً ردیف دوم را هم skip کند؛ پس از پایان transactionهای claim دوباره بررسی می‌شود.
        for (var i = 0; i < claims.Length; i++) claims[i] ??= await ClaimAsync();
        Assert.All(claims, c => Assert.NotNull(c)); Assert.NotEqual(claims[0]!.RequestId, claims[1]!.RequestId);
        await Task.WhenAll(ProcessAsync(claims[0]!), ProcessAsync(claims[1]!)); await DrainAsync();
        var states = new[] { await Status(a.Id), await Status(b.Id) }; Assert.Contains(AccountDeletionStatus.Completed, states); Assert.Contains(AccountDeletionStatus.NeedsAttention, states);
        Assert.Equal(1, await Db(db => db.Users.CountAsync(u => u.AccountStatus == UserAccountStatus.Active)));
    }

    [Fact]
    public async Task DeletionPersonalClassManagerRequiresExistingTransferOrArchive()
    {
        var owner = await ActorAsync();
        await Db(async db => { db.PersonalClasses.Add(PersonalClass.Create(owner.Id, "مدیر مسئول", Guid.NewGuid(), Now)); await db.SaveChangesAsync(); });
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id));
        Assert.Equal(AccountDeletionErrorCodes.LastPersonalClassManager, await Db(db => db.AccountDeletionRequests.Where(r => r.Id == request.Id).Select(r => r.FailureCode).SingleAsync()));
        Assert.Equal(UserAccountStatus.Active, await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.AccountStatus).SingleAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionPhotoUsesMetadataLifecycleAndKeepsRetainedFileBytes(bool retained)
    {
        var owner = await ActorAsync(); var id = await Db(async db =>
        {
            var file = FileAsset.CreatePending(Guid.NewGuid(), null, owner.Id, "synthetic.png", "profile/" + Guid.NewGuid().ToString("N"), "image/png", Now, Now.AddDays(1));
            file.MarkReady(1, new string('a', 64), Now); if (retained) file.Retain(Now);
            db.FileAssets.Add(file); var user = await db.Users.SingleAsync(u => u.Id == owner.Id); user.SetProfilePhoto(file.Id, Now);
            await db.SaveChangesAsync(); return file.Id;
        });
        var storageKey = await Db(db => db.FileAssets.Where(f => f.Id == id).Select(f => f.StorageKey).SingleAsync());
        await using (var storageScope = _factory.Services.CreateAsyncScope())
        {
            var storage = storageScope.ServiceProvider.GetRequiredService<MakanApp.Application.Storage.IFileStorage>();
            await using var content = new MemoryStream([1]); await storage.SaveAsync(storageKey, content, 1024, default);
        }
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        Assert.Null(await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.ProfilePhotoFileAssetId).SingleAsync()));
        Assert.Equal(retained ? FileAssetStatus.Ready : FileAssetStatus.Deleted, await Db(db => db.FileAssets.Where(f => f.Id == id).Select(f => f.Status).SingleAsync()));
        await using var finalScope = _factory.Services.CreateAsyncScope();
        Assert.True(await finalScope.ServiceProvider.GetRequiredService<MakanApp.Application.Storage.IFileStorage>().ExistsAsync(storageKey, default));
    }

    [Fact]
    public async Task DeletionRevokesEveryPersonalVersionButPreservesOrganizationAndLibrarySources()
    {
        var owner = await ActorAsync(); var org = await _factory.CreateOrganizationAsync("منابع مشترک");
        var personal = await SourceAsync(owner); var organization = await SourceAsync(owner, EducationalSourceScope.Organization, org);
        var library = await SourceAsync(owner, EducationalSourceScope.Library);
        var classroom = await _factory.CreateAcademicClassAsync(org); var classSource = await SourceAsync(owner, EducationalSourceScope.Class, org, classroom.ClassId);
        _knowledge.BeforeResponse = async () =>
        {
            Assert.Equal(UserAccountStatus.Suspended, await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.AccountStatus).SingleAsync()));
            Assert.False(await Db(db => db.UserSessions.AnyAsync(s => s.UserId == owner.Id && s.RevokedAtUtc == null)));
        };
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        Assert.Equal(new KnowledgeSourceVersion(personal, 1), Assert.Single(_knowledge.Deletes));
        await Db(async db =>
        {
            Assert.Equal(EducationalSourceStatus.Archived, (await db.EducationalSources.SingleAsync(s => s.Id == personal)).Status);
            Assert.Equal(EducationalSourceStatus.Active, (await db.EducationalSources.SingleAsync(s => s.Id == organization)).Status);
            Assert.Equal(EducationalSourceStatus.Active, (await db.EducationalSources.SingleAsync(s => s.Id == library)).Status);
            Assert.Equal(EducationalSourceStatus.Active, (await db.EducationalSources.SingleAsync(s => s.Id == classSource)).Status);
            Assert.NotNull((await db.AccountDeletionKnowledgePurges.SingleAsync()).CompletedAtUtc);
            var invalidOutcome = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [knowledge].[AccountDeletionKnowledgePurges] SET [Outcome]=NULL WHERE [RequestId]={request.Id}"));
            Assert.Equal(547, invalidOutcome.Number);
            Assert.Equal(4, await db.EducationalSourceVersions.CountAsync()); Assert.Equal(4, await db.FileAssets.CountAsync());
        });
    }
}
