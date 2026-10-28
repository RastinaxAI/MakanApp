using System.Text.Json;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class CorsConfigurationSchemaTests
{
    [Fact]
    public void TestAndProductionTemplatesKeepIdenticalConfigurationPathsAndTypes()
    {
        using var test = ReadTemplate("Test");
        using var production = ReadTemplate("Production");

        Assert.Equal(Schema(production.RootElement).Distinct().OrderBy(value => value, StringComparer.Ordinal),
            Schema(test.RootElement).Distinct().OrderBy(value => value, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Production")]
    public void BothTemplatesExplicitlySelectOpenCorsAndWildcardHosts(string environmentName)
    {
        using var document = ReadTemplate(environmentName);
        var cors = document.RootElement.GetProperty("Cors");

        Assert.True(cors.GetProperty("AllowAnyOrigin").GetBoolean());
        Assert.Equal(JsonValueKind.Array, cors.GetProperty("AllowedOrigins").ValueKind);
        Assert.Empty(cors.GetProperty("AllowedOrigins").EnumerateArray());
        Assert.Equal("*", document.RootElement.GetProperty("AllowedHosts").GetString());
    }

    private static JsonDocument ReadTemplate(string environmentName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد.");
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "MakanApp.Api", $"appsettings.{environmentName}.json.example")));
    }

    private static IEnumerable<string> Schema(JsonElement element, string path = "$")
    {
        var type = element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? "Boolean" : element.ValueKind.ToString();
        yield return $"{path}:{type}";
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                foreach (var entry in Schema(property.Value, path + "." + property.Name))
                    yield return entry;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var entry in Schema(item, path + "[]"))
                    yield return entry;
        }
    }
}
