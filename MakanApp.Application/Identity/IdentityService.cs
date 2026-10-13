using System.Text.RegularExpressions;
using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public sealed partial class IdentityService(
    IIdentityStore store,
    IIdentitySecurity security,
    IOtpDeliveryProvider otpDeliveryProvider,
    IIdentitySecurityEventRecorder securityEventRecorder,
    TimeProvider timeProvider,
    OtpOptions otpOptions,
    SessionOptions sessionOptions) : IIdentityService
{
    public async Task<RequestOtpResult> RequestOtpAsync(
        RequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var phoneNumber = NormalizePhoneNumber(command.PhoneNumber);
        var requestedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var latestChallenge = await store.GetLatestOtpChallengeAsync(
            phoneNumber,
            cancellationToken);

        if (latestChallenge is not null &&
            latestChallenge.CreatedAtUtc + otpOptions.ResendDelay > requestedAtUtc)
        {
            throw new IdentityException(
                IdentityErrorCodes.OtpRateLimited,
                "برای درخواست مجدد کد کمی صبر کنید.");
        }

        var recentCount = await store.CountOtpChallengesAsync(
            phoneNumber,
            requestedAtUtc - otpOptions.RateLimitWindow,
            cancellationToken);

        if (recentCount >= otpOptions.MaxRequestsPerWindow)
        {
            throw new IdentityException(
                IdentityErrorCodes.OtpRateLimited,
                "تعداد درخواست‌های کد بیش از حد مجاز است.");
        }

        var delivery = await otpDeliveryProvider.RequestOtpAsync(
            phoneNumber,
            cancellationToken);
        var code = NormalizeOtpCode(delivery.VerificationCode);
        if (code is null)
        {
            throw new IdentityException(
                IdentityErrorCodes.SmsProviderUnavailable,
                "سرویس ارسال پیامک پاسخ قابل استفاده‌ای برنگرداند.");
        }

        var deliveredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        latestChallenge?.Supersede(deliveredAtUtc);
        var salt = security.GenerateSalt();
        var expiresAtUtc = deliveredAtUtc + otpOptions.Lifetime;
        var challenge = OtpChallenge.Create(
            phoneNumber,
            security.HashOtp(code, salt),
            salt,
            deliveredAtUtc,
            expiresAtUtc,
            otpOptions.MaxFailedAttempts);

        store.Add(challenge);
        await store.SaveChangesAsync(cancellationToken);

        return new RequestOtpResult(
            challenge.Id,
            expiresAtUtc,
            deliveredAtUtc + otpOptions.ResendDelay);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(
        VerifyOtpCommand command,
        CancellationToken cancellationToken)
    {
        var phoneNumber = NormalizePhoneNumber(command.PhoneNumber);
        var code = NormalizeOtpCode(command.Code);
        if (code is null)
        {
            throw OtpException(OtpVerificationResult.Invalid);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var challenge = await store.GetOtpChallengeAsync(
            command.ChallengeId,
            phoneNumber,
            cancellationToken);

        if (challenge is null)
        {
            throw OtpException(OtpVerificationResult.Invalid);
        }

        var verificationResult = challenge.Verify(
            security.HashOtp(code, challenge.Salt),
            nowUtc);

        if (verificationResult != OtpVerificationResult.Valid)
        {
            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw OtpException(verificationResult);
        }

        var credential = await store.GetMobileCredentialAsync(phoneNumber, cancellationToken);
        User user;
        if (credential is null)
        {
            user = User.Create(nowUtc);
            credential = UserCredential.CreateMobile(user.Id, phoneNumber, nowUtc);
            store.Add(user);
            store.Add(credential);
        }
        else
        {
            user = await store.GetUserAsync(credential.UserId, cancellationToken)
                ?? throw new InvalidOperationException("کاربر مرتبط با روش ورود پیدا نشد.");
        }

        if (user.AccountStatus == UserAccountStatus.Suspended)
        {
            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new IdentityException(
                IdentityErrorCodes.AccountSuspended,
                "حساب کاربری معلق است.");
        }

        var rawToken = security.GenerateSessionToken();
        var rawRefreshToken = security.GenerateRefreshToken();
        var expiresAtUtc = nowUtc + sessionOptions.AccessLifetime;
        var refreshExpiresAtUtc = nowUtc + sessionOptions.RefreshLifetime;
        var session = UserSession.Create(
            user.Id,
            security.HashSessionToken(rawToken),
            nowUtc,
            expiresAtUtc,
            refreshExpiresAtUtc,
            NormalizeDeviceName(command.DeviceName));
        var refreshToken = RefreshToken.Create(
            session.Id,
            security.HashRefreshToken(rawRefreshToken),
            nowUtc,
            refreshExpiresAtUtc);
        store.Add(session);
        store.Add(refreshToken);

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new VerifyOtpResult(
            rawToken,
            "Bearer",
            expiresAtUtc,
            rawRefreshToken,
            refreshExpiresAtUtc,
            await BuildCurrentUserAsync(user, phoneNumber, cancellationToken));
    }

    public async Task<RefreshSessionResult> RefreshSessionAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken) || command.RefreshToken.Length > 512)
        {
            throw RefreshInvalid();
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var currentToken = await store.GetRefreshTokenForUpdateAsync(
            security.HashRefreshToken(command.RefreshToken.Trim()),
            cancellationToken);
        if (currentToken is null)
        {
            throw RefreshInvalid();
        }

        var session = await store.GetSessionByIdAsync(currentToken.SessionId, cancellationToken)
            ?? throw RefreshInvalid();
        var familyTokens = await store.GetRefreshTokensAsync(session.Id, cancellationToken);
        if (currentToken.UsedAtUtc.HasValue)
        {
            session.Revoke(nowUtc);
            foreach (var familyToken in familyTokens)
            {
                familyToken.Revoke(nowUtc);
            }

            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await securityEventRecorder.RecordRefreshTokenReuseAsync(
                session.UserId,
                session.Id,
                nowUtc,
                cancellationToken);
            throw new IdentityException(
                IdentityErrorCodes.RefreshTokenReused,
                "refresh token قبلاً استفاده شده و نشست برای حفاظت از حساب لغو شد.");
        }

        var user = await store.GetUserAsync(session.UserId, cancellationToken);
        var absoluteExpiresAtUtc = AsUtc(session.AbsoluteExpiresAtUtc);
        if (!currentToken.IsUsable(nowUtc) ||
            session.RevokedAtUtc.HasValue ||
            nowUtc >= absoluteExpiresAtUtc ||
            user?.AccountStatus != UserAccountStatus.Active)
        {
            session.Revoke(nowUtc);
            foreach (var familyToken in familyTokens)
            {
                familyToken.Revoke(nowUtc);
            }

            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw RefreshInvalid();
        }

        var rawAccessToken = security.GenerateSessionToken();
        var rawRefreshToken = security.GenerateRefreshToken();
        var accessExpiresAtUtc = Min(
            nowUtc + sessionOptions.AccessLifetime,
            absoluteExpiresAtUtc);
        var replacement = RefreshToken.Create(
            session.Id,
            security.HashRefreshToken(rawRefreshToken),
            nowUtc,
            absoluteExpiresAtUtc);
        currentToken.MarkUsed(replacement.Id, nowUtc);
        session.RotateAccessToken(
            security.HashSessionToken(rawAccessToken),
            accessExpiresAtUtc);
        store.Add(replacement);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RefreshSessionResult(
            rawAccessToken,
            "Bearer",
            accessExpiresAtUtc,
            rawRefreshToken,
            absoluteExpiresAtUtc);
    }

    public async Task<SessionIdentity?> AuthenticateAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var session = await store.GetSessionByTokenHashAsync(
            security.HashSessionToken(rawToken),
            cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (session is null || !session.IsActive(nowUtc))
        {
            return null;
        }

        var user = await store.GetUserAsync(session.UserId, cancellationToken);
        return user?.AccountStatus == UserAccountStatus.Active
            ? new SessionIdentity(session.UserId, session.Id)
            : null;
    }

    public async Task<CurrentUserResult> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await store.GetUserAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();
        var credential = await store.GetMobileCredentialByUserIdAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();

        return await BuildCurrentUserAsync(
            user,
            credential.NormalizedIdentifier,
            cancellationToken);
    }

    public async Task<CurrentUserResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileCommand command,
        CancellationToken cancellationToken)
    {
        var firstName = NormalizeRequiredName(command.FirstName);
        var lastName = NormalizeRequiredName(command.LastName);
        var displayName = NormalizeDisplayName(command.DisplayName);
        var username = NormalizeUsername(command.Username, out var normalizedUsername);
        var user = await store.GetUserAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();

        if (await store.UsernameExistsAsync(
                normalizedUsername,
                userId,
                cancellationToken))
        {
            throw new IdentityException(
                IdentityErrorCodes.UsernameAlreadyExists,
                "این نام کاربری قبلاً استفاده شده است.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        Person person;
        if (user.PersonId.HasValue)
        {
            person = await store.GetPersonAsync(user.PersonId.Value, cancellationToken)
                ?? throw new InvalidOperationException("پروفایل مرتبط با کاربر پیدا نشد.");
            person.Update(firstName, lastName, displayName, nowUtc);
        }
        else
        {
            person = Person.Create(firstName, lastName, displayName, nowUtc);
            store.Add(person);
        }

        user.SetProfile(person.Id, username, normalizedUsername, nowUtc);
        await store.SaveChangesAsync(cancellationToken);

        var credential = await store.GetMobileCredentialByUserIdAsync(userId, cancellationToken)
            ?? throw AuthRequiredException();
        return await BuildCurrentUserAsync(
            user,
            credential.NormalizedIdentifier,
            cancellationToken);
    }

    public async Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken);
        if (session is null)
        {
            throw AuthRequiredException();
        }

        session.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        foreach (var refreshToken in await store.GetRefreshTokensAsync(session.Id, cancellationToken))
        {
            refreshToken.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        }
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserSessionResult>> GetSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await store.GetSessionsAsync(userId, cancellationToken);
        return sessions
            .Where(session => !session.RevokedAtUtc.HasValue && nowUtc < AsUtc(session.AbsoluteExpiresAtUtc))
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => new UserSessionResult(
                session.Id,
                session.DeviceName,
                AsUtc(session.CreatedAtUtc),
                AsUtc(session.ExpiresAtUtc),
                AsUtc(session.AbsoluteExpiresAtUtc),
                session.Id == currentSessionId))
            .ToArray();
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken)
            ?? throw new IdentityException(
                IdentityErrorCodes.SessionNotFound,
                "نشست پیدا نشد.");
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        session.Revoke(nowUtc);
        foreach (var refreshToken in await store.GetRefreshTokensAsync(session.Id, cancellationToken))
        {
            refreshToken.Revoke(nowUtc);
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await store.GetSessionsAsync(userId, cancellationToken);
        foreach (var session in sessions.Where(session => session.Id != currentSessionId))
        {
            session.Revoke(nowUtc);
            foreach (var refreshToken in await store.GetRefreshTokensAsync(session.Id, cancellationToken))
            {
                refreshToken.Revoke(nowUtc);
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<CurrentUserResult> BuildCurrentUserAsync(
        User user,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        Person? person = null;
        if (user.PersonId.HasValue)
        {
            person = await store.GetPersonAsync(user.PersonId.Value, cancellationToken);
        }

        return new CurrentUserResult(
            user.Id,
            phoneNumber,
            user.IsProfileComplete,
            person?.FirstName,
            person?.LastName,
            person?.DisplayName,
            user.Username);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalized))
        {
            throw new IdentityException(
                IdentityErrorCodes.PhoneInvalid,
                "شماره تلفن معتبر نیست.");
        }

        return normalized;
    }

    private string? NormalizeOtpCode(string? value)
    {
        var candidate = value?.Trim();
        if (candidate is null ||
            candidate.Length < otpOptions.MinimumCodeLength ||
            candidate.Length > otpOptions.MaximumCodeLength)
        {
            return null;
        }

        Span<char> normalized = stackalloc char[candidate.Length];
        for (var index = 0; index < candidate.Length; index++)
        {
            normalized[index] = PhoneNumberNormalizer.ToAsciiDigit(candidate[index]);

            if (char.IsControl(normalized[index]))
            {
                return null;
            }
        }

        return new string(normalized);
    }

    private static string NormalizeRequiredName(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
        {
            throw ProfileValidationException();
        }


        return normalized;
    }

    private static string? NormalizeDisplayName(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > 200)
        {
            throw ProfileValidationException();
        }

        return normalized;
    }

    private static string NormalizeUsername(string? value, out string normalizedUsername)
    {
        var username = value?.Trim();
        if (string.IsNullOrWhiteSpace(username) ||
            username.Length is < 3 or > 32 ||
            !UsernamePattern().IsMatch(username))
        {
            throw ProfileValidationException();
        }

        normalizedUsername = username.ToUpperInvariant();
        return username;
    }

    private static IdentityException OtpException(OtpVerificationResult result) => result switch
    {
        OtpVerificationResult.Expired => new(
            IdentityErrorCodes.OtpExpired,
            "کد یک‌بارمصرف منقضی شده است."),
        OtpVerificationResult.AlreadyUsed => new(
            IdentityErrorCodes.OtpAlreadyUsed,
            "این کد یک‌بارمصرف قبلاً استفاده شده است."),
        OtpVerificationResult.TooManyAttempts => new(
            IdentityErrorCodes.OtpTooManyAttempts,
            "تعداد تلاش‌های ناموفق بیش از حد مجاز است."),
        _ => new IdentityException(
            IdentityErrorCodes.OtpInvalid,
            "کد یک‌بارمصرف معتبر نیست.")
    };

    private static IdentityException AuthRequiredException() =>
        new(IdentityErrorCodes.AuthRequired, "برای ادامه باید وارد شوید.");

    private static IdentityException RefreshInvalid() =>
        new(IdentityErrorCodes.RefreshTokenInvalid, "refresh token معتبر یا فعال نیست.");

    private string? NormalizeDeviceName(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length > sessionOptions.MaximumDeviceNameLength ||
            normalized.Any(char.IsControl))
        {
            throw new IdentityException(
                IdentityErrorCodes.SessionDeviceInvalid,
                "نام دستگاه معتبر نیست.");
        }

        return normalized;
    }

    private static DateTime Min(DateTime first, DateTime second) =>
        first <= second ? first : second;

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static IdentityException ProfileValidationException() =>
        new(
            IdentityErrorCodes.ProfileValidationFailed,
            "اطلاعات پروفایل معتبر نیست.");

    [GeneratedRegex("^[A-Za-z0-9._]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
