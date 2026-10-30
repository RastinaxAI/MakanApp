namespace MakanApp.AiIntegrationProbe;

internal static class LiveAiTargetPolicy
{
    // مجوز شبکه فقط برای runner است؛ هرگز تنظیمات Production را تغییر نمی‌دهد.
    public static Uri Validate(string? baseUrl, bool allowRemoteHttps, string? approvedOrigin,
        bool allowWireGuard = false, bool hasBackendWireGuardAddress = false)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var target) ||
            target.Scheme is not ("http" or "https") ||
            target.UserInfo.Length != 0 || target.Query.Length != 0 || target.Fragment.Length != 0 ||
            target.AbsolutePath != "/")
            throw new InvalidOperationException("LIVE_AI_TARGET_INVALID");

        if (target.IsLoopback) return target;
        if (allowWireGuard)
        {
            if (allowRemoteHttps || !hasBackendWireGuardAddress || target.AbsoluteUri != "http://10.77.0.2:8001/" ||
                !Uri.TryCreate(approvedOrigin, UriKind.Absolute, out var wireGuardOrigin) || wireGuardOrigin != target)
                throw new InvalidOperationException("LIVE_AI_WIREGUARD_TARGET_NOT_APPROVED");
            return target;
        }
        if (!allowRemoteHttps || target.Scheme != "https" ||
            !Uri.TryCreate(approvedOrigin, UriKind.Absolute, out var approved) ||
            approved.Scheme != "https" || approved.UserInfo.Length != 0 ||
            approved.Query.Length != 0 || approved.Fragment.Length != 0 || approved.AbsolutePath != "/" ||
            !string.Equals(target.GetLeftPart(UriPartial.Authority), approved.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LIVE_AI_TARGET_NOT_APPROVED");
        return target;
    }
}
