namespace MakanApp.Infrastructure.Platform;

public sealed class SchoolProvisioningOptions
{
    public const string SectionName = "Provisioning";
    public int InitialManagerInvitationLifetimeDays { get; set; } = 7;
}
