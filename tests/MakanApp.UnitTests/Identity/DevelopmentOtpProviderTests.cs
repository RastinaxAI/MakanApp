using MakanApp.Infrastructure.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class DevelopmentOtpProviderTests
{
    [Fact]
    public async Task RequestOtpGeneratesAndCapturesSixDigitDevelopmentCode()
    {
        var store = new DevelopmentOtpStore();
        var provider = new DevelopmentOtpProvider(store);
        const string phoneNumber = "+989121234567";

        var result = await provider.RequestOtpAsync(
            phoneNumber,
            CancellationToken.None);

        Assert.True(store.TryGet(phoneNumber, out var message));
        Assert.NotNull(message);
        Assert.Equal(phoneNumber, message.PhoneNumber);
        Assert.Equal(result.VerificationCode, message.Code);
        Assert.Equal(6, result.VerificationCode.Length);
        Assert.All(result.VerificationCode, character => Assert.InRange(character, '0', '9'));
        Assert.Equal("Development", result.ProviderStatus);
    }

    [Fact]
    public async Task RequestOtpDoesNotCaptureCodeWhenRequestIsCancelled()
    {
        var store = new DevelopmentOtpStore();
        var provider = new DevelopmentOtpProvider(store);
        const string phoneNumber = "+989121234567";
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.RequestOtpAsync(
            phoneNumber,
            cancellationSource.Token));

        Assert.False(store.TryGet(phoneNumber, out _));
    }
}
