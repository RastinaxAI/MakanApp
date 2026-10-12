using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.IntegrationTests;

public sealed class ContentSafetyProviderTestControl
{
    private readonly object _sync = new();
    private readonly List<ContentSafetyRequest> _requests = [];
    private readonly Dictionary<Guid, ProviderBehavior> _assignedBehaviors = [];
    private ContentSafetyResult _result = new(ContentSafetyDecision.Allowed);
    private bool _fail;
    private Func<Task>? _beforeReturn;

    public IReadOnlyCollection<ContentSafetyRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return _requests.ToArray();
            }
        }
    }

    public void Configure(
        ContentSafetyDecision decision,
        string? riskLevel = null,
        string? providerReference = null,
        IReadOnlyCollection<string>? policyCodes = null,
        Func<Task>? beforeReturn = null)
    {
        lock (_sync)
        {
            _result = new ContentSafetyResult(
                decision,
                riskLevel,
                providerReference,
                policyCodes);
            _fail = false;
            _beforeReturn = beforeReturn;
            _requests.Clear();
            _assignedBehaviors.Clear();
        }
    }

    public void ConfigureUnavailable()
    {
        lock (_sync)
        {
            _fail = true;
            _beforeReturn = null;
            _requests.Clear();
            _assignedBehaviors.Clear();
        }
    }

    internal (ContentSafetyResult Result, bool Fail, Func<Task>? BeforeReturn) Record(
        ContentSafetyRequest request)
    {
        lock (_sync)
        {
            _requests.Add(request);
            if (_assignedBehaviors.TryGetValue(request.ClientMessageId, out var assigned))
            {
                return (assigned.Result, assigned.Fail, assigned.BeforeReturn);
            }

            var behavior = new ProviderBehavior(_result, _fail, _beforeReturn);
            _assignedBehaviors[request.ClientMessageId] = behavior;
            _result = new ContentSafetyResult(ContentSafetyDecision.Allowed);
            _fail = false;
            _beforeReturn = null;
            return (behavior.Result, behavior.Fail, behavior.BeforeReturn);
        }
    }

    private sealed record ProviderBehavior(
        ContentSafetyResult Result,
        bool Fail,
        Func<Task>? BeforeReturn);
}

public sealed class RecordingContentSafetyProvider(ContentSafetyProviderTestControl control)
    : IContentSafetyProvider
{
    public async Task<ContentSafetyResult> EvaluateAsync(
        ContentSafetyRequest request,
        CancellationToken cancellationToken)
    {
        var behavior = control.Record(request);
        if (behavior.BeforeReturn is not null)
        {
            await behavior.BeforeReturn();
        }

        if (behavior.Fail)
        {
            throw new ContentSafetyProviderUnavailableException("خطای کنترل‌شده provider تست.");
        }

        return behavior.Result;
    }
}
