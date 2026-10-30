using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiSourcePolicyTests
{
    private static readonly Guid User = Guid.NewGuid(), Org = Guid.NewGuid(), Class = Guid.NewGuid();
    private static AiSourceContext Context => new(new(User, Guid.NewGuid(),
        new(User, User, Guid.NewGuid(), WorkspaceType.Personal, null, null, null, null),
        new(User, User, Guid.NewGuid(), WorkspaceType.Organization, Org, Guid.NewGuid(), OrganizationRole.Manager, null)),
        AiSourceOperation.TeacherActivitySuggestions, Class, null);
    [Fact] public void PersonalOwnerIsAllowedRegardlessOfEducationalRole() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Personal, User, null, null, Context);
    [Fact] public void ForeignPersonalIsConcealedEvenForManager() => Assert.Equal(AiSourceErrorCodes.NotFound, Assert.Throws<AiServiceException>(() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Personal, Guid.NewGuid(), null, null, Context)).Code);
    [Fact] public void SameClassAndOrganizationContextIsCompatible() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Class, null, Org, Class, Context);
    [Fact] public void OtherClassCannotEnterCurrentContext() => Assert.Equal(AiSourceErrorCodes.ContextMismatch, Assert.Throws<AiServiceException>(() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Class, null, Org, Guid.NewGuid(), Context)).Code);
    [Fact] public void SameOrganizationIsCompatible() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Organization, null, Org, null, Context);
    [Fact] public void CrossOrganizationIsConcealed() => Assert.Equal(AiSourceErrorCodes.NotFound, Assert.Throws<AiServiceException>(() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Organization, null, Guid.NewGuid(), null, Context)).Code);
    [Fact] public void LibraryIsDisabledEvenForManager() => Assert.Equal(AiSourceErrorCodes.NotAllowed, Assert.Throws<AiServiceException>(() => AiSourcePolicy.EnsureContext(EducationalSourceScope.Library, null, null, null, Context)).Code);
    [Theory]
    [InlineData(EducationalSourceStatus.Archived, SourcePublicationStatus.Published, true, SourceProcessingStatus.NotRequested)]
    [InlineData(EducationalSourceStatus.Inactive, SourcePublicationStatus.Published, true, SourceProcessingStatus.NotRequested)]
    [InlineData(EducationalSourceStatus.Active, SourcePublicationStatus.Draft, true, SourceProcessingStatus.NotRequested)]
    [InlineData(EducationalSourceStatus.Active, SourcePublicationStatus.Published, false, SourceProcessingStatus.NotRequested)]
    [InlineData(EducationalSourceStatus.Active, SourcePublicationStatus.Published, true, SourceProcessingStatus.Pending)]
    [InlineData(EducationalSourceStatus.Active, SourcePublicationStatus.Published, true, SourceProcessingStatus.Processing)]
    [InlineData(EducationalSourceStatus.Active, SourcePublicationStatus.Published, true, SourceProcessingStatus.Failed)]
    public void UnusableStatesAreRejected(EducationalSourceStatus s, SourcePublicationStatus p, bool ready, SourceProcessingStatus processing) =>
        Assert.Equal(AiSourceErrorCodes.NotReady, Assert.Throws<AiServiceException>(() => AiSourcePolicy.EnsureReady(s, p, ready, processing)).Code);
    [Theory] [InlineData(SourceProcessingStatus.NotRequested)] [InlineData(SourceProcessingStatus.Ready)]
    public void TechnicalReadinessNeverClaimsRetrievalExists(SourceProcessingStatus state)
    {
        AiSourcePolicy.EnsureReady(EducationalSourceStatus.Active, SourcePublicationStatus.Published, true, state);
        Assert.False(new AuthorizedAiRetrievalPlan([], false).RetrievalAvailable);
    }
}
