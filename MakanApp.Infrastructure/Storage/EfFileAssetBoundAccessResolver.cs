using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

public sealed class EfFileAssetBoundAccessResolver(MakanDbContext dbContext)
    : IFileAssetBoundAccessResolver
{
    public async Task<bool> IsBoundAsync(
        Guid fileAssetId,
        CancellationToken cancellationToken) =>
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
        var canReadMessage = await (
            from attachment in dbContext.MessageAttachments
            join message in dbContext.Messages on attachment.MessageId equals message.Id
            join conversation in dbContext.Conversations on message.ConversationId equals conversation.Id
            join participant in dbContext.ConversationParticipants
                on conversation.Id equals participant.ConversationId
            where attachment.FileAssetId == fileAsset.Id &&
                  message.DeletedAtUtc == null &&
                  participant.UserId == accessContext.UserId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  message.Sequence >= participant.HistoryVisibleFromSequence &&
                  !dbContext.MessageUserVisibilities.Any(hidden =>
                      hidden.MessageId == message.Id && hidden.UserId == accessContext.UserId) &&
                  (conversation.Scope == ConversationScope.Personal &&
                   accessContext.WorkspaceType == WorkspaceType.Personal &&
                   accessContext.OrganizationId == null ||
                   conversation.Scope == ConversationScope.Organization &&
                   accessContext.WorkspaceType == WorkspaceType.Organization &&
                   conversation.OrganizationId == accessContext.OrganizationId &&
                   accessContext.MembershipId != null &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == accessContext.MembershipId &&
                       membership.UserId == accessContext.UserId &&
                       membership.OrganizationId == conversation.OrganizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            select attachment)
            .AnyAsync(cancellationToken);
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
}
