using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task ResetPlatformIamAsync()
    {
        PlatformStepUp.SetSatisfied(true);
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await dbContext.AdminAuditLogs.ExecuteDeleteAsync();
        await dbContext.PlatformRoleAssignments.ExecuteDeleteAsync();
    }

    public async Task<PlatformBootstrapResult> BootstrapPlatformOwnerAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformBootstrapService>();
        return await service.BootstrapOwnerAsync(
            userId,
            $"test-bootstrap-{Guid.NewGuid():N}",
            CancellationToken.None);
    }

    public async Task<int> CountActivePlatformOwnersAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.PlatformRoleAssignments.CountAsync(item =>
            item.RoleId == PlatformCatalogIds.PlatformOwnerRole &&
            item.Status == PlatformRoleAssignmentStatus.Active &&
            item.EndedAtUtc == null);
    }

    public async Task<int> CountAdminAuditLogsAsync(string action)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AdminAuditLogs.CountAsync(item => item.Action == action);
    }

    public async Task<bool> AdminAuditRejectsDeleteAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var audit = await dbContext.AdminAuditLogs.FirstAsync();
        dbContext.AdminAuditLogs.Remove(audit);
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public async Task<Guid> CreatePlatformSafetyCaseAsync(
        Guid conversationId,
        Guid messageId,
        Guid reporterUserId,
        Guid reportedUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var message = await dbContext.Messages.AsNoTracking()
            .SingleAsync(item => item.Id == messageId);
        var nowUtc = DateTime.UtcNow;
        var report = AbuseReport.Create(
            reporterUserId,
            reportedUserId,
            conversationId,
            messageId,
            Guid.NewGuid(),
            AbuseReportReason.Harassment,
            "privacy regression",
            message.Kind,
            message.Text,
            message.RowVersion,
            new byte[32],
            nowUtc);
        var moderationCase = ModerationCase.CreateForReport(
            conversationId,
            report.Id,
            ModerationQueueScope.PlatformSafety,
            ModerationPriority.Normal,
            nowUtc);
        dbContext.AbuseReports.Add(report);
        dbContext.ModerationCases.Add(moderationCase);
        await dbContext.SaveChangesAsync();
        return moderationCase.Id;
    }

    public async Task AttachPlatformSafetyEvidenceAsync(Guid caseId, Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.ModerationEvidenceAttachments.Add(ModerationEvidenceAttachment.Create(
            caseId,
            fileAssetId,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsMessageDeletedAsync(Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Messages.AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(message => message.DeletedAtUtc != null)
            .SingleAsync();
    }
}

public sealed class PlatformStepUpTestControl
{
    private int _satisfied = 1;

    public bool IsSatisfied => Volatile.Read(ref _satisfied) == 1;

    public void SetSatisfied(bool satisfied) =>
        Interlocked.Exchange(ref _satisfied, satisfied ? 1 : 0);
}

public sealed class TestPlatformStepUpAuthorization(PlatformStepUpTestControl control)
    : IPlatformStepUpAuthorization, IPlatformMfaAssurance
{
    // تست‌های legacy IAM عامل امنیتی را شبیه‌سازی می‌کنند؛ suite جدید MFA هر دو port را واقعی جایگزین می‌کند.
    Task<bool> IPlatformMfaAssurance.IsSatisfiedAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<bool> IsSatisfiedAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        Task.FromResult(control.IsSatisfied);
}
