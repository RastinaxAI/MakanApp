using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public sealed record SetConversationPhotoCommand(Guid FileAssetId, string ExpectedVersion);
public sealed record ConversationPhotoResult(Guid FileAssetId, string ContentType)
{
    public string DownloadUrl => $"/api/v1/files/{FileAssetId:D}/content";
}
public sealed record ConversationPhotoMutationResult(Guid ConversationId, ConversationPhotoResult? Photo, string Version);

public interface IConversationPhotoStore
{
    Task<ConversationPhotoMutationResult> SetPhotoAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid? fileAssetId,
        byte[] expectedVersion, AccessContext context, CancellationToken ct);
}

public sealed class ConversationPhotoService(IConversationPhotoStore store, MessagingOperationContextResolver contexts)
{
    public async Task<ConversationPhotoMutationResult> SetAsync(Guid userId, Guid sessionId, Guid conversationId,
        Guid? fileAssetId, string expectedVersion, CancellationToken ct)
    {
        byte[] version;
        try
        {
            version = Convert.FromBase64String(expectedVersion);
            if (version.Length != 8 || fileAssetId == Guid.Empty || conversationId == Guid.Empty) throw new FormatException();
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        { throw new MessagingException(MessagingErrorCodes.ManagedConversationInvalid, "شناسه یا نسخهٔ عکس گفتگو معتبر نیست."); }
        return await store.SetPhotoAsync(userId, sessionId, conversationId, fileAssetId, version,
            await contexts.ConversationAsync(userId, sessionId, conversationId, ct), ct);
    }
}
