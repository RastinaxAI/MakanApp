using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class ManualMediaSafetyProvider(MakanDbContext db) : IMediaSafetyProvider
{
    public const string PolicyVersion = "makan-manual-media-review-v1";

    public async Task<MediaSafetyResult> EvaluateAsync(ContentSafetyAttachment file, CancellationToken ct)
    {
        var review = await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c => c.MediaFileAssetId == file.FileAssetId, ct);
        if (review is null) return new(ContentSafetyDecision.ReviewRequired, null, null);
        if (!review.MatchesMedia(file.FileAssetId, file.Sha256Hash, file.ContentType, file.SizeBytes))
            return new(ContentSafetyDecision.Blocked, review.Id, review.MediaReviewedAtUtc);
        return new(review.MediaDecision ?? ContentSafetyDecision.ReviewRequired, review.Id, review.MediaReviewedAtUtc);
    }
}
