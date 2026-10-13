namespace MakanApp.Application.Identity;

public static class IdentityErrorCodes
{
    public const string PhoneInvalid = "PHONE_INVALID";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpAlreadyUsed = "OTP_ALREADY_USED";
    public const string OtpTooManyAttempts = "OTP_TOO_MANY_ATTEMPTS";
    public const string OtpRateLimited = "OTP_RATE_LIMITED";
    public const string AuthRequired = "AUTH_REQUIRED";
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";
    public const string UsernameAlreadyExists = "USERNAME_ALREADY_EXISTS";
    public const string ProfileValidationFailed = "PROFILE_VALIDATION_FAILED";
    public const string SmsProviderAuthenticationFailed = "SMS_PROVIDER_AUTHENTICATION_FAILED";
    public const string SmsProviderConfigurationError = "SMS_PROVIDER_CONFIGURATION_ERROR";
    public const string SmsProviderRejected = "SMS_PROVIDER_REJECTED";
    public const string SmsProviderUnavailable = "SMS_PROVIDER_UNAVAILABLE";
    public const string SmsRecipientInvalid = "SMS_RECIPIENT_INVALID";
    public const string SmsRecipientBlocked = "SMS_RECIPIENT_BLOCKED";
    public const string SmsRateLimited = "SMS_RATE_LIMITED";
}
