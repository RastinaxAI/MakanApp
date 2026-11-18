using System.Runtime.CompilerServices;

namespace MakanApp.Application.Ai;

public sealed partial class AiInvocationExecutor
{
    public async IAsyncEnumerable<AiChatStreamEvent> ExecuteStreamAsync<TRequest>(Guid userId, Guid sessionId, TRequest request,
        Func<TRequest, CancellationToken, IAsyncEnumerable<AiChatStreamEvent>> invoke,
        Func<CancellationToken, Task> reauthorize, AiInvocationOptions? options,
        [EnumeratorCancellation] CancellationToken ct) where TRequest : IAiInvocationRequest
    {
        // ExecuteAsync مالک همان Reserve/Finalize موجود است؛ stream هیچ ledger موازی ندارد.
        // callback تا پایان enumeration زنده می‌ماند و فقط done معتبر می‌تواند Consumed بسازد.
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IAsyncEnumerator<AiChatStreamEvent>? upstream = null;
        var partial = false;
        var execution = ExecuteAsync(userId, sessionId, request, async (value, token) =>
        {
            upstream = invoke(value, token).GetAsyncEnumerator(token);
            ready.TrySetResult();
            await finished.Task.WaitAsync(token);
            return new StreamCompletion();
        }, reauthorize, AiInvocationOptions.Resolve(options) with { HasPartialOutput = () => partial }, lifetime.Token);
        // خطاهای admission پیش از ساخت upstream نیز باید مستقیم به caller برسند.
        await Task.WhenAny(ready.Task, execution);
        if (!ready.Task.IsCompletedSuccessfully) { await execution; yield break; }
        var completed = false;
        try
        {
            await using var events = upstream!;
            while (true)
            {
                bool next;
                try { next = await events.MoveNextAsync(); }
                catch (Exception error)
                {
                    finished.TrySetException(error);
                    await execution;
                    throw;
                }
                if (!next)
                {
                    var error = new AiServiceException(AiServiceErrorCodes.ResponseInvalid, "جریان بدون پایان معتبر قطع شد.");
                    finished.TrySetException(error); await execution; throw error;
                }
                var item = events.Current;
                if (item is AiChatStreamDelta) partial = true;
                if (item is AiChatStreamDone)
                {
                    finished.TrySetResult();
                    await execution;
                    completed = true;
                    yield return item;
                    yield break;
                }
                yield return item;
            }
        }
        finally
        {
            if (!completed)
            {
                // Dispose یا قطع Client نیز عملیات نیمه‌تمام را موفق اعلام نمی‌کند.
                finished.TrySetCanceled(); lifetime.Cancel();
                try { await execution; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                // خطای اصلی در مسیر MoveNext به caller رسیده است؛ fault دوم همان Task است.
                catch (Exception) when (finished.Task.IsFaulted) { }
            }
        }
    }

    private sealed record StreamCompletion;
}
