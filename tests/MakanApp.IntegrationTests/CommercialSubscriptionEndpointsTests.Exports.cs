using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ReportExportReservesWithJobAndConcurrentRetryConsumesOnceAfterSuccessfulGeneration()
    {
        var (actor, organizationId, subscriptionId) = await ReportSetupAsync();
        var operation = Guid.NewGuid();
        var command = new CreateReportExportCommand(ReportExportType.ManagerEnrollments, ReportExportFormat.Csv, new(), operation);
        var created = await Task.WhenAll(PostAsync<ReportExportJobResult>(actor.Client, "/api/v1/reports/exports", command),
            PostAsync<ReportExportJobResult>(actor.Client, "/api/v1/reports/exports", command));
        Assert.Equal(created[0].JobId, created[1].JobId);
        var reservation = await WithDb(db => db.UsageReservations.AsNoTracking().SingleAsync(r => r.ConsumerUserId == actor.UserId && r.OperationId == operation));
        Assert.Equal(QuotaKey.ReportExports, reservation.QuotaKey); Assert.Equal(organizationId, reservation.OwnerId);
        Assert.Equal(UsageReservationStatus.Reserved, reservation.Status);
        using var full = await actor.Client.PostAsJsonAsync("/api/v1/reports/exports", command with { ClientOperationId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode); Assert.Equal(CommerceErrorCodes.QuotaExceeded, await ProblemAsync(full));
        await factory.ProcessAllReportExportsAsync(1000);
        var completed = await GetAsync<ReportExportJobResult>(actor.Client, $"/api/v1/reports/exports/{created[0].JobId}");
        Assert.Equal(ReportExportJobStatus.Completed, completed.Status);
        var replay = await PostAsync<ReportExportJobResult>(actor.Client, "/api/v1/reports/exports", command);
        Assert.Equal(completed.JobId, replay.JobId); Assert.Equal(ReportExportJobStatus.Completed, replay.Status);
        Assert.Equal(1, await WithDb(db => db.UsageLedgerEntries.CountAsync(e => e.SubscriptionId == subscriptionId && e.QuotaKey == QuotaKey.ReportExports
            && e.Status == UsageReservationStatus.Consumed && e.SourceOperationId == operation)));
        using var changed = await actor.Client.PostAsJsonAsync("/api/v1/reports/exports", command with { Filters = new ReportExportFilters(ClassId: Guid.NewGuid()) }, Json);
        Assert.Equal(CommerceErrorCodes.UsageConflict, await ProblemAsync(changed));
    }

    [Fact]
    public async Task ReportExhaustedLeaseReleasesOnlyAfterTerminalFenceAndStaleWorkerCannotConsume()
    {
        var (actor, _, subscriptionId) = await ReportSetupAsync();
        var result = await PostAsync<ReportExportJobResult>(actor.Client, "/api/v1/reports/exports",
            new CreateReportExportCommand(ReportExportType.ManagerEnrollments, ReportExportFormat.Csv, new(), Guid.NewGuid()));
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var staleVersion = await WithDb(async db =>
        {
            var job = await db.ReportExportJobs.SingleAsync(j => j.Id == result.JobId);
            job.Claim(now, now.AddMinutes(1)); await db.SaveChangesAsync(); return job.RowVersion.ToArray();
        });
        factory.Clock.SetUtcNow(now.AddMinutes(2));
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IReportExportJobStore>();
        await store.TryClaimNextAsync(factory.Clock.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(1), 1, default);
        var jobAfter = await store.GetAsync(result.JobId, default);
        Assert.Equal(ReportExportJobStatus.Failed, jobAfter!.Status);
        var reservation = await WithDb(db => db.UsageReservations.AsNoTracking().SingleAsync(r => r.Id == jobAfter.CommerceReservationId));
        Assert.Equal(UsageReservationStatus.Released, reservation.Status);
        Assert.False(await store.TrySaveClaimChangesAsync(jobAfter, staleVersion, default));
        Assert.Equal(0, await WithDb(db => db.UsageLedgerEntries.CountAsync(e => e.SubscriptionId == subscriptionId
            && e.QuotaKey == QuotaKey.ReportExports && e.Status == UsageReservationStatus.Consumed)));
    }

    private async Task<(TestActor Actor, Guid OrganizationId, Guid SubscriptionId)> ReportSetupAsync()
    {
        var actor = await ActorAsync(); var org = await factory.CreateOrganizationAsync("سهمیه خروجی آزمون " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
        await SelectAsync(actor, membership.MembershipId, OrganizationRole.Manager);
        await factory.CreateAcademicClassAsync(org);
        var version = await PlanAsync(SubscriptionOwnerType.Organization, reports: true, exports: 1);
        return (actor, org, await SubscribeAsync(SubscriptionOwner.Organization(org), version));
    }
}
