using MakanApp.Domain.Identity;
using MakanApp.Domain.Intelligence;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AccountDeletionAcceptanceTests
{
    [Fact]
    public async Task AdaptiveAiHistoryIsPurgedByExistingAccountDeletionAndCannotPassToReusedPhone()
    {
        var owner = await ActorAsync(); var other = await ActorAsync();
        var conversation = Guid.NewGuid(); var retained = Guid.NewGuid(); var now = DateTime.UtcNow;
        await Db(async db =>
        {
            foreach (var item in new[] { (User: owner.Id, Conversation: conversation), (User: other.Id, Conversation: retained) })
            {
                var state = AiConversation.Create(item.Conversation, item.User, new byte[32], now);
                var prompt = AiConversationTurn.ReserveUser(item.User, item.Conversation, Guid.NewGuid(), state.ReservePair(now), new byte[32], now);
                var answer = prompt.Complete("PRIVATE_OWNER_PROMPT", "PRIVATE_ASSISTANT_RESPONSE", now);
                db.AiConversations.Add(state); db.AiConversationTurns.AddRange(prompt, answer);
            }
            await db.SaveChangesAsync();
        });
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            Assert.False(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.AiConversations.Where(x => x.OwnerUserId == owner.Id)));
            Assert.False(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.AiConversationTurns.Where(x => x.OwnerUserId == owner.Id)));
            Assert.True(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.AiConversations.Where(x => x.Id == retained)));
        });
        var replacement = await ActorAsync(owner.Phone); Assert.NotEqual(owner.Id, replacement.Id);
        await Db(async db => Assert.False(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.AiConversations.Where(x => x.OwnerUserId == replacement.Id))));
    }
}
