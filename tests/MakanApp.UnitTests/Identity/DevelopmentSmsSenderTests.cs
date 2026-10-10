using MakanApp.Infrastructure.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class DevelopmentSmsSenderTests
{
    [Fact]
    public async Task SendOtpCapturesExactCodeForItsChallenge()
    {
        var store = new DevelopmentOtpStore();
        var sender = new DevelopmentSmsSender(store);
        var challengeId = Guid.NewGuid();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);

        await sender.SendOtpAsync(
            "+989121234567",
            challengeId,
            "012345",
            expiresAtUtc,
            CancellationToken.None);

        Assert.True(store.TryGet(challengeId, out var message));
        Assert.NotNull(message);
        Assert.Equal("+989121234567", message.PhoneNumber);
        Assert.Equal("012345", message.Code);
        Assert.Equal(expiresAtUtc, message.ExpiresAtUtc);
    }

    [Fact]
    public async Task SendOtpDoesNotCaptureCodeWhenRequestIsCancelled()
    {
        var store = new DevelopmentOtpStore();
        var sender = new DevelopmentSmsSender(store);
        var challengeId = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendOtpAsync(
            "+989121234567",
            challengeId,
            "012345",
            DateTime.UtcNow.AddMinutes(5),
            cancellationSource.Token));

        Assert.False(store.TryGet(challengeId, out _));
    }
}
