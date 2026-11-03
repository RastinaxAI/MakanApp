using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record AcademicClassRefDto(AcademicClassKind Kind, Guid ClassId)
{
    public static implicit operator AcademicClassRefDto(AcademicClassRef reference) => new(reference.Kind, reference.ClassId);
}

public sealed record AcademicClassRecord(AcademicClassRef Reference, string Title, bool IsActive);

public sealed record AcademicParticipationRecord(AcademicParticipantRef Reference, bool IsActive, Guid? LearnerOrganizationPersonId)
{
    public Guid Id => Reference.Id;
}
