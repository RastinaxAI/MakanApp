namespace MakanApp.Domain.Knowledge;

// این سابقه فقط هویت نسخه و اثرانگشت درخواست را نگه می‌دارد؛ مجوز یا سهمیه ایجاد نمی‌کند.
public sealed class AiSourceSelectionOperation
{
    private AiSourceSelectionOperation() { }
    public Guid UserId { get; private set; }
    public Guid OperationId { get; private set; }
    public byte[] PayloadHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public List<AiSourceSelectionPin> Pins { get; private set; } = [];
    public static AiSourceSelectionOperation Create(Guid userId, Guid operationId, byte[] hash,
        IReadOnlyCollection<(Guid SourceId, int Number)> pins, DateTime utcNow)
    {
        if (userId == Guid.Empty || operationId == Guid.Empty || hash.Length != 32 || pins.Count is < 1 or > 32 ||
            pins.Any(p => p.SourceId == Guid.Empty || p.Number < 1) || pins.Select(p => p.SourceId).Distinct().Count() != pins.Count || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("هویت انتخاب منبع معتبر نیست.");
        return new() { UserId = userId, OperationId = operationId, PayloadHash = hash.ToArray(), CreatedAtUtc = utcNow,
            Pins = pins.Select(p => new AiSourceSelectionPin(userId, operationId, p.SourceId, p.Number)).ToList() };
    }
}
public sealed class AiSourceSelectionPin
{
    private AiSourceSelectionPin() { }
    internal AiSourceSelectionPin(Guid userId, Guid operationId, Guid sourceId, int number)
    { UserId = userId; OperationId = operationId; SourceId = sourceId; Number = number; }
    public Guid UserId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid SourceId { get; private set; }
    public int Number { get; private set; }
}
