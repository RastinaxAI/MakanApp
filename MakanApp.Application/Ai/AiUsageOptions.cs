using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Ai;

public enum AiUsageMode { Commercial = 1, Unmetered = 2 }

// این سیاست فقط شرط تجاری را تعیین می‌کند؛ مجوز داده و نشست همچنان مستقل‌اند.
public sealed class AiUsageOptions
{
    public const string SectionName = "AiUsage";
    public AiUsageMode Mode { get; set; } = AiUsageMode.Commercial;
    public bool IsUnmetered => Mode == AiUsageMode.Unmetered;

    public AiPayerSelection? NormalizePayer(AiPayerSelection? payer)
    {
        if (payer.HasValue && !Enum.IsDefined(payer.Value))
            throw new AiServiceException(AiServiceErrorCodes.InvalidRequest, "انتخاب پرداخت‌کننده معتبر نیست.");
        return IsUnmetered ? null : payer;
    }
}
