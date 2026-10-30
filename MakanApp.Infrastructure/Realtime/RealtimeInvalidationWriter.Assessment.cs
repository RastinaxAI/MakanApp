using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Realtime;

public sealed partial class RealtimeInvalidationWriter
{
    public async Task CaptureAssessmentAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var pending = new HashSet<InvalidationEnvelope>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            switch (entry.Entity)
            {
                case AssignmentVersion version:
                    pending.Add(new("assessment.assignment", version.OrganizationId, version.ClassId, version.AssignmentId, StaffOnly: !version.IsPublished)); break;
                case ExamVersion version:
                    await ExamAsync(version, !version.IsPublished); break;
                case QuestionVersion question:
                    await QuestionAsync(question.ExamVersionId); break;
                case QuestionOption option:
                    var optionQuestion = db.QuestionVersions.Local.SingleOrDefault(q => q.Id == option.QuestionVersionId)
                        ?? await db.QuestionVersions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == option.QuestionVersionId, ct);
                    if (optionQuestion is not null) await QuestionAsync(optionQuestion.ExamVersionId);
                    break;
                case SubmissionAttempt attempt:
                    await SubmissionAsync(attempt, "assessment.submission", false, attempt.IsDraft); break;
                case SubmissionAttachment attachment:
                    var attached = await SubmissionAsyncById(attachment.SubmissionAttemptId);
                    if (attached is not null) await SubmissionAsync(attached, "assessment.submission", false, attached.IsDraft);
                    break;
                case EvaluationRevision grade:
                    var submission = await SubmissionAsyncById(grade.SubmissionAttemptId);
                    if (submission is not null) await SubmissionAsync(submission, "assessment.grade", true, false);
                    break;
                case GradeRelease release:
                    var released = await SubmissionAsyncById(release.SubmissionAttemptId);
                    if (released is not null) await SubmissionAsync(released, "assessment.grade", false, false);
                    break;
                case ExamAttempt attempt:
                    await AttemptAsync(attempt, "assessment.exam-attempt", false, true); break;
                case ExamGradeRevision grade:
                    var graded = await AttemptAsyncById(grade.ExamAttemptId);
                    if (graded is not null) await AttemptAsync(graded, "assessment.exam-grade", true, false);
                    break;
                case ExamQuestionGrade questionGrade:
                    var revision = db.ExamGradeRevisions.Local.SingleOrDefault(g => g.Id == questionGrade.ExamGradeRevisionId)
                        ?? await db.ExamGradeRevisions.AsNoTracking().SingleOrDefaultAsync(g => g.Id == questionGrade.ExamGradeRevisionId, ct);
                    var reviewed = revision is null ? null : await AttemptAsyncById(revision.ExamAttemptId);
                    if (reviewed is not null) await AttemptAsync(reviewed, "assessment.exam-grade", true, false);
                    break;
                case ExamGradeRelease release:
                    var final = await AttemptAsyncById(release.ExamAttemptId);
                    if (final is not null) await AttemptAsync(final, "assessment.exam-grade", false, false);
                    break;
            }
        }
        Enqueue(pending);

        async Task QuestionAsync(Guid versionId)
        {
            var version = db.ExamVersions.Local.SingleOrDefault(v => v.Id == versionId)
                ?? await db.ExamVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, ct);
            if (version is not null) await ExamAsync(version, true);
        }
        async Task ExamAsync(ExamVersion version, bool staffOnly)
        {
            var exam = db.Exams.Local.SingleOrDefault(e => e.Id == version.ExamId)
                ?? await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == version.ExamId, ct);
            if (exam is not null) pending.Add(new("assessment.exam", version.OrganizationId, exam.ClassId, exam.Id, StaffOnly: staffOnly));
        }
        async Task SubmissionAsync(SubmissionAttempt attempt, string key, bool staffOnly, bool ownerOnly)
        {
            var assignment = db.Assignments.Local.SingleOrDefault(a => a.Id == attempt.AssignmentId)
                ?? await db.Assignments.AsNoTracking().SingleAsync(a => a.Id == attempt.AssignmentId, ct);
            var (learner, owner) = await LearnerAsync(attempt.EnrollmentId);
            if (ownerOnly && owner is null) return;
            pending.Add(new(key, attempt.OrganizationId, assignment.ClassId, attempt.Id, ownerOnly ? owner : null,
                staffOnly, LearnerOrganizationPersonId: learner));
        }
        async Task AttemptAsync(ExamAttempt attempt, string key, bool staffOnly, bool ownerOnly)
        {
            var (learner, owner) = await LearnerAsync(attempt.EnrollmentId);
            if (ownerOnly && owner is null) return;
            pending.Add(new(key, attempt.OrganizationId, attempt.ClassId, attempt.Id, ownerOnly ? owner : null,
                staffOnly, LearnerOrganizationPersonId: learner));
        }
        async Task<(Guid LearnerId, Guid? OwnerId)> LearnerAsync(Guid enrollmentId)
        {
            var enrollment = db.Enrollments.Local.SingleOrDefault(e => e.Id == enrollmentId)
                ?? await db.Enrollments.AsNoTracking().SingleAsync(e => e.Id == enrollmentId, ct);
            var owner = await (from p in db.OrganizationPersons join u in db.Users on p.PersonId equals u.PersonId
                where p.Id == enrollment.LearnerOrganizationPersonId && p.OrganizationId == enrollment.OrganizationId select (Guid?)u.Id).SingleOrDefaultAsync(ct);
            return (enrollment.LearnerOrganizationPersonId, owner);
        }
        async Task<SubmissionAttempt?> SubmissionAsyncById(Guid id) => db.SubmissionAttempts.Local.SingleOrDefault(a => a.Id == id)
            ?? await db.SubmissionAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        async Task<ExamAttempt?> AttemptAsyncById(Guid id) => db.ExamAttempts.Local.SingleOrDefault(a => a.Id == id)
            ?? await db.ExamAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
    }
}
