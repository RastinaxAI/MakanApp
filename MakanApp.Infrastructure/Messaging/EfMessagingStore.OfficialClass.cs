using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<OfficialClassConversationResult> ResolveClassAsync(Guid actorId, Guid classId, AccessContext access, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        if (access.WorkspaceType != WorkspaceType.Organization || access.OrganizationId is null ||
            !await HasActiveActorContextAsync(actorId, ConversationScope.Organization, access.OrganizationId, access, ct)) throw ConversationNotFound();
        var academicClass = await dbContext.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={classId} AND [OrganizationId]={access.OrganizationId}")
            .SingleOrDefaultAsync(ct) ?? throw ConversationNotFound();
        if (academicClass.Status != ClassStatus.Active) throw ConversationNotFound();
        var members = await OfficialSpaceAccessQuery.SourceReasons(dbContext, access.OrganizationId.Value, classId)
            .Select(m => m.UserId).Distinct().ToArrayAsync(ct);
        if (access.ActiveRole != OrganizationRole.Manager && !members.Contains(actorId)) throw ConversationNotFound();
        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(c => c.AcademicClassId == classId && c.OrganizationId == access.OrganizationId && c.Type == ConversationType.Group, ct);
        if (conversation is null)
        {
            conversation = Conversation.CreateSystemManagedAcademic(ConversationType.Group, access.OrganizationId.Value, academicClass.Title, null, nowUtc);
            conversation.BindAcademicClass(classId);
            dbContext.Conversations.Add(conversation);
        }
        await SynchronizeOfficialParticipantsAsync(conversation, actorId, members, nowUtc, ct);
        await SaveManagementAsync(ct);
        await transaction.CommitAsync(ct);
        return new OfficialClassConversationResult(conversation.Id, access.OrganizationId.Value, classId, members.Length);
    }

    private async Task SynchronizeOfficialParticipantsAsync(Conversation conversation, Guid actorId, Guid[] members, DateTime nowUtc, CancellationToken ct)
    {
        var participants = await dbContext.ConversationParticipants.Where(p => p.ConversationId == conversation.Id &&
            p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null).ToArrayAsync(ct);
        foreach (var old in participants.Where(p => !members.Contains(p.UserId))) old.EndSystemAccess(nowUtc);
        foreach (var memberId in members.Where(id => participants.All(p => p.UserId != id)))
            dbContext.ConversationParticipants.Add(ConversationParticipant.CreateActive(conversation.Id, memberId, nowUtc));
    }
}
