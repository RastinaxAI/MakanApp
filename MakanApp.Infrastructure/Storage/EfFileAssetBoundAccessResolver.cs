using MakanApp.Application.Organization;
using MakanApp.Application.Academic;
using MakanApp.Application.Storage;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

public sealed partial class EfFileAssetBoundAccessResolver(MakanDbContext dbContext, TimeProvider clock, ISchoolReadStore schoolAccess,
    ISubmissionService submissions, ISubmissionStore submissionStore, IExamAttemptService examAttempts, IExamGradingStore grading,
    MakanApp.Application.Knowledge.IEducationalSourceStore sources, IAcademicContextResolver academicContexts,
    MakanApp.Application.Platform.IPlatformPermissionResolver platformPermissions)
    : IFileAssetBoundAccessResolver
{
    public Task<bool> IsSupportTicketBoundAsync(Guid fileAssetId, CancellationToken ct) =>
        dbContext.SupportTicketAttachments.AnyAsync(a => a.FileAssetId == fileAssetId, ct);
    public Task<bool> IsSchoolApplicationBoundAsync(Guid fileAssetId, CancellationToken ct) =>
        dbContext.SchoolApplicationDocuments.AnyAsync(d => d.FileAssetId == fileAssetId, ct);
    public Task<bool> IsEducationalSourceBoundAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.EducationalSourceVersions.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken);
    public Task<bool> IsProfilePhotoBoundAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.ProfilePhotoFileAssetId == fileAssetId, cancellationToken);
    public Task<bool> IsConversationPhotoBoundAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.Conversations.AnyAsync(conversation => conversation.PhotoFileAssetId == fileAssetId, cancellationToken);

    public Task<bool> IsSchoolLessonBoundAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.ClassLessonAttachments.AnyAsync(x => x.FileAssetId == fileAssetId, cancellationToken);

    public async Task<bool> IsBoundAsync(
        Guid fileAssetId,
        CancellationToken cancellationToken) =>
        await IsSupportTicketBoundAsync(fileAssetId, cancellationToken) ||
        await IsSchoolApplicationBoundAsync(fileAssetId, cancellationToken) ||
        await dbContext.EducationalSourceVersions.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.AssignmentVersionAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await IsProfilePhotoBoundAsync(fileAssetId, cancellationToken) ||
        await IsConversationPhotoBoundAsync(fileAssetId, cancellationToken) ||
        await IsSchoolLessonBoundAsync(fileAssetId, cancellationToken) ||
        await dbContext.SubmissionAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.AnswerRevisionAttachments.AnyAsync(item => item.FileAssetId == fileAssetId, cancellationToken) ||
        await dbContext.MessageAttachments.AnyAsync(
            attachment => attachment.FileAssetId == fileAssetId,
            cancellationToken) ||
        await dbContext.ModerationEvidenceAttachments.AnyAsync(
            evidence => evidence.FileAssetId == fileAssetId,
            cancellationToken) ||
        await dbContext.ReportExportJobs.AnyAsync(
            job => job.Id == fileAssetId || job.OutputFileAssetId == fileAssetId,
            cancellationToken);

    public async Task<bool> CanReadBoundFileAsync(
        FileAsset fileAsset,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        // مدرک پرونده پیش از fallback مالک فایل، فقط با ACL جاری پرونده خوانده می‌شود.
        if (await IsSchoolApplicationBoundAsync(fileAsset.Id, cancellationToken))
        {
            var applications = from document in dbContext.SchoolApplicationDocuments join app in dbContext.SchoolApplications on document.ApplicationId equals app.Id
                               where document.FileAssetId == fileAsset.Id select app;
            return await applications.AnyAsync(a => a.ApplicantUserId == accessContext.UserId, cancellationToken) ||
                await applications.AnyAsync(a => a.Status != MakanApp.Domain.Organization.SchoolApplicationStatus.Draft, cancellationToken) &&
                await platformPermissions.HasPermissionAsync(accessContext.UserId, MakanApp.Domain.Platform.PlatformPermissionKeys.SchoolApplicationsRead, cancellationToken);
        }
        if (await IsSupportTicketBoundAsync(fileAsset.Id, cancellationToken))
            return await dbContext.SupportTicketAttachments.AnyAsync(a => a.FileAssetId == fileAsset.Id &&
                dbContext.SupportTickets.Any(t => t.Id == a.TicketId && t.OwnerUserId == accessContext.UserId), cancellationToken) ||
                await platformPermissions.HasPermissionAsync(accessContext.UserId, MakanApp.Domain.Platform.PlatformPermissionKeys.SupportRead, cancellationToken);
        if (await IsConversationPhotoBoundAsync(fileAsset.Id, cancellationToken))
            return fileAsset.Status == FileAssetStatus.Ready && fileAsset.ContentType is ("image/jpeg" or "image/png") &&
                await AccountInboxAccessQuery.Conversations(dbContext, accessContext.UserId).AnyAsync(c => c.PhotoFileAssetId == fileAsset.Id, cancellationToken);
        if (await dbContext.EducationalSourceVersions.AnyAsync(item => item.FileAssetId == fileAsset.Id, cancellationToken))
            return await sources.CanReadFileAsync(fileAsset.Id, accessContext, cancellationToken);
        if (await CanReadProfilePhotoAsync(fileAsset, accessContext, cancellationToken)) return true;
        // استثنای مرجع شخصی فقط برای Avatar است؛ scope قبلی همهٔ مصرف‌کننده‌های دیگر حفظ می‌شود.
        if (!FileAccessPolicy.IsCurrentScope(fileAsset, accessContext)) return false;
        if (await dbContext.AssignmentVersionAttachments.AnyAsync(a => a.FileAssetId == fileAsset.Id, cancellationToken))
            return await CanReadAssignmentAttachmentAsync(fileAsset, accessContext, cancellationToken);
        if (fileAsset.PersonalClassId.HasValue) return await CanReadPersonalAcademicAnswerAsync(fileAsset, accessContext, cancellationToken);
        if (await CanReadAcademicAnswerAsync(fileAsset, accessContext, cancellationToken)) return true;
        var lessons = await (from a in dbContext.ClassLessonAttachments
                             join lesson in dbContext.ClassLessons on a.LessonId equals lesson.Id
                             where a.FileAssetId == fileAsset.Id && a.OrganizationId == accessContext.OrganizationId && lesson.OrganizationId == accessContext.OrganizationId
                             select new { lesson.ClassId, lesson.Status }).Distinct().ToArrayAsync(cancellationToken);
        foreach (var lesson in lessons)
        {
            if (await schoolAccess.CanManageClassAsync(accessContext, lesson.ClassId, cancellationToken) ||
                lesson.Status == MakanApp.Domain.Academic.LessonStatus.Published && await schoolAccess.CanStudyClassAsync(accessContext, lesson.ClassId, cancellationToken)) return true;
        }
        var readableMessages = ReadableMessages(accessContext);
        var canReadMessage = await dbContext.MessageAttachments.AnyAsync(attachment =>
            attachment.FileAssetId == fileAsset.Id && readableMessages.Any(message => message.Id == attachment.MessageId),
            cancellationToken);
        if (canReadMessage)
        {
            return true;
        }

        return await dbContext.ReportExportJobs.AnyAsync(
            job =>
                (job.Id == fileAsset.Id || job.OutputFileAssetId == fileAsset.Id) &&
                job.Status == ReportExportJobStatus.Completed &&
                job.OutputFileAssetId == fileAsset.Id &&
                job.RequestedByUserId == accessContext.UserId &&
                job.OrganizationId == accessContext.OrganizationId &&
                job.MembershipId == accessContext.MembershipId &&
                job.Audience == accessContext.ActiveRole &&
                job.SubjectOrganizationPersonId == accessContext.SubjectOrganizationPersonId &&
                (job.Audience != OrganizationRole.Teacher ||
                 job.ClassId != null &&
                 dbContext.TeacherAssignments.Any(assignment =>
                     assignment.OrganizationId == job.OrganizationId &&
                     assignment.ClassId == job.ClassId &&
                     assignment.TeacherMembershipId == job.MembershipId &&
                     assignment.Status == MakanApp.Domain.Academic.TeacherAssignmentStatus.Active &&
                     assignment.EndedAtUtc == null)),
            cancellationToken);
    }

    private async Task<bool> CanReadProfilePhotoAsync(FileAsset file, AccessContext context, CancellationToken ct)
    {
        if (file.Status != FileAssetStatus.Ready || file.OrganizationId is not null ||
            file.ContentType is not ("image/jpeg" or "image/png")) return false;
        var owner = await dbContext.Users.AsNoTracking()
            .Where(user => user.ProfilePhotoFileAssetId == file.Id && user.Id == file.UploadedByUserId)
            .Select(user => new { user.Id, user.PersonId, user.Username, user.ProfileCompletedAtUtc })
            .SingleOrDefaultAsync(ct);
        if (owner is null) return false;
        if (owner.Id == context.UserId) return true;
        if (owner.PersonId is null || owner.Username is null || owner.ProfileCompletedAtUtc is null) return false;
        if (await MessengerRelationshipQuery.DiscoverableAccounts(dbContext, context.UserId, clock.GetUtcNow().UtcDateTime)
            .AnyAsync(user => user.Id == owner.Id, ct)) return true;

        // هویتِ فرستنده در تاریخچهٔ مجاز، همان عکس فعلی را دارد؛ این مسیر کشف عمومی تازه‌ای نیست.
        if (await ReadableMessages(context).AnyAsync(message => message.SenderUserId == owner.Id, ct)) return true;
        var readableMessages = ReadableMessages(context);
        if (await (from vote in dbContext.ConversationPollVotes
                   join poll in dbContext.ConversationPolls on vote.PollId equals poll.Id
                   where vote.UserId == owner.Id && !poll.IsAnonymous && readableMessages.Any(message => message.Id == poll.Id)
                   select vote.UserId).AnyAsync(ct)) return true;
        var actors = ReadableActors(context);
        var officialMembers = OfficialSpaceAccessQuery.Conversations(dbContext);
        return await (from actor in actors
                      join member in dbContext.ConversationParticipants on actor.ConversationId equals member.ConversationId
                      join conversation in dbContext.Conversations on actor.ConversationId equals conversation.Id
                      where member.UserId == owner.Id && member.Status == ConversationParticipantStatus.Active && member.EndedAtUtc == null &&
                          (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                           officialMembers.Any(item => item.UserId == owner.Id && item.ConversationId == conversation.Id))
                      select member.Id).AnyAsync(ct);
    }

    private IQueryable<ConversationParticipant> ReadableActors(AccessContext context)
    {
        var officialMembers = OfficialSpaceAccessQuery.Conversations(dbContext);
        return from participant in dbContext.ConversationParticipants
               join conversation in dbContext.Conversations on participant.ConversationId equals conversation.Id
               where participant.UserId == context.UserId && participant.Status == ConversationParticipantStatus.Active && participant.EndedAtUtc == null &&
                   (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                    officialMembers.Any(item => item.UserId == context.UserId && item.ConversationId == conversation.Id)) &&
                   (conversation.Scope == ConversationScope.Personal && context.WorkspaceType == WorkspaceType.Personal && context.OrganizationId == null ||
                    conversation.Scope == ConversationScope.Organization && context.WorkspaceType == WorkspaceType.Organization &&
                    conversation.OrganizationId == context.OrganizationId && context.MembershipId != null &&
                    dbContext.Memberships.Any(membership => membership.Id == context.MembershipId && membership.UserId == context.UserId &&
                        membership.OrganizationId == conversation.OrganizationId && membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null))
               select participant;
    }

    private IQueryable<Message> ReadableMessages(AccessContext context) =>
        from participant in ReadableActors(context)
        join message in dbContext.Messages on participant.ConversationId equals message.ConversationId
        join stateValue in dbContext.ConversationUserStates
            on new { participant.ConversationId, UserId = context.UserId } equals new { stateValue.ConversationId, stateValue.UserId } into states
        from state in states.DefaultIfEmpty()
        where message.DeletedAtUtc == null && message.Sequence >= participant.HistoryVisibleFromSequence &&
            message.Sequence > (state == null ? 0 : state.HistoryHiddenThroughSequence) &&
            !dbContext.MessageUserVisibilities.Any(hidden => hidden.MessageId == message.Id && hidden.UserId == context.UserId)
        select message;
}
