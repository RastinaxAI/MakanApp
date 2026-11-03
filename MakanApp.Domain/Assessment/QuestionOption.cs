namespace MakanApp.Domain.Assessment;

public sealed class QuestionOption : IAcademicScoped
{
    public const int MaximumTextLength = 2_000;

    private QuestionOption()
    {
    }

    private QuestionOption(
        Guid id,
        AcademicScope scope,
        Guid questionVersionId,
        int order,
        string text,
        bool isCorrect)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        QuestionVersionId = questionVersionId;
        Order = order;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid QuestionVersionId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    internal static QuestionOption Create(
        AcademicScope scope,
        Guid questionVersionId,
        ExamQuestionOptionDefinition definition)
    {
        var normalizedText = definition.Text?.Trim();
        if (scope.Id == Guid.Empty || questionVersionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های گزینه سؤال الزامی هستند.");
        }

        if (definition.Order <= 0 ||
            string.IsNullOrWhiteSpace(normalizedText) ||
            normalizedText.Length > MaximumTextLength)
        {
            throw new ExamAnswerKeyInvalidException();
        }

        return new QuestionOption(
            Guid.NewGuid(),
            scope,
            questionVersionId,
            definition.Order,
            normalizedText,
            definition.IsCorrect);
    }
}
