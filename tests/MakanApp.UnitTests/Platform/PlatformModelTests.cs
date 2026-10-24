using MakanApp.Domain.Platform;
using Xunit;

namespace MakanApp.UnitTests.Platform;

public sealed class PlatformModelTests
{
    [Fact]
    public void RoleAssignmentUsesUserIdentityAndHasExplicitLifecycle()
    {
        var nowUtc = DateTime.SpecifyKind(new DateTime(2026, 10, 13, 6, 0, 0), DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();

        var assignment = PlatformRoleAssignment.CreateActive(
            userId,
            PlatformCatalogIds.PlatformOwnerRole,
            actorUserId,
            nowUtc);
        assignment.End(actorUserId, nowUtc.AddMinutes(1));

        Assert.Equal(userId, assignment.UserId);
        Assert.False(assignment.IsActive);
        Assert.Equal(PlatformRoleAssignmentStatus.Ended, assignment.Status);
        Assert.Equal(actorUserId, assignment.EndedByUserId);
    }

    [Fact]
    public void RoleAssignmentRejectsNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() => PlatformRoleAssignment.CreateActive(
            Guid.NewGuid(),
            PlatformCatalogIds.PlatformOwnerRole,
            null,
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)));
    }

    [Fact]
    public void AdminAuditRequiresBoundedReasonAndCorrelationId()
    {
        var nowUtc = DateTime.SpecifyKind(new DateTime(2026, 10, 13, 6, 0, 0), DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => AdminAuditLog.Create(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "platform.role.grant",
            "PlatformRoleAssignment",
            Guid.NewGuid().ToString("D"),
            null,
            string.Empty,
            nowUtc,
            "trace",
            AdminAuditResult.Succeeded,
            null,
            null));
    }

    [Fact]
    public void Step15ACatalogContainsOnlyServerOwnedPermissionKeys()
    {
        Assert.Equal(7, PlatformRoleKeys.All.Count);
        Assert.Equal(5, PlatformPermissionKeys.Step15A.Count);
        Assert.DoesNotContain(
            "platform.private_chats.read_all",
            PlatformPermissionKeys.Step15A,
            StringComparer.Ordinal);
    }

}
