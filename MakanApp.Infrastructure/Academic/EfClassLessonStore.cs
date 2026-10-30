using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfClassLessonStore(MakanDbContext db, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IClassLessonStore
{
    public async Task<IOrganizationTransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct));

    public async Task LockClassAsync(Guid organizationId, Guid classId, CancellationToken ct)
    {
        if (await db.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={organizationId} AND [Id]={classId}")
            .SingleOrDefaultAsync(ct) is null) throw new AcademicException(SchoolErrorCodes.LessonNotFound, "کلاس مجاز پیدا نشد.");
    }

    public Task<ClassLesson?> FindOperationAsync(Guid organizationId, Guid actorId, Guid operationId, CancellationToken ct) =>
        db.ClassLessons.FromSqlInterpolated($"SELECT * FROM [academic].[ClassLessons] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={organizationId} AND [CreatedByUserId]={actorId} AND [ClientOperationId]={operationId}")
            .Include(x => x.Attachments).SingleOrDefaultAsync(ct);

    public Task<ClassLesson?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken ct) =>
        (forUpdate ? db.ClassLessons.FromSqlInterpolated($"SELECT * FROM [academic].[ClassLessons] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={organizationId} AND [Id]={id}")
            : db.ClassLessons.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Id == id))
        .Include(x => x.Attachments).SingleOrDefaultAsync(ct);

    public async Task<SchoolPage<ClassLessonResult>> ListAsync(AccessContext c, Guid classId, SchoolPageQuery q, bool includeDrafts, DateTime now, CancellationToken ct)
    {
        var query = db.ClassLessons.AsNoTracking().Where(x => x.OrganizationId == c.OrganizationId && x.ClassId == classId &&
            (includeDrafts || x.Status == LessonStatus.Published) && (q.Search == null || q.Search == "" || x.Title.Contains(q.Search)));
        var total = await query.CountAsync(ct);
        var lessons = await query.Include(x => x.Attachments).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        var courseId = await db.Classes.Where(x => x.OrganizationId == c.OrganizationId && x.Id == classId).Select(x => x.CourseId).SingleAsync(ct);
        var fileIds = lessons.SelectMany(x => x.Attachments).Select(x => x.FileAssetId).Distinct().ToArray();
        var files = await db.FileAssets.AsNoTracking().Where(x => fileIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return new(now, q.Page, q.PageSize, total, lessons.Select(x => Map(x, courseId, files, false)).ToArray());
    }

    public async Task<ClassLessonResult> ResultAsync(ClassLesson lesson, bool replayed, CancellationToken ct)
    {
        var courseId = await db.Classes.Where(x => x.Id == lesson.ClassId && x.OrganizationId == lesson.OrganizationId).Select(x => x.CourseId).SingleAsync(ct);
        var ids = lesson.Attachments.Select(x => x.FileAssetId).ToArray();
        var files = await db.FileAssets.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return Map(lesson, courseId, files, replayed);
    }

    private static ClassLessonResult Map(ClassLesson x, Guid courseId, IReadOnlyDictionary<Guid, FileAsset> files, bool replayed) =>
        new(x.Id, x.OrganizationId, x.ClassId, courseId, x.Title, x.Body, x.DisplayOrder, x.Status, x.RevisionNumber,
            Convert.ToBase64String(x.RowVersion), x.CreatedByUserId, Utc(x.CreatedAtUtc), x.UpdatedAtUtc.HasValue ? Utc(x.UpdatedAtUtc.Value) : null, x.PublishedAtUtc.HasValue ? Utc(x.PublishedAtUtc.Value) : null,
            x.Attachments.OrderBy(a => a.DisplayOrder).Select(a => {
                var f = files[a.FileAssetId]; var available = f.Status == FileAssetStatus.Ready;
                return new LessonAttachmentResult(f.Id, f.OriginalFileName, f.ContentType, f.SizeBytes, f.Status,
                    available, available ? $"/api/v1/files/{f.Id:D}/content" : null);
            }).ToArray(), replayed);

    // datetime2 منطقهٔ زمانی ندارد؛ زمان ذخیره‌شدهٔ این مدل همواره UTC سرور است.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public void Add(ClassLesson lesson) => db.ClassLessons.Add(lesson);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await realtime.CaptureSchoolAsync(ct); await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AcademicException(SchoolErrorCodes.LessonVersionConflict, "درس یا فایل هم‌زمان تغییر کرده است."); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 1205 })
        { throw new AcademicException(SchoolErrorCodes.LessonConflict, "عملیات درس با درخواست هم‌زمان تعارض دارد."); }
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IOrganizationTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
