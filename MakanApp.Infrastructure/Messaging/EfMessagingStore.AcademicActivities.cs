using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    // ورودی فقط رویداد داخلیِ commitشده است؛ SendMessageCommand عمومی این actor/منشأ را دریافت نمی‌کند.
    public async Task<bool> DeliverAcademicActivityAsync(Guid publicationId, ConversationType purpose, CancellationToken ct)
    {
        await using var tx = await BeginManagementTransactionAsync(ct);
        var publication = await dbContext.AcademicActivityPublications.AsNoTracking().SingleAsync(p => p.Id == publicationId, ct);
        _ = await dbContext.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={publication.OrganizationId} AND [Id]={publication.ClassId}").SingleAsync(ct);
        var delivery = await dbContext.AcademicActivityDeliveries.FromSqlInterpolated($"SELECT * FROM [messaging].[AcademicActivityDeliveries] WITH (UPDLOCK,HOLDLOCK) WHERE [PublicationId]={publicationId} AND [Purpose]={(int)purpose}").SingleAsync(ct);
        if (delivery.State != AcademicActivityDeliveryState.Pending)
        { await tx.CommitAsync(ct); dbContext.ChangeTracker.Clear(); return true; }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        delivery.Attempt(now);
        var source = await AcademicActivityProjection.ResolveAsync(dbContext, publication, ct);
        if (!source.Current)
        { delivery.Supersede(); await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); dbContext.ChangeTracker.Clear(); return true; }
        var destination = await dbContext.Conversations.Where(c => c.OrganizationId == publication.OrganizationId && c.AcademicClassId == publication.ClassId &&
            c.Type == purpose && c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && c.Status == ConversationStatus.Active).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);
        if (destination is null)
        { delivery.WaitForSpace(); await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); dbContext.ChangeTracker.Clear(); return false; }
        var conversation = await LockConversationAsync(destination.Value, ct) ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants.Where(p => p.ConversationId == conversation.Id && p.UserId == publication.InitiatedByUserId)
            .OrderByDescending(p => p.JoinedAtUtc).FirstOrDefaultAsync(ct);
        if (participant is null)
        {
            // رکورد صرفاً برای FK نویسندگی است؛ ناشرِ لغوشده هیچ grant فعالی دریافت نمی‌کند.
            participant = ConversationParticipant.CreateActive(conversation.Id, publication.InitiatedByUserId, now);
            participant.EndSystemAccess(now); dbContext.ConversationParticipants.Add(participant);
        }
        var message = Message.CreateAcademicActivity(conversation.Id, participant.Id, publication.InitiatedByUserId,
            publication.Id, conversation.AllocateNextMessageSequence(), publication.Title, now);
        dbContext.Messages.Add(message); delivery.Deliver(conversation.Id, message.Id);
        await dbContext.SaveChangesAsync(ct);
        await AppendChangeAsync(conversation, MessagingChangeType.MessageCreated, message.Id, VersionOf(message.RowVersion), now, null, null, ct);
        await tx.CommitAsync(ct); dbContext.ChangeTracker.Clear(); outboxWakeSignal.Signal();
        // اعلان منبع موجود کافی است؛ دو کارت، دو Push جدید برای یک User تولید نمی‌کنند.
        return true;
    }
}
