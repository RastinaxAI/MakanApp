using MakanApp.Application.Commerce;
using MakanApp.Domain.Support;

namespace MakanApp.Application.Support;

public static class SupportErrorCodes
{
    public const string Invalid = "SUPPORT_REQUEST_INVALID";
    public const string NotFound = "SUPPORT_TICKET_NOT_FOUND";
    public const string Forbidden = "SUPPORT_PERMISSION_REQUIRED";
    public const string Conflict = "SUPPORT_TICKET_CONFLICT";
    public const string FileUnavailable = "SUPPORT_FILE_UNAVAILABLE";
}
public sealed class SupportException(string code) : Exception("عملیات پشتیبانی با مجوز، نسخه یا وضعیت جاری سازگار نیست.") { public string Code { get; } = code; }
public sealed record SupportActor(Guid UserId, Guid SessionId, string CorrelationId);
public sealed record CreateSupportTicketCommand(Guid ClientOperationId, string Subject, string Category, string Body, IReadOnlyCollection<Guid>? FileAssetIds = null);
public sealed record ReplySupportTicketCommand(Guid ClientOperationId, long ExpectedVersion, string Body, IReadOnlyCollection<Guid>? FileAssetIds = null);
public sealed record ManageSupportTicketCommand(Guid ClientOperationId, long ExpectedVersion, string Reason, Guid? AssignedStaffUserId = null);
public sealed record SupportMessageResult(Guid Id, string AuthorType, string Body, long TicketVersion, DateTime CreatedAtUtc, IReadOnlyCollection<Guid> FileAssetIds);
public sealed record SupportTicketResult(Guid Id, string Subject, string Category, SupportTicketStatus Status, long Version, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    Guid? OwnerUserId, Guid? AssignedStaffUserId, IReadOnlyCollection<SupportMessageResult> Messages, bool HasMoreMessages = false, long? NextMessageVersion = null);
public sealed record SupportTicketPage(IReadOnlyCollection<SupportTicketResult> Items, int Total, int Page, int PageSize);
public interface ISupportTicketStore
{
    Task<SupportTicketResult> CreateAsync(SupportActor actor, CreateSupportTicketCommand command, CancellationToken ct);
    Task<SupportTicketResult> ReplyAsync(SupportActor actor, Guid id, ReplySupportTicketCommand command, bool staff, CancellationToken ct);
    Task<SupportTicketResult> ManageAsync(SupportActor actor, Guid id, ManageSupportTicketCommand command, bool resolve, CancellationToken ct);
    Task<SupportTicketResult> ReadAsync(SupportActor actor, Guid id, bool staff, CancellationToken ct, long afterMessageVersion = 0);
    Task<SupportTicketPage> PageAsync(SupportActor actor, bool staff, SupportTicketStatus? status, int page, int pageSize, CancellationToken ct);
}
public sealed class SupportTicketService(ISupportTicketStore store, CommercialActorAccess actors)
{
    public async Task<SupportTicketResult> CreateAsync(SupportActor actor, CreateSupportTicketCommand command, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (command.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.Subject) || command.Subject.Length > 200 ||
            string.IsNullOrWhiteSpace(command.Category) || command.Category.Length > 64) throw new SupportException(SupportErrorCodes.Invalid);
        ValidateMessage(command.Body, command.FileAssetIds);
        return await store.CreateAsync(actor, command, ct);
    }
    public async Task<SupportTicketResult> ReplyAsync(SupportActor actor, Guid id, ReplySupportTicketCommand command, bool staff, CancellationToken ct)
    {
        await Ensure(actor, ct); ValidateMessage(command.Body, command.FileAssetIds);
        if (id == Guid.Empty || command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1) throw new SupportException(SupportErrorCodes.Invalid);
        return await store.ReplyAsync(actor, id, command, staff, ct);
    }
    public async Task<SupportTicketResult> ManageAsync(SupportActor actor, Guid id, ManageSupportTicketCommand command, bool resolve, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (id == Guid.Empty || command.ClientOperationId == Guid.Empty || command.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Length > 500 || !resolve && command.AssignedStaffUserId is null || command.AssignedStaffUserId == Guid.Empty) throw new SupportException(SupportErrorCodes.Invalid);
        return await store.ManageAsync(actor, id, command, resolve, ct);
    }
    public async Task<SupportTicketResult> ReadAsync(SupportActor actor, Guid id, bool staff, CancellationToken ct, long afterMessageVersion = 0)
    { await Ensure(actor, ct); if (afterMessageVersion < 0) throw new SupportException(SupportErrorCodes.Invalid); return await store.ReadAsync(actor, id, staff, ct, afterMessageVersion); }
    public async Task<SupportTicketPage> PageAsync(SupportActor actor, bool staff, SupportTicketStatus? status, int page, int pageSize, CancellationToken ct)
    {
        await Ensure(actor, ct);
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100 || status.HasValue && !Enum.IsDefined(status.Value)) throw new SupportException(SupportErrorCodes.Invalid);
        return await store.PageAsync(actor, staff, status, page, pageSize, ct);
    }
    private Task Ensure(SupportActor actor, CancellationToken ct) => actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
    private static void ValidateMessage(string body, IReadOnlyCollection<Guid>? files)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 8000 || files?.Count > 10 || files?.Contains(Guid.Empty) == true || files?.Distinct().Count() != files?.Count)
            throw new SupportException(SupportErrorCodes.Invalid);
    }
}
