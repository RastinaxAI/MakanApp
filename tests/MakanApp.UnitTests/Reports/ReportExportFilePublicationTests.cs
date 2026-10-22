using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class ReportExportFilePublicationTests
{
    [Fact]
    public void OnlyPendingFileCanReplaceItsUnpublishedStorageKey()
    {
        var now = DateTime.UtcNow;
        var file = FileAsset.CreatePending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.csv",
            "legacy/pending", "text/csv", now, now.AddHours(1));
        file.ReplacePendingStorageKey("attempt/new");
        Assert.Equal("attempt/new", file.StorageKey);
        file.MarkReady(10, new string('A', 64), now);
        Assert.Throws<InvalidOperationException>(() => file.ReplacePendingStorageKey("attempt/rewrite"));
        Assert.Equal("attempt/new", file.StorageKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalFileCannotReplaceItsStorageKey(bool deleted)
    {
        var now = DateTime.UtcNow;
        var file = FileAsset.CreatePending(Guid.NewGuid(), null, Guid.NewGuid(), "report.csv",
            "terminal/original", "text/csv", now, now.AddHours(1));
        if (deleted) file.MarkDeleted(now); else file.MarkRejected(now);
        Assert.Throws<InvalidOperationException>(() => file.ReplacePendingStorageKey("attempt/revive"));
        Assert.Equal("terminal/original", file.StorageKey);
    }
}
