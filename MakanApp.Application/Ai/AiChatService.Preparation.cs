using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed partial class AiChatService
{
    private async Task<Prepared> Prepare(Guid user, Guid session, string correlation, SendAiChatCommand command,
        AiInvocationOptions? invocation, bool streaming, CancellationToken ct)
    {
        Validate(command, correlation);
        var profile = command.AssistantProfile?.Validate();
        var options = AiInvocationOptions.Resolve(invocation, command.OperationId);
        var context = await accessContextResolver.ResolveAsync(user, session, ct);
        var learner = await Learner(context, user, session, ct);
        var binding = AiConversationIdentity.Fingerprint(context, learner);
        IAiInvocationRequest intent;
        AiChatRequest wire;
        if (context.WorkspaceType == WorkspaceType.Personal)
        {
            var personal = PersonalAiChatIntent.Create(user, session, context, correlation, command);
            intent = personal; wire = PersonalAiChatContract.ToServiceRequest(personal);
        }
        else
        {
            wire = new(Guid.NewGuid().ToString("N"), correlation, command.ConversationId?.ToString("N"),
                new(user.ToString("D"), context.ActiveRole!.Value.ToString()),
                new(context.OrganizationId!.Value.ToString("D"), context.OrganizationId.Value.ToString("D"),
                    context.AssistantClassId?.ToString("D"), learner?.ToString("D")), new(command.Message.Trim()), AiAuthorizedContext.Empty);
            intent = wire;
        }
        // درخواست سادهٔ قدیمی همان hash را دارد؛ history و شناسهٔ ساخته‌شدهٔ سرور جزو intent نیستند.
        var plain = !streaming && profile is null && command.Mode is null && command.SourceIntent is null &&
            command.SelectedSourceIds?.Count is not > 0 && command.SelectedSources?.Count is not > 0;
        var hash = plain ? AiInvocationExecutor.Fingerprint(intent, context) : SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Kind = "adaptive-chat-v1", Streaming = streaming, command.ConversationId, Message = command.Message.Trim(), Profile = profile,
            command.Mode, command.SourceIntent, Sources = AiSourceSelection.Canonicalize(command.SelectedSourceIds),
            ExactVersions = command.SelectedSources?.OrderBy(x => x.SourceId).ToArray(), Binding = Convert.ToHexString(binding) }));
        options = options with { IntentFingerprint = hash };
        AiChatSourcePlan plan;
        try { plan = await sources.PrepareAsync(user, session, options.OperationId!.Value, hash, command, options.Payer, ct); }
        catch (MakanApp.Application.Knowledge.EducationalSourceException error)
        {
            throw AiSourceSelection.Error(error.Code switch {
                MakanApp.Application.Knowledge.SourceErrorCodes.NotFound => AiSourceErrorCodes.NotFound,
                MakanApp.Application.Knowledge.SourceErrorCodes.NotReady => AiSourceErrorCodes.NotReady,
                _ => AiSourceErrorCodes.NotAllowed });
        }
        async Task Authorize(AiConversationState state, CancellationToken token)
        {
            await EnsureCurrent(context, learner, user, session, token);
            await sources.ReauthorizeSelectionAsync(plan, token);
            await sources.ReauthorizeHistoryAsync(user, session, state.SourcePins, options.Payer, token);
            if (state.SourcePins.Count != 0 && plan.Versions.Count != 0 && !state.SourcePins.SequenceEqual(Pins(plan)))
                throw new AiServiceException(AiConversationErrorCodes.SourceBindingConflict, "برای تغییر نسخهٔ منابع، گفت‌وگوی تازه شروع کنید.");
        }
        var conversation = await conversations.OpenAsync(user, command.ConversationId, options.OperationId.Value, binding, hash, Authorize, ct);
        var history = await conversations.ReadHistoryAsync(user, conversation.ConversationId, options.OperationId, binding, Authorize, ct);
        wire = wire with { ConversationId = conversation.ConversationId.ToString("N"),
            Context = wire.Context with { AssistantProfile = profile }, History = history.History };
        return new(context, binding, options, conversation, wire, intent, plan, Authorize);
    }

    private async Task<Guid?> Learner(AccessContext context, Guid user, Guid session, CancellationToken ct)
    {
        if (context.WorkspaceType == WorkspaceType.Personal) { AiInvocationContext.EnsurePersonal(context, user, session); return null; }
        if (context.WorkspaceType != WorkspaceType.Organization || context.OrganizationId is null || context.MembershipId is null ||
            context.ActiveRole is not (OrganizationRole.Student or OrganizationRole.Parent or OrganizationRole.Teacher or OrganizationRole.Manager)) throw Denied();
        var learner = context.SubjectOrganizationPersonId;
        if (context.ActiveRole == OrganizationRole.Student)
            learner = await scopeStore.GetActiveLearnerOrganizationPersonIdAsync(context.OrganizationId.Value, user, ct) ?? throw Denied();
        return learner;
    }
    private async Task EnsureCurrent(AccessContext before, Guid? learner, Guid user, Guid session, CancellationToken ct)
    {
        var current = await accessContextResolver.ResolveAsync(user, session, ct);
        // فیلد پنهان از JSON، مانند AssistantClassId، همچنان بخشی از هویت امنیتی بافت است.
        if (before != current) throw Denied();
        if (await Learner(current, user, session, ct) != learner) throw Denied();
    }
    private static void Validate(SendAiChatCommand command, string correlation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.Message) || AiConversationTurn.CharacterCount(command.Message.Trim()) > AiConversationTurn.MaximumUserCharacters ||
                string.IsNullOrWhiteSpace(correlation) || correlation.Length > 128 || correlation.Any(char.IsControl) || command.ConversationId == Guid.Empty ||
                command.Mode.HasValue && !Enum.IsDefined(command.Mode.Value) || command.SourceIntent.HasValue && !Enum.IsDefined(command.SourceIntent.Value) ||
                command.SelectedSources?.Any(x => x is null) == true) throw Invalid();
        }
        catch (ArgumentException) { throw Invalid(); }
    }
}
