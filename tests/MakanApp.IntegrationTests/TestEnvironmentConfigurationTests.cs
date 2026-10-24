using System.Security.Cryptography;
using MakanApp.Api.Configuration;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Platform;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Identity.Otp.MeliPayamak;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using SessionOptions = MakanApp.Application.Identity.SessionOptions;

namespace MakanApp.IntegrationTests;

// تغییر متغیرهای محیطی باید با سایر تست‌های میزبان در یک collection سریالی باشد.
[Collection(SqlServerCollection.Name)]
public sealed class TestEnvironmentConfigurationTests : IDisposable
{
    private readonly string _configurationDirectory = Path.Combine(
        Path.GetTempPath(), "MakanApp-TestConfiguration", Guid.NewGuid().ToString("N"));

    public TestEnvironmentConfigurationTests()
    {
        // فقط الگوهای عمومی کپی می‌شوند؛ هیچ Secret یا فایل خصوصی وارد میزبان این تست نمی‌شود.
        Directory.CreateDirectory(_configurationDirectory);
        var source = SourceApiDirectory();
        File.Copy(Path.Combine(source, "appsettings.json"), Path.Combine(_configurationDirectory, "appsettings.json"));
        File.Copy(Path.Combine(source, "appsettings.Production.json.example"),
            Path.Combine(_configurationDirectory, "appsettings.Production.json.example"));
        File.Copy(Path.Combine(source, "appsettings.Test.json.example"),
            Path.Combine(_configurationDirectory, "appsettings.Test.json"));
    }

    public void Dispose() => Directory.Delete(_configurationDirectory, recursive: true);

    [Fact]
    public void DefaultBuilder_LoadsBaseThenTestWithoutLoadingProductionOrDevelopment()
    {
        using var variables = new EnvironmentScope();
        var builder = Builder();
        using var configuration = builder.Configuration;
        var jsonFiles = ((IConfigurationRoot)configuration).Providers.OfType<JsonConfigurationProvider>()
            .Select(provider => provider.Source.Path).ToArray();

        Assert.Equal("Test", builder.Environment.EnvironmentName);
        Assert.True(Array.IndexOf(jsonFiles, "appsettings.json") >= 0);
        Assert.True(Array.IndexOf(jsonFiles, "appsettings.Test.json") > Array.IndexOf(jsonFiles, "appsettings.json"));
        Assert.DoesNotContain("appsettings.Production.json", jsonFiles);
        Assert.DoesNotContain("appsettings.Development.json", jsonFiles);
        Assert.True(configuration.GetValue<bool>("Swagger:Enabled"));
        Assert.False(OtpAuthenticationOptions.Load(configuration, "Test").Enabled);
    }

    [Fact]
    public void BaseOnlySections_KeepTheirExistingValuesAfterLoadingTest()
    {
        using var baseline = new ConfigurationManager();
        baseline.SetBasePath(ApiDirectory()).AddJsonFile("appsettings.json");
        using var configuration = FileConfiguration();

        foreach (var section in new[] { "AiService", "Reports", "Identity:Session", "Notifications:Delivery" })
        {
            var expected = baseline.GetSection(section).AsEnumerable().OrderBy(item => item.Key).ToArray();
            var actual = configuration.GetSection(section).AsEnumerable().OrderBy(item => item.Key).ToArray();
            // فقط برابری بررسی می‌شود؛ هیچ مقدار بالقوهٔ محرمانه در پیام assertion چاپ نمی‌شود.
            Assert.True(expected.SequenceEqual(actual), $"تنظیم ارث‌رسیدهٔ {section} تغییر کرده است.");
        }
    }

    [Theory]
    [InlineData("Test", null, "Test")]
    [InlineData("Production", "Test", "Test")]
    [InlineData("Test", "Production", "Production")]
    public void EnvironmentSelection_UsesDotnetEnvironmentWhenItIsPresent(string aspnet, string? dotnet, string expected)
    {
        using var variables = new EnvironmentScope(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = aspnet,
            ["DOTNET_ENVIRONMENT"] = dotnet
        });
        var builder = Builder();
        using var configuration = builder.Configuration;
        Assert.Equal(expected, builder.Environment.EnvironmentName);
        if (expected == "Production")
            Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, expected));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnvironmentAndCommandLineOverrides_KeepTheirDefaultPrecedence(bool commandLine)
    {
        using var variables = new EnvironmentScope(new Dictionary<string, string?>
        {
            ["Otp__Enabled"] = "true",
            ["Otp__AllowMockAuthentication"] = "false",
            ["Identity__Session__AccessLifetimeSeconds"] = "777"
        });
        var builder = Builder(commandLine ? ["--Otp:Enabled=false", "--Otp:AllowMockAuthentication=true"] : []);
        using var configuration = builder.Configuration;
        var options = OtpAuthenticationOptions.Load(configuration, "Test");
        Assert.Equal(!commandLine, options.Enabled);
        Assert.Equal(commandLine, options.AllowMockAuthentication);
        Assert.Equal(777, configuration.GetValue<int>("Identity:Session:AccessLifetimeSeconds"));
    }

    [Theory]
    [InlineData("Test", false)]
    [InlineData("Production", true)]
    public void FullSettings_BindThroughActualInfrastructureFactoriesWithoutUsingProductionSecrets(string environment, bool realOtp)
    {
        using var variables = new EnvironmentScope();
        var builder = Builder();
        using var configuration = builder.Configuration;
        builder.Environment.EnvironmentName = environment;
        if (realOtp) configuration.AddJsonFile("appsettings.Production.json.example");
        configuration.AddInMemoryCollection(RuntimeValues());
        configuration["Sms:MeliPayamakOtp:ServiceToken"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        ProductionConfigurationValidator.Validate(configuration, environment);
        HostShutdownConfiguration.ConfigureServices(builder.Services, configuration);
        builder.Services.AddInfrastructure(configuration, builder.Environment.EnvironmentName);
        using var provider = builder.Services.BuildServiceProvider();

        var authentication = provider.GetRequiredService<IOptions<OtpAuthenticationOptions>>().Value;
        Assert.Equal(realOtp, authentication.Enabled);
        Assert.Equal(!realOtp, authentication.AllowMockAuthentication);
        Assert.Equal(realOtp, provider.GetRequiredService<OtpOptions>().Enabled);
        Assert.Equal(4, provider.GetRequiredService<OtpOptions>().MinimumCodeLength);
        Assert.Equal(64, provider.GetRequiredService<OtpOptions>().MaximumCodeLength);
        Assert.Equal(TimeSpan.FromHours(1), provider.GetRequiredService<SessionOptions>().AccessLifetime);
        Assert.NotNull(provider.GetRequiredService<MessagingOptions>());
        Assert.NotNull(provider.GetRequiredService<MessagingEphemeralOptions>());
        Assert.NotNull(provider.GetRequiredService<PushDeviceOptions>());
        Assert.NotNull(provider.GetRequiredService<PaymentOptions>());
        Assert.NotNull(provider.GetRequiredService<ReportExportOptions>());
        Assert.NotNull(provider.GetRequiredService<NotificationDeliveryOptions>());
        Assert.NotNull(provider.GetRequiredService<OperationalMetricsOptions>());
        // provider غیرفعال نباید transport خارجی را ثبت کند؛ نبود ثبت اینجا رفتار درست DI است.
        Assert.Null(provider.GetService<WebPushTransportOptions>());
        Assert.Null(provider.GetService<FcmTransportOptions>());
        Assert.False(provider.GetRequiredService<TerminalFileCleanupOptions>().Enabled);
        Assert.False(provider.GetRequiredService<AiServiceOptions>().Enabled);
        Assert.Equal("makan.ai.invoke", provider.GetRequiredService<AiServiceAuthenticationOptions>().Scope);
        Assert.Equal(configuration["Storage:RootPath"], provider.GetRequiredService<StorageOptions>().RootPath);
        Assert.Equal(TimeSpan.FromSeconds(30), provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        if (realOtp)
            Assert.IsType<MeliPayamakOtpProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());
        else
        {
            Assert.IsType<UnavailableOtpDeliveryProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());
            Assert.DoesNotContain(builder.Services, item => item.ServiceType == typeof(MeliPayamakOtpOptions));
        }
        Assert.NotNull(configuration.GetSection(MeliPayamakOtpOptions.SectionName).Get<MeliPayamakOtpOptions>());
        using var scope = provider.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.IsSqlServer());
    }

    [Theory]
    [InlineData("ConnectionStrings:MakanDatabase")]
    [InlineData("Identity:SecurityKey")]
    public void TestStillRejectsMissingRequiredRuntimeConfiguration(string missingKey)
    {
        using var configuration = FileConfiguration();
        configuration.AddInMemoryCollection(RuntimeValues());
        configuration[missingKey] = string.Empty;
        var services = new ServiceCollection().AddLogging();
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration, "Test"));
        Assert.Contains(missingKey, exception.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void SchemaAlignment_DoesNotChangeProductionSecurityOrAuthorizeMock(string environment)
    {
        using var configuration = new ConfigurationManager();
        configuration.SetBasePath(ApiDirectory())
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Production.json.example");
        configuration.AddInMemoryCollection(RuntimeValues());
        ProductionConfigurationValidator.Validate(configuration, environment);
        Assert.True(OtpAuthenticationOptions.Load(configuration, environment).Enabled);

        configuration["Sms:MeliPayamakOtp:ServiceToken"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(configuration, environment);
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<OtpOptions>().Enabled);
        Assert.IsType<MeliPayamakOtpProvider>(provider.GetRequiredService<IOtpDeliveryProvider>());

        configuration["Otp:AllowMockAuthentication"] = "true";
        Assert.Throws<InvalidOperationException>(() => OtpAuthenticationOptions.Load(configuration, environment));
    }

    private static Dictionary<string, string?> RuntimeValues() => new()
    {
        // این مقادیر فقط برای binding هستند؛ اتصال یا درخواست شبکه باز نمی‌شود.
        ["ConnectionStrings:MakanDatabase"] = "Server=sql.example.test;Database=ConfigurationOnly;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
        ["Identity:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["Storage:RootPath"] = Path.Combine(Path.GetTempPath(), "MakanApp-ConfigurationOnly", "Storage")
    };

    private WebApplicationBuilder Builder(string[]? args = null) => WebApplication.CreateBuilder(new WebApplicationOptions
    {
        ContentRootPath = ApiDirectory(),
        Args = args ?? []
    });

    private ConfigurationManager FileConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(ApiDirectory()).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Test.json");
        return configuration;
    }

    private string ApiDirectory() => _configurationDirectory;

    private static string SourceApiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد."), "MakanApp.Api");
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        public EnvironmentScope(IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var values = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Test",
                ["DOTNET_ENVIRONMENT"] = "Test",
                ["Otp__Enabled"] = null,
                ["Otp__AllowMockAuthentication"] = null,
                ["Identity__Session__AccessLifetimeSeconds"] = null
            };
            if (overrides is not null)
                foreach (var (key, value) in overrides) values[key] = value;
            foreach (var (key, value) in values)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous) Environment.SetEnvironmentVariable(key, value);
        }
    }
}
