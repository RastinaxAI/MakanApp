namespace MakanApp.Domain.Platform;

public sealed class PlatformRolePermission
{
    private PlatformRolePermission()
    {
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
}
