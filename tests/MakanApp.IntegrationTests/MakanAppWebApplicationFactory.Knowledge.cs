namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public const string KnowledgeProbeDatabasePrefix = "MakanApp_KnowledgeProbe_";

    // نام دیتابیس از ورودی یا config گرفته نمی‌شود؛ هر اجرا فقط پایگاه تازهٔ خودش را می‌سازد و حذف می‌کند.
    public static MakanAppWebApplicationFactory CreateKnowledgeProbe() => new(knowledgeProbe: true);

    private MakanAppWebApplicationFactory(bool knowledgeProbe)
    {
        if (!knowledgeProbe) throw new ArgumentException("محیط مستقل Probe لازم است.");
        var name = KnowledgeProbeDatabasePrefix + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("Otp__Enabled", "true");
        Environment.SetEnvironmentVariable("Otp__AllowMockAuthentication", "false");
        _instanceConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _instanceConnectionString);
    }
}
