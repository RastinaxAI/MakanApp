using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class InternalMakanContentSafetyProvider : IContentSafetyProvider
{
    public const string PolicyVersion = "makan-child-safety-v1-rules-1";
    private static readonly (string Category, ContentSafetyDecision Decision, string[] Patterns)[] Rules =
    [
        ("ChildAbuseRisk", ContentSafetyDecision.Blocked, ["سوء استفاده جنسی از کودک", "child sexual abuse"]),
        ("ThreatViolence", ContentSafetyDecision.Blocked, ["میکشمت", "می کشمت", "i will kill you"]),
        ("SexualSafetyGrooming", ContentSafetyDecision.ReviewRequired, ["عکس خصوصی بفرست", "از پدر و مادرت مخفی کن", "keep this secret from your parents"]),
        ("HarassmentBullying", ContentSafetyDecision.ReviewRequired, ["احمق", "بی شعور", "you are worthless"]),
        ("SensitivePersonalData", ContentSafetyDecision.ReviewRequired, ["کد ملی", "رمز بانکی", "my password is"]),
        ("SpamScam", ContentSafetyDecision.ReviewRequired, ["برنده جایزه شدید", "send your password", "پرداخت برای دریافت جایزه"])
    ];
    private static readonly Regex SensitiveNumber = new(@"(?<!\d)(?:09\d{9}|\d{16})(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public Task<ContentSafetyResult> EvaluateAsync(ContentSafetyRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var correlation = Guid.NewGuid().ToString("N");
        // این موتور قواعد قطعی متن است، نه طبقه‌بند معنایی یا اسکنر بایت فایل.
        if (request.Kind != MessageKind.Text || request.Attachments.Count != 0)
            return Task.FromResult(Result(ContentSafetyDecision.ReviewRequired, "Other.UnsupportedMedia", correlation));
        var text = Normalize(request.Text ?? string.Empty);
        foreach (var rule in Rules)
        {
            if (rule.Patterns.Any(pattern => text.Contains(Normalize(pattern), StringComparison.Ordinal)))
                return Task.FromResult(Result(rule.Decision, rule.Category, correlation));
        }
        if (SensitiveNumber.IsMatch(text))
            return Task.FromResult(Result(ContentSafetyDecision.ReviewRequired, "SensitivePersonalData", correlation));
        if (text.Contains("https://", StringComparison.Ordinal) || text.Contains("http://", StringComparison.Ordinal) ||
            text.Contains("www.", StringComparison.Ordinal))
            return Task.FromResult(Result(ContentSafetyDecision.ReviewRequired, "Other.UnsupportedLink", correlation));
        return Task.FromResult(Result(ContentSafetyDecision.Allowed, "NoRuleMatched", correlation));
    }

    private static ContentSafetyResult Result(ContentSafetyDecision decision, string reason, string correlation) =>
        new(decision, decision == ContentSafetyDecision.Blocked ? "High" : decision == ContentSafetyDecision.ReviewRequired ? "Review" : "Low",
            correlation, [$"{PolicyVersion}:{reason}"], reason, PolicyVersion, correlation);

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var c in value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (c is '\u200c' or '\u200d' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!space) builder.Append(' ');
                space = true;
                continue;
            }
            space = false;
            builder.Append(c is 'ي' or 'ى' ? 'ی' : c == 'ك' ? 'ک' : char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c);
        }
        return builder.ToString();
    }
}
