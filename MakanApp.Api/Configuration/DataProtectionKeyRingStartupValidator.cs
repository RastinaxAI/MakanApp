using MakanApp.Api.Hosting;

namespace MakanApp.Api.Configuration;

public sealed class DataProtectionKeyRingStartupValidator(DataProtectionKeyRingProbe probe) : IHostedLifecycleService
{
    // StartingAsync برای همهٔ hosted serviceها پیش از شروع workerها اجرا می‌شود.
    public Task StartingAsync(CancellationToken cancellationToken) => ValidateAsync(cancellationToken);
    public Task StartAsync(CancellationToken cancellationToken) => ValidateAsync(cancellationToken);
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (!await probe.InitializeAsync(cancellationToken))
        {
            throw new InvalidOperationException("key ring پایدار Data Protection پیکربندی نشده یا قابل استفاده نیست؛ برنامه شروع نشد.");
        }
    }
}
