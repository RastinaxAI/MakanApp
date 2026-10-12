namespace MakanApp.Application.Commerce;

public sealed class CommerceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
