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
    [Fact]
    public async Task AcademicExamUpgradePreservesOldQuestionsAndAnswersWithoutGuessedLimits()
    {
        const string baseline = "20261029122246_BindReportExportsToCommercialReservations";
        const string migration = "20261030060025_AddExamAnswerAttachments";
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
            db.Classes.Add(classroom); await db.SaveChangesAsync();
            var enrollment = Enrollment.CreateActive(org.Id, classroom.Id, learner.Id, now);
            var exam = Exam.CreateDraft(org.Id, classroom.Id, membership.Id, now);
            var version = ExamVersion.CreateDraft(org.Id, exam.Id, 1, "آزمون قدیمی", null, now.AddMinutes(-1), now.AddHours(1), 30, 1, 10m, ExamRandomizationPolicy.None, membership.Id, now);
            db.Enrollments.Add(enrollment); db.Exams.Add(exam); db.ExamVersions.Add(version); await db.SaveChangesAsync();
            var question = QuestionVersion.CreateDraft(org.Id, version.Id, 1, ExamQuestionType.Descriptive, "سؤال قدیمی", 10m, null, now);
            // نسخهٔ قبل ستون‌های policy ندارد؛ درج با ستون‌های همان نسخه انجام می‌شود.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [assessment].[QuestionVersions] ([Id],[OrganizationId],[ExamVersionId],[Order],[Type],[Prompt],[Score],[CreatedAtUtc]) VALUES ({question.Id},{org.Id},{version.Id},1,2,{question.Prompt},{question.Score},{now})");
            version.Publish([question], now); exam.PublishCurrentVersion(1); await db.SaveChangesAsync();
            var session = UserSession.Create(user.Id, new byte[32], now, now.AddDays(1)); db.UserSessions.Add(session);
            var attempt = ExamAttempt.Start(org.Id, exam.Id, version.Id, classroom.Id, enrollment.Id, Guid.NewGuid(), 1, 1, now.AddMinutes(-1), now.AddHours(1), 30, now);
            attempt.AcquireWriteLease(session.Id, now);
            var mapping = ExamAttemptQuestion.Create(attempt, question.Id, 1);
            db.ExamAttempts.Add(attempt); db.ExamAttemptQuestions.Add(mapping); await db.SaveChangesAsync();
            var revision = AnswerRevision.CreateDescriptive(attempt, mapping, question, "پاسخ قدیمی", 1, Guid.NewGuid(), new('A', 64), session.Id, null, 1, 1, now);
            db.AnswerRevisions.Add(revision); await db.SaveChangesAsync();
            attempt.AcceptAnswerMutation(); mapping.AcceptAnswerRevision(revision); await db.SaveChangesAsync();

            var sql = migrator.GenerateScript(baseline, migration);
            Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM ", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DEFAULT 3", sql, StringComparison.OrdinalIgnoreCase);
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var savedQuestion = await db.QuestionVersions.SingleAsync();
            Assert.Equal(question.Id, savedQuestion.Id); Assert.False(savedQuestion.AttachmentsAllowed);
            Assert.Null(savedQuestion.AttachmentPolicy); Assert.Null(savedQuestion.MaximumAnswerAttachmentCount);
            Assert.Null(savedQuestion.MaximumAnswerAttachmentBytes);
            var savedAnswer = await db.AnswerRevisions.Include(x => x.Attachments).SingleAsync();
            Assert.Equal(revision.Id, savedAnswer.Id); Assert.Equal(revision.TextAnswer, savedAnswer.TextAnswer);
            Assert.True(savedAnswer.HasAnswer); Assert.Empty(savedAnswer.Attachments);
            Assert.Equal(revision.Id, (await db.ExamAttemptQuestions.SingleAsync()).CurrentAnswerRevisionId);
            Assert.Empty(await db.AnswerRevisionAttachments.ToArrayAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
