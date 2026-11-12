using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class SchoolApplicationTests
{
    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)]
    [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public void DraftDoesNotInventCapacityAndApprovalIsSeparateFromProvisioning(SchoolApplicationKind kind)
    {
        var now = DateTime.UtcNow;
        var app = SchoolApplication.Create(Guid.NewGuid(), kind, new(null, null, null, null, null, null, null), now);
        Assert.Null(app.StudentCapacity); Assert.Null(app.TeacherCapacity); Assert.Equal(SchoolApplicationStatus.Draft, app.Status);
        Assert.Throws<InvalidOperationException>(() => app.StartReview(now));
        // این فقط آزمون state machine با policy ساختگی تست است؛ Submit عمومی همچنان مسدود به تصمیم Product است.
        app.Submit("test-fixture-policy", now); app.StartReview(now); app.Decide(SchoolApplicationStatus.NeedsChanges, now);
        app.UpdateDraft(new("نام صریح", null, null, null, null, 37, null), now);
        Assert.Equal(37, app.StudentCapacity); Assert.Null(app.TeacherCapacity); Assert.Equal(2, app.Revision);
        app.Submit("test-fixture-policy", now); app.StartReview(now); app.Decide(SchoolApplicationStatus.Approved, now);
        Assert.Throws<InvalidOperationException>(() => app.UpdateDraft(new(null, null, null, null, null, null, null), now));
    }
}
