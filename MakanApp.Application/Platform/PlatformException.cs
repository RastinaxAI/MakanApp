namespace MakanApp.Application.Platform;

public sealed class PlatformException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
