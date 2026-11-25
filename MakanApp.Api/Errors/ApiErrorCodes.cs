namespace MakanApp.Api.Errors;

public static class ApiErrorCodes
{
    public const string RateLimited = "RATE_LIMITED";
    public const string InvalidRequest = "REQUEST_INVALID";
    public const string Forbidden = "AUTH_FORBIDDEN";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string RequestTooLarge = "REQUEST_TOO_LARGE";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
}
