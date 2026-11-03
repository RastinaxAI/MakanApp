using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Application.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService : IAssignmentService
{
    private readonly IAcademicContextResolver _accessContextResolver;
    private readonly IAssignmentStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly IOutbox _outbox;
    private readonly IAcademicActivityPublisher? _activities;
    private readonly IFileAssetStore _files;
    private readonly StorageOptions _storageOptions;

    public AssignmentService(
        IAcademicContextResolver accessContextResolver,
        IAssignmentStore store,
        TimeProvider timeProvider,
        IOutbox outbox,
        IFileAssetStore files,
        StorageOptions storageOptions,
        IAcademicActivityPublisher? activities = null)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _timeProvider = timeProvider;
        _outbox = outbox;
        _activities = activities;
        _files = files;
        _storageOptions = storageOptions;
    }

    public async Task<AssignmentResult> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateAssignmentDraftCommand command,
        CancellationToken cancellationToken, AcademicClassKind classKind = AcademicClassKind.OrganizationClass)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId,
            classKind == AcademicClassKind.PersonalClass ? AcademicResourceKind.PersonalClass : AcademicResourceKind.OrganizationClass,
            classId, AcademicOperation.AuthorAssignment, false, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var academicClass = await GetActiveClassForUpdateAsync(
            context.Scope,
            classId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, classId, cancellationToken);

        var nowUtc = UtcNow();
        var assignment = Assignment.CreateDraft(
            context.Scope,
            classId,
            context.ActorMembershipId,
            nowUtc);
        var version = CreateVersion(assignment, context.ActorMembershipId, command, nowUtc);
        await SetAttachmentsAsync(context, version, command.AttachmentFileIds ?? [], cancellationToken);

        _store.Add(assignment);
        _store.Add(version);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(assignment, version, academicClass.Title);
    }

    public async Task<AssignmentResult> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        UpdateAssignmentDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Assignment, assignmentId, AcademicOperation.AuthorAssignment, false, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetAssignmentForUpdateAsync(
            context.Scope,
            assignmentId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Assignment.ClassId, cancellationToken);
        EnsureClassActive(record);
        EnsureDraft(record);
        _store.SetOriginalRowVersion(
            record.Assignment,
            DecodeRowVersion(command.ExpectedAssignmentRowVersion));
        _store.SetOriginalRowVersion(
            record.Version,
            DecodeRowVersion(command.ExpectedVersionRowVersion));

        var nowUtc = UtcNow();
        try
        {
            record.Version.UpdateDraft(
                command.Title,
                command.Description,
                command.DueAtUtc,
                command.AllowLateSubmission,
                command.MaxAttempts,
                command.MaxScore,
                nowUtc);
            record.Assignment.MarkDraftUpdated(nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        if (command.AttachmentFileIds is not null)
            await SetAttachmentsAsync(context, record.Version, command.AttachmentFileIds, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record.Assignment, record.Version, record.ClassTitle);
    }

    public async Task<PublishAssignmentResult> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        PublishAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Assignment, assignmentId, AcademicOperation.AuthorAssignment, false, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetAssignmentForUpdateAsync(
            context.Scope,
            assignmentId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Assignment.ClassId, cancellationToken);
        EnsureClassActive(record);
        if (context.IsPersonal && record.Version.IsPublished)
        {
            var replay = new PublishAssignmentResult(ToResult(record.Assignment, record.Version, record.ClassTitle),
                await _store.CountRecipientsAsync(record.Version.Id, cancellationToken))
            { PublicationReceipt = _activities is null ? null : await _activities.ReadReceiptAsync(record.Version.Scope,
                record.Version.ClassId, AcademicActivityKind.Assignment, record.Version.Id, record.Version.VersionNumber, cancellationToken) };
            await transaction.CommitAsync(cancellationToken); return replay;
        }
        EnsureDraft(record);
        await ValidateAttachmentsAsync(context, record.Version, record.Version.Attachments.Select(a => a.FileAssetId).ToArray(), cancellationToken);
        _store.SetOriginalRowVersion(
            record.Assignment,
            DecodeRowVersion(command.ExpectedAssignmentRowVersion));
        _store.SetOriginalRowVersion(
            record.Version,
            DecodeRowVersion(command.ExpectedVersionRowVersion));

        var nowUtc = UtcNow();
        var participantIds = await _store.GetActiveParticipantIdsForUpdateAsync(
            record.Assignment.Scope,
            record.Assignment.ClassId,
            cancellationToken);
        var recipientUserIds = await _store.GetActiveLearnerUserIdsForUpdateAsync(
            record.Assignment.Scope,
            record.Assignment.ClassId,
            cancellationToken);
        var recipients = participantIds
            .Distinct()
            .Select(participantId => AssignmentRecipient.Create(
                record.Assignment.Scope,
                record.Assignment.ClassId,
                record.Assignment.Id,
                record.Version.Id,
                new AcademicParticipantRef(record.Assignment.ContextKind, participantId),
                nowUtc))
            .ToArray();

        try
        {
            record.Version.Publish(nowUtc);
            record.Assignment.Publish(nowUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentDueDateInvalid,
                exception.Message);
        }

        _store.AddRange(recipients);
        if (_activities is not null)
            await _activities.RecordAsync(AcademicActivityPublication.Create(AcademicActivityKind.Assignment,
                record.Version.Scope, record.Version.ClassId, record.Version.Id, record.Version.VersionNumber,
                userId, record.Version.Title, nowUtc, dueAtUtc: DateTime.SpecifyKind(record.Version.DueAtUtc, DateTimeKind.Utc)), cancellationToken);
        _outbox.EnqueueRange(NotificationOutboxContract.Create(new NotificationIntent(
            Guid.NewGuid(),
            record.Assignment.OrganizationId,
            NotificationCategory.Educational,
            NotificationType.AssignmentPublished,
            NotificationResourceType.Assignment,
            record.Assignment.Id,
            nowUtc,
            recipientUserIds.Select(recipientUserId => new NotificationRecipientIntent(
                recipientUserId,
                "تکلیف جدید منتشر شد",
                "یک تکلیف جدید برای شما منتشر شده است.")).ToArray())));
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PublishAssignmentResult(
            ToResult(record.Assignment, record.Version, record.ClassTitle),
            recipients.Length) { PublicationReceipt = _activities is null ? null : await _activities.ReadReceiptAsync(
                record.Version.Scope, record.Version.ClassId, AcademicActivityKind.Assignment,
                record.Version.Id, record.Version.VersionNumber, cancellationToken) };
    }

    private AssignmentVersion CreateVersion(
        Assignment assignment,
        Guid membershipId,
        CreateAssignmentDraftCommand command,
        DateTime nowUtc)
    {
        try
        {
            return AssignmentVersion.CreateDraft(
                assignment.Scope,
                assignment.ClassId,
                assignment.Id,
                assignment.CurrentVersionNumber,
                command.Title,
                command.Description,
                command.DueAtUtc,
                command.AllowLateSubmission,
                command.MaxAttempts,
                command.MaxScore,
                membershipId,
                nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }
    }

    private async Task SetAttachmentsAsync(AcademicAccessContext context, AssignmentVersion version, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count > 20 || ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new AssessmentException(AssessmentErrorCodes.SubmissionFileNotAllowed, "فهرست پیوست تکلیف معتبر نیست.");
        await ValidateAttachmentsAsync(context, version, ids, ct);
        version.ReplaceAttachments(ids, UtcNow());
    }

    private async Task ValidateAttachmentsAsync(AcademicAccessContext context, AssignmentVersion version, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        foreach (var id in ids.Order())
        {
            var file = await _files.GetForUpdateAsync(id, ct);
            var retained = version.Attachments.Any(a => a.FileAssetId == id);
            if (file is null || file.Status != FileAssetStatus.Ready || file.OrganizationId != context.OrganizationId ||
                file.PersonalClassId != context.Scope.PersonalClassId || !retained && (file.UploadedByUserId != context.UserId ||
                    context.IsPersonal && file.PersonalClassMembershipId != context.PersonalMembershipId) ||
                file.ContentType is not ("image/jpeg" or "image/png" or "application/pdf") ||
                !_storageOptions.AllowedContentTypes.Contains(file.ContentType) || file.SizeBytes > _storageOptions.MaxFileSizeBytes ||
                !retained && file.RetainedAtUtc is null && file.UnattachedExpiresAtUtc <= UtcNow() ||
                !await _store.IsFileAvailableForAssignmentAsync(id, version.AssignmentId, ct))
                throw new AssessmentException(AssessmentErrorCodes.SubmissionFileNotAllowed, "پیوست تکلیف قابل استفاده نیست.");
            file.Retain(UtcNow());
        }
    }

    private static void EnsureDraft(AssignmentWithVersionRecord record)
    {
        if (record.Assignment.Status == AssignmentStatus.Published || record.Version.IsPublished)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentAlreadyPublished,
                "تکلیف قبلاً منتشر شده و نسخه منتشرشده تغییرپذیر نیست.");
        }

        if (!record.Assignment.IsDraft)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentNotDraft,
                "تکلیف در وضعیت پیش‌نویس نیست.");
        }
    }

    private static void EnsureClassActive(AssignmentWithVersionRecord record)
    {
        if (!record.ClassIsActive)
        {
            throw new AssessmentException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای تغییر یا انتشار تکلیف فعال نیست.");
        }
    }
}
