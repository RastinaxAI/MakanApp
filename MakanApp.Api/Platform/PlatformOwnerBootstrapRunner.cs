using MakanApp.Application.Platform;

namespace MakanApp.Api.Platform;

public static class PlatformOwnerBootstrapRunner
{
    public static async Task<bool> TryRunAsync(
        WebApplication app,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || !string.Equals(args[0], "platform", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length != 4 ||
            !string.Equals(args[1], "bootstrap-owner", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(args[2], "--user-id", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(args[3], out var userId))
        {
            Console.Error.WriteLine(
                "Usage: dotnet run --project MakanApp.Api -- platform bootstrap-owner --user-id <existing-user-guid>");
            Environment.ExitCode = 2;
            return true;
        }

        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IPlatformBootstrapService>();
            var result = await service.BootstrapOwnerAsync(
                userId,
                $"bootstrap-{Guid.NewGuid():N}",
                cancellationToken);
            Console.WriteLine(
                $"PlatformOwner assignment {result.Assignment.AssignmentId:D} is active for user {userId:D}. Replayed={result.Replayed}.");
            return true;
        }
        catch (PlatformException exception)
        {
            Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
            Environment.ExitCode = 1;
            return true;
        }
    }
}
