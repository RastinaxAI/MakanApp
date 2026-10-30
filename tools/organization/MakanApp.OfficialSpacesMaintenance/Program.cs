using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
json.Converters.Add(new JsonStringEnumConverter());
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
try
{
    var apply = false; var explicitDryRun = false; Guid? organizationId = null; string? approvedHash = null; string? output = null;
    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--dry-run": explicitDryRun = true; break;
            case "--apply": apply = true; break;
            case "--organization-id" when index + 1 < args.Length && Guid.TryParse(args[++index], out var id) && id != Guid.Empty:
                organizationId = id; break;
            case "--approved-plan-sha256" when index + 1 < args.Length: approvedHash = args[++index]; break;
            case "--output" when index + 1 < args.Length: output = args[++index]; break;
            default: throw new InvalidOperationException();
        }
    }
    // مجوز SQL و تأیید طرح، مسئولیت اپراتور است؛ ابزار عمومی HTTP یا مسیر دورزدن مجوز کاربر نیست.
    if (apply && (explicitDryRun || !organizationId.HasValue || approvedHash is null || approvedHash.Length != 64 || !approvedHash.All(Uri.IsHexDigit)))
        throw new InvalidOperationException();
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__MakanDatabase");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException();
    // مجوز و نام تازهٔ فایل گزارش پیش از نخستین write دیتابیس بررسی می‌شوند.
    await using var destination = output is null ? null : new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection).Options);
    const string requiredMigration = "20261030093927_AddOfficialOrganizationAndClassSpaces";
    if (!(await db.Database.GetAppliedMigrationsAsync(cancellation.Token)).Contains(requiredMigration)) throw new InvalidOperationException();
    var store = new EfOfficialSpacesStore(db, new EfOutbox(db), TimeProvider.System, NullLogger<EfOfficialSpacesStore>.Instance);
    var plan = await store.PlanAsync(cancellation.Token, organizationId);
    if (organizationId.HasValue && plan.Count == 0) throw new InvalidOperationException();
    var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan, json)));
    if (apply)
    {
        // قبل از نخستین write، هر تغییر طرح یا ابهام، اجرای کل محدودهٔ انتخابی را متوقف می‌کند.
        if (!string.Equals(hash, approvedHash, StringComparison.OrdinalIgnoreCase) || plan.Any(row => row.Conflict))
            throw new InvalidOperationException();
        foreach (var row in plan)
            if (!await store.EnsurePairAsync(row.OrganizationId, row.SourceKind == MakanApp.Domain.Messaging.OfficialSourceKind.Class ? row.SourceId : null, cancellation.Token))
                throw new InvalidOperationException();
    }
    var report = JsonSerializer.Serialize(new {
        passed = true, mode = apply ? "Apply" : "DryRun", schemaVersion = 1, requiredMigration,
        planSha256 = hash, scannedResources = plan.Count, conflicts = plan.Count(row => row.Conflict),
        wouldCreate = plan.Sum(row => row.Conflict ? 0 : (row.MissingGroup ? 1 : 0) + (row.MissingChannel ? 1 : 0)),
        resources = plan
    }, json);
    if (destination is not null)
    {
        await JsonSerializer.SerializeAsync(destination, JsonSerializer.Deserialize<JsonElement>(report), json, cancellation.Token);
    }
    Console.WriteLine(report);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("OFFICIAL_SPACES_MAINTENANCE_CANCELLED");
    return 2;
}
catch (Exception)
{
    // اتصال، SQL، محتوا و Exception خام چاپ نمی‌شوند؛ شکست هرگز موفق گزارش نمی‌شود.
    Console.Error.WriteLine("OFFICIAL_SPACES_MAINTENANCE_FAILED");
    return 1;
}
