using System.Text.Json;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MakanApp.Documentation;

internal static class DocumentationSnapshot
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("مسیر خروجی شواهد مستندات لازم است.");
        var repositoryRoot = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(repositoryRoot, "MakanApp.sln")))
            throw new InvalidOperationException("ابزار را از ریشهٔ مخزن اجرا کنید.");
        var output = Path.GetFullPath(args[0], repositoryRoot);
        if (!output.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("خروجی باید داخل workspace باشد.");
        Directory.CreateDirectory(output);

        // هیچ کانفیگ خصوصی انتشار یا سرویس واقعی در میزبان مستندسازی مصرف نمی‌شود.
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp_Documentation_NoConnection;Integrated Security=True;Encrypt=True;TrustServerCertificate=True",
            ["Identity:SecurityKey"] = "documentation-only-not-a-production-secret",
            ["Sms:Provider"] = "Unavailable",
            ["Sms:MeliPayamakOtp:ServiceToken"] = "",
            ["Storage:RootPath"] = Path.Combine(output, "unused-storage"),
            ["AiService:Enabled"] = "false",
            ["Swagger:Enabled"] = "true",
            ["Logging:LogLevel:Default"] = "Critical"
        };
        await using var host = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            // OpenAPI فقط metadata است؛ worker و probe دیتابیس نباید اجرا شوند.
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
        using var client = host.CreateClient();
        var openApi = await client.GetStringAsync("/openapi/v1.json");
        await File.WriteAllTextAsync(Path.Combine(output, "openapi.json"), openApi);
        var apiDescriptions = host.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items).ToArray();
        var operations = apiDescriptions.Select(api =>
        {
            var action = (ControllerActionDescriptor)api.ActionDescriptor;
            var attributes = action.MethodInfo.GetCustomAttributes(true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(true))
                .Concat(action.EndpointMetadata.Cast<object>()).ToArray();
            var authorization = attributes.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().ToArray();
            return new
            {
                Controller = action.ControllerName,
                Action = action.ActionName,
                Services = action.ControllerTypeInfo.DeclaredConstructors
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType.FullName).Distinct().ToArray(),
                Method = api.HttpMethod,
                Path = "/" + api.RelativePath,
                Anonymous = attributes.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any() || authorization.Length == 0,
                Policies = authorization.Select(item => item.Policy)
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray(),
                Parameters = api.ParameterDescriptions.Select(parameter => new
                {
                    parameter.Name,
                    Source = parameter.Source?.Id,
                    Type = parameter.Type?.FullName,
                    parameter.IsRequired
                }).ToArray()
            };
        }).OrderBy(item => item.Controller).ThenBy(item => item.Path).ThenBy(item => item.Method).ToArray();
        if (!operations.Single(item => item.Controller == "Auth" && item.Action == "RequestOtp").Anonymous ||
            operations.Single(item => item.Controller == "Auth" && item.Action == "GetSessions").Anonymous ||
            operations.Any(item => item.Controller == "PlatformMfa" && item.Anonymous))
            throw new InvalidOperationException("ماتریس احراز هویت استخراج‌شده با guardهای شناخته‌شده سازگار نیست.");

        // خواندن مدل طراحی EF هیچ اتصال SQL یا اجرای Migration ایجاد نمی‌کند.
        var options = new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(settings["ConnectionStrings:MakanDatabase"]).Options;
        await using var context = new MakanDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var tables = model.GetEntityTypes().Select(entity =>
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            return new
            {
                Entity = entity.ClrType.FullName,
                Schema = entity.GetSchema(),
                Table = entity.GetTableName(),
                Columns = entity.GetProperties().Select(property => new
                {
                    Name = property.GetColumnName(table),
                    Type = property.GetColumnType(),
                    property.IsNullable,
                    property.IsConcurrencyToken,
                    ValueGenerated = property.ValueGenerated.ToString(),
                    DefaultSql = property.GetDefaultValueSql()
                }).OrderBy(item => item.Name).ToArray(),
                Keys = entity.GetKeys().Select(key => new { Name = key.GetName(), Primary = key.IsPrimaryKey(), Columns = key.Properties.Select(p => p.GetColumnName(table)).ToArray() }).ToArray(),
                Indexes = entity.GetIndexes().Select(index => new { Name = index.GetDatabaseName(), index.IsUnique, Filter = index.GetFilter(), Columns = index.Properties.Select(p => p.GetColumnName(table)).ToArray() }).ToArray(),
                ForeignKeys = entity.GetForeignKeys().Select(fk => new
                {
                    Target = fk.PrincipalEntityType.GetSchema() + "." + fk.PrincipalEntityType.GetTableName(),
                    Columns = fk.Properties.Select(p => p.GetColumnName(table)).ToArray(),
                    PrincipalColumns = fk.PrincipalKey.Properties.Select(p => p.Name).ToArray(),
                    Delete = fk.DeleteBehavior.ToString()
                }).ToArray(),
                Checks = entity.GetCheckConstraints().Select(check => new { check.Name, check.Sql }).ToArray()
            };
        }).OrderBy(item => item.Schema).ThenBy(item => item.Table).ToArray();
        var migrations = context.Database.GetMigrations().ToArray();
        var sourceFiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in new[] { "MakanApp.Api", "MakanApp.Application", "MakanApp.Domain", "MakanApp.Infrastructure" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, directory), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                if (relative.Split('/').Any(part => part is "bin" or "obj")) continue;
                sourceFiles[relative] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(path)));
            }
        }
        using var git = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = repositoryRoot, RedirectStandardOutput = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        git.Start();
        var sourceCommit = (await git.StandardOutput.ReadToEndAsync()).Trim();
        await git.WaitForExitAsync();
        if (git.ExitCode != 0 || !System.Text.RegularExpressions.Regex.IsMatch(sourceCommit, "^[a-f0-9]{40}$"))
            throw new InvalidOperationException("مبنای Git برای مستندات قابل تأیید نیست.");
        var serializer = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var enums = new[] { typeof(MakanApp.Domain.Common.DomainAssembly).Assembly, typeof(MakanApp.Application.Common.ApplicationAssembly).Assembly, typeof(global::Program).Assembly }
            .SelectMany(assembly => assembly.GetTypes()).Where(type => type.IsEnum).Append(typeof(DayOfWeek))
            .Distinct().Select(type => new
            {
                Name = type.Name,
                ClrType = type.FullName,
                IsFlags = type.IsDefined(typeof(FlagsAttribute), false),
                Values = Enum.GetValues(type).Cast<object>().Select(value => JsonSerializer.SerializeToElement(value, type, serializer)).ToArray()
            }).OrderBy(item => item.Name).ToArray();
        if (!enums.Single(item => item.Name == "MessageKind").Values.All(value => value.ValueKind == JsonValueKind.String))
            throw new InvalidOperationException("قرارداد serializer Enum تغییر کرده؛ مرجع رشته‌ای باید بازبینی شود.");
        var snapshot = new { SourceCommit = sourceCommit, SourceFiles = sourceFiles, Operations = operations, Tables = tables, Migrations = migrations, Enums = enums, DatabaseConnected = false, HostedWorkersEnabled = false, SmsEnabled = false, AiEnabled = false };
        await File.WriteAllTextAsync(Path.Combine(output, "repository-inventory.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { Operations = operations.Length, Controllers = operations.Select(item => item.Controller).Distinct().Count(), Tables = tables.Length, Migrations = migrations.Length, DatabaseConnected = false, HostedWorkersEnabled = false }));
    }
}
