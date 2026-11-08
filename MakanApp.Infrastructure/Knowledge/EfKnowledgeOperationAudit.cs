using System.Text.Json;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Knowledge;

public sealed class EfKnowledgeOperationAudit(MakanDbContext db, TimeProvider clock) : IKnowledgeOperationAudit
{
    public async Task WriteAsync(KnowledgeOperationAudit entry, CancellationToken ct)
    {
        if (entry.Operation is not ("ingest" or "delete" or "supersede" or "reindex"))
            throw new InvalidOperationException("نوع عملیات دانش معتبر نیست.");
        db.AdminAuditLogs.Add(AdminAuditLog.Create(entry.Actor.UserId, null, entry.Actor.SessionId,
            "knowledge.ai." + entry.Operation, "EducationalSourceVersion", $"{entry.Source.SourceId:D}/{entry.Source.Version}",
            entry.Actor.Organization?.OrganizationId, entry.Reason, clock.GetUtcNow().UtcDateTime, entry.CorrelationId,
            AdminAuditResult.Succeeded, null, JsonSerializer.Serialize(new
            {
                entry.OperationId, entry.RequestId, entry.Outcome, entry.Source.SourceId, entry.Source.Version
            })));
        await db.SaveChangesAsync(ct);
    }
}
