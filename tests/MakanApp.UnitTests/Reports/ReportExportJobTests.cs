using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class ReportExportJobTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewJobIsQueuedAndCanCompleteOnlyAfterClaim()
    {
        var job = CreateJob();

        Assert.Equal(ReportExportJobStatus.Queued, job.Status);
        Assert.Throws<InvalidOperationException>(() => job.Complete(Guid.NewGuid(), NowUtc));

        job.Claim(NowUtc, NowUtc.AddMinutes(2));
        var fileId = Guid.NewGuid();
        job.Complete(fileId, NowUtc.AddSeconds(1));

        Assert.Equal(ReportExportJobStatus.Completed, job.Status);
        Assert.Equal(fileId, job.OutputFileAssetId);
        Assert.Null(job.LeaseExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => job.Claim(NowUtc.AddMinutes(3), NowUtc.AddMinutes(5)));
    }

    [Fact]
    public void ActiveLeaseCannotBeClaimedButExpiredLeaseCanBeRecovered()
    {
        var job = CreateJob();
        job.Claim(NowUtc, NowUtc.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => job.Claim(NowUtc.AddMinutes(1), NowUtc.AddMinutes(3)));

        job.Claim(NowUtc.AddMinutes(3), NowUtc.AddMinutes(5));

        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(ReportExportJobStatus.Processing, job.Status);
    }

    [Fact]
    public void RetryAndFailureTransitionsRemainFinite()
    {
        var job = CreateJob();
        job.Claim(NowUtc, NowUtc.AddMinutes(2));
        job.ScheduleRetry(NowUtc.AddMinutes(1));

        Assert.Equal(ReportExportJobStatus.Queued, job.Status);
        Assert.Equal(NowUtc.AddMinutes(1), job.NextAttemptAtUtc);

        job.Claim(NowUtc.AddMinutes(2), NowUtc.AddMinutes(4));
        job.Fail("REPORT_GENERATION_FAILED", NowUtc.AddMinutes(3));

        Assert.Equal(ReportExportJobStatus.Failed, job.Status);
        Assert.Equal("REPORT_GENERATION_FAILED", job.FailureCode);
        Assert.Throws<InvalidOperationException>(() => job.ScheduleRetry(NowUtc.AddMinutes(4)));
    }

    private static ReportExportJob CreateJob() => ReportExportJob.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        OrganizationRole.Manager,
        null,
        ReportExportType.ManagerAttendance,
        ReportExportFormat.Csv,
        NowUtc,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "classtitleasc",
        0,
        NowUtc);
}
