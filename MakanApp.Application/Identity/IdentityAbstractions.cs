using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public interface IIdentityService
{
    Task<RequestOtpResult> RequestOtpAsync(
        RequestOtpCommand command,
        CancellationToken cancellationToken);

    Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyOtpCommand command,
        CancellationToken cancellationToken);

    Task<RefreshSessionResult> RefreshSessionAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken);

    Task<SessionIdentity?> AuthenticateAsync(
        string rawToken,
        CancellationToken cancellationToken);

    Task<CurrentUserResult> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<CurrentUserResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileCommand command,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserSessionResult>> GetSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);
}

public sealed record OtpDeliveryResult(
    string VerificationCode,
    string? ProviderStatus = null);

public interface IOtpDeliveryProvider
{
    Task<OtpDeliveryResult> RequestOtpAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);
}

public interface IIdentitySecurity
{
    byte[] GenerateSalt();
    byte[] HashOtp(string code, byte[] salt);
    string GenerateSessionToken();
    byte[] HashSessionToken(string rawToken);
    string GenerateRefreshToken();
    byte[] HashRefreshToken(string rawToken);
}

public interface IIdentitySecurityEventRecorder
{
    Task RecordRefreshTokenReuseAsync(
        Guid userId,
        Guid sessionId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken);
}

public interface IIdentityTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IIdentityStore
{
    Task<IIdentityTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<OtpChallenge?> GetLatestOtpChallengeAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<OtpChallenge?> GetOtpChallengeAsync(
        Guid challengeId,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<int> CountOtpChallengesAsync(
        string normalizedPhoneNumber,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken);

    Task<UserCredential?> GetMobileCredentialAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<UserCredential?> GetMobileCredentialByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> GetUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfilePhotoResult?> GetProfilePhotoAsync(Guid userId, CancellationToken cancellationToken);
    Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken);

    Task<UserSession?> GetSessionByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<RefreshToken?> GetRefreshTokenForUpdateAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        Guid exceptUserId,
        CancellationToken cancellationToken);

    void Add(OtpChallenge challenge);
    void Add(User user);
    void Add(Person person);
    void Add(UserCredential credential);
    void Add(UserSession session);
    void Add(RefreshToken refreshToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
