using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Ai;

public sealed record AiInvocationOptions(Guid? OperationId = null, AiPayerSelection? Payer = null)
{
    public static AiInvocationOptions Resolve(AiInvocationOptions? options, Guid? bodyId = null)
    {
        if (bodyId == Guid.Empty || options?.OperationId == Guid.Empty ||
            bodyId.HasValue && options?.OperationId is Guid headerId && headerId != bodyId ||
            options?.Payer is { } payer && !Enum.IsDefined(payer))
            throw new AiServiceException(AiServiceErrorCodes.InvalidRequest, "شناسهٔ عملیات یا انتخاب پرداخت‌کننده معتبر نیست.");
        return new(bodyId ?? options?.OperationId ?? Guid.NewGuid(), options?.Payer);
    }
}
public interface IAiRuntimeAvailability { void EnsureEnabled(); }
public interface IAiInvocationExecutor
{
    Task<TResponse> ExecuteAsync<TRequest, TResponse>(Guid userId, Guid sessionId, TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> invoke, Func<CancellationToken, Task> reauthorize,
        AiInvocationOptions? options, CancellationToken ct)
        where TRequest : IAiServiceRequest where TResponse : class, IAiServiceResponse;
}
public static class AiInvocationErrorCodes
{
    public const string Completed = "AI_OPERATION_ALREADY_COMPLETED";
    public const string InProgress = "AI_OPERATION_IN_PROGRESS";
    public const string Unknown = "AI_OPERATION_OUTCOME_UNKNOWN";
    public const string Failed = "AI_OPERATION_PREVIOUSLY_FAILED";
}
public static class AiInvocationContext
{
    public static void EnsureSame(object expectedScope, object currentScope)
    {
        if (expectedScope.GetType() != currentScope.GetType() ||
            !System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(expectedScope).SequenceEqual(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(currentScope)))
            throw new AiServiceException(AiServiceErrorCodes.ContextNotAllowed, "زمینهٔ مجاز عملیات تغییر کرده است.");
    }
}
