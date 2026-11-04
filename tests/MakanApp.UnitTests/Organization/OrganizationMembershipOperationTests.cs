using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class OrganizationMembershipOperationTests
{
    [Fact]
    public void ReceiptPreservesScopedIntentAndNormalizesHashWithoutStoringCommandBody()
    {
        var org = Guid.NewGuid(); var actor = Guid.NewGuid(); var membership = Guid.NewGuid(); var operation = Guid.NewGuid();
        var role = Guid.NewGuid(); var now = DateTime.UtcNow;
        var receipt = OrganizationMembershipOperation.Record(org, actor, membership, operation,
            OrganizationMembershipOperationKind.GrantRole, new string('a', 64), role, now);
        Assert.Equal(org, receipt.OrganizationId); Assert.Equal(actor, receipt.ActorUserId); Assert.Equal(membership, receipt.MembershipId);
        Assert.Equal(operation, receipt.ClientOperationId); Assert.Equal(new string('A', 64), receipt.RequestHash);
        Assert.Equal(role, receipt.ResultRoleAssignmentId); Assert.Equal(now, receipt.CompletedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EmptyScopeOrIntentIsRejected(int field)
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray(); ids[field] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => OrganizationMembershipOperation.Record(ids[0], ids[1], ids[2], ids[3],
            OrganizationMembershipOperationKind.GrantRole, new string('A', 64), Guid.NewGuid(), DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(4, true)]
    public void KindAndResultReferenceMustAgree(int kind, bool hasRole)
    {
        Assert.Throws<ArgumentException>(() => OrganizationMembershipOperation.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            (OrganizationMembershipOperationKind)kind, new string('A', 64), hasRole ? Guid.NewGuid() : null, DateTime.UtcNow));
    }

    [Fact]
    public void InvalidHashOrNonUtcTimeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Record(new string('G', 64), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => Record(new string('A', 63), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => Record(new string('A', 64), DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
        static OrganizationMembershipOperation Record(string hash, DateTime time) => OrganizationMembershipOperation.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OrganizationMembershipOperationKind.EndMembership, hash, null, time);
    }
}
