namespace MakanApp.Domain.Platform;

public sealed class PlatformPermission
{
    private PlatformPermission()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
}
