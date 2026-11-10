using MakanApp.Application.Commerce;

namespace MakanApp.Infrastructure.Commerce;

public sealed class PaymentGatewayResolver(IPaymentGateway current, ZibalPaymentGateway zibal, UnavailablePaymentGateway unavailable)
    : IPaymentGatewayResolver
{
    public IPaymentGateway? Resolve(string providerName) =>
        current.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase) ? current :
        providerName.Equals(ZibalPaymentGateway.Name, StringComparison.OrdinalIgnoreCase) ? zibal :
        providerName.Equals(unavailable.ProviderName, StringComparison.OrdinalIgnoreCase) ? unavailable : null;
}
