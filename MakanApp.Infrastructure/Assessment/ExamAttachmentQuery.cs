using MakanApp.Application.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

internal static class ExamAttachmentQuery
{
    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<ExamAnswerAttachmentRecord>>> LoadAsync(
        MakanDbContext db, IReadOnlyCollection<Guid> revisionIds, CancellationToken ct)
    {
        var items = await (from attachment in db.AnswerRevisionAttachments.AsNoTracking()
            join file in db.FileAssets.AsNoTracking() on attachment.FileAssetId equals file.Id
            where revisionIds.Contains(attachment.AnswerRevisionId)
            select new ExamAnswerAttachmentRecord(attachment, file)).ToArrayAsync(ct);
        return items.GroupBy(item => item.Attachment.AnswerRevisionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<ExamAnswerAttachmentRecord>)group.OrderBy(item => item.Attachment.FileAssetId).ToArray());
    }
}
