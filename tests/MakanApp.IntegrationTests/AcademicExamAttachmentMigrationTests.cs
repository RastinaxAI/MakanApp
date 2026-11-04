using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AcademicExamAttachmentMigrationTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData("20261029122246_BindReportExportsToCommercialReservations", "20261030060025_AddExamAnswerAttachments")]
    [InlineData("20261103074858_BindPersonalClassInvitationRoles", "20261103104624_ExtendAcademicEngineToPersonalClasses")]
    public async Task AcademicExamUpgradePreservesOldQuestionsAndAnswersWithoutGuessedLimits(string baseline, string migration)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
        { InitialCatalog = "MakanApp_AcademicAttachment_Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_AcademicAttachment_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(baseline);
            var now = DateTime.UtcNow;
            var org = Organization.Create("آزمون ارتقای پیوست", now);
            var user = User.Create(now); var person = Person.Create("کاربر", "آزمایشی", null, now);
            db.Organizations.Add(org); db.Users.Add(user); db.Persons.Add(person); await db.SaveChangesAsync();
            var membership = Membership.CreateActive(user.Id, org.Id, now);
            var learner = OrganizationPerson.CreateActive(org.Id, person.Id, now);
            var period = AcademicPeriod.Create(org.Id, "دوره", new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), now);
            var course = Course.Create(org.Id, "درس", now);
            db.Memberships.Add(membership); db.OrganizationPersons.Add(learner); db.AcademicPeriods.Add(period); db.Courses.Add(course);
            await db.SaveChangesAsync();
            var classroom = AcademicClass.CreateDraft(org.Id, period.Id, course.Id, "کلاس", 10, now); classroom.Activate(now);
            await LegacyAcademicMigrationFixture.InsertAsync(db, classroom);
            var enrollment = Enrollment.CreateActive(org.Id, classroom.Id, learner.Id, now);
            var exam = Exam.CreateDraft(org.Id, classroom.Id, membership.Id, now);
            var version = ExamVersion.CreateDraft(org.Id, exam.Id, 1, "آزمون قدیمی", null, now.AddMinutes(-1), now.AddHours(1), 30, 1, 10m, ExamRandomizationPolicy.None, membership.Id, now);
            db.Enrollments.Add(enrollment); await db.SaveChangesAsync();
            await LegacyAcademicMigrationFixture.InsertAsync(db, exam);
            await LegacyAcademicMigrationFixture.InsertAsync(db, version);
            var question = QuestionVersion.CreateDraft(org.Id, version.Id, 1, ExamQuestionType.Descriptive, "سؤال قدیمی", 10m, null, now);
            // نسخهٔ قبل ستون‌های policy ندارد؛ درج با ستون‌های همان نسخه انجام می‌شود.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [assessment].[QuestionVersions] ([Id],[OrganizationId],[ExamVersionId],[Order],[Type],[Prompt],[Score],[CreatedAtUtc]) VALUES ({question.Id},{org.Id},{version.Id},1,2,{question.Prompt},{question.Score},{now})");
            version.Publish([question], now); exam.PublishCurrentVersion(1);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [assessment].[ExamVersions] SET [Status]={(int)version.Status},[PublishedAtUtc]={now} WHERE [Id]={version.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [assessment].[Exams] SET [Status]={(int)exam.Status},[LatestPublishedVersionNumber]=1 WHERE [Id]={exam.Id}");
            var session = UserSession.Create(user.Id, new byte[32], now, now.AddDays(1)); db.UserSessions.Add(session);
            await db.SaveChangesAsync();
            var attempt = ExamAttempt.Start(org.Id, exam.Id, version.Id, classroom.Id, enrollment.Id, Guid.NewGuid(), 1, 1, now.AddMinutes(-1), now.AddHours(1), 30, now);
            attempt.AcquireWriteLease(session.Id, now);
            var mapping = ExamAttemptQuestion.Create(attempt, question.Id, 1);
            await LegacyAcademicMigrationFixture.InsertAsync(db, attempt);
            await LegacyAcademicMigrationFixture.InsertAsync(db, mapping);
            var revision = AnswerRevision.CreateDescriptive(attempt, mapping, question, "پاسخ قدیمی", 1, Guid.NewGuid(), new('A', 64), session.Id, null, 1, 1, now);
            await LegacyAcademicMigrationFixture.InsertAsync(db, revision);
            attempt.AcceptAnswerMutation(); mapping.AcceptAnswerRevision(revision);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [assessment].[ExamAttempts] SET [AnswerSetVersion]={attempt.AnswerSetVersion} WHERE [Id]={attempt.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [assessment].[ExamAttemptQuestions] SET [CurrentAnswerRevisionId]={revision.Id} WHERE [Id]={mapping.Id}");

            var assignment = Assignment.CreateDraft(org.Id, classroom.Id, membership.Id, now);
            var assignmentVersion = AssignmentVersion.CreateDraft(org.Id, classroom.Id, assignment.Id, 1, "تکلیف تاریخی", "متن ثابت", now.AddDays(1), false, 2, 20m, membership.Id, now);
            assignment.Publish(now); assignmentVersion.Publish(now);
            var recipient = AssignmentRecipient.Create(org.Id, classroom.Id, assignment.Id, assignmentVersion.Id, enrollment.Id, now);
            var submission = SubmissionAttempt.CreateDraft(org.Id, assignment.Id, assignmentVersion.Id, recipient.Id, enrollment.Id, 1, now);
            await LegacyAcademicMigrationFixture.InsertAsync(db, assignment);
            await LegacyAcademicMigrationFixture.InsertAsync(db, assignmentVersion);
            await LegacyAcademicMigrationFixture.InsertAsync(db, recipient);
            await LegacyAcademicMigrationFixture.InsertAsync(db, submission);

            var sql = migrator.GenerateScript(baseline, migration);
            if (migration.EndsWith("AddExamAnswerAttachments", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
            }
            Assert.DoesNotContain("DELETE FROM ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DEFAULT 3", sql, StringComparison.OrdinalIgnoreCase);
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var migratedClass = await db.Classes.SingleAsync();
            Assert.Null(migratedClass.ClientOperationId); Assert.Null(migratedClass.CreatedByUserId); Assert.Null(migratedClass.CreationRequestHash);
            var savedQuestion = await db.QuestionVersions.SingleAsync();
            Assert.Equal(question.Id, savedQuestion.Id); Assert.False(savedQuestion.AttachmentsAllowed);
            Assert.Null(savedQuestion.AttachmentPolicy); Assert.Null(savedQuestion.MaximumAnswerAttachmentCount);
            Assert.Null(savedQuestion.MaximumAnswerAttachmentBytes);
            var savedAnswer = await db.AnswerRevisions.Include(x => x.Attachments).SingleAsync();
            Assert.Equal(revision.Id, savedAnswer.Id); Assert.Equal(revision.TextAnswer, savedAnswer.TextAnswer);
            Assert.True(savedAnswer.HasAnswer); Assert.Empty(savedAnswer.Attachments);
            Assert.Equal(revision.Id, (await db.ExamAttemptQuestions.SingleAsync()).CurrentAnswerRevisionId);
            Assert.Empty(await db.AnswerRevisionAttachments.ToArrayAsync());
            var migratedAttempt = await db.ExamAttempts.SingleAsync();
            Assert.Equal(AcademicClassKind.OrganizationClass, migratedAttempt.ContextKind);
            Assert.Equal(org.Id, migratedAttempt.ContextId); Assert.Equal(enrollment.Id, migratedAttempt.ParticipantId);
            Assert.Null(migratedAttempt.PersonalClassId); Assert.Equal(attempt.EffectiveDeadlineUtc, migratedAttempt.EffectiveDeadlineUtc);
            var migratedSubmission = await db.SubmissionAttempts.SingleAsync();
            Assert.Equal(submission.Id, migratedSubmission.Id); Assert.Equal(enrollment.Id, migratedSubmission.ParticipantId);
            Assert.Equal(org.Id, migratedSubmission.ContextId); Assert.Null(migratedSubmission.PersonalClassMembershipId);
            Assert.Equal("متن ثابت", (await db.AssignmentVersions.SingleAsync()).Description);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
