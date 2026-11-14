using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AccountDeletionConfigurationTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public void DeletionStartupRejectsEnabledExecutionWithoutAnApprovedGrace()
    {
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AccountDeletion:ExecutionEnabled"] = "true", ["AccountDeletion:GracePeriodHours"] = null
            })));
        Assert.Throws<OptionsValidationException>(() => invalid.CreateClient());
    }
}
