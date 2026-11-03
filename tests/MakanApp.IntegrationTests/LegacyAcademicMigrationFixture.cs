using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MakanApp.IntegrationTests;

// مدل جاری ستون‌های جدید دارد؛ شاهد ارتقا باید فقط ستون‌های واقعاً موجود در schema تاریخی را درج کند.
internal static class LegacyAcademicMigrationFixture
{
    internal static async Task InsertAsync(MakanDbContext db, object entity)
    {
        var type = db.Model.FindEntityType(entity.GetType())!;
        var table = StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema());
        var qualified = $"[{table.Schema}].[{table.Name}]";
        await db.Database.OpenConnectionAsync();
        try
        {
            using var columns = db.Database.GetDbConnection().CreateCommand();
            columns.CommandText = "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID(@table) AND system_type_id<>189 AND is_computed=0";
            var parameter = columns.CreateParameter(); parameter.ParameterName = "@table"; parameter.Value = qualified; columns.Parameters.Add(parameter);
            var existing = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await columns.ExecuteReaderAsync())
                while (await reader.ReadAsync()) existing.Add(reader.GetString(0));
            using var insert = db.Database.GetDbConnection().CreateCommand();
            var names = new List<string>(); var parameters = new List<string>();
            foreach (var property in type.GetProperties())
            {
                var column = property.GetColumnName(table);
                if (column is null || !existing.Contains(column)) continue;
                var name = "@p" + parameters.Count;
                names.Add("[" + column + "]"); parameters.Add(name);
                insert.Parameters.Add(property.GetRelationalTypeMapping().CreateParameter(insert, name, db.Entry(entity).Property(property.Name).CurrentValue, property.IsNullable));
            }
            insert.CommandText = $"INSERT INTO {qualified} ({string.Join(',', names)}) VALUES ({string.Join(',', parameters)})";
            await insert.ExecuteNonQueryAsync();
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
