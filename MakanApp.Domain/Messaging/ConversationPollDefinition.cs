namespace MakanApp.Domain.Messaging;

public sealed class ConversationPollDefinition
{
    public const int MaximumQuestionLength = 300;
    public const int MaximumOptionLength = 150;
    public const int MaximumOptions = 10;
    public const int MaximumExplanationLength = 1000;
    private ConversationPollDefinition(string question, string[] options, bool multiple, bool anonymous, bool quiz, int? correct, string? explanation)
    {
        Question = question; Options = Array.AsReadOnly(options); AllowsMultipleAnswers = multiple;
        IsAnonymous = anonymous; IsQuiz = quiz; CorrectOptionNumber = correct; Explanation = explanation;
    }
    public string Question { get; }
    public IReadOnlyList<string> Options { get; }
    public bool AllowsMultipleAnswers { get; }
    public bool IsAnonymous { get; }
    public bool IsQuiz { get; }
    public int? CorrectOptionNumber { get; }
    public string? Explanation { get; }
    public static ConversationPollDefinition Create(string question, IReadOnlyCollection<string> options, bool multiple, bool anonymous,
        bool quiz, int? correctOptionNumber, string? explanation)
    {
        var normalizedQuestion = NormalizeText(question, MaximumQuestionLength);
        if (options is null || options.Count is < 2 or > MaximumOptions) throw new ArgumentException("تعداد گزینه‌ها معتبر نیست.");
        var normalizedOptions = options.Select(o => NormalizeText(o, MaximumOptionLength)).ToArray();
        if (normalizedOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedOptions.Length) throw new ArgumentException("گزینهٔ تکراری مجاز نیست.");
        var normalizedExplanation = string.IsNullOrWhiteSpace(explanation) ? null : NormalizeText(explanation, MaximumExplanationLength, true);
        if (quiz && (multiple || correctOptionNumber is null || correctOptionNumber < 1 || correctOptionNumber > normalizedOptions.Length) ||
            !quiz && (correctOptionNumber.HasValue || normalizedExplanation is not null)) throw new ArgumentException("تعریف پاسخ صحیح معتبر نیست.");
        return new(normalizedQuestion, normalizedOptions, multiple, anonymous, quiz, correctOptionNumber, normalizedExplanation);
    }
    private static string NormalizeText(string value, int maximum, bool multiline = false)
    {
        if (value is null || value.Length > 4000) throw new ArgumentException("متن خام معتبر نیست.");
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum || normalized.Any(c => char.IsControl(c) && (!multiline || c is not ('\r' or '\n' or '\t'))))
            throw new ArgumentException("متن نظرسنجی معتبر نیست.");
        return normalized;
    }
}
