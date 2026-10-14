using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class RecoveryDrillTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static int _phoneSequence = 7_710_000;

    [Fact]
    public async Task RecoveryToolRejectsUnsafeTargetsAndDetectsMissingOrCorruptFiles()
    {
        var result = await RunPowerShellAsync("Test-MakanRecovery.ps1", null, []);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("RECOVERY_TOOL_TESTS_PASSED=24", result.Output);
    }

    [Fact]
    public async Task FullDatabaseAndFilesRestorePreservesDomainHistoryAndAuthorization()
    {
        var repository = FindRepository();
        var runId = Guid.NewGuid().ToString("N");
        var sourceDatabase = "MakanApp_Recovery_Source_" + runId;
        var targetDatabase = "MakanApp_Recovery_Target_" + runId;
        var runRoot = Path.Combine(Path.GetTempPath(), "MakanApp_RecoveryDrill_" + runId);
        var sourceStorage = Path.Combine(runRoot, "source-files");
        var targetStorage = Path.Combine(runRoot, "restored-files");
        var bundle = Path.Combine(runRoot, "bundle");
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var originalConnection = Environment.GetEnvironmentVariable("ConnectionStrings__MakanDatabase");
        Directory.CreateDirectory(runRoot);
        try
        {
            using var source = new MakanAppWebApplicationFactory(sourceDatabase, sourceStorage, key);
            await using (var scope = source.Services.CreateAsyncScope())
            {
                // نام یکتا است؛ برخلاف fixture عادی هیچ DB حذف یا جایگزین نمی‌شود.
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                Assert.False(await db.Database.CanConnectAsync());
                await db.Database.MigrateAsync();
            }

            using var client = source.CreateClient();
            using var parentClient = source.CreateClient();
            var manager = await AuthenticateAsync(source, client);
            var parent = await AuthenticateAsync(source, parentClient);
            var organization = await source.CreateOrganizationAsync("Synthetic recovery school " + runId);
            var otherOrganization = await source.CreateOrganizationAsync("Synthetic other school " + runId);
            var academic = await source.CreateAcademicClassAsync(organization);
            var foreignClass = await source.CreateAcademicClassAsync(otherOrganization);
            var membership = await source.CreateMembershipAsync(manager.User.Id, organization, OrganizationRole.Manager);
            var parentMembership = await source.CreateMembershipAsync(parent.User.Id, organization, OrganizationRole.Parent);
            var learner = (await source.CreateOrganizationPersonsAsync(organization, 1))[0];
            var unrelatedLearner = (await source.CreateOrganizationPersonsAsync(organization, 1))[0];
            var enrollment = await source.CreateEnrollmentAsync(organization, academic.ClassId, learner);
            var relation = await source.CreateGuardianRelationAsync(parent.User.Id, organization, learner);
            var now = DateTime.UtcNow;
            var session = await source.CreateAcademicSessionAsync(organization, academic.ClassId, now.AddHours(-2), now.AddHours(-1), SessionStatus.Completed);
            _ = await source.CreateAttendanceAsync(organization, academic.ClassId, session, enrollment, membership.MembershipId);
            var assignment = await source.CreateDashboardAssignmentAsync(organization, academic.ClassId, enrollment, membership.MembershipId, now.AddDays(1), "Synthetic released assignment", DashboardAssignmentSeedState.Released);
            var exam = await source.CreateDashboardExamAsync(organization, academic.ClassId, enrollment, membership.MembershipId, now.AddHours(-1), now.AddHours(3), "Synthetic released exam", DashboardExamSeedState.Released);
            var notification = await source.CreateNotificationAsync(parent.User.Id, organization, now);

            await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
            await SelectWorkspaceAsync(parentClient, parentMembership.MembershipId, OrganizationRole.Parent);
            using (var selectChild = await parentClient.PostAsync($"/api/v1/guardian/children/{learner}/select", null))
            {
                selectChild.EnsureSuccessStatusCode();
            }
            await source.SetCommunicationAgeCategoryAsync(manager.User.Id, MakanApp.Domain.Identity.CommunicationAgeCategory.Adult);
            await source.SetCommunicationAgeCategoryAsync(parent.User.Id, MakanApp.Domain.Identity.CommunicationAgeCategory.Adult);
            using (var personal = await client.PostAsJsonAsync("/api/v1/workspaces/select",
                       new SelectWorkspaceCommand(WorkspaceType.Personal, null, null), JsonOptions))
            {
                personal.EnsureSuccessStatusCode();
            }
            var conversation = await PostAsync<DirectConversationResult>(client, "/api/v1/conversations/direct",
                new StartDirectConversationCommand(parent.User.Id, ConversationScope.Personal));
            var message = await PostAsync<MessageReceiptResult>(client, $"/api/v1/conversations/{conversation.ConversationId}/messages",
                new SendTextMessageCommand(Guid.NewGuid(), "Synthetic recovery message"));
            await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
            var export = await PostAsync<ReportExportJobResult>(client, "/api/v1/reports/exports",
                new CreateReportExportCommand(ReportExportType.ManagerEnrollments, ReportExportFormat.Csv, new ReportExportFilters(ClassId: academic.ClassId)));
            Assert.Equal(1, await source.ProcessAllReportExportsAsync());
            var exportedBytes = await client.GetByteArrayAsync($"/api/v1/reports/exports/{export.JobId}/download");
            Assert.NotEmpty(exportedBytes);

            // یک فایل پیوست‌پذیر و یک orphan مصنوعی برای اثبات پوشش تمام root.
            Guid fileId;
            Guid outboxId;
            string[] migrations;
            Dictionary<string, long> sourceCounts;
            await using (var scope = source.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                fileId = Guid.NewGuid();
                var storageKey = FileUploadPolicy.CreateStorageKey(fileId, now);
                var bytes = Encoding.UTF8.GetBytes("Synthetic upload recovery bytes");
                await using var stream = new MemoryStream(bytes);
                var stored = await scope.ServiceProvider.GetRequiredService<IFileStorage>()
                    .SaveAsync(storageKey, stream, 1024, CancellationToken.None);
                var file = FileAsset.CreatePending(fileId, organization, manager.User.Id, "recovery.txt", storageKey, "text/plain", now, now.AddDays(1));
                file.MarkReady(stored.SizeBytes, stored.Sha256Hash, DateTime.UtcNow);
                file.Retain(DateTime.UtcNow);
                db.FileAssets.Add(file);
                var envelope = NotificationOutboxContract.Create(new NotificationIntent(
                    Guid.NewGuid(), organization, NotificationCategory.Educational, NotificationType.AssignmentPublished,
                    NotificationResourceType.Assignment, assignment.AssignmentId, now,
                    [new NotificationRecipientIntent(parent.User.Id, "Synthetic title", "Synthetic body")])).Single();
                var outbox = OutboxMessage.Create(envelope.MessageId, envelope.MessageType, envelope.Payload, now);
                var claim = Guid.NewGuid();
                outbox.Claim(claim, now, now.AddMinutes(1));
                outbox.ScheduleRetry(claim, now.AddHours(1), "synthetic_retry");
                outboxId = outbox.Id;
                db.OutboxMessages.Add(outbox);
                db.AdminAuditLogs.Add(AdminAuditLog.Create(manager.User.Id, null, null, "recovery.synthetic", "Organization", organization.ToString(), organization,
                    "Synthetic recovery drill", now, runId, AdminAuditResult.Succeeded, null, "Synthetic audit marker"));
                await db.SaveChangesAsync();
                migrations = db.Database.GetMigrations().ToArray();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.True(await db.GradeReleases.AnyAsync(r => r.SubmissionAttemptId == assignment.SubmissionAttemptId));
                Assert.True(await db.ExamGradeReleases.AnyAsync(r => r.ExamAttemptId == exam.ExamAttemptId));
                sourceCounts = await ReadTableCountsAsync(db);
            }
            await File.WriteAllTextAsync(Path.Combine(sourceStorage, "synthetic-orphan.txt"), "preserve; never delete automatically");
            var migrationList = Path.Combine(runRoot, "artifact-migrations.txt");
            await File.WriteAllLinesAsync(migrationList, migrations);
            var sourceConnection = $"Server=(localdb)\\MSSQLLocalDB;Database={sourceDatabase};Integrated Security=True;TrustServerCertificate=True";
            source.Dispose();

            var backup = await RunPowerShellAsync("Invoke-MakanRecovery.ps1", sourceConnection,
                ["-Backup", "-ConnectionEnvironmentVariable", "MAKAN_RECOVERY_CONNECTION", "-BundlePath", bundle,
                 "-ApplicationRoot", repository, "-StorageRoot", sourceStorage, "-WritersStopped"]);
            Assert.True(backup.ExitCode == 0, backup.Output);
            using var backupResult = JsonDocument.Parse(backup.Output);
            var manifestHash = backupResult.RootElement.GetProperty("ManifestSha256").GetString()!;
            Assert.Equal(3, backupResult.RootElement.GetProperty("Files").GetInt32());
            var restoreArguments = new[] { "-Restore", "-ConnectionEnvironmentVariable", "MAKAN_RECOVERY_CONNECTION", "-BundlePath", bundle,
                "-ApplicationRoot", repository, "-TargetDatabase", targetDatabase, "-TargetStorageRoot", targetStorage,
                "-ExpectedManifestHash", manifestHash, "-MigrationListPath", migrationList };
            var timer = Stopwatch.StartNew();
            var restore = await RunPowerShellAsync("Invoke-MakanRecovery.ps1", sourceConnection, restoreArguments);
            Assert.True(restore.ExitCode == 0, restore.Output);
            using var restoreResult = JsonDocument.Parse(restore.Output);
            Assert.Equal(3, restoreResult.RootElement.GetProperty("Integrity").GetProperty("Files").GetInt32());
            Assert.Equal(1, restoreResult.RootElement.GetProperty("Integrity").GetProperty("UnreferencedFiles").GetInt32());
            var repeated = await RunPowerShellAsync("Invoke-MakanRecovery.ps1", sourceConnection, restoreArguments);
            Assert.NotEqual(0, repeated.ExitCode);

            using (var wrongKeyHost = new MakanAppWebApplicationFactory(targetDatabase, targetStorage,
                       Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))))
            using (var wrongKeyClient = wrongKeyHost.CreateClient())
            {
                wrongKeyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", manager.AccessToken);
                Assert.Equal(HttpStatusCode.Unauthorized, (await wrongKeyClient.GetAsync("/api/v1/me")).StatusCode);
            }

            using var restored = new MakanAppWebApplicationFactory(targetDatabase, targetStorage, key);
            await using (var scope = restored.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                Assert.Equal(sourceCounts.OrderBy(p => p.Key), (await ReadTableCountsAsync(db)).OrderBy(p => p.Key));
                Assert.Equal(migrations, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.Equal("Synthetic recovery message", (await db.Messages.SingleAsync(m => m.Id == message.MessageId)).Text);
                Assert.True(await db.Notifications.AnyAsync(n => n.Id == notification));
                Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.CorrelationId == runId));
                var outbox = await db.OutboxMessages.SingleAsync(o => o.Id == outboxId);
                Assert.Equal(1, outbox.AttemptCount);
                Assert.Null(outbox.ProcessedAtUtc);
                Assert.Equal("synthetic_retry", outbox.LastErrorCode);
                Assert.NotNull(outbox.NextAttemptAtUtc);
            }
            using var recoveredManager = restored.CreateClient();
            recoveredManager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", manager.AccessToken);
            using var recoveredParent = restored.CreateClient();
            recoveredParent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parent.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await recoveredManager.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await recoveredManager.GetAsync("/health/ready")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await recoveredManager.GetAsync("/api/v1/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await recoveredManager.GetAsync($"/api/v1/reports/manager/classes/{academic.ClassId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await recoveredManager.GetAsync($"/api/v1/reports/manager/classes/{foreignClass.ClassId}")).StatusCode);
            Assert.Equal(exportedBytes, await recoveredManager.GetByteArrayAsync($"/api/v1/reports/exports/{export.JobId}/download"));
            var recoveredFile = await recoveredManager.GetByteArrayAsync($"/api/v1/files/{fileId}/content");
            Assert.Equal("Synthetic upload recovery bytes", Encoding.UTF8.GetString(recoveredFile));
            Assert.Equal(HttpStatusCode.OK, (await recoveredParent.GetAsync("/api/v1/dashboards/parent")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await recoveredParent.PostAsync($"/api/v1/guardian/children/{unrelatedLearner}/select", null)).StatusCode);
            await restored.RevokeGuardianRelationAsync(relation);
            Assert.Equal(HttpStatusCode.Forbidden, (await recoveredParent.GetAsync("/api/v1/dashboards/parent")).StatusCode);
            _ = await AuthenticateAsync(restored, recoveredManager);
            timer.Stop();
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(bundle, "manifest.json")));
            await File.WriteAllTextAsync(Path.Combine(runRoot, "evidence.json"), JsonSerializer.Serialize(new
            {
                Result = "DB_FILES_APP_AUTHORIZATION_VERIFIED", SourceDatabase = sourceDatabase, TargetDatabase = targetDatabase,
                BackupStartedAtUtc = manifest.RootElement.GetProperty("BackupStartedAtUtc").GetString(),
                Restore = restoreResult.RootElement.Clone(), TotalRestoreAndSmokeSeconds = timer.Elapsed.TotalSeconds,
                CompletedAtUtc = DateTime.UtcNow, Migrations = migrations.Length, Tables = sourceCounts.Count,
                Live = 200, Ready = 200, AuthorizedRead = 200, CrossOrganizationRead = 404,
                GuardianRead = 200, UnrelatedChild = 404, RevokedGuardian = 403, ExistingSession = 200, WrongKey = 401, TestSafeOtp = "PASS",
                Files = 3, OrphansPreserved = 1, ProductionEnvironment = false, Host = "ASP.NET Core TestServer"
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            // شواهد، DBها و فایل‌های disposable حفظ می‌شوند؛ secret آزمایشی ذخیره نمی‌شود.
            Environment.SetEnvironmentVariable("ConnectionStrings__MakanDatabase", originalConnection);
        }
    }

    private static async Task<VerifyOtpResult> AuthenticateAsync(MakanAppWebApplicationFactory factory, HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var phone = $"+98912{Interlocked.Increment(ref _phoneSequence):D7}";
        var challenge = await PostAsync<RequestOtpResult>(client, "/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var user = await PostAsync<VerifyOtpResult>(client, "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(challenge.ChallengeId, phone, factory.GetLatestOtpCode(phone)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("Synthetic", "Recovery", "Synthetic Recovery", "recovery." + Guid.NewGuid().ToString("N")[..12]), JsonOptions);
        profile.EnsureSuccessStatusCode();
        return user;
    }

    private static async Task SelectWorkspaceAsync(HttpClient client, Guid membership, OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membership, role), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string route, object body)
    {
        using var response = await client.PostAsJsonAsync(route, body, JsonOptions);
        Assert.True(response.IsSuccessStatusCode, $"{route}: {(int)response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<Dictionary<string, long>> ReadTableCountsAsync(MakanDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        var tables = new List<(string Schema, string Table)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SCHEMA_NAME(schema_id), name FROM sys.tables ORDER BY schema_id, name";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { tables.Add((reader.GetString(0), reader.GetString(1))); }
        }
        var counts = new Dictionary<string, long>();
        foreach (var (schema, table) in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT_BIG(*) FROM [{schema.Replace("]", "]]")}].[{table.Replace("]", "]]")}]";
            counts.Add(schema + "." + table, (long)(await command.ExecuteScalarAsync())!);
        }
        return counts;
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln"))) { directory = directory.Parent; }
        return directory?.FullName ?? throw new DirectoryNotFoundException("ریشه solution پیدا نشد.");
    }

    private static async Task<(int ExitCode, string Output)> RunPowerShellAsync(string script, string? connection, string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(FindRepository(), "tools", "production", "recovery", script) }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        if (connection is not null) { start.Environment["MAKAN_RECOVERY_CONNECTION"] = connection; }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw; }
        return (process.ExitCode, (await output) + (await error));
    }
}
