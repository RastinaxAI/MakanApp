using System.Security.Cryptography;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    // Environment=Test سقف واقعی۲۰/دقیقهٔ Commercial را فعال می‌کند؛ guard فقط با Unmetered برداشته می‌شود.
    public WebApplicationFactory<Program> WithAiUsageMode(AiUsageMode mode, bool enforceHttpLimits = false,
        bool? commercialEnforcementEnabled = null) =>
        WithWebHostBuilder(builder =>
        {
            // پیش‌نیازهای bootstrap محیط Test صریح‌اند؛ کلید موقت و Storage ایزوله به فایل خصوصی وابسته نیستند.
            var bootstrapSecurityKey = enforceHttpLimits
                ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                : null;
            if (enforceHttpLimits)
            {
                builder.UseEnvironment("Test");
                builder.UseSetting("Identity:SecurityKey", bootstrapSecurityKey);
                builder.UseSetting("Storage:RootPath", StorageRootPath);
            }
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?> { ["AiUsage:Mode"] = mode.ToString() };
                if (bootstrapSecurityKey is not null)
                {
                    settings["Identity:SecurityKey"] = bootstrapSecurityKey;
                    settings["Storage:RootPath"] = StorageRootPath;
                }
                if (commercialEnforcementEnabled.HasValue)
                    settings["Commerce:EnforcementEnabled"] = commercialEnforcementEnabled.Value.ToString();
                configuration.AddInMemoryCollection(settings);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(Services.GetRequiredService<IIdentitySecurity>());
                services.RemoveAll<IDataProtectionProvider>(); services.AddSingleton(Services.GetRequiredService<IDataProtectionProvider>());
                services.RemoveAll<AiServiceTestControl>(); services.AddSingleton(AiService);
            });
        });

}
