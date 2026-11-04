using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class PaymentAvailabilityTests
{
    [Fact]
    public async Task UnavailableGatewayNeverClaimsConfiguredOrVerifiedPayment()
    {
        IPaymentGateway gateway = new UnavailablePaymentGateway(); Assert.False(gateway.IsConfigured);
        var created = await gateway.CreatePaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1000m, CurrencyCode.Irr, "آزمون بدون تماس خارجی"), default);
        Assert.Equal(PaymentGatewayCreationOutcome.Unavailable, created.Outcome); Assert.Null(created.PaymentUrl);
        var verified = await gateway.VerifyPaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1000m, CurrencyCode.Irr, null), default);
        Assert.Equal(PaymentGatewayVerificationOutcome.Unavailable, verified.Outcome); Assert.Null(verified.VerifiedAmount);
        Assert.Equal(CommerceErrorCodes.PaymentProviderUnavailable, verified.FailureCode);
    }
}
