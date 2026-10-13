namespace MakanApp.Application.Ai;

public sealed record AiServiceCredential(
    string Scheme,
    string Token,
    DateTime ExpiresAtUtc);

public interface IAiServiceCredentialProvider
{
    ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken cancellationToken);
}
