namespace MakanApp.Domain.Assessment;

public sealed class ExamAnswerAttachmentPolicy
{
    public ExamAnswerAttachmentPolicy(bool textRequired, int maximumCount, long maximumTotalBytes,
        IEnumerable<string> allowedContentTypes)
    {
        if (maximumCount <= 0 || maximumTotalBytes <= 0) throw new ArgumentException("سقف پیوست باید صریح و مثبت باشد.");
        var types = allowedContentTypes.Select(value => value.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (types.Length == 0 || types.Any(value => value.Length == 0 || value.Count(c => c == '/') != 1 ||
            value.Any(c => char.IsWhiteSpace(c) || c is ',' or ';' or ':' or '\\')))
            throw new ArgumentException("نوع‌های مجاز فایل معتبر نیستند.");
        TextRequired = textRequired;
        MaximumCount = maximumCount;
        MaximumTotalBytes = maximumTotalBytes;
        AllowedContentTypes = Array.AsReadOnly(types);
    }

    public bool TextRequired { get; }
    public int MaximumCount { get; }
    public long MaximumTotalBytes { get; }
    public IReadOnlyCollection<string> AllowedContentTypes { get; }
}
