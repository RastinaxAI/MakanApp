namespace MakanApp.Application.Commerce;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public TimeSpan InitiationClaimLease { get; init; } = TimeSpan.FromSeconds(30);
}
