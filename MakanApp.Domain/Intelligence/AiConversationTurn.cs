using System.Text;

namespace MakanApp.Domain.Intelligence;

public enum AiConversationTurnRole { User = 1, Assistant = 2 }

public sealed class AiConversationTurn
{
    public const int MaximumUserCharacters = 4_000;
    // upstream سقف کل پاسخ تعریف نکرده است؛ این حدِ صریح، حافظه و ذخیره‌سازی Backend را محدود می‌کند.
    public const int MaximumAssistantCharacters = 32_768;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private AiConversationTurn() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid OperationId { get; private set; }
    public long Sequence { get; private set; }
    public AiConversationTurnRole Role { get; private set; }
    public string? Text { get; private set; }
    public bool IsCompleted { get; private set; }
    public byte[] IntentHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public static AiConversationTurn ReserveUser(Guid owner, Guid conversation, Guid operation, long sequence,
        byte[] intentHash, DateTime nowUtc)
    {
        if (owner == Guid.Empty || conversation == Guid.Empty || operation == Guid.Empty || sequence < 1 ||
            sequence % 2 != 1 || intentHash.Length != 32 || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("هویت نوبت گفت‌وگو معتبر نیست.");
        // پیش از completion فقط metadata ذخیره می‌شود؛ prompt شکست‌خورده وارد storage نمی‌شود.
        return new() { Id = Guid.NewGuid(), ConversationId = conversation, OwnerUserId = owner,
            OperationId = operation, Sequence = sequence, Role = AiConversationTurnRole.User,
            IntentHash = intentHash.ToArray(), CreatedAtUtc = nowUtc };
    }

    public AiConversationTurn Complete(string userText, string assistantText, DateTime nowUtc)
    {
        if (Role != AiConversationTurnRole.User || IsCompleted || nowUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("نوبت گفت‌وگو قابل تکمیل نیست.");
        ValidateText(userText, MaximumUserCharacters);
        ValidateText(assistantText, MaximumAssistantCharacters);
        Text = userText; IsCompleted = true; CompletedAtUtc = nowUtc;
        return new() { Id = Guid.NewGuid(), ConversationId = ConversationId, OwnerUserId = OwnerUserId,
            OperationId = OperationId, Sequence = checked(Sequence + 1), Role = AiConversationTurnRole.Assistant,
            Text = assistantText, IsCompleted = true, IntentHash = IntentHash.ToArray(),
            CreatedAtUtc = nowUtc, CompletedAtUtc = nowUtc };
    }

    public static int CharacterCount(string text)
    {
        _ = StrictUtf8.GetByteCount(text);
        return text.EnumerateRunes().Count();
    }

    private static void ValidateText(string text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || CharacterCount(text) > maximum)
            throw new ArgumentException("متن نوبت گفت‌وگو معتبر نیست.");
    }
}
