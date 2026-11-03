using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class PersonalAcademicContractBoundaryTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void AssignmentDefinitionAttachmentBoundaryIsEnforcedBeforePublication(int count, bool accepted)
    {
        var classId = Guid.NewGuid(); var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        var version = AssignmentVersion.CreateDraft(AcademicScope.Personal(classId), classId, Guid.NewGuid(),
            1, "تکلیف", "راهنمای تکلیف", now.AddDays(1), false, 1, 10, Guid.NewGuid(), now);
        var files = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        if (accepted) { version.ReplaceAttachments(files, now); Assert.Equal(count, version.Attachments.Count); }
        else Assert.Throws<ArgumentException>(() => version.ReplaceAttachments(files, now));
    }
}
