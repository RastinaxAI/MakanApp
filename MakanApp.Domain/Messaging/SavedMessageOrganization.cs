using System.Text;

namespace MakanApp.Domain.Messaging;

public sealed class SavedMessageOrganization
{
    private SavedMessageOrganization() { }
    public Guid MessageId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static SavedMessageOrganization Create(Guid messageId, Guid ownerUserId)
    {
        if (messageId == Guid.Empty || ownerUserId == Guid.Empty) throw new ArgumentException("پیام ذخیره‌شده و مالک الزامی است.");
        return new() { MessageId = messageId, OwnerUserId = ownerUserId };
    }
    public void Touch() => Revision = checked(Revision + 1);
}

public sealed class SavedMessageLabel
{
    public const int MaximumLength = 32;
    public const int MaximumLabels = 10;
    private SavedMessageLabel() { }
    public Guid MessageId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) throw new ArgumentException("برچسب معتبر نیست.");
        var value = name.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (value.Length > MaximumLength) throw new ArgumentException("برچسب طولانی است.");
        return value;
    }
    public static SavedMessageLabel Create(Guid messageId, string name)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("شناسهٔ پیام الزامی است.");
        return new() { MessageId = messageId, Name = Normalize(name) };
    }
}
