using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Realtime;

namespace MakanApp.IntegrationTests;

internal static class AccountReplayTestClient
{
    internal static async Task<AccountChangePageResult> ReadAsync(HttpClient client, JsonSerializerOptions json, string? cursor = null, int limit = 100)
    {
        using var response = await client.GetAsync("/api/v1/realtime/changes?limit=" + limit + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountChangePageResult>(json))!;
    }
    internal static async Task<Dictionary<Guid, AccountChangeResult>> DrainAsync(HttpClient client, JsonSerializerOptions json, string cursor, int limit = 100)
    {
        var result = new Dictionary<Guid, AccountChangeResult>();
        for (var page = 0; page < 100; page++)
        {
            var next = await ReadAsync(client, json, cursor, limit);
            foreach (var change in next.Changes) result[change.EventId] = change;
            cursor = next.NextCursor;
            if (!next.HasMore) return result;
            if (next.RetryAfterMilliseconds is int delay) await Task.Delay(delay);
        }
        throw new InvalidOperationException("آزمون replay باید در budget صفحه‌ها پایان یابد.");
    }
}
