namespace MakanApp.Application.Ai;

public interface IAiServiceClient
{
    Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken);
}
