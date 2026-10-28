using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationPollVoterPageResult> GetPollVotersAsync(Guid userId, Guid conversationId, Guid messageId, int? optionNumber,
        int skip, int take, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var resource = await LoadVisiblePollAsync(userId, conversationId, messageId, context, ct);
        if (resource.Poll.IsAnonymous) throw PollNotFound();
        if (optionNumber > resource.Poll.OptionCount) throw new MessagingException(MessagingErrorCodes.PollInvalid, "گزینه معتبر نیست.");
        var votes = await dbContext.ConversationPollVotes.AsNoTracking().Where(v => v.PollId == messageId &&
            (!optionNumber.HasValue || dbContext.ConversationPollSelections.Any(s => s.PollId == messageId && s.UserId == v.UserId && s.OptionNumber == optionNumber)))
            .OrderBy(v => v.CreatedAtUtc).ThenBy(v => v.UserId).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        var page = votes.Take(take).ToArray(); var ids = page.Select(v => v.UserId).ToArray();
        var identities = await LoadSafeIdentitiesAsync(ids, ct);
        var choices = await dbContext.ConversationPollSelections.AsNoTracking().Where(s => s.PollId == messageId && ids.Contains(s.UserId))
            .OrderBy(s => s.OptionNumber).ToArrayAsync(ct);
        var result = new ConversationPollVoterPageResult(page.Select(v =>
        {
            var identity = identities.GetValueOrDefault(v.UserId) ?? throw RecipientNotAvailable();
            return new ConversationPollVoterResult(new(identity.UserId, identity.Username, identity.DisplayName, identity.ProfilePhoto),
                choices.Where(s => s.UserId == v.UserId).Select(s => s.OptionNumber).ToArray(), AdvancedUtc(v.UpdatedAtUtc));
        }).ToArray(), votes.Length > take);
        await transaction.CommitAsync(ct); return result;
    }
}
