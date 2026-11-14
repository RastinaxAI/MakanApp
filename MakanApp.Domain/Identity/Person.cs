namespace MakanApp.Domain.Identity;

public sealed class Person
{
    public const string DeletedFirstName = "حساب";
    public const string DeletedLastName = "حذف‌شده";
    public const string DeletedDisplayName = "حساب حذف‌شده";
    private Person()
    {
    }

    private Person(
        Guid id,
        string firstName,
        string lastName,
        string? displayName,
        DateTime createdAtUtc)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Person Create(
        string firstName,
        string lastName,
        string? displayName,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), firstName, lastName, displayName, createdAtUtc);

    public void Update(
        string firstName,
        string lastName,
        string? displayName,
        DateTime updatedAtUtc)
    {
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetDateOfBirth(DateOnly dateOfBirth, DateTime updatedAtUtc)
    {
        if (dateOfBirth > DateOnly.FromDateTime(updatedAtUtc))
        {
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "تاریخ تولد نمی‌تواند در آینده باشد.");
        }

        DateOfBirth = dateOfBirth;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Anonymize(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد.");
        FirstName = DeletedFirstName; LastName = DeletedLastName; DisplayName = DeletedDisplayName;
        DateOfBirth = null; UpdatedAtUtc = now;
    }
}
