using MakanApp.Domain.Knowledge;
using Xunit;

namespace MakanApp.UnitTests.Knowledge;

public sealed class EducationalSourceTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(EducationalSourceScope.Personal)]
    [InlineData(EducationalSourceScope.Class)]
    [InlineData(EducationalSourceScope.Organization)]
    [InlineData(EducationalSourceScope.Library)]
    public void SourceHasTypedOwnerAndIndependentStableIdentity(EducationalSourceScope scope)
    {
        var actor = Guid.NewGuid(); var org = scope is EducationalSourceScope.Class or EducationalSourceScope.Organization ? Guid.NewGuid() : (Guid?)null;
        var source = EducationalSource.Create(scope, actor, org, scope == EducationalSourceScope.Class ? Guid.NewGuid() : null, Guid.NewGuid(), new string('A', 64), Now);
        Assert.NotEqual(Guid.Empty, source.Id); Assert.Equal(scope == EducationalSourceScope.Personal ? actor : null, source.OwnerUserId);
        Assert.Equal(org, source.OrganizationId); Assert.Equal(1, source.CurrentVersion); Assert.Null(source.PublishedVersion);
    }
    [Theory]
    [InlineData(EducationalSourceScope.Personal, true, false)]
    [InlineData(EducationalSourceScope.Library, true, false)]
    [InlineData(EducationalSourceScope.Class, false, true)]
    [InlineData(EducationalSourceScope.Class, true, false)]
    [InlineData(EducationalSourceScope.Organization, false, false)]
    [InlineData(EducationalSourceScope.Organization, true, true)]
    public void ContradictoryOwnershipIsRejected(EducationalSourceScope scope, bool org, bool classroom) =>
        Assert.Throws<ArgumentException>(() => EducationalSource.Create(scope, Guid.NewGuid(), org ? Guid.NewGuid() : null,
            classroom ? Guid.NewGuid() : null, Guid.NewGuid(), new string('B', 64), Now));
    [Theory]
    [InlineData(SourceSafetyStatus.PendingValidation, false)]
    [InlineData(SourceSafetyStatus.ReviewRequired, false)]
    [InlineData(SourceSafetyStatus.Rejected, false)]
    [InlineData(SourceSafetyStatus.FileValidated, true)]
    public void PhysicalReadinessNeverBypassesSafety(SourceSafetyStatus safety, bool ready)
    {
        var version = Version(); version.RecordFileValidation(safety);
        Assert.False(version.IsSourceReady(false)); Assert.Equal(ready, version.IsSourceReady(true));
        if (!ready) Assert.Throws<InvalidOperationException>(() => version.Publish(true, Now));
    }
    [Theory]
    [InlineData(SourceSafetyStatus.Rejected)]
    [InlineData(SourceSafetyStatus.ReviewRequired)]
    public void SafetyDecisionCannotBeSilentlyApproved(SourceSafetyStatus state)
    {
        var version = Version(); version.RecordFileValidation(state);
        Assert.Throws<InvalidOperationException>(() => version.RecordFileValidation(SourceSafetyStatus.FileValidated));
    }
    [Fact]
    public void PublishedRevisionCannotRewriteHistoricalMetadataAndNewRevisionKeepsSourceIdentity()
    {
        var source = EducationalSource.Create(EducationalSourceScope.Personal, Guid.NewGuid(), null, null, Guid.NewGuid(), new string('C',64), Now);
        var version = Version(source.Id); version.RecordFileValidation(SourceSafetyStatus.FileValidated); version.Publish(true, Now); source.Publish(version, Now);
        Assert.Throws<InvalidOperationException>(() => version.UpdateDraft("تغییر تاریخچه", null));
        var id = source.Id; source.AdvanceVersion(Now.AddSeconds(1));
        Assert.Equal(id, source.Id); Assert.Equal(1, source.PublishedVersion); Assert.Equal(2, source.CurrentVersion);
        Assert.Equal("منبع ساختگی", version.Title);
        source.AdvanceVersion(Now.AddSeconds(2)); Assert.Equal(3, source.CurrentVersion); Assert.Equal(1, source.PublishedVersion);
    }
    [Fact]
    public void ProcessingFailureKeepsNonAiFileUsableWithoutInventingSuccessfulAiProcessing()
    {
        var version = Version(); version.RecordFileValidation(SourceSafetyStatus.FileValidated);
        Assert.Equal(SourceProcessingStatus.NotRequested, version.ProcessingStatus);
        version.RecordProcessing(SourceProcessingStatus.Failed, "SOURCE_PROCESSING_FAILED");
        Assert.True(version.IsSourceReady(true)); Assert.Equal("SOURCE_PROCESSING_FAILED", version.ProcessingFailureCode);
        Assert.Throws<ArgumentException>(() => version.RecordProcessing(SourceProcessingStatus.Failed, "private provider body"));
    }
    [Fact]
    public void ArchivePreservesVersionIdentityAndRejectsLaterMutation()
    {
        var source = EducationalSource.Create(EducationalSourceScope.Personal, Guid.NewGuid(), null, null, Guid.NewGuid(), new string('D',64), Now);
        var id = source.Id; source.Archive(Now.AddSeconds(1));
        Assert.Equal(id, source.Id); Assert.Equal(1, source.CurrentVersion); Assert.Equal(EducationalSourceStatus.Archived, source.Status);
        Assert.Throws<InvalidOperationException>(() => source.Touch(Now.AddSeconds(2)));
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTitleIsRejected(string title) => Assert.Throws<ArgumentException>(() => Version().UpdateDraft(title, null));
    [Fact]
    public void PersianTitleNormalizationPreservesDisplayAndSupportsCatalogSearch()
    {
        var version = Version(); version.UpdateDraft("  كتاب فارسي‌ما  ", null);
        Assert.Equal("كتاب فارسي‌ما", version.Title); Assert.Equal("کتاب فارسی ما", version.NormalizedTitle);
    }
    private static EducationalSourceVersion Version(Guid? id = null) => EducationalSourceVersion.Create(id ?? Guid.NewGuid(), 1,
        Guid.NewGuid(), Guid.NewGuid(), "منبع ساختگی", null, EducationalSourceType.Document, Now);
}
