using MakanApp.Application.Notification;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.IntegrationTests;

// fixture سیاست delivery از گراف واقعی Domain/SQL استفاده می‌کند؛ ResourceId تصادفی مجوز منبع را ثابت نمی‌کند.
internal static class NotificationAcademicFixture
{
    internal static async Task<NotificationIntent> CreateAsync(MakanDbContext db, Guid userId, Guid? organizationId, NotificationType type, DateTime now)
    {
        var database = db.Database.GetDbConnection().Database;
        if (!database.Contains("IntegrationTests", StringComparison.Ordinal) && !database.StartsWith("MakanApp_Operations_Workers_", StringComparison.Ordinal))
            throw new InvalidOperationException("fixture اعلان فقط روی دیتابیس ایزولهٔ آزمون مجاز است.");
        var user = db.Users.Local.SingleOrDefault(u => u.Id == userId) ?? await db.Users.SingleAsync(u => u.Id == userId);
        if (user.PersonId is null)
        {
            var person = Person.Create("کاربر", "آزمایشی", null, now); db.Persons.Add(person);
            var username = "nfixture_" + Guid.NewGuid().ToString("N")[..16];
            user.SetProfile(person.Id, username, username.ToUpperInvariant(), now);
        }
        AcademicScope scope; Guid classId; Guid creator; AcademicParticipantRef participation;
        if (organizationId is Guid org)
        {
            var member = db.Memberships.Local.SingleOrDefault(m => m.OrganizationId == org && m.UserId == userId && m.IsActive) ??
                await db.Memberships.SingleOrDefaultAsync(m => m.OrganizationId == org && m.UserId == userId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null);
            if (member is null) { member = Membership.CreateActive(userId, org, now); db.Memberships.Add(member); }
            if (!db.RoleAssignments.Local.Any(r => r.MembershipId == member.Id && r.Role == OrganizationRole.Student && r.IsActive) &&
                !await db.RoleAssignments.AnyAsync(r => r.MembershipId == member.Id && r.Role == OrganizationRole.Student && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null))
                db.RoleAssignments.Add(RoleAssignment.CreateActive(member.Id, OrganizationRole.Student, now));
            var period = AcademicPeriod.Create(org, "دورهٔ آزمون اعلان", DateOnly.FromDateTime(now), DateOnly.FromDateTime(now.AddDays(30)), now);
            var course = Course.Create(org, "درس آزمون اعلان", now);
            var classroom = AcademicClass.CreateDraft(org, period.Id, course.Id, "کلاس آزمون اعلان", 10, now); classroom.Activate(now);
            var person = db.OrganizationPersons.Local.SingleOrDefault(p => p.OrganizationId == org && p.PersonId == user.PersonId) ??
                await db.OrganizationPersons.SingleOrDefaultAsync(p => p.OrganizationId == org && p.PersonId == user.PersonId);
            if (person is null) { person = OrganizationPerson.CreateActive(org, user.PersonId!.Value, now); db.OrganizationPersons.Add(person); }
            var enrollment = Enrollment.CreateActive(org, classroom.Id, person.Id, now);
            db.AddRange(period, course, classroom, enrollment);
            scope = new(AcademicClassKind.OrganizationClass, org); classId = classroom.Id; creator = member.Id;
            participation = new(AcademicClassKind.OrganizationClass, enrollment.Id);
        }
        else
        {
            var classroom = PersonalClass.Create(userId, "کلاس مستقل آزمون اعلان", Guid.NewGuid(), now);
            classroom.SetManager(userId); classroom.Configure(1, false, 0); classroom.RecordProvisioning(true); classroom.Activate(true);
            var member = PersonalClassParticipant.Admit(classroom.Id, userId, PersonalClassLocalRoles.Manager | PersonalClassLocalRoles.Learner, now);
            db.AddRange(classroom, member);
            scope = new(AcademicClassKind.PersonalClass, classroom.Id); classId = classroom.Id; creator = member.Id;
            participation = new(AcademicClassKind.PersonalClass, member.Id);
        }
        await db.SaveChangesAsync();
        Guid resource; NotificationResourceType resourceType;
        if (type is NotificationType.AssignmentPublished or NotificationType.AssignmentGradeReleased)
        {
            var assignment = Assignment.CreateDraft(scope, classId, creator, now);
            var version = AssignmentVersion.CreateDraft(scope, classId, assignment.Id, 1, "تکلیف آزمون اعلان", "متن ساختگی", now.AddDays(1), false, 1, 10, creator, now);
            version.Publish(now); assignment.Publish(now);
            var recipient = AssignmentRecipient.Create(scope, classId, assignment.Id, version.Id, participation, now);
            db.AddRange(assignment, version, recipient);
            resource = assignment.Id; resourceType = NotificationResourceType.Assignment;
            if (type == NotificationType.AssignmentGradeReleased)
            {
                var attempt = SubmissionAttempt.CreateDraft(scope, assignment.Id, version.Id, recipient.Id, participation, 1, now);
                attempt.SaveAnswer("پاسخ ساختگی آزمون", now); attempt.Submit(now, version.DueAtUtc, false, false);
                var evaluation = EvaluationRevision.CreateDraft(scope, attempt.Id, 1, 5, 10, "بازخورد ساختگی", null, "یادداشت خصوصی ساختگی", creator, now);
                evaluation.Release(now);
                db.AddRange(attempt, evaluation, GradeRelease.Create(scope, attempt.Id, evaluation.Id, creator, now));
                resource = attempt.Id; resourceType = NotificationResourceType.SubmissionAttempt;
            }
        }
        else if (type is NotificationType.ExamPublished or NotificationType.ExamGradeReleased)
        {
            var exam = Exam.CreateDraft(scope, classId, creator, now);
            var version = ExamVersion.CreateDraft(scope, exam.Id, 1, "آزمون اعلان", null, now.AddMinutes(-1), now.AddDays(1), 30, 1, 10,
                ExamRandomizationPolicy.None, creator, now);
            var question = QuestionVersion.CreateDraft(scope, version.Id, 1, ExamQuestionType.Descriptive, "پرسش ساختگی", 10, null, now);
            db.AddRange(exam, version, question); await db.SaveChangesAsync();
            version.Publish([question], now); exam.PublishCurrentVersion(1);
            resource = exam.Id; resourceType = NotificationResourceType.Exam;
            if (type == NotificationType.ExamGradeReleased)
            {
                var sessionId = await db.UserSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null).Select(s => s.Id).FirstAsync();
                var attempt = ExamAttempt.Start(scope, exam.Id, version.Id, classId, participation, Guid.NewGuid(), 1, 1,
                    version.AvailableFromUtc, version.AvailableUntilUtc, 30, now);
                var frozen = ExamAttemptQuestion.Create(attempt, question.Id, 1);
                attempt.AcquireWriteLease(sessionId, now); attempt.Finalize(sessionId, attempt.WriteLeaseVersion, 0, Guid.NewGuid(), new string('A', 64), now);
                var grade = ExamGradeRevision.CreateDraft(scope, attempt.Id, creator, now);
                var questionGrade = ExamQuestionGrade.Create(grade, attempt, frozen, question, null, null, creator, now);
                questionGrade.ReviewManually(0, null, null, creator, now);
                grade.CompleteReview([questionGrade], 10, null, null, null, now); grade.Release([questionGrade], 10, now);
                db.AddRange(attempt, frozen, grade, questionGrade, ExamGradeRelease.Create(grade, creator, Guid.NewGuid(), new string('B', 64), now));
                resource = attempt.Id; resourceType = NotificationResourceType.ExamAttempt;
            }
        }
        else throw new ArgumentOutOfRangeException(nameof(type));
        await db.SaveChangesAsync();
        return new(Guid.NewGuid(), organizationId, NotificationCategory.Educational, type, resourceType, resource, now,
            [new(userId, "اعلان آموزشی داخلی", "بدنهٔ عمومی و بدون نمره یا پاسخ خصوصی است.")]);
    }
}
