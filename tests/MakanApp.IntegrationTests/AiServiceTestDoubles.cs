using MakanApp.Application.Ai;

namespace MakanApp.IntegrationTests;

public sealed class AiServiceTestControl
{
    private readonly object _sync = new();
    private AiChatRequest? _lastRequest;
    private Exception? _nextException;

    public AiChatRequest? LastRequest
    {
        get
        {
            lock (_sync)
            {
                return _lastRequest;
            }
        }
    }

    public void FailNext(Exception exception)
    {
        lock (_sync)
        {
            _nextException = exception;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _lastRequest = null;
            _nextException = null;
        }
    }

    internal AiChatResponse Execute(AiChatRequest request)
    {
        lock (_sync)
        {
            _lastRequest = request;
            if (_nextException is not null)
            {
                var exception = _nextException;
                _nextException = null;
                throw exception;
            }
        }

        return new AiChatResponse(
            request.RequestId,
            request.CorrelationId,
            request.ConversationId,
            new AiMessage("پاسخ آزمایشی"),
            [],
            [],
            [],
            [],
            new AiResponseMetadata("integration-test", 1));
    }
}

public sealed class RecordingAiServiceClient(AiServiceTestControl control) : IAiServiceClient
{
    public Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(control.Execute(request));
    }
}
