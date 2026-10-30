using System.Diagnostics;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

internal sealed record OfficialSpaceWork(Guid OrganizationId, Guid? ClassId, string? TraceId = null);

public sealed class OfficialSpaceEventWriter(MakanDbContext db, IOutbox outbox, TimeProvider clock) : IAcademicActivityPublisher
{
    public const string EnsureType = "messaging.official-spaces.ensure.v1";
    public const string SyncType = "messaging.official-spaces.sync.v1";
    public const string PublicationType = "messaging.academic-activity.published.v1";

    public async Task RecordAsync(AcademicActivityPublication publication, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("ثبت فعالیت باید در تراکنش انتشار منبع باشد.");
        if (db.AcademicActivityPublications.Local.Any(p => p.Kind == publication.Kind && p.SourceRevision == publication.SourceRevision &&
            p.LessonId == publication.LessonId && p.AssignmentVersionId == publication.AssignmentVersionId && p.ExamVersionId == publication.ExamVersionId) ||
            await db.AcademicActivityPublications.AnyAsync(p => p.OrganizationId == publication.OrganizationId && p.ClassId == publication.ClassId &&
                p.Kind == publication.Kind && p.SourceRevision == publication.SourceRevision && p.LessonId == publication.LessonId &&
                p.AssignmentVersionId == publication.AssignmentVersionId && p.ExamVersionId == publication.ExamVersionId, ct)) return;
        db.AcademicActivityPublications.Add(publication);
        db.AcademicActivityDeliveries.AddRange(AcademicActivityDelivery.Pending(publication.Id, ConversationType.Group),
            AcademicActivityDelivery.Pending(publication.Id, ConversationType.Channel));
        outbox.Enqueue(new(publication.Id, PublicationType, JsonSerializer.Serialize(new { PublicationId = publication.Id,
            TraceId = Activity.Current?.TraceId.ToString() }), publication.OccurredAtUtc));
    }

    public void RequestPair(Guid organizationId, Guid? classId, DateTime now)
    {
        foreach (var purpose in new[] { ConversationType.Group, ConversationType.Channel })
            db.OfficialSpaceProvisions.Add(OfficialSpaceProvision.Request(organizationId, classId, purpose, now));
        Enqueue(EnsureType, organizationId, classId, now);
    }

    // فقط Storeهای دامنهٔ مدرسه/سازمان این capture را در تراکنش موجود اجرا می‌کنند؛ hook سراسری DbContext نیست.
    public async Task CaptureSchoolAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var sync = new HashSet<(Guid OrganizationId, Guid? ClassId)>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            switch (entry.Entity)
            {
                case MakanApp.Domain.Organization.Organization org when entry.State == EntityState.Added:
                    RequestPair(org.Id, null, clock.GetUtcNow().UtcDateTime); break;
                case MakanApp.Domain.Academic.Class classroom when entry.State == EntityState.Added:
                    RequestPair(classroom.OrganizationId, classroom.Id, clock.GetUtcNow().UtcDateTime); break;
                case MakanApp.Domain.Organization.Organization org: sync.Add((org.Id, null)); break;
                case MakanApp.Domain.Academic.Class classroom: sync.Add((classroom.OrganizationId, classroom.Id)); break;
                case Enrollment enrollment: sync.Add((enrollment.OrganizationId, enrollment.ClassId)); break;
                case TeacherAssignment assignment: sync.Add((assignment.OrganizationId, assignment.ClassId)); break;
                case Membership member: sync.Add((member.OrganizationId, null)); break;
                case RoleAssignment role:
                    var membership = db.Memberships.Local.SingleOrDefault(x => x.Id == role.MembershipId)
                        ?? await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == role.MembershipId, ct);
                    if (membership is not null) sync.Add((membership.OrganizationId, null));
                    break;
            }
        }
        foreach (var item in sync) Enqueue(SyncType, item.OrganizationId, item.ClassId, clock.GetUtcNow().UtcDateTime);
    }

    private void Enqueue(string type, Guid organizationId, Guid? classId, DateTime now) =>
        outbox.Enqueue(new(Guid.NewGuid(), type, JsonSerializer.Serialize(new OfficialSpaceWork(organizationId, classId, Activity.Current?.TraceId.ToString())), now));
}
