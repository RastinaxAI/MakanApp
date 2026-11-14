namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    private bool _accountDeletionProbe;
    public static MakanAppWebApplicationFactory CreateAccountDeletionProbe()
    {
        var factory = new MakanAppWebApplicationFactory(knowledgeProbe: true) { _accountDeletionProbe = true };
        var name = "MakanApp_DeletionProbe_" + Guid.NewGuid().ToString("N");
        factory._instanceConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, factory._instanceConnectionString);
        return factory;
    }
}
