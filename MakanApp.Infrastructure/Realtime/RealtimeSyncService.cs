using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure.Realtime;

public sealed class RealtimeSyncService(MakanDbContext db, IAccessContextResolver contexts,
    ISchoolReadStore school, IMessagingService messaging, IRealtimeCursorProtector protection,
    IServiceScopeFactory scopes, TimeProvider clock, RealtimeTelemetry telemetry, AssessmentRealtimeAccess assessment,
    MakanApp.Application.Platform.IPlatformPermissionResolver platform)
    : IRealtimeSyncService, IRealtimeInvalidationAudience, IRealtimeSessionScope
{
    public const string Epoch = "outbox-v1";
    public static readonly TimeSpan CursorLifetime = TimeSpan.FromHours(24);
    private readonly IRealtimeCursorProtector _protector = protection;

    public async Task<AccountChangePageResult> ReadAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct, ConversationScope? scope = null)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try { var result = await ReadCoreAsync(userId, sessionId, cursor, limit, ct, scope); outcome = "success"; return result; }
        catch (RealtimeException e) { outcome = e.Code == RealtimeErrorCodes.ResyncRequired ? "resync" : "invalid"; throw; }
        catch (IdentityException) { outcome = "denied"; throw; }
        catch (SqlException e) when (e.Number is 1205 or -2) { throw new RealtimeException(RealtimeErrorCodes.Unavailable); }
        finally { telemetry.Replay(outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private async Task<AccountChangePageResult> ReadCoreAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct, ConversationScope? scope)
    {
        if (limit is < 1 or > 200 || scope.HasValue && !Enum.IsDefined(scope.Value)) throw new RealtimeException(RealtimeErrorCodes.InvalidQuery);
        var binding = await BindAsync(userId, sessionId, ct, scope);
        if (binding.Context is null) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
        if (scope == ConversationScope.Organization && binding.Context.WorkspaceType != WorkspaceType.Organization)
            throw new RealtimeException(RealtimeErrorCodes.InvalidQuery);
        // مرز exclusive، transaction باز و دیرCommitشده را از replay حذف نمی‌کند.
        var upper = await db.Database.SqlQuery<byte[]>($"SELECT CONVERT(binary(8), MIN_ACTIVE_ROWVERSION()) AS [Value]").SingleAsync(ct);
        if (cursor is null)
        {
            var start = Encode(binding, upper);
            return new([], start, false, start, Epoch, binding.Revision);
        }
        var lower = Decode(cursor, binding);
        if (BinaryPrimitives.ReadUInt64BigEndian(lower) > BinaryPrimitives.ReadUInt64BigEndian(upper))
            throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
        var orgId = binding.Context.OrganizationId;
        var role = binding.Context.ActiveRole;
        var membershipId = binding.Context.MembershipId;
        // فقط cut کوتاه SQL در تراکنش است؛ مجوز و هر شبکه‌ای پس از آزادشدن lockها اجرا می‌شوند.
        await using var scan = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var rows = await db.Database.SqlQuery<FeedRow>($"""
            SELECT TOP ({limit + 1}) [EventId], [Revision], [MessagingChangeId], [Payload], [OccurredAtUtc]
            FROM (
                SELECT q.[ChangeEventId] AS [EventId], q.[RowVersion] AS [Revision], q.[ChangeEventId] AS [MessagingChangeId],
                       CAST(NULL AS nvarchar(max)) AS [Payload], q.[OccurredAtUtc]
                FROM [messaging].[RealtimeOutbox] q
                JOIN [messaging].[ChangeEvents] e ON e.[Id] = q.[ChangeEventId]
                JOIN [messaging].[Conversations] c ON c.[Id] = e.[ConversationId]
                WHERE EXISTS (SELECT 1 FROM [messaging].[ConversationParticipants] p
                    WHERE p.[ConversationId] = c.[Id] AND p.[UserId] = {userId}
                      AND p.[Status] = {(int)ConversationParticipantStatus.Active} AND p.[EndedAtUtc] IS NULL)
                  AND ((c.[Scope] = {(int)ConversationScope.Personal} AND {orgId} IS NULL)
                    OR (c.[Scope] = {(int)ConversationScope.Organization} AND c.[OrganizationId] = {orgId}))
                  AND (e.[AudienceUserId] IS NULL OR e.[AudienceUserId] = {userId})
                UNION ALL
                SELECT q.[Id], q.[RowVersion], CAST(NULL AS uniqueidentifier), q.[Payload], q.[OccurredAtUtc]
                FROM [platform].[OutboxMessages] q WHERE q.[MessageType] = {RealtimeInvalidationWriter.MessageType}
                  AND ((JSON_VALUE(CASE WHEN ISJSON(q.[Payload]) = 1 THEN q.[Payload] ELSE 'null' END, '$.Key') = 'knowledge.library'
                        AND JSON_VALUE(q.[Payload], '$.TargetUserId') IS NULL)
                    OR JSON_VALUE(CASE WHEN ISJSON(q.[Payload]) = 1 THEN q.[Payload] ELSE 'null' END, '$.TargetUserId') = {userId.ToString()}
                    OR (JSON_VALUE(CASE WHEN ISJSON(q.[Payload]) = 1 THEN q.[Payload] ELSE 'null' END, '$.OrganizationId') = {(orgId == null ? null : orgId.Value.ToString())}
                      AND JSON_VALUE(q.[Payload], '$.TargetUserId') IS NULL
                      AND ({role == OrganizationRole.Manager} = 1
                        OR ({role == OrganizationRole.Teacher} = 1 AND JSON_VALUE(q.[Payload], '$.Key') IN ('school.class','school.roster','school.lesson','school.schedule',
                            'assessment.assignment','assessment.exam','assessment.submission','assessment.grade','assessment.exam-grade','knowledge.source')
                          AND EXISTS (SELECT 1 FROM [academic].[TeacherAssignments] t WHERE t.[OrganizationId] = {orgId}
                            AND t.[ClassId] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(q.[Payload], '$.ClassId'))
                            AND t.[TeacherMembershipId] = {membershipId} AND t.[Status] = {(int)TeacherAssignmentStatus.Active} AND t.[EndedAtUtc] IS NULL))
                        OR ({role == OrganizationRole.Student} = 1 AND JSON_VALUE(q.[Payload], '$.StaffOnly') = 'false'
                          AND JSON_VALUE(q.[Payload], '$.Key') IN ('school.class','school.lesson','school.schedule',
                            'assessment.assignment','assessment.exam','assessment.submission','assessment.grade','assessment.exam-grade','assessment.exam-attempt','knowledge.source')
                          AND EXISTS (SELECT 1 FROM [academic].[Enrollments] e JOIN [organization].[OrganizationPersons] p ON p.[Id] = e.[LearnerOrganizationPersonId]
                            JOIN [identity].[Users] u ON u.[PersonId] = p.[PersonId]
                            WHERE u.[Id] = {userId} AND e.[OrganizationId] = {orgId} AND p.[OrganizationId] = {orgId}
                              AND p.[Status] = {(int)OrganizationPersonStatus.Active} AND p.[EndedAtUtc] IS NULL
                              AND e.[ClassId] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(q.[Payload], '$.ClassId'))
                              AND e.[Status] = {(int)EnrollmentStatus.Active} AND e.[EndedAtUtc] IS NULL
                              AND (JSON_VALUE(q.[Payload], '$.LearnerOrganizationPersonId') IS NULL
                                OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(q.[Payload], '$.LearnerOrganizationPersonId')) = p.[Id])))
                        OR ({role == OrganizationRole.Parent} = 1 AND JSON_VALUE(q.[Payload], '$.StaffOnly') = 'false'
                          AND ((JSON_VALUE(q.[Payload], '$.Key') IN ('assessment.grade','assessment.exam-grade')
                            AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(q.[Payload], '$.LearnerOrganizationPersonId')) = {binding.Context.SubjectOrganizationPersonId})
                            OR (JSON_VALUE(q.[Payload], '$.Key') = 'assessment.assignment' AND EXISTS (
                              SELECT 1 FROM [academic].[Enrollments] e WHERE e.[OrganizationId] = {orgId}
                                AND e.[ClassId] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(q.[Payload], '$.ClassId'))
                                AND e.[LearnerOrganizationPersonId] = {binding.Context.SubjectOrganizationPersonId}
                                AND e.[Status] = {(int)EnrollmentStatus.Active} AND e.[EndedAtUtc] IS NULL)))))))
            ) feed
            WHERE [Revision] >= {lower}
            ORDER BY [Revision]
            """).ToArrayAsync(ct);
        await scan.CommitAsync(ct);
        // tail ثبت‌شده ولی جلوتر از cut امن، به معنی «کاملاً بازیابی شد» نیست؛ claim نیز rowversion را عوض می‌کند.
        var stable = rows.TakeWhile(row => BinaryPrimitives.ReadUInt64BigEndian(row.Revision) < BinaryPrimitives.ReadUInt64BigEndian(upper)).ToArray();
        var pendingCut = stable.Length < rows.Length;
        var hasMore = stable.Length > limit || pendingCut;
        var scanned = stable.Take(limit).ToArray();
        var changes = new List<AccountChangeResult>(scanned.Length);
        foreach (var row in scanned)
        {
            ct.ThrowIfCancellationRequested();
            var version = Convert.ToHexString(row.Revision);
            if (row.MessagingChangeId is Guid changeId)
            {
                var notification = await AuthorizedConversationAsync(binding, changeId, ct);
                if (notification is not null) changes.Add(new(row.EventId, version, notification, null));
            }
            else
            {
                var envelope = Deserialize(row.Payload!);
                if (await AllowsAsync(binding, envelope, ct))
                    changes.Add(new(row.EventId, version, null, Notification(binding, envelope, row.EventId, version, row.OccurredAtUtc)));
            }
        }
        // Context عوض‌شده هنگام Query نباید پاسخ موفقِ بافت قبلی بگیرد.
        var current = await BindAsync(userId, sessionId, ct, scope);
        if (binding.Revision != current.Revision) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
        var next = stable.Length > limit ? Increment(scanned[^1].Revision) : upper;
        return new(changes, Encode(binding, next), hasMore, Encode(binding, upper), Epoch, binding.Revision,
            pendingCut && scanned.Length < limit ? 100 : null);
    }

    public async IAsyncEnumerable<(Guid SessionId, RealtimeInvalidation Notification)> ResolveAsync(Guid eventId, string payload,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var envelope = Deserialize(payload);
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == eventId && x.MessageType == RealtimeInvalidationWriter.MessageType, ct);
        Guid? after = null;
        while (true)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var query = db.UserSessions.AsNoTracking().Where(s => s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now &&
                (envelope.TargetUserId.HasValue ? s.UserId == envelope.TargetUserId : envelope.Key == "knowledge.library" || s.SelectedMembershipId != null &&
                    db.Memberships.Any(m => m.Id == s.SelectedMembershipId && m.OrganizationId == envelope.OrganizationId)));
            if (after is Guid id) query = query.Where(s => s.Id.CompareTo(id) > 0);
            var page = await query.OrderBy(s => s.Id).Select(s => new { s.Id, s.UserId }).Take(128).ToArrayAsync(ct);
            if (page.Length == 0) yield break;
            after = page[^1].Id;
            foreach (var session in page)
            {
                // Tracking عملیات قبلی یا گروه قدیمی به‌جای مجوز جاری استفاده نمی‌شود.
                await using var scope = scopes.CreateAsyncScope();
                var fresh = scope.ServiceProvider.GetRequiredService<RealtimeSyncService>();
                Binding? binding = null;
                try { binding = await fresh.BindAsync(session.UserId, session.Id, ct,
                    envelope.Key == "knowledge.source" && envelope.TargetUserId.HasValue ? ConversationScope.Personal : null); }
                catch (IdentityException) { }
                if (binding is not null && await fresh.AllowsAsync(binding, envelope, ct))
                    yield return (session.Id, Notification(binding, envelope, eventId, Convert.ToHexString(row.RowVersion), row.OccurredAtUtc));
                else telemetry.Denied();
            }
        }
    }

    private async Task<MessagingRealtimeNotification?> AuthorizedConversationAsync(Binding binding, Guid changeId, CancellationToken ct)
    {
        var change = await db.MessagingChangeEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == changeId, ct);
        if (change is null) return null;
        try
        {
            var page = await messaging.GetConversationChangesAsync(binding.UserId, binding.SessionId, change.ConversationId,
                new GetConversationChangesQuery(MessagingChangeCursor.Encode(change.ConversationId, change.ChangeSequence - 1), 1), ct);
            var visible = page.Changes.SingleOrDefault(c => c.ChangeId == changeId);
            return visible is null ? null : new(change.ConversationId, visible.Type, visible.ResourceId, visible.ResourceVersion,
                visible.Cursor, visible.OccurredAtUtc, visible.PayloadVersion, visible.PeerReadThroughSequence, binding.Revision);
        }
        catch (Exception e) when (e is MessagingException or OrganizationException or GuardianException) { return null; }
    }

    private async Task<bool> AllowsAsync(Binding binding, InvalidationEnvelope item, CancellationToken ct)
    {
        if (item.Key == "access") return item.TargetUserId == binding.UserId && item.ResourceId is null && item.OrganizationId is null;
        if (item.Key == "knowledge.library")
            return item.ResourceId is null && item.OrganizationId is null && item.ClassId is null &&
                (!item.TargetUserId.HasValue || item.TargetUserId == binding.UserId &&
                    await platform.HasPermissionAsync(binding.UserId, MakanApp.Domain.Platform.PlatformPermissionKeys.LibrarySourcesManage, ct));
        if (item.Key == "knowledge.source")
        {
            var source = await db.EducationalSources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == item.ResourceId, ct);
            if (source is null) return false;
            if (source.Scope == MakanApp.Domain.Knowledge.EducationalSourceScope.Personal)
                return item.TargetUserId == binding.UserId && source.OwnerUserId == binding.UserId && item.OrganizationId is null;
            var context = binding.Context;
            if (context is null || context.OrganizationId != source.OrganizationId || item.OrganizationId != source.OrganizationId ||
                item.ClassId != source.ClassId || item.TargetUserId.HasValue) return false;
            if (source.Scope == MakanApp.Domain.Knowledge.EducationalSourceScope.Organization)
                return await school.CanManageOrganizationAsync(context, ct);
            return source.ClassId is Guid sourceClassId && (await school.CanManageClassAsync(context, sourceClassId, ct) ||
                !item.StaffOnly && source.PublishedVersion.HasValue && await school.CanStudyClassAsync(context, sourceClassId, ct));
        }
        var c = binding.Context;
        if (c is null) return false;
        if (item.Key.StartsWith("assessment.", StringComparison.Ordinal))
            return c.WorkspaceType == WorkspaceType.Organization && c.OrganizationId == item.OrganizationId &&
                (!item.TargetUserId.HasValue || item.TargetUserId == binding.UserId) && await assessment.AllowsAsync(c, item, ct);
        if (item.TargetUserId is Guid owner)
            return owner == binding.UserId && c.WorkspaceType == WorkspaceType.Personal && item.OrganizationId is null && item.Key == "commerce";
        if (c.WorkspaceType != WorkspaceType.Organization || c.OrganizationId != item.OrganizationId) return false;
        if (await school.CanManageOrganizationAsync(c, ct)) return item.Key is "commerce" or "school.directory" or "school.guardians" or "school.roster" or "school.class" or "school.lesson" or "school.schedule";
        if (item.ClassId is not Guid classId) return false;
        if (await school.CanManageClassAsync(c, classId, ct)) return item.Key is "school.roster" or "school.class" or "school.lesson" or "school.schedule";
        return !item.StaffOnly && (item.Key is "school.class" or "school.lesson" or "school.schedule") && await school.CanStudyClassAsync(c, classId, ct);
    }

    public async Task<string?> AuthorizeConversationAsync(Guid sessionId, MessagingRealtimeNotification notification, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var fresh = scope.ServiceProvider.GetRequiredService<RealtimeSyncService>();
        var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var owner = await database.UserSessions.AsNoTracking().Where(s => s.Id == sessionId).Select(s => (Guid?)s.UserId).SingleOrDefaultAsync(ct);
        if (!owner.HasValue || !MessagingChangeCursor.TryDecode(notification.Cursor, notification.ConversationId, out var sequence)) return null;
        try
        {
            var sourceScope = await database.Conversations.AsNoTracking().Where(c => c.Id == notification.ConversationId).Select(c => (ConversationScope?)c.Scope).SingleOrDefaultAsync(ct);
            var operationScope = sourceScope == ConversationScope.Personal ? ConversationScope.Personal : (ConversationScope?)null;
            var binding = await fresh.BindAsync(owner.Value, sessionId, ct, operationScope);
            if (notification.ContextRevision is not null && notification.ContextRevision != binding.Revision) return null;
            var id = await database.MessagingChangeEvents.AsNoTracking().Where(e => e.ConversationId == notification.ConversationId &&
                e.ChangeSequence == sequence && e.ResourceId == notification.ResourceId && e.ChangeType == notification.Type).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);
            if (binding.Context is null || !id.HasValue || await fresh.AuthorizedConversationAsync(binding, id.Value, ct) is null) return null;
            var current = await fresh.BindAsync(owner.Value, sessionId, ct, operationScope);
            return current.Revision == binding.Revision ? binding.Revision : null;
        }
        catch (IdentityException) { telemetry.Denied(); return null; }
    }

    private async Task<Binding> BindAsync(Guid userId, Guid sessionId, CancellationToken ct, ConversationScope? operationScope = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var session = await db.UserSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId &&
            s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now &&
            db.Users.Any(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active), ct)
            ?? throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        AccessContext? c;
        // نشست و Account بالا از SQL اثبات شده‌اند؛ فقط درخواست صریح Personal بافت موقت می‌سازد.
        try { c = operationScope == ConversationScope.Personal ? new(userId, userId, sessionId, WorkspaceType.Personal, null, null, null, null)
            : await contexts.ResolveAsync(userId, sessionId, ct); }
        catch (Exception e) when (e is OrganizationException or GuardianException) { c = null; }
        var parts = new List<string> { userId.ToString(), sessionId.ToString(), Convert.ToHexString(session.RowVersion),
            session.SelectedMembershipId.ToString() ?? "", session.SelectedRole ?? "", session.SelectedSubjectOrganizationPersonId.ToString() ?? "", c is null ? "invalid" : "valid" };
        parts.Add(c?.WorkspaceType == WorkspaceType.Personal ? "PersonalOperation" : "SelectedOrganization");
        if (c?.MembershipId is Guid membershipId)
        {
            var membership = await db.Memberships.AsNoTracking().SingleAsync(m => m.Id == membershipId, ct);
            parts.Add(Convert.ToHexString(membership.RowVersion));
            var role = await db.RoleAssignments.AsNoTracking().Where(r => r.MembershipId == membershipId && r.Role == c.ActiveRole &&
                r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null).Select(r => r.RowVersion).SingleOrDefaultAsync(ct);
            parts.Add(role is null ? "missing" : Convert.ToHexString(role));
        }
        if (c?.SubjectOrganizationPersonId is Guid childId)
        {
            var relations = await db.GuardianRelations.AsNoTracking().Where(r => r.OrganizationId == c.OrganizationId &&
                r.GuardianUserId == userId && r.LearnerOrganizationPersonId == childId && r.EndedAtUtc == null &&
                r.Status == MakanApp.Domain.Guardian.GuardianRelationStatus.Active && r.ValidFromUtc <= now)
                .OrderBy(r => r.Id).Select(r => r.RowVersion).ToArrayAsync(ct);
            parts.AddRange(relations.Select(Convert.ToHexString));
        }
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(':', parts))));
        return new(userId, sessionId, c, revision);
    }

    private string Encode(Binding binding, byte[] lower) => _protector.Protect(JsonSerializer.Serialize(
        new Cursor(binding.UserId, binding.SessionId, binding.Revision, Convert.ToHexString(lower), clock.GetUtcNow().UtcDateTime, Epoch)));

    private byte[] Decode(string token, Binding binding)
    {
        if (token.Length > 4096) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
        try
        {
            var value = JsonSerializer.Deserialize<Cursor>(_protector.Unprotect(token));
            if (value is null) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
            var age = clock.GetUtcNow().UtcDateTime - value.IssuedAtUtc;
            if (value.UserId != binding.UserId || value.SessionId != binding.SessionId || value.ContextRevision != binding.Revision ||
                value.Epoch != Epoch || value.IssuedAtUtc.Kind != DateTimeKind.Utc || age < TimeSpan.Zero || age > CursorLifetime || value.Lower is not { Length: 16 })
                throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
            return Convert.FromHexString(value.Lower);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        { throw new RealtimeException(RealtimeErrorCodes.ResyncRequired); }
    }

    private static byte[] Increment(byte[] value)
    {
        var next = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(next, checked(BinaryPrimitives.ReadUInt64BigEndian(value) + 1));
        return next;
    }
    private static InvalidationEnvelope Deserialize(string payload) => JsonSerializer.Deserialize<InvalidationEnvelope>(payload)
        ?? throw new JsonException("قرارداد invalidation معتبر نیست.");
    private static RealtimeInvalidation Notification(Binding binding, InvalidationEnvelope item, Guid id, string version, DateTime occurredAtUtc) =>
        new(id, item.Key, item.ResourceId, binding.Revision, version, DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), TraceId: item.TraceId);
    private sealed record Binding(Guid UserId, Guid SessionId, AccessContext? Context, string Revision);
    private sealed record Cursor(Guid UserId, Guid SessionId, string ContextRevision, string Lower, DateTime IssuedAtUtc, string Epoch);
    private sealed class FeedRow
    {
        public Guid EventId { get; set; }
        public byte[] Revision { get; set; } = [];
        public Guid? MessagingChangeId { get; set; }
        public string? Payload { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }
}
