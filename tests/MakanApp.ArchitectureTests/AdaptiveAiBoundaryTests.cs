using MakanApp.Application.Ai;
using MakanApp.Domain.Intelligence;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.ArchitectureTests;

public sealed class AdaptiveAiBoundaryTests
{
    [Fact]
    public void ConversationPersistencePortDoesNotExposeEfHttpOrMessengerEntities()
    {
        var contract = typeof(IAiConversationStore);
        Assert.Equal(typeof(AiChatService).Assembly, contract.Assembly);
        var signatures = contract.GetMethods().SelectMany(x => x.GetParameters().Select(p => p.ParameterType.ToString()).Append(x.ReturnType.ToString()));
        Assert.DoesNotContain(signatures, x => x.Contains("EntityFrameworkCore") || x.Contains("HttpResponseMessage") || x.Contains("Messaging.Message"));
        Assert.True(contract.IsAssignableFrom(typeof(EfAiConversationStore)));
    }
    [Fact]
    public void StreamPortExposesApplicationEventsAndDomainConversationIsIndependent()
    {
        Assert.Equal(typeof(IAsyncEnumerable<AiChatStreamEvent>), typeof(IAiServiceClient).GetMethod("StreamChatAsync")!.ReturnType);
        Assert.Equal(typeof(AiConversationTurn).Assembly, typeof(AiConversation).Assembly);
        Assert.DoesNotContain(typeof(AiConversation).GetProperties(), x => x.PropertyType.Namespace?.StartsWith("MakanApp.Domain.Messaging") == true);
    }
}
