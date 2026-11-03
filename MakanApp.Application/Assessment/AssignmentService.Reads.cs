using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService
{
    public async Task<AssignmentResult> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Assignment,
            assignmentId, AcademicOperation.Read, false, cancellationToken);
        var record = await _store.GetAssignmentAsync(
            context.Scope,
            assignmentId,
            cancellationToken);
        if (record is null || !await CanReadAsync(context, record, cancellationToken))
        {
            throw AssignmentNotFound();
        }

        return ToResult(record.Assignment, record.Version, record.ClassTitle);
    }

    public async Task<IReadOnlyCollection<AssignmentResult>> GetForClassAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken, AcademicClassKind classKind = AcademicClassKind.OrganizationClass)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId,
            classKind == AcademicClassKind.PersonalClass ? AcademicResourceKind.PersonalClass : AcademicResourceKind.OrganizationClass,
            classId, AcademicOperation.Read, false, cancellationToken);
        var organizationId = context.Scope;
        if (await _store.GetClassAsync(organizationId, classId, cancellationToken) is null)
        {
            throw AssignmentNotFound();
        }

        IReadOnlyCollection<AssignmentWithVersionRecord> records = context.IsPersonal
            ? context.Has(PersonalClassAcademicCapabilities.AuthorAssignments) || context.Has(PersonalClassAcademicCapabilities.Grade)
                ? await _store.GetForManagerAsync(context.Scope, classId, cancellationToken)
                : context.IsLocalLearner ? await _store.GetForStudentAsync(context.Scope, classId, userId, cancellationToken) : throw NotAllowed()
            : context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetForManagerAsync(
                organizationId,
                classId,
                cancellationToken),
            OrganizationRole.Teacher => await GetForTeacherAsync(
                context,
                classId,
                cancellationToken),
            OrganizationRole.Student => await _store.GetForStudentAsync(
                organizationId,
                classId,
                userId,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.GetForLearnerAsync(
                    organizationId,
                    classId,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => throw NotAllowed()
        };

        return records
            .Select(record => ToResult(record.Assignment, record.Version, record.ClassTitle))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(
        AcademicAccessContext context,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageClassAsync(context, classId, cancellationToken);
        return await _store.GetForTeacherAsync(
            context.OrganizationId!.Value,
            classId,
            context.MembershipId!.Value,
            cancellationToken);
    }

    private async Task<bool> CanReadAsync(
        AcademicAccessContext context,
        AssignmentWithVersionRecord record,
        CancellationToken cancellationToken) =>
        context.IsPersonal
            ? context.Has(PersonalClassAcademicCapabilities.AuthorAssignments) || context.Has(PersonalClassAcademicCapabilities.Grade) ||
                context.IsLocalLearner && record.Assignment.IsRecipientVisible &&
                await _store.IsRecipientForStudentAsync(context.Scope, record.Version.Id, context.UserId, cancellationToken)
            : context.ActiveRole switch
        {
            OrganizationRole.Manager => true,
            OrganizationRole.Teacher => await _store.HasActiveTeacherAssignmentAsync(
                record.Assignment.Scope,
                record.Assignment.ClassId,
                context.MembershipId!.Value,
                cancellationToken),
            OrganizationRole.Student when record.Assignment.IsRecipientVisible =>
                await _store.IsRecipientForStudentAsync(
                    record.Assignment.Scope,
                    record.Version.Id,
                    context.UserId,
                    cancellationToken),
            OrganizationRole.Parent when
                record.Assignment.IsRecipientVisible &&
                context.SubjectOrganizationPersonId.HasValue =>
                await _store.IsRecipientForLearnerAsync(
                    record.Assignment.Scope,
                    record.Version.Id,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => false
        };
}
