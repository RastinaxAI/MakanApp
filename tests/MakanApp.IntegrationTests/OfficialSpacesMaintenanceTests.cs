using System.Diagnostics;
using System.Text.Json;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task OfficialSpacesMaintenanceDefaultsToReadOnlyAndRejectsUnapprovedApplyBeforeAnyWrite()
    {
        using var scenario = await CreateOfficialScenarioAsync(false);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = db.Database.GetConnectionString()!;
        var before = await Counts();
        var dry = await Run("--organization-id", scenario.OrganizationId.ToString());
        Assert.Equal(0, dry.ExitCode);
        using (var document = JsonDocument.Parse(dry.Output))
        {
            Assert.Equal("DryRun", document.RootElement.GetProperty("mode").GetString());
            Assert.True(document.RootElement.GetProperty("wouldCreate").GetInt32() > 0);
        }
        Assert.Equal(before, await Counts());
        var missingApproval = await Run("--apply", "--organization-id", scenario.OrganizationId.ToString());
        Assert.NotEqual(0, missingApproval.ExitCode); Assert.Equal(before, await Counts());
        var wrongApproval = await Run("--apply", "--organization-id", scenario.OrganizationId.ToString(), "--approved-plan-sha256", new string('0', 64));
        Assert.NotEqual(0, wrongApproval.ExitCode); Assert.Equal(before, await Counts());
        var contradictory = await Run("--apply", "--dry-run", "--organization-id", scenario.OrganizationId.ToString(), "--approved-plan-sha256", new string('0', 64));
        Assert.NotEqual(0, contradictory.ExitCode); Assert.Equal(before, await Counts());

        async Task<(int Provisions, int Conversations, int Publications)> Counts() => (
            await db.OfficialSpaceProvisions.CountAsync(p => p.OrganizationId == scenario.OrganizationId),
            await db.Conversations.CountAsync(c => c.OrganizationId == scenario.OrganizationId),
            await db.AcademicActivityPublications.CountAsync(p => p.OrganizationId == scenario.OrganizationId));

        async Task<(int ExitCode, string Output)> Run(params string[] arguments)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "MakanApp.sln"))) root = root.Parent ?? throw new InvalidOperationException();
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var tool = Path.Combine(root.FullName, "tools", "organization", "MakanApp.OfficialSpacesMaintenance", "bin", configuration, "net9.0", "MakanApp.OfficialSpacesMaintenance.dll");
            Assert.True(File.Exists(tool));
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(tool); foreach (var argument in arguments) start.ArgumentList.Add(argument);
            // فقط connection متعلق به fixture ایزوله به فرایند فرزند داده می‌شود؛ هیچ مقدار محرمانه‌ای چاپ نمی‌شود.
            start.Environment["ConnectionStrings__MakanDatabase"] = connection;
            using var process = Process.Start(start) ?? throw new InvalidOperationException();
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(budget.Token); }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
            _ = await stderr;
            return (process.ExitCode, await stdout);
        }
    }
}
