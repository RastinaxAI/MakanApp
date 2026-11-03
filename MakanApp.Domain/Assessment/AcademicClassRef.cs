namespace MakanApp.Domain.Assessment;

public enum AcademicClassKind
{
    OrganizationClass = 1,
    PersonalClass = 2
}

/// <summary>مرز مالکیت موتور مشترک؛ شناسهٔ کلاس مستقل هرگز شناسهٔ سازمان نیست.</summary>
public readonly record struct AcademicScope
{
    public AcademicScope(AcademicClassKind kind, Guid id)
    {
        if (!Enum.IsDefined(kind) || id == Guid.Empty)
            throw new ArgumentException("مرجع مالکیت آموزشی معتبر نیست.");
        Kind = kind;
        Id = id;
    }

    public AcademicClassKind Kind { get; }
    public Guid Id { get; }
    public Guid? OrganizationId => Kind == AcademicClassKind.OrganizationClass ? Id : null;
    public Guid? PersonalClassId => Kind == AcademicClassKind.PersonalClass ? Id : null;
    public static AcademicScope Organization(Guid organizationId) => new(AcademicClassKind.OrganizationClass, organizationId);
    public static AcademicScope Personal(Guid classId) => new(AcademicClassKind.PersonalClass, classId);
    public static implicit operator AcademicScope(Guid organizationId) => Organization(organizationId);

    public void EnsureClass(Guid classId)
    {
        if (classId == Guid.Empty || Kind == AcademicClassKind.PersonalClass && Id != classId)
            throw new ArgumentException("کلاس با مرز مالکیت آموزشی سازگار نیست.");
    }
}

public readonly record struct AcademicClassRef
{
    public AcademicClassRef(AcademicScope scope, Guid classId)
    {
        if (scope.Id == Guid.Empty) throw new ArgumentException("مرجع مالکیت آموزشی الزامی است.");
        scope.EnsureClass(classId);
        Scope = scope;
        ClassId = classId;
    }

    public AcademicScope Scope { get; }
    public AcademicClassKind Kind => Scope.Kind;
    public Guid ClassId { get; }
    public static AcademicClassRef Organization(Guid organizationId, Guid classId) => new(AcademicScope.Organization(organizationId), classId);
    public static AcademicClassRef Personal(Guid classId) => new(AcademicScope.Personal(classId), classId);
}

/// <summary>شناسهٔ دورهٔ مشارکت: Enrollment واقعی یا عضویت محلی واقعی، بدون تبدیل یکی به دیگری.</summary>
public readonly record struct AcademicParticipantRef
{
    public AcademicParticipantRef(AcademicClassKind kind, Guid id)
    {
        if (!Enum.IsDefined(kind) || id == Guid.Empty) throw new ArgumentException("مرجع مشارکت آموزشی معتبر نیست.");
        Kind = kind;
        Id = id;
    }

    public AcademicClassKind Kind { get; }
    public Guid Id { get; }
    public Guid? EnrollmentId => Kind == AcademicClassKind.OrganizationClass ? Id : null;
    public Guid? PersonalClassMembershipId => Kind == AcademicClassKind.PersonalClass ? Id : null;
    public static implicit operator AcademicParticipantRef(Guid enrollmentId) => new(AcademicClassKind.OrganizationClass, enrollmentId);
    public void EnsureScope(AcademicScope scope)
    {
        if (Id == Guid.Empty || Kind != scope.Kind) throw new ArgumentException("نوع مشارکت با مالکیت آموزشی سازگار نیست.");
    }
}
