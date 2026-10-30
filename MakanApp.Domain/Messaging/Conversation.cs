namespace MakanApp.Domain.Messaging;

public sealed class Conversation
{
    public const int TitleMaximumLength = 160;
    public const int DescriptionMaximumLength = 1000;
    public const int CreationPayloadHashLength = 32;

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }
    public ConversationScope Scope { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? AcademicClassId { get; private set; }
    public bool IsOfficialOrganizationSpace { get; private set; }
    public Guid? DirectUserLowId { get; private set; }
    public Guid? DirectUserHighId { get; private set; }
    public Guid? SavedOwnerUserId { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public ConversationManagementPolicy ManagementPolicy { get; private set; }
    public ConversationVisibility Visibility { get; private set; }
    public GroupHistoryPolicy GroupHistoryPolicy { get; private set; }
    public GroupMemberCapability MemberCapabilities { get; private set; }
    public Guid? ClientOperationId { get; private set; }
    public byte[]? CreationPayloadHash { get; private set; }
    public long NextMessageSequence { get; private set; }
    public long NextChangeSequence { get; private set; }
    public ConversationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Conversation CreateDirect(
        ConversationScope scope,
        Guid? organizationId,
        Guid firstUserId,
        Guid secondUserId,
        DateTime createdAtUtc)
    {
        ValidateScope(scope, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        var pair = DirectUserPair.Create(firstUserId, secondUserId);
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            Scope = scope,
            OrganizationId = organizationId,
            DirectUserLowId = pair.LowerUserId,
            DirectUserHighId = pair.HigherUserId,
            ManagementPolicy = ConversationManagementPolicy.None,
            Visibility = ConversationVisibility.Private,
            GroupHistoryPolicy = GroupHistoryPolicy.FullHistory,
            MemberCapabilities = GroupMemberCapability.None,
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static Conversation CreateSaved(Guid ownerUserId, DateTime createdAtUtc)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه مالک پیام‌های ذخیره‌شده الزامی است.", nameof(ownerUserId));
        }

        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Saved,
            Scope = ConversationScope.Personal,
            SavedOwnerUserId = ownerUserId,
            ManagementPolicy = ConversationManagementPolicy.None,
            Visibility = ConversationVisibility.Private,
            GroupHistoryPolicy = GroupHistoryPolicy.FullHistory,
            MemberCapabilities = GroupMemberCapability.None,
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static Conversation CreateUserManaged(
        ConversationType type,
        ConversationScope scope,
        Guid? organizationId,
        string title,
        string? description,
        Guid createdByUserId,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        DateTime createdAtUtc) =>
        CreateUserManaged(
            type,
            scope,
            organizationId,
            title,
            description,
            createdByUserId,
            clientOperationId,
            creationPayloadHash,
            ConversationVisibility.Private,
            GroupHistoryPolicy.FullHistory,
            type == ConversationType.Group
                ? GroupMemberCapability.LegacyMemberDefault
                : GroupMemberCapability.None,
            createdAtUtc);

    public static Conversation CreateUserManaged(
        ConversationType type,
        ConversationScope scope,
        Guid? organizationId,
        string title,
        string? description,
        Guid createdByUserId,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities,
        DateTime createdAtUtc)
    {
        ValidateManagedType(type);
        ValidateScope(scope, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        if (createdByUserId == Guid.Empty || clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سازنده و عملیات ایجاد الزامی است.");
        }

        if (creationPayloadHash is null || creationPayloadHash.Length != CreationPayloadHashLength)
        {
            throw new ArgumentException("اثر انگشت عملیات ایجاد معتبر نیست.", nameof(creationPayloadHash));
        }

        ValidateBehaviorSettings(type, visibility, groupHistoryPolicy, memberCapabilities);

        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Scope = scope,
            OrganizationId = organizationId,
            Title = NormalizeTitle(title),
            Description = NormalizeDescription(description),
            CreatedByUserId = createdByUserId,
            ManagementPolicy = ConversationManagementPolicy.UserManaged,
            Visibility = visibility,
            GroupHistoryPolicy = groupHistoryPolicy,
            MemberCapabilities = memberCapabilities,
            ClientOperationId = clientOperationId,
            CreationPayloadHash = creationPayloadHash.ToArray(),
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static Conversation CreateSystemManagedAcademic(
        ConversationType type,
        Guid organizationId,
        string title,
        string? description,
        DateTime createdAtUtc)
    {
        ValidateManagedType(type);
        ValidateScope(ConversationScope.Organization, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Scope = ConversationScope.Organization,
            OrganizationId = organizationId,
            Title = NormalizeTitle(title),
            Description = NormalizeDescription(description),
            ManagementPolicy = ConversationManagementPolicy.SystemManagedAcademic,
            Visibility = ConversationVisibility.Private,
            GroupHistoryPolicy = GroupHistoryPolicy.FullHistory,
            MemberCapabilities = GroupMemberCapability.None,
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public DirectUserPair GetDirectPair()
    {
        if (Type != ConversationType.Direct ||
            !DirectUserLowId.HasValue ||
            !DirectUserHighId.HasValue)
        {
            throw new InvalidOperationException("گفتگو زوج مستقیم معتبر ندارد.");
        }

        return new DirectUserPair(DirectUserLowId.Value, DirectUserHighId.Value);
    }

    public void BindAcademicClass(Guid classId)
    {
        if (classId == Guid.Empty || IsOfficialOrganizationSpace || ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
            Type is not ConversationType.Group and not ConversationType.Channel || Scope != ConversationScope.Organization ||
            AcademicClassId.HasValue && AcademicClassId != classId)
            throw new InvalidOperationException("اتصال رسمی کلاس معتبر نیست.");
        AcademicClassId = classId;
    }

    public static Conversation CreateOfficialOrganization(ConversationType purpose, Guid organizationId, string title, DateTime now)
    {
        var conversation = CreateSystemManagedAcademic(purpose, organizationId, title, null, now);
        conversation.IsOfficialOrganizationSpace = true;
        return conversation;
    }

    public long AllocateNextMessageSequence()
    {
        if (Status != ConversationStatus.Active)
        {
            throw new InvalidOperationException("گفتگوی غیرفعال پیام تازه نمی‌پذیرد.");
        }

        if (NextMessageSequence == long.MaxValue)
        {
            throw new InvalidOperationException("ظرفیت ترتیب پیام گفتگو به پایان رسیده است.");
        }

        var allocated = NextMessageSequence;
        NextMessageSequence++;
        return allocated;
    }

    public long AllocateNextChangeSequence()
    {
        if (NextChangeSequence == long.MaxValue)
        {
            throw new InvalidOperationException("ظرفیت ترتیب تغییرات گفتگو به پایان رسیده است.");
        }

        var allocated = NextChangeSequence;
        NextChangeSequence++;
        return allocated;
    }

    public void Archive(DateTime archivedAtUtc)
    {
        ValidateUtc(archivedAtUtc, nameof(archivedAtUtc));
        if (Status == ConversationStatus.Archived)
        {
            return;
        }

        Status = ConversationStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
    }

    public void UpdateInformation(string title, string? description)
    {
        if (ManagementPolicy != ConversationManagementPolicy.UserManaged || Status != ConversationStatus.Active ||
            Type is not ConversationType.Group and not ConversationType.Channel)
            throw new InvalidOperationException("تغییر مشخصات این گفتگو مجاز نیست.");
        Title = NormalizeTitle(title); Description = NormalizeDescription(description);
    }

    public void UpdateBehaviorSettings(
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities)
    {
        if (ManagementPolicy != ConversationManagementPolicy.UserManaged)
        {
            throw new InvalidOperationException("تنظیمات گفتگوی مدیریت‌شده سیستمی قابل تغییر دستی نیست.");
        }

        ValidateBehaviorSettings(Type, visibility, groupHistoryPolicy, memberCapabilities);
        Visibility = visibility;
        GroupHistoryPolicy = groupHistoryPolicy;
        MemberCapabilities = memberCapabilities;
    }

    public static string NormalizeTitle(string? title)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > TitleMaximumLength)
        {
            throw new ArgumentException(
                $"عنوان گفتگو باید بین 1 و {TitleMaximumLength} نویسه باشد.",
                nameof(title));
        }

        return normalized;
    }

    public static string? NormalizeDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalized?.Length > DescriptionMaximumLength)
        {
            throw new ArgumentException(
                $"توضیح گفتگو نمی‌تواند بیشتر از {DescriptionMaximumLength} نویسه باشد.",
                nameof(description));
        }

        return normalized;
    }

    private static void ValidateManagedType(ConversationType type)
    {
        if (type is not ConversationType.Group and not ConversationType.Channel)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static void ValidateBehaviorSettings(
        ConversationType type,
        ConversationVisibility visibility,
        GroupHistoryPolicy groupHistoryPolicy,
        GroupMemberCapability memberCapabilities)
    {
        if (!Enum.IsDefined(visibility) || !Enum.IsDefined(groupHistoryPolicy) ||
            (memberCapabilities & ~GroupMemberCapability.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibility));
        }

        if (type == ConversationType.Channel &&
            (groupHistoryPolicy != GroupHistoryPolicy.FullHistory ||
             memberCapabilities != GroupMemberCapability.None))
        {
            throw new ArgumentException("سیاست تاریخچه و capability عضو فقط برای Group قابل تنظیم است.");
        }
    }

    private static void ValidateScope(ConversationScope scope, Guid? organizationId)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        if (scope == ConversationScope.Personal && organizationId.HasValue)
        {
            throw new ArgumentException("گفتگوی شخصی نباید سازمان داشته باشد.", nameof(organizationId));
        }

        if (scope == ConversationScope.Organization &&
            (!organizationId.HasValue || organizationId.Value == Guid.Empty))
        {
            throw new ArgumentException("گفتگوی سازمانی باید سازمان معتبر داشته باشد.", nameof(organizationId));
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان گفتگو باید UTC باشد.", parameterName);
        }
    }
}
