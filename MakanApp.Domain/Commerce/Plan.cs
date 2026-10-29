namespace MakanApp.Domain.Commerce;

public sealed partial class Plan
{
    public const int MaximumCodeLength = 100;
    public const int MaximumDisplayNameLength = 200;

    private Plan()
    {
    }

    private Plan(Guid id, string code, string displayName, DateTime createdAtUtc)
    {
        Id = id;
        Code = code;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Plan Create(string code, string displayName, DateTime createdAtUtc, SubscriptionOwnerType audience = SubscriptionOwnerType.Organization)
    {
        var normalizedCode = code?.Trim();
        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) ||
            normalizedCode.Length > MaximumCodeLength ||
            normalizedCode.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException("کد پلن معتبر نیست.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(normalizedDisplayName) ||
            normalizedDisplayName.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentException("نام نمایشی پلن معتبر نیست.", nameof(displayName));
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (!Enum.IsDefined(audience)) throw new ArgumentOutOfRangeException(nameof(audience));
        return new Plan(Guid.NewGuid(), normalizedCode, normalizedDisplayName, createdAtUtc) { Audience = audience };
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پلن باید UTC باشد.", parameterName);
        }
    }
}
