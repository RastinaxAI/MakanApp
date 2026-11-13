namespace MakanApp.Application.Platform;

public interface ISchoolProvisioningWorkHandler
{
    Task<Guid> PreparePaidOrderAsync(Guid orderId, CancellationToken ct);
    Task RequeueAsync(Guid requestId, Guid operationId, CancellationToken ct);
    bool Handles(string messageType);
    Task HandleAsync(string payload, CancellationToken ct);
}
