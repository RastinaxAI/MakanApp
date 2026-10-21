using System.Text.RegularExpressions;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    internal MakanAppWebApplicationFactory(string developmentTestDatabaseName)
    {
        if (!Regex.IsMatch(developmentTestDatabaseName, "^MakanApp_DevelopmentTestUsers_[a-f0-9]{32}\\z"))
            throw new ArgumentException("نام دیتابیس مستقل تست seed معتبر نیست.");
        _instanceConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=" + developmentTestDatabaseName +
            ";Trusted_Connection=True;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _instanceConnectionString);
    }
}
