using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ChecklistMigrationUpgradesBaselinePreservesDataAndRefusesDestructiveRollbackAfterUse()
    {
        const string baseline = "20261016211112_AddConversationPollsAndQuiz";
        const string migration = "20261017033804_AddConversationChecklists";
        await using var scope = factory.Services.CreateAsyncScope(); var root = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(root.Database.GetConnectionString()) { InitialCatalog = "MakanApp_Checklist_Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("MakanApp_Checklist_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(baseline);
            var now = DateTime.UtcNow; var user = User.Create(now); var conversation = Conversation.CreateSaved(user.Id, now);
            var participant = ConversationParticipant.CreateActive(conversation.Id, user.Id, now);
            var oldMessage = Message.CreateText(conversation.Id, participant.Id, user.Id, Guid.NewGuid(), conversation.AllocateNextMessageSequence(), "پیام پیش از ارتقا", 4000, now);
            // درجِ fixture قدیمی نباید ستون تازهٔ مدل جاری را از schema قدیمی درخواست کند.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[Users] ([Id],[CreatedAtUtc],[AccountStatus],[CommunicationAgeCategory],[CommunicationAgeVerificationStatus])
                VALUES ({user.Id},{now},1,0,0)
                """);
            await InsertLegacyAsync("messaging", "Conversations", conversation);
            await InsertLegacyAsync("messaging", "ConversationParticipants", participant);
            await InsertLegacyAsync("messaging", "Messages", oldMessage);
            await InsertLegacyAsync("messaging", "MessageRevisions", MessageRevision.Create(oldMessage.Id, 1, oldMessage.Text, user.Id, now));
            await migrator.MigrateAsync(); Assert.Contains(migration, await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal("پیام پیش از ارتقا", (await db.Messages.AsNoTracking().SingleAsync()).Text);
            Assert.Empty(await db.ConversationChecklists.ToArrayAsync());
            await migrator.MigrateAsync(baseline); Assert.DoesNotContain(migration, await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(oldMessage.Id, await db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [messaging].[Messages]").SingleAsync());
            await migrator.MigrateAsync();
            var definition = ConversationChecklistDefinition.Create("فهرست پس از ارتقا", ConversationChecklistMode.Personal, [new(Guid.NewGuid(), "آیتم")]);
            var message = Message.Create(conversation.Id, participant.Id, user.Id, Guid.NewGuid(), conversation.AllocateNextMessageSequence(), MessageKind.Checklist, definition.Title, 0, null, null, 4000, now);
            var checklist = ConversationChecklist.Create(message, definition, new byte[32]); var item = ConversationChecklistItem.Create(checklist, definition.Items[0], 1);
            db.Messages.Add(message); db.MessageRevisions.Add(MessageRevision.Create(message.Id, 1, message.Text, user.Id, now));
            db.ConversationChecklists.Add(checklist); db.ConversationChecklistItems.Add(item); db.ConversationChecklistPersonalStates.Add(ConversationChecklistPersonalState.Create(item, user.Id, true));
            await db.SaveChangesAsync();
            var refused = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(baseline)); Assert.Equal(51071, refused.Number);
            Assert.Contains(migration, await db.Database.GetAppliedMigrationsAsync()); Assert.Equal(2, await db.Messages.CountAsync());
            Assert.True((await db.ConversationChecklistPersonalStates.AsNoTracking().SingleAsync()).IsChecked);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            // فقط DB یکتای همین تست؛ نه fixture مشترک، DB توسعه یا سرور.
            await db.Database.EnsureDeletedAsync();
        }

        async Task InsertLegacyAsync(string schema, string table, object entity)
        {
            // fixture با ستون‌های همان schema قدیمی درج می‌شود؛ مدل جدید نباید پیش از Migration به آن تحمیل شود.
            await using var sql = new SqlConnection(connection.ConnectionString); await sql.OpenAsync();
            var columns = new HashSet<string>();
            await using (var metadata = sql.CreateCommand())
            {
                metadata.CommandText = "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND system_type_id<>189 AND is_computed=0 AND is_identity=0";
                metadata.Parameters.AddWithValue("@table", schema + "." + table);
                await using var reader = await metadata.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            }
            var properties = entity.GetType().GetProperties().Where(p => columns.Contains(p.Name)).ToArray();
            await using var command = sql.CreateCommand();
            command.CommandText = $"INSERT INTO [{schema}].[{table}] ({string.Join(',', properties.Select(p => '[' + p.Name + ']'))}) VALUES ({string.Join(',', properties.Select((_, i) => "@p" + i))})";
            for (var i = 0; i < properties.Length; i++)
            {
                var value = properties[i].GetValue(entity);
                if (value?.GetType().IsEnum == true) value = Convert.ToInt32(value);
                var parameter = command.Parameters.AddWithValue("@p" + i, value ?? DBNull.Value);
                if (properties[i].PropertyType == typeof(byte[]))
                { parameter.SqlDbType = System.Data.SqlDbType.VarBinary; parameter.Size = -1; }
            }
            await command.ExecuteNonQueryAsync();
        }
    }
}
