using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformEducationSafetyStore
{
    private async Task<(Guid? ConversationId, Guid? FileId)> ReadMediaReviewResourcesAsync(Guid caseId, CancellationToken ct)
    {
        // فقط Hint ثابت قفل است؛ مجوز و وضعیت پرونده بعد از گرفتن قفل دوباره بررسی می‌شوند.
        var row = await dbContext.ModerationCases.AsNoTracking().Where(c => c.Id == caseId && c.Source == ModerationCaseSource.MediaReview)
            .Select(c => new { c.ConversationId, c.MediaFileAssetId }).SingleOrDefaultAsync(ct);
        return row is null ? (null, null) : (row.ConversationId, row.MediaFileAssetId);
    }

    private async Task LockMediaReviewResourcesAsync((Guid? ConversationId, Guid? FileId) resources, CancellationToken ct)
    {
        if (!resources.FileId.HasValue) return;
        await dbContext.Conversations.FromSqlInterpolated($"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={resources.ConversationId}")
            .AsNoTracking().SingleAsync(ct);
        await dbContext.FileAssets.FromSqlInterpolated($"SELECT * FROM [storage].[FileAssets] WITH (XLOCK,HOLDLOCK) WHERE [Id]={resources.FileId}")
            .AsNoTracking().SingleAsync(ct);
    }

    private async Task<MediaReviewStatusResult?> ReadMediaReviewStatusAsync(ModerationCase review, CancellationToken ct)
    {
        if (!review.MediaFileAssetId.HasValue) return null;
        var file = await dbContext.FileAssets.AsNoTracking().SingleAsync(f => f.Id == review.MediaFileAssetId.Value, ct);
        var state = review.MediaDecision switch
        {
            ContentSafetyDecision.Allowed => MediaReviewState.Approved,
            ContentSafetyDecision.Blocked => MediaReviewState.Rejected,
            _ => MediaReviewState.PendingReview
        };
        var allowed = state == MediaReviewState.Approved && file.Status == FileAssetStatus.Ready &&
            review.MatchesMedia(file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes);
        return new(file.Id, state, allowed, review.Id, review.MediaReviewedAtUtc);
    }
}
