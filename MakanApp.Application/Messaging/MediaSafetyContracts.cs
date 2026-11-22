using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Messaging;

public sealed record MediaSafetyResult(ContentSafetyDecision Decision, Guid? ReviewCaseId, DateTime? ReviewedAtUtc);

public interface IMediaSafetyProvider
{
    Task<MediaSafetyResult> EvaluateAsync(ContentSafetyAttachment file, CancellationToken cancellationToken);
}

public enum MediaReviewState { NotRequested = 0, PendingReview = 1, Approved = 2, Rejected = 3 }

public sealed record MediaReviewStatusResult(Guid FileAssetId, MediaReviewState State,
    bool AttachmentAllowed, Guid? ReviewReference, DateTime? ReviewedAtUtc);

public sealed class MediaSafetyService(IStorageService storage, IMediaSafetyProvider provider)
{
    public async Task<MediaReviewStatusResult> GetStatusAsync(Guid userId, Guid sessionId, Guid fileAssetId, CancellationToken ct)
    {
        var file = await storage.GetFileMetadataAsync(userId, sessionId, fileAssetId, ct);
        // مجوز دانلودِ عضو گفتگو، مجوز خواندن وضعیت خصوصی پروندهٔ مالک نیست.
        if (file.UploadedByUserId != userId) throw new StorageException(StorageErrorCodes.FileNotFound, "فایل یافت نشد.");
        if (file.Status != FileAssetStatus.Ready || file.Sha256Hash is null)
            return new(file.Id, MediaReviewState.NotRequested, false, null, null);
        var result = await provider.EvaluateAsync(new(file.Id, file.ContentType, file.SizeBytes, file.Sha256Hash), ct);
        var state = result.Decision switch
        {
            ContentSafetyDecision.Allowed => MediaReviewState.Approved,
            ContentSafetyDecision.Blocked => MediaReviewState.Rejected,
            _ => result.ReviewCaseId.HasValue ? MediaReviewState.PendingReview : MediaReviewState.NotRequested
        };
        return new(file.Id, state, result.Decision == ContentSafetyDecision.Allowed, result.ReviewCaseId, result.ReviewedAtUtc);
    }
}
