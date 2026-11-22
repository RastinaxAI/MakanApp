namespace MakanApp.Application.Commerce;

public sealed class CommercialOptions
{
    public bool EnforcementEnabled { get; set; } = true;
    public int QuoteLifetimeMinutes { get; set; } = 15;
    public bool RenewalAfterGraceStartsAtPayment { get; set; } = true;
}
