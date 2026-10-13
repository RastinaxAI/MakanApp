using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class RefreshTokenTests
{
    [Fact]
    public void RawTokenIsNotPartOfPersistedModelAndTokenCanBeUsedOnlyOnce()
    {
        var nowUtc = DateTime.UtcNow;
        var replacementId = Guid.NewGuid();
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            Enumerable.Repeat((byte)7, 32).ToArray(),
            nowUtc,
            nowUtc.AddDays(30));

        token.MarkUsed(replacementId, nowUtc.AddMinutes(1));

        Assert.False(token.IsUsable(nowUtc.AddMinutes(2)));
        Assert.Equal(replacementId, token.ReplacedByTokenId);
        Assert.Throws<InvalidOperationException>(() =>
            token.MarkUsed(Guid.NewGuid(), nowUtc.AddMinutes(2)));
        Assert.DoesNotContain(
            typeof(RefreshToken).GetProperties(),
            property => property.Name.Equals("RawToken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExpiredOrRevokedTokenIsNotUsable()
    {
        var nowUtc = DateTime.UtcNow;
        var expired = RefreshToken.Create(
            Guid.NewGuid(),
            Enumerable.Repeat((byte)1, 32).ToArray(),
            nowUtc.AddDays(-2),
            nowUtc.AddDays(-1));
        var revoked = RefreshToken.Create(
            Guid.NewGuid(),
            Enumerable.Repeat((byte)2, 32).ToArray(),
            nowUtc,
            nowUtc.AddDays(1));

        revoked.Revoke(nowUtc);

        Assert.False(expired.IsUsable(nowUtc));
        Assert.False(revoked.IsUsable(nowUtc));
    }
}
