using System.Diagnostics;
using MakanApp.Domain.Messaging;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private void LogSafetyDecision(SafetyIncident incident, bool replayed)
    {
        // PolicyCodes و ProviderReference دادهٔ خصوصی‌اند؛ فقط دستهٔ داخلیِ شناخته‌شده وارد لاگ می‌شود.
        var unsupportedMedia = (incident.PolicyCodes ?? string.Empty).Split(',').Contains(
            $"{InternalMakanContentSafetyProvider.PolicyVersion}:Other.UnsupportedMedia", StringComparer.Ordinal);
        logger.LogWarning(
            "تغییر پیام به‌دلیل ایمنی اعمال نشد. SafetyReference={SafetyReference}; SafetyDecision={SafetyDecision}; SafetyReason={SafetyReason}; ContentKind={ContentKind}; Replayed={Replayed}; OperationState={OperationState}; NewMessageCreated={NewMessageCreated}; NewAttachmentsBound={NewAttachmentsBound}; TraceId={TraceId}",
            incident.Id, incident.Decision, unsupportedMedia ? "UnsupportedMedia" : "ContentPolicy",
            incident.ContentKind, replayed, "NotApplied", false, false, Activity.Current?.TraceId.ToString());
    }
}
