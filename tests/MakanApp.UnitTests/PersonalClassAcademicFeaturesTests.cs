using MakanApp.Application.Assessment;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class PersonalClassAcademicFeaturesTests
{
    [Fact]
    public void DisabledPersonalNotificationDoesNotSuppressSchoolOrMessengerNotifications()
    {
        var features = new PersonalClassAcademicFeatures();
        Assert.False(features.AllowsNotification(null, MakanApp.Domain.Notification.NotificationResourceType.SubmissionAttempt));
        Assert.False(features.AllowsNotification(null, MakanApp.Domain.Notification.NotificationResourceType.ExamAttempt));
        Assert.True(features.AllowsNotification(Guid.NewGuid(), MakanApp.Domain.Notification.NotificationResourceType.Exam));
        Assert.True(features.AllowsNotification(null, MakanApp.Domain.Notification.NotificationResourceType.Conversation));
    }
    [Fact]
    public void DefaultsDenyBothProposalsAndMaskExistingGrants()
    {
        var features = new PersonalClassAcademicFeatures();
        Assert.False(features.AnyEnabled);
        Assert.Equal(CommerceErrorCodes.FeatureNotEntitled, Assert.Throws<CommerceException>(features.EnsureAssignments).Code);
        Assert.Throws<CommerceException>(features.EnsureExams);
        Assert.Equal(PersonalClassAcademicCapabilities.None, features.Effective(
            PersonalClassAcademicCapabilities.AuthorAssignments | PersonalClassAcademicCapabilities.AuthorExams | PersonalClassAcademicCapabilities.Grade));
        features.EnsureCapabilities(PersonalClassAcademicCapabilities.None);
    }

    [Theory]
    [InlineData(AcademicResourceKind.Assignment)]
    [InlineData(AcademicResourceKind.SubmissionAttempt)]
    public void EnablingExamsDoesNotEnableAssignmentResources(AcademicResourceKind kind)
    {
        var features = new PersonalClassAcademicFeatures { ExamsEnabled = true };
        features.EnsureExams();
        Assert.Throws<CommerceException>(() => features.EnsureResource(kind, AcademicOperation.Read));
        Assert.Throws<CommerceException>(() => features.EnsureCapabilities(PersonalClassAcademicCapabilities.AuthorAssignments));
    }

    [Theory]
    [InlineData(AcademicResourceKind.Exam)]
    [InlineData(AcademicResourceKind.ExamAttempt)]
    public void EnablingAssignmentsDoesNotEnableExamResources(AcademicResourceKind kind)
    {
        var features = new PersonalClassAcademicFeatures { AssignmentsEnabled = true };
        features.EnsureAssignments();
        Assert.Throws<CommerceException>(() => features.EnsureResource(kind, AcademicOperation.Learn));
    }
}
