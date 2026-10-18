namespace MakanApp.IntegrationTests;

internal static class MessagingRealtimeAudienceTestExtensions
{
    // فقط assertion تست، stream محدودشدهٔ تولید را به collection تبدیل می‌کند.
    internal static async Task<IReadOnlyCollection<Guid>> ToTestArrayAsync(this IAsyncEnumerable<Guid> sessions)
    {
        var result = new List<Guid>();
        await foreach (var session in sessions) result.Add(session);
        return result.ToArray();
    }
}
