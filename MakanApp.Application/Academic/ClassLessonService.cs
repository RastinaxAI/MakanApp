using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Storage;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Academic;

public sealed class ClassLessonService(IAccessContextResolver access, ISchoolReadStore permissions,
    IClassLessonStore store, IFileAssetStore files, TimeProvider clock, IAcademicActivityPublisher? activities = null)
{
    public async Task<ClassLessonResult> CreateAsync(Guid userId, Guid sessionId, Guid classId, CreateClassLessonCommand command, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        if (context.OrganizationId is null) throw NotFound();
        await using var tx = await store.BeginAsync(ct);
        await store.LockClassAsync(context.OrganizationId.Value, classId, ct);
        await RequireWriterAsync(context, classId, ct);
        if (command.ClientOperationId == Guid.Empty || command.FileAssetIds is null) throw Invalid();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            classId, Title = command.Title?.Trim(), Body = command.Body?.Trim() ?? "", command.DisplayOrder, command.FileAssetIds }))));
        var old = await store.FindOperationAsync(context.OrganizationId.Value, userId, command.ClientOperationId, ct);
        if (old is not null)
        {
            if (old.CreationRequestHash != hash) throw Conflict();
            await tx.CommitAsync(ct); return await store.ResultAsync(old, true, ct);
        }
        ClassLesson lesson;
        try { lesson = ClassLesson.Create(context.OrganizationId.Value, classId, userId, command.ClientOperationId, hash,
            command.Title!, command.Body, command.DisplayOrder, command.FileAssetIds, Now()); }
        catch (ArgumentException) { throw Invalid(); }
        await ValidateFilesAsync(context, command.FileAssetIds, [], ct);
        store.Add(lesson); await store.SaveAsync(ct); await tx.CommitAsync(ct);
        return await store.ResultAsync(lesson, false, ct);
    }

    public async Task<ClassLessonResult> UpdateAsync(Guid userId, Guid sessionId, Guid id, UpdateClassLessonCommand command, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        if (context.OrganizationId is null) throw NotFound();
        var initial = await store.GetAsync(context.OrganizationId.Value, id, false, ct) ?? throw NotFound();
        await using var tx = await store.BeginAsync(ct);
        await store.LockClassAsync(context.OrganizationId.Value, initial.ClassId, ct);
        await RequireWriterAsync(context, initial.ClassId, ct);
        var lesson = await store.GetAsync(context.OrganizationId.Value, id, true, ct) ?? throw NotFound();
        Version(lesson.RowVersion, command.ExpectedVersion);
        if (lesson.Status != LessonStatus.Draft) throw Conflict();
        if (command.FileAssetIds is null) throw Invalid();
        var existing = lesson.Attachments.Select(x => x.FileAssetId).ToArray();
        await ValidateFilesAsync(context, command.FileAssetIds, existing, ct);
        try { lesson.UpdateDraft(command.Title, command.Body, command.DisplayOrder, command.FileAssetIds, Now()); }
        catch (ArgumentException) { throw Invalid(); }
        await store.SaveAsync(ct); await tx.CommitAsync(ct);
        return await store.ResultAsync(lesson, false, ct);
    }

    public async Task<ClassLessonResult> PublishAsync(Guid userId, Guid sessionId, Guid id, PublishClassLessonCommand command, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        if (context.OrganizationId is null) throw NotFound();
        var initial = await store.GetAsync(context.OrganizationId.Value, id, false, ct) ?? throw NotFound();
        await using var tx = await store.BeginAsync(ct);
        await store.LockClassAsync(context.OrganizationId.Value, initial.ClassId, ct);
        await RequireWriterAsync(context, initial.ClassId, ct);
        var lesson = await store.GetAsync(context.OrganizationId.Value, id, true, ct) ?? throw NotFound();
        DecodeVersion(command.ExpectedVersion);
        if (lesson.Status == LessonStatus.Published)
        {
            await tx.CommitAsync(ct); return await store.ResultAsync(lesson, true, ct);
        }
        Version(lesson.RowVersion, command.ExpectedVersion);
        await ValidateFilesAsync(context, lesson.Attachments.Select(x => x.FileAssetId).ToArray(), lesson.Attachments.Select(x => x.FileAssetId).ToArray(), ct);
        try { lesson.Publish(Now()); } catch (ArgumentException) { throw Invalid(); }
        if (activities is not null)
            await activities.RecordAsync(AcademicActivityPublication.Create(AcademicActivityKind.Lesson, lesson.OrganizationId,
                lesson.ClassId, lesson.Id, lesson.RevisionNumber, userId, lesson.Title, Now()), ct);
        await store.SaveAsync(ct); await tx.CommitAsync(ct);
        return await store.ResultAsync(lesson, false, ct);
    }

    public async Task<ClassLessonResult> GetAsync(Guid userId, Guid sessionId, Guid id, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        if (context.OrganizationId is null) throw NotFound();
        var lesson = await store.GetAsync(context.OrganizationId.Value, id, false, ct) ?? throw NotFound();
        if (!await permissions.CanManageClassAsync(context, lesson.ClassId, ct) &&
            !(lesson.Status == LessonStatus.Published && await permissions.CanStudyClassAsync(context, lesson.ClassId, ct))) throw NotFound();
        return await store.ResultAsync(lesson, false, ct);
    }

    public async Task<SchoolPage<ClassLessonResult>> ListAsync(Guid userId, Guid sessionId, Guid classId, SchoolPageQuery query, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        var writer = await permissions.CanManageClassAsync(context, classId, ct);
        if (!writer && !await permissions.CanStudyClassAsync(context, classId, ct)) throw NotFound();
        return await store.ListAsync(context, classId, SchoolQueryValidation.Normalize(query), writer, Now(), ct);
    }

    private async Task RequireWriterAsync(AccessContext context, Guid classId, CancellationToken ct)
    {
        if (!await permissions.CanManageClassAsync(context, classId, ct)) throw NotFound();
    }

    private async Task ValidateFilesAsync(AccessContext context, IReadOnlyCollection<Guid> ids, IReadOnlyCollection<Guid> existing, CancellationToken ct)
    {
        if (ids.Count > 10 || ids.Distinct().Count() != ids.Count || ids.Any(x => x == Guid.Empty)) throw Invalid();
        foreach (var id in ids.Order())
        {
            var file = await files.GetForUpdateAsync(id, ct);
            if (file is null || file.OrganizationId != context.OrganizationId || !existing.Contains(id) && file.UploadedByUserId != context.UserId)
                throw new StorageException(StorageErrorCodes.FileNotFound, "فایل مجاز پیوست درس پیدا نشد.");
            if (file.Status != FileAssetStatus.Ready) throw new StorageException(StorageErrorCodes.FileNotReady, "پیوست درس باید آماده باشد.");
            if (!existing.Contains(id)) files.TouchForBinding(file);
        }
    }

    private static byte[] DecodeVersion(string value)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); if (bytes.Length == 8) return bytes; }
        catch (FormatException) { }
        throw Invalid();
    }
    private static void Version(byte[] current, string expected)
    {
        if (!current.SequenceEqual(DecodeVersion(expected)))
            throw new AcademicException(SchoolErrorCodes.LessonVersionConflict, "نسخهٔ درس تغییر کرده است؛ دوباره دریافت کنید.");
    }
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
    private static AcademicException Invalid() => new(SchoolErrorCodes.Invalid, "درخواست درس معتبر نیست.");
    private static AcademicException NotFound() => new(SchoolErrorCodes.LessonNotFound, "درس مجاز پیدا نشد.");
    private static AcademicException Conflict() => new(SchoolErrorCodes.LessonConflict, "درخواست با وضعیت یا عملیات درس تعارض دارد.");
}
