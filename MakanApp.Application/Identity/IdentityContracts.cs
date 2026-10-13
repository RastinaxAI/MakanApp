namespace MakanApp.Application.Identity;

public sealed record RequestOtpCommand(string PhoneNumber);

public sealed record RequestOtpResult(
    Guid ChallengeId,
    DateTime ExpiresAtUtc,
    DateTime ResendAvailableAtUtc);

public sealed record VerifyOtpCommand(
    Guid ChallengeId,
    string PhoneNumber,
    string Code,
    string? DeviceName = null);

public sealed record VerifyOtpResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    CurrentUserResult User);

public sealed record RefreshSessionCommand(string RefreshToken);

public sealed record RefreshSessionResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc);

public sealed record UserSessionResult(
    Guid Id,
    string? DeviceName,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime AbsoluteExpiresAtUtc,
    bool IsCurrent);

public sealed record CompleteProfileCommand(
    string FirstName,
    string LastName,
    string? DisplayName,
    string Username);

public sealed record CurrentUserResult(
    Guid Id,
    string PhoneNumber,
    bool IsProfileComplete,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Username);

public sealed record SessionIdentity(Guid UserId, Guid SessionId);
