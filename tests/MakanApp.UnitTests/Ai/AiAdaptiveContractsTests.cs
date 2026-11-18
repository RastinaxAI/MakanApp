using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiAdaptiveContractsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("{\"gradeLevel\":0}")] [InlineData("{\"gradeLevel\":13}")]
    [InlineData("{\"gradeLevel\":1.5}")] [InlineData("{\"gradeLevel\":true}")]
    [InlineData("{\"gradeLevel\":\"5\"}")] [InlineData("{\"goals\":\"learn\"}")]
    [InlineData("{\"goals\":[\" \" ]}")] [InlineData("{\"goals\":[null]}")]
    [InlineData("{\"proficiency\":\"beginner\"}")] [InlineData("{\"proficiency\":0}")]
    [InlineData("{\"proficiency\":\"Expert\"}")] [InlineData("{\"schoolId\":\"forged\"}")]
    public void InvalidProfileIsRejected(string raw)
    {
        var error = Record.Exception(() => JsonSerializer.Deserialize<AiAssistantProfile>(raw, Json)!.Validate());
        Assert.True(error is JsonException or AiServiceException);
    }

    [Theory]
    [InlineData(null, null)] [InlineData(1, AiAssistantProficiency.Beginner)]
    [InlineData(12, AiAssistantProficiency.Advanced)] [InlineData(6, AiAssistantProficiency.Intermediate)]
    public void OptionalValidProfileNeedsNoOrganization(int? grade, AiAssistantProficiency? level)
    {
        var profile = new AiAssistantProfile(grade, level, null).Validate(); Assert.Empty(profile.Goals!);
        var wire = JsonSerializer.Serialize(profile, Json); Assert.Contains("\"goals\":[]", wire);
        if (level.HasValue) Assert.Contains(level.Value.ToString(), wire);
    }

    [Fact]
    public void GoalBoundsCountUnicodeCodePointsAndNotUtf16Units()
    {
        var goal = string.Concat(Enumerable.Repeat("😀", 80));
        Assert.Single(new AiAssistantProfile(Goals: [goal]).Validate().Goals!);
        Assert.Throws<AiServiceException>(() => new AiAssistantProfile(Goals: [goal + "x"]).Validate());
        Assert.Throws<AiServiceException>(() => new AiAssistantProfile(Goals: Enumerable.Repeat("goal", 9).ToArray()).Validate());
        Assert.Throws<AiServiceException>(() => new AiAssistantProfile(Goals: ["\ud800"]).Validate());
    }

    [Fact]
    public void HistoryKeepsNewestTenCompletedPairsInChronologicalOrder()
    {
        var pairs = Enumerable.Range(1, 15).Reverse().Select(x => new AiCompletedHistoryPair(x * 2 - 1, "q" + x, "a" + x));
        var selected = AiHistoryWindow.Select(pairs); Assert.Equal(20, selected.Count);
        Assert.Equal(new AiHistoryTurn("User", "q6"), selected[0]); Assert.Equal(new AiHistoryTurn("Assistant", "a15"), selected[^1]);
        Assert.All(selected, x => Assert.Contains(x.Role, new[] { "User", "Assistant" }));
    }

    [Fact]
    public void HistorySkipsOversizedPairsAndEnforcesTwelveThousandCodePoints()
    {
        var pairs = new[] { new AiCompletedHistoryPair(9, "q", new string('a', 4001)),
            new AiCompletedHistoryPair(7, new string('q', 4000), new string('a', 2000)),
            new AiCompletedHistoryPair(5, new string('q', 4000), new string('a', 2000)), new AiCompletedHistoryPair(3, "old", "old") };
        var selected = AiHistoryWindow.Select(pairs); Assert.Equal(4, selected.Count);
        Assert.Equal(12000, selected.Sum(x => AiConversationTurn.CharacterCount(x.Text)));
    }

    [Fact]
    public void FailedPromptContainsMetadataOnlyAndCompletionIsAnAtomicPair()
    {
        var now = DateTime.UtcNow; var prompt = AiConversationTurn.ReserveUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, new byte[32], now);
        Assert.Null(prompt.Text); Assert.False(prompt.IsCompleted);
        var answer = prompt.Complete("question", "answer", now);
        Assert.True(prompt.IsCompleted); Assert.True(answer.IsCompleted); Assert.Equal(prompt.OperationId, answer.OperationId);
        Assert.Equal(2, answer.Sequence); Assert.Throws<InvalidOperationException>(() => prompt.Complete("q", "a", now));
    }

    [Fact]
    public void GeneratedBufferBoundIsExplicitAndPartialCompletionIsRejected()
    {
        var now = DateTime.UtcNow;
        AiConversationTurn Prompt() => AiConversationTurn.ReserveUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, new byte[32], now);
        Assert.Equal(32768, AiConversationTurn.MaximumAssistantCharacters);
        Assert.True(Prompt().Complete("q", new string('a', 32768), now).IsCompleted);
        Assert.Throws<ArgumentException>(() => Prompt().Complete("q", new string('a', 32769), now));
        Assert.Throws<ArgumentException>(() => Prompt().Complete("q", "  ", now));
    }

    [Fact]
    public void BindingExcludesSessionButIncludesOwnerContextRoleClassAndLearner()
    {
        var user = Guid.NewGuid(); var context = new AccessContext(user, user, Guid.NewGuid(), WorkspaceType.Personal, null, null, null, null);
        var baseline = AiConversationIdentity.Fingerprint(context, null);
        Assert.Equal(baseline, AiConversationIdentity.Fingerprint(context with { SessionId = Guid.NewGuid() }, null));
        Assert.NotEqual(baseline, AiConversationIdentity.Fingerprint(context with { UserId = Guid.NewGuid() }, null));
        Assert.NotEqual(baseline, AiConversationIdentity.Fingerprint(context with { OrganizationId = Guid.NewGuid(), WorkspaceType = WorkspaceType.Organization }, null));
        Assert.NotEqual(baseline, AiConversationIdentity.Fingerprint(context with { AssistantClassId = Guid.NewGuid() }, null));
        Assert.NotEqual(baseline, AiConversationIdentity.Fingerprint(context, Guid.NewGuid()));
    }

    [Fact]
    public void BoundSourceVersionsCannotSilentlyMoveToLatest()
    {
        var source = Guid.NewGuid(); var now = DateTime.UtcNow;
        var conversation = AiConversation.Create(Guid.NewGuid(), Guid.NewGuid(), new byte[32], now);
        conversation.BindSources([new(source, 1)], now);
        Assert.Throws<InvalidOperationException>(() => conversation.BindSources([new(source, 2)], now));
        Assert.Equal(1, Assert.Single(conversation.SourcePins).Version);
    }
}
