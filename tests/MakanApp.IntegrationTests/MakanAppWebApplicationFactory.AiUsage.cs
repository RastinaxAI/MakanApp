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
    public WebApplicationFactory<Program> WithAiUsageMode(AiUsageMode mode, bool enforceHttpLimits = false) =>
        WithWebHostBuilder(builder =>
        {
            if (enforceHttpLimits) builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AiUsage:Mode"] = mode.ToString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIdentitySecurity>(); services.AddSingleton(Services.GetRequiredService<IIdentitySecurity>());
                services.RemoveAll<IDataProtectionProvider>(); services.AddSingleton(Services.GetRequiredService<IDataProtectionProvider>());
                services.RemoveAll<AiServiceTestControl>(); services.AddSingleton(AiService);
            });
        });
}
