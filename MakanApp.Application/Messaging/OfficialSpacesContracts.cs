using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record OfficialSpaceMetadata(OfficialSourceKind SourceKind, Guid SourceId, Guid OrganizationId,
    ConversationType Purpose, string DisplayLabel, string Version)
{
    public static OfficialSpaceMetadata? From(Conversation c) => c.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
        c.OrganizationId is null || c.AcademicClassId is null && !c.IsOfficialOrganizationSpace ? null :
        new(c.AcademicClassId.HasValue ? OfficialSourceKind.Class : OfficialSourceKind.Organization, c.AcademicClassId ?? c.OrganizationId.Value,
            c.OrganizationId.Value, c.Type, c.Title ?? string.Empty, Convert.ToBase64String(c.RowVersion));
}
public sealed record OfficialSpaceCapabilities(bool CanRead, bool CanSend, bool CanManage);
public sealed record OfficialSpaceEndpointResult(Guid? ConversationId, string State, OfficialSpaceCapabilities Capabilities,
    OfficialSpaceMetadata? Official, int AttemptCount, string? LastFailureCategory);
public sealed record OfficialSpacesPairResult(OfficialSourceKind SourceKind, Guid SourceId, Guid OrganizationId,
    string Version, OfficialSpaceEndpointResult Group, OfficialSpaceEndpointResult Channel);
public sealed record OfficialBackfillPlanRow(OfficialSourceKind SourceKind, Guid SourceId, Guid OrganizationId,
    Guid? GroupCandidateId, Guid? ChannelCandidateId, bool MissingGroup, bool MissingChannel, string Action, bool Conflict);
public sealed record AcademicActivityCardResult(Guid EventId, AcademicActivityKind Kind, Guid SourceId, Guid SourceVersionId,
    int SourceRevision, Guid OrganizationId, Guid ClassId, Guid DestinationConversationId, ConversationType DestinationPurpose,
    string ActorKind, Guid InitiatedByUserId, DateTime OccurredAtUtc, string Title, DateTime? DueAtUtc,
    DateTime? AvailableFromUtc, DateTime? AvailableUntilUtc, string State, bool IsActionable);
public sealed record AcademicActivityDeliveryResult(Guid EventId, ConversationType Purpose, AcademicActivityDeliveryState State,
    Guid? ConversationId, Guid? MessageId, int AttemptCount, string? LastFailureCategory);

public interface IAcademicActivityPublisher
{
    Task RecordAsync(AcademicActivityPublication publication, CancellationToken ct);
}

public interface IOfficialSpacesStore
{
    Task<OfficialSpacesPairResult> ReadAsync(Guid userId, AccessContext context, Guid? classId, CancellationToken ct);
    Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadDeliveryAsync(Guid userId, AccessContext context, Guid classId, Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadSourceDeliveryAsync(Guid userId, AccessContext context, Guid classId,
        AcademicActivityKind kind, Guid sourceVersionId, int revision, CancellationToken ct);
}

public interface IOfficialSpacesWorkHandler
{
    bool Handles(string messageType);
    Task HandleAsync(Guid eventId, string messageType, string payload, CancellationToken ct);
}

public sealed class OfficialSpacesService(IAccessContextResolver contexts, IOfficialSpacesStore store)
{
    public async Task<OfficialSpacesPairResult> ReadAsync(Guid userId, Guid sessionId, Guid? classId, CancellationToken ct) =>
        await store.ReadAsync(userId, await contexts.ResolveAsync(userId, sessionId, ct), classId, ct);
    public async Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadDeliveryAsync(Guid userId, Guid sessionId, Guid classId, Guid eventId, CancellationToken ct) =>
        await store.ReadDeliveryAsync(userId, await contexts.ResolveAsync(userId, sessionId, ct), classId, eventId, ct);
    public async Task<IReadOnlyList<AcademicActivityDeliveryResult>> ReadSourceDeliveryAsync(Guid userId, Guid sessionId, Guid classId,
        AcademicActivityKind kind, Guid sourceVersionId, int revision, CancellationToken ct) =>
        await store.ReadSourceDeliveryAsync(userId, await contexts.ResolveAsync(userId, sessionId, ct), classId, kind, sourceVersionId, revision, ct);
}
