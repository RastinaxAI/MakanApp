using System.Data.Common;
using System.Transactions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MakanApp.Infrastructure.Persistence;

public sealed class SqlServerConnectionInitializationInterceptor : DbConnectionInterceptor
{
    private const string InitializeSession = "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (Transaction.Current is not null) return;
        using var command = connection.CreateCommand();
        command.CommandText = InitializeSession;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Transaction.Current is not null) return;
        // Pool الزاماً isolation قبلی را پاک نمی‌کند؛ این پیش‌فرض پیش از آغاز تراکنش صریح EF تنظیم می‌شود.
        await using var command = connection.CreateCommand();
        command.CommandText = InitializeSession;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
