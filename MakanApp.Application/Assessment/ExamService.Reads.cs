using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Academic;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamService
{
    public async Task<ExamEditorDto> GetEditorAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Exam,
            examId, AcademicOperation.AuthorExam, false, cancellationToken);
        var record = await _store.GetCurrentAsync(
            context.Scope,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
        return ToEditor(record);
    }

    public async Task<ExamTeacherPreview> GetTeacherPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Exam,
            examId, AcademicOperation.AuthorExam, false, cancellationToken);
        var record = await _store.GetCurrentAsync(
            context.Scope,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
        return ToTeacherPreview(record);
    }

    public async Task<StudentSafeExamPreview> GetStudentPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Exam,
            examId, AcademicOperation.Read, false, cancellationToken);
        ExamAggregateRecord record;
        if (context.Has(PersonalClassAcademicCapabilities.AuthorExams) || context.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher)
        {
            record = await _store.GetCurrentAsync(
                context.Scope,
                examId,
                cancellationToken) ?? throw ExamNotFound();
            await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
            return ToStudentSafePreview(record);
        }

        if (!context.IsLocalLearner && context.ActiveRole != OrganizationRole.Student)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamStudentPreviewNotAllowed,
                "محتوای پیش‌نمایش آزمون فقط برای دانش‌آموز مجاز یا مدیر/معلم کلاس قابل مشاهده است.");
        }

        record = await _store.GetLatestPublishedAsync(
            context.Scope,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        if (!await _store.IsStudentActivelyEnrolledAsync(
                record.Exam.Scope,
                record.Exam.ClassId,
                userId,
                cancellationToken))
        {
            throw ExamNotFound();
        }

        var nowUtc = UtcNow();
        if (nowUtc < record.Version.AvailableFromUtc || nowUtc > record.Version.AvailableUntilUtc)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamStudentPreviewNotAllowed,
                "محتوای آزمون خارج از بازه فعال قابل مشاهده نیست.");
        }

        return ToStudentSafePreview(record);
    }

    public async Task<IReadOnlyCollection<StudentExamSummary>> GetMyExamsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken, Guid? personalClassId = null)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId,
            personalClassId.HasValue ? AcademicResourceKind.PersonalClass : AcademicResourceKind.Organization,
            personalClassId ?? Guid.Empty, AcademicOperation.Read, false, cancellationToken);
        var organizationId = context.Scope;
        IReadOnlyCollection<ExamSummaryRecord> records = context.IsPersonal
            ? context.Has(PersonalClassAcademicCapabilities.AuthorExams) || context.Has(PersonalClassAcademicCapabilities.Grade)
                ? await _store.GetForManagerAsync(context.Scope, cancellationToken)
                : context.IsLocalLearner ? await _store.GetForStudentAsync(context.Scope, userId, cancellationToken) : throw NotAllowed()
            : context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetForManagerAsync(
                organizationId,
                cancellationToken),
            OrganizationRole.Teacher => await _store.GetForTeacherAsync(
                organizationId,
                context.MembershipId!.Value,
                cancellationToken),
            OrganizationRole.Student => await _store.GetForStudentAsync(
                organizationId,
                userId,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.GetForLearnerAsync(
                    organizationId,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => throw NotAllowed()
        };

        return records.Select(ToSummary).ToArray();
    }
}
