namespace MakanApp.Domain.Platform;

public sealed class PlatformRole
{
    private PlatformRole()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
}
