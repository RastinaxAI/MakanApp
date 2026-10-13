namespace MakanApp.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid sessionId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        SessionId = sessionId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static RefreshToken Create(
        Guid sessionId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (sessionId == Guid.Empty || tokenHash.Length != 32 || expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("مشخصات refresh token معتبر نیست.");
        }

        return new RefreshToken(Guid.NewGuid(), sessionId, tokenHash, createdAtUtc, expiresAtUtc);
    }

    public bool IsUsable(DateTime nowUtc) =>
        !UsedAtUtc.HasValue && !RevokedAtUtc.HasValue && nowUtc < ExpiresAtUtc;

    public void MarkUsed(Guid replacementTokenId, DateTime usedAtUtc)
    {
        EnsureUtc(usedAtUtc, nameof(usedAtUtc));
        if (!IsUsable(usedAtUtc) || replacementTokenId == Guid.Empty)
        {
            throw new InvalidOperationException("refresh token قابل چرخش نیست.");
        }

        UsedAtUtc = usedAtUtc;
        ReplacedByTokenId = replacementTokenId;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        RevokedAtUtc ??= revokedAtUtc;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان refresh token باید UTC باشد.", parameterName);
        }
    }
}
