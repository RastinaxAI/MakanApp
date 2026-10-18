namespace MakanApp.Api.Hosting;

// probe عمومی نشست را مصرف نمی‌کند؛ حتی Bearer ارسالی نباید liveness را به SQL وابسته کند.
public sealed class CoreHealthProbeMetadata;
