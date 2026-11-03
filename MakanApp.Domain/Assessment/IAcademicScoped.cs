namespace MakanApp.Domain.Assessment;

public interface IAcademicScoped
{
    Guid? OrganizationId { get; }
    Guid? PersonalClassId { get; }
    AcademicClassKind ContextKind { get; }
    Guid ContextId { get; }
    AcademicScope Scope { get; }
}
