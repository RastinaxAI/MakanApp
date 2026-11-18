using System.Runtime.CompilerServices;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed partial class AiChatService(IAccessContextResolver accessContextResolver, IAiScopeStore scopeStore,
    IAiServiceClient client, IAiChatSources sources, IAiInvocationExecutor invocationExecutor,
    IAiConversationStore conversations) : IAiChatService
{
    public async Task<AiChatResult> ChatAsync(Guid userId, Guid sessionId, string correlationId, SendAiChatCommand command,
        CancellationToken cancellationToken, AiInvocationOptions? invocation = null)
    {
        var prepared = await Prepare(userId, sessionId, correlationId, command, invocation, false, cancellationToken);
        return await invocationExecutor.ExecuteAsync(userId, sessionId, prepared.Intent, async (_, ct) =>
        {
            await Reauthorize(prepared, ct);
            var grounded = await sources.AnswerAsync(prepared.Sources, prepared.Wire.Message.Text,
                prepared.Wire.RequestId, correlationId, false, ct);
            AiChatResult result;
            if (grounded.Answer is { } rag)
                result = new(rag.RequestId, rag.CorrelationId, prepared.Wire.ConversationId, new(rag.Answer), [], [], [], [])
                    { Provenance = grounded.Provenance, Citations = rag.Citations };
            else
            {
                await Reauthorize(prepared, ct);
                var response = await client.ChatAsync(prepared.Wire, ct);
                result = new(response.RequestId, response.CorrelationId, response.ConversationId, response.Answer,
                    response.Insights, response.Risks, response.Recommendations, response.Sources) { Provenance = grounded.Provenance };
            }
            await Complete(prepared, result.Answer.Text, grounded.Answer is null ? [] : Pins(prepared.Sources), ct);
            return result;
        }, ct => Reauthorize(prepared, ct), prepared.Options, cancellationToken);
    }

    public async IAsyncEnumerable<AiChatStreamEvent> StreamChatAsync(Guid userId, Guid sessionId, string correlationId,
        SendAiChatCommand command, [EnumeratorCancellation] CancellationToken cancellationToken, AiInvocationOptions? invocation = null)
    {
        var prepared = await Prepare(userId, sessionId, correlationId, command, invocation, true, cancellationToken);
        await foreach (var item in invocationExecutor.ExecuteStreamAsync(userId, sessionId, prepared.Intent,
            (_, ct) => Generate(prepared, correlationId, ct), ct => Reauthorize(prepared, ct), prepared.Options, cancellationToken))
            yield return item;
    }

    private async IAsyncEnumerable<AiChatStreamEvent> Generate(Prepared prepared, string correlation,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // RAG رسمی knowledge-v1 جریان ندارد؛ فقط مسیر General می‌تواند SSE واقعی تولید کند.
        var routing = await sources.AnswerAsync(prepared.Sources, prepared.Wire.Message.Text, prepared.Wire.RequestId, correlation, true, ct);
        await Reauthorize(prepared, ct);
        var text = new StringBuilder(); var count = 0;
        await foreach (var item in client.StreamChatAsync(prepared.Wire, ct))
        {
            await Reauthorize(prepared, ct);
            if (item is AiChatStreamDelta delta)
            {
                count += AiConversationTurn.CharacterCount(delta.Text);
                if (count > AiConversationTurn.MaximumAssistantCharacters) throw Invalid();
                text.Append(delta.Text);
            }
            if (item is AiChatStreamStart start) yield return start with { Provenance = routing.Provenance };
            else
            {
                if (item is AiChatStreamDone) await Complete(prepared, text.ToString(), [], ct);
                yield return item;
            }
        }
    }

    public async Task<AiConversationHistoryResult> HistoryAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, ct);
        var learner = await Learner(context, userId, sessionId, ct);
        var result = await conversations.ReadHistoryAsync(userId, conversationId, null, AiConversationIdentity.Fingerprint(context, learner),
            async (state, token) =>
            {
                await EnsureCurrent(context, learner, userId, sessionId, token);
                await sources.ReauthorizeHistoryAsync(userId, sessionId, state.SourcePins, null, token);
            }, ct);
        return new(result.State.ConversationId.ToString("N"), result.History);
    }

    private Task Reauthorize(Prepared prepared, CancellationToken ct) => prepared.Authorize(prepared.Conversation, ct);
    private Task Complete(Prepared prepared, string answer, IReadOnlyList<AiConversationSourcePin> pins, CancellationToken ct) =>
        conversations.CompleteAsync(prepared.Context.UserId, prepared.Conversation.ConversationId, prepared.Options.OperationId!.Value, prepared.Binding,
            prepared.Wire.Message.Text, answer, pins, async (state, token) =>
            {
                await prepared.Authorize(state, token);
                await sources.ReauthorizeHistoryAsync(prepared.Context.UserId, prepared.Context.SessionId, pins, prepared.Options.Payer, token);
            }, ct);
    private static AiConversationSourcePin[] Pins(AiChatSourcePlan plan) => plan.Versions.OrderBy(x => x.SourceId).Select(x => new AiConversationSourcePin(x.SourceId, x.Version)).ToArray();
    private static AiServiceException Invalid() => new(AiServiceErrorCodes.InvalidRequest, "درخواست گفت‌وگوی AI معتبر نیست.");
    private static AiServiceException Denied() => new(AiServiceErrorCodes.ContextNotAllowed, "زمینهٔ مجاز و جاری لازم است.");
    private sealed record Prepared(AccessContext Context, byte[] Binding, AiInvocationOptions Options,
        AiConversationState Conversation, AiChatRequest Wire, IAiInvocationRequest Intent, AiChatSourcePlan Sources,
        Func<AiConversationState, CancellationToken, Task> Authorize);
}
