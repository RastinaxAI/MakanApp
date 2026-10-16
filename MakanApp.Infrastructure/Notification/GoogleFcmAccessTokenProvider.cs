using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;

namespace MakanApp.Infrastructure.Notification;

public interface IFcmAccessTokenProvider
{
    bool IsConfigured { get; }
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed class GoogleFcmAccessTokenProvider(FcmTransportOptions options) : IFcmAccessTokenProvider, IDisposable
{
    public const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private ServiceAccountCredential? _credential;
    public bool IsConfigured => options.IsConfigured;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidDataException("پیکربندی FCM کامل نیست.");
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_credential is null)
            {
                await using var stream = new FileStream(options.ServiceAccountPath!, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (stream.Length > 16_384) throw new InvalidDataException("فایل credential معتبر نیست.");
                using var document = await JsonDocument.ParseAsync(stream,
                    new JsonDocumentOptions { MaxDepth = 8 }, cancellationToken);
                var root = document.RootElement;
                if (Read(root, "type") != "service_account" || Read(root, "project_id") != options.ProjectId ||
                    Read(root, "token_uri") != "https://oauth2.googleapis.com/token" ||
                    root.TryGetProperty("universe_domain", out var universe) && universe.GetString() != "googleapis.com")
                    throw new InvalidDataException("نوع یا پروژه credential مجاز نیست.");
                var email = Read(root, "client_email");
                if (email.Length > 254 || !email.EndsWith(".iam.gserviceaccount.com", StringComparison.Ordinal) || email.Any(char.IsControl))
                    throw new InvalidDataException("شناسه credential معتبر نیست.");
                // نوع credential و token server ثابت‌اند؛ JSON نمی‌تواند endpoint یا جریان احراز هویت دلخواه تعیین کند.
                var initializer = new ServiceAccountCredential.Initializer(email, "https://oauth2.googleapis.com/token")
                {
                    ProjectId = options.ProjectId,
                    Scopes = [MessagingScope],
                    UseJwtAccessWithScopes = false,
                    DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.None
                }.FromPrivateKey(Read(root, "private_key"));
                _credential = new ServiceAccountCredential(initializer);
            }
        }
        finally { _initializationLock.Release(); }
        // cache و refresh توکن OAuth متعلق به SDK رسمی است؛ raw token ثبت یا برگرداندهٔ API نمی‌شود.
        return await _credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }

    private static string Read(JsonElement root, string key) => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()! : throw new InvalidDataException("فایل credential کامل نیست.");

    public void Dispose()
    {
        _credential?.HttpClient.Dispose();
        _credential?.Key.Dispose();
        _initializationLock.Dispose();
    }
}
