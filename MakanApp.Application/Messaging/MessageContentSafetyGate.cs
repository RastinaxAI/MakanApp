using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

// یک گیت واقعی برای محتوای پیام و عنوان موضوع؛ فراخوانی provider بیرون transaction می‌ماند.
internal static class MessageContentSafetyGate
{
    public static async Task<(Message? ExistingMessage, bool Evaluated)> ApplyAsync(
        IMessagingStore store,
        IContentSafetyProvider contentSafetyProvider,
        TimeProvider clock,
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        byte[] payloadHash,
        CancellationToken cancellationToken)
    {
        var preparation = await store.PrepareMessageSafetyAsync(
            userId,
            conversationId,
            accessContext,
            command,
            payloadHash,
            clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (preparation.ExistingMessage is not null)
        {
            return (preparation.ExistingMessage, false);
        }

        if (preparation.ExistingSafetyDecision.HasValue)
        {
            throw SafetyDecisionError(preparation.ExistingSafetyDecision.Value, preparation.SafetyIncidentId);
        }

        if (!preparation.RequiresEvaluation)
        {
            return (null, false);
        }

        ContentSafetyResult safetyResult;
        try
        {
            safetyResult = await contentSafetyProvider.EvaluateAsync(
                new ContentSafetyRequest(
                    conversationId,
                    userId,
                    command.ClientMessageId,
                    command.Kind,
                    command.Text,
                    preparation.Attachments),
                cancellationToken);
            ValidateSafetyResult(safetyResult);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw SafetyUnavailable();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContentSafetyProviderUnavailableException)
        {
            throw SafetyUnavailable();
        }
        catch (MessagingException)
        {
            throw;
        }
        catch (Exception)
        {
            throw SafetyUnavailable();
        }

        if (safetyResult.Decision == ContentSafetyDecision.Allowed)
        {
            return (null, true);
        }

        if (safetyResult.Decision == ContentSafetyDecision.Unavailable) throw SafetyUnavailable();

        var incidentResult = await store.RecordSafetyIncidentAsync(
            userId,
            conversationId,
            accessContext,
            command,
            payloadHash,
            safetyResult,
            clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (incidentResult.ExistingMessage is not null)
        {
            return (incidentResult.ExistingMessage, false);
        }

        throw SafetyDecisionError(incidentResult.Decision, incidentResult.SafetyIncidentId);
    }

    private static void ValidateSafetyResult(ContentSafetyResult result)
    {
        if (result is null || !Enum.IsDefined(result.Decision) ||
            result.RiskLevel?.Trim().Length > SafetyIncident.MaximumRiskLevelLength ||
            result.ProviderReference?.Trim().Length > SafetyIncident.MaximumProviderReferenceLength ||
            (result.PolicyCodes ?? []).Any(code =>
                string.IsNullOrWhiteSpace(code) || code.Trim().Length > 100) ||
            string.Join(',', (result.PolicyCodes ?? []).Select(code => code.Trim()).Distinct())
                .Length > SafetyIncident.MaximumPolicyCodesLength)
        {
            throw new ContentSafetyProviderUnavailableException("پاسخ سرویس ایمنی محتوا معتبر نیست.");
        }
    }

    private static MessagingException SafetyDecisionError(ContentSafetyDecision decision, Guid? safetyReference) =>
        decision switch
        {
            ContentSafetyDecision.Blocked => new(
                MessagingErrorCodes.MessageSafetyBlocked,
                "ارسال این پیام مجاز نیست.") { SafetyReference = safetyReference },
            ContentSafetyDecision.ReviewRequired => new(
                MessagingErrorCodes.MessageSafetyReviewRequired,
                "این تغییر اعمال نشد؛ بررسی ایمنی به نتیجهٔ قابل ارسال نرسیده است. پیش‌نویس را نگه دارید و خودکار دوباره ارسال نکنید.")
                { SafetyReference = safetyReference },
            _ => SafetyUnavailable()
        };

    private static MessagingException SafetyUnavailable() => new(
        MessagingErrorCodes.MessageSafetyUnavailable,
        "بررسی ایمنی پیام موقتاً ممکن نیست؛ دوباره تلاش کنید.");

}
