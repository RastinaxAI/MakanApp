using System.Text.Json;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class TestConfigurationSchemaTests
{
    [Fact]
    public void TestTemplate_MatchesEveryProductionTemplatePathAndJsonType()
    {
        using var production = ReadSettings("appsettings.Production.json.example");
        using var test = ReadSettings("appsettings.Test.json.example");

        Assert.Equal(Shape(production.RootElement), Shape(test.RootElement));

        // الگوها در CI نیز بدون دسترسی به فایل‌های خصوصی آزموده می‌شوند.
    }

    [Fact]
    public void AvailableRuntimeSettings_MatchBidirectionallyAndKeepEnvironmentSpecificOtp()
    {
        using var production = ReadSettings(File.Exists(SettingsPath("appsettings.Production.json"))
            ? "appsettings.Production.json" : "appsettings.Production.json.example");
        using var test = ReadSettings(File.Exists(SettingsPath("appsettings.Test.json"))
            ? "appsettings.Test.json" : "appsettings.Test.json.example");
        // فقط ساختار و فلگ‌ها بررسی می‌شوند؛ مقدار خصوصی در assertion قرار نمی‌گیرد.
        Assert.Equal(Shape(production.RootElement), Shape(test.RootElement));
        AssertOtp(production.RootElement, realOtp: true);
        AssertOtp(test.RootElement, realOtp: false);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json.example")]
    [InlineData("appsettings.Test.json.example")]
    public void VersionedSettings_AreValidJsonWithoutDuplicateKeys(string fileName)
    {
        using var document = ReadSettings(fileName);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.NotEmpty(Shape(document.RootElement));
    }

    [Theory]
    [InlineData("{}", "{\"Otp\":{}}")]
    [InlineData("{\"Otp\":{\"Enabled\":false}}", "{\"Otp\":{\"Enabled\":\"false\"}}")]
    [InlineData("{\"A\":[]}", "{\"A\":{}}")]
    [InlineData("{\"A\":[\"x\"]}", "{\"A\":[1]}")]
    [InlineData("{\"A\":[{}]}", "{\"A\":[]}")]
    [InlineData("{\"A\":{\"B\":1}}", "{\"A\":{\"C\":1}}")]
    [InlineData("{\"A\":null}", "{\"A\":\"\"}")]
    [InlineData("{\"A\":[[{\"B\":true}]]}", "{\"A\":[[{\"B\":\"true\"}]]}")]
    [InlineData("{\"A\":[{\"B\":1},{\"B\":2}]}", "{\"A\":[{\"B\":1},{\"C\":2}]}")]
    public void RecursiveComparison_DetectsMissingExtraNestedAndMistypedValues(string left, string right)
    {
        using var first = JsonDocument.Parse(left);
        using var second = JsonDocument.Parse(right);
        Assert.False(Shape(first.RootElement).SequenceEqual(Shape(second.RootElement)));
    }

    [Fact]
    public void RecursiveComparison_IgnoresValuesAndPropertyOrderButNotStructure()
    {
        using var production = JsonDocument.Parse("{\"Enabled\":true,\"Values\":[1,2]}");
        using var test = JsonDocument.Parse("{\"Values\":[3,4],\"Enabled\":false}");
        Assert.Equal(Shape(production.RootElement), Shape(test.RootElement));
    }

    [Theory]
    [InlineData("{\"Otp\":{},\"Otp\":{}}")]
    [InlineData("{\"Otp\":{\"Enabled\":true,\"enabled\":false}}")]
    [InlineData("{\"A\":[[{\"B\":1,\"B\":2}]]}")]
    public void RecursiveComparison_RejectsDuplicateKeysIncludingConfigurationCasing(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Shape(document.RootElement));
    }

    [Theory]
    [InlineData("{\"A\":null}", "{\"A\":null}")]
    [InlineData("{\"A\":[[{\"B\":true}]]}", "{\"A\":[[{\"B\":false}]]}")]
    public void RecursiveComparison_AcceptsMatchingNullAndNestedArraySchemas(string left, string right)
    {
        using var first = JsonDocument.Parse(left);
        using var second = JsonDocument.Parse(right);
        Assert.Equal(Shape(first.RootElement), Shape(second.RootElement));
    }

    private static void AssertOtp(JsonElement root, bool realOtp)
    {
        var otp = root.GetProperty("Otp");
        Assert.Equal(realOtp, otp.GetProperty("Enabled").GetBoolean());
        Assert.Equal(!realOtp, otp.GetProperty("AllowMockAuthentication").GetBoolean());
    }

    [Theory]
    [InlineData("ConnectionStrings", "MakanDatabase")]
    [InlineData("Identity", "SecurityKey")]
    [InlineData("Security", "DataProtection", "CertificatePassword")]
    [InlineData("Sms", "MeliPayamakOtp", "ServiceToken")]
    public void TestSecretSlots_RemainEmptyRatherThanCopyingProductionCredentials(params string[] path)
    {
        using var document = ReadSettings("appsettings.Test.json.example");
        var value = document.RootElement;
        foreach (var segment in path) value = value.GetProperty(segment);
        Assert.True(value.ValueKind == JsonValueKind.String && value.GetString() == string.Empty,
            $"جایگاه محرمانهٔ {string.Join(':', path)} باید رشتهٔ خالی بماند.");
    }

    [Fact]
    public void ExistingTestOtpAndSwaggerValues_ArePreserved()
    {
        using var document = ReadSettings("appsettings.Test.json.example");
        var root = document.RootElement;
        Assert.False(root.GetProperty("Otp").GetProperty("Enabled").GetBoolean());
        Assert.True(root.GetProperty("Otp").GetProperty("AllowMockAuthentication").GetBoolean());
        Assert.True(root.GetProperty("Swagger").GetProperty("Enabled").GetBoolean());
    }

    private static string[] Shape(JsonElement root)
    {
        var paths = new SortedDictionary<string, JsonValueKind>(StringComparer.OrdinalIgnoreCase);
        Visit(root, "$", paths);
        return paths.Select(item => $"{item.Key}: {item.Value}").ToArray();
    }

    private static void Visit(JsonElement node, string path, IDictionary<string, JsonValueKind> paths)
    {
        // مقدار true/false بخشی از تفاوت محیط است؛ نوع مشترک آن‌ها boolean است.
        var kind = node.ValueKind is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : node.ValueKind;
        Assert.True(paths.TryAdd(path, kind), $"کلید تکراری در مسیر {path}");
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject()) Visit(property.Value, $"{path}/{property.Name}", paths);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in node.EnumerateArray()) Visit(item, $"{path}/[{index++}]", paths);
        }
    }

    private static JsonDocument ReadSettings(string fileName) => JsonDocument.Parse(File.ReadAllText(SettingsPath(fileName)));

    private static string SettingsPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("ریشهٔ پروژه پیدا نشد.");
        return Path.Combine(root, "MakanApp.Api", fileName);
    }
}
