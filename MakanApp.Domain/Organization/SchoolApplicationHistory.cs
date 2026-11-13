namespace MakanApp.Domain.Organization;

public sealed class SchoolApplicationHistory
{
    private SchoolApplicationHistory() { }
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = "";
    public int Revision { get; private set; }
    public long Version { get; private set; }
    public SchoolApplicationStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ProposedInitialManagerUserId { get; private set; }
    public Guid? FinalInitialManagerUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public static SchoolApplicationHistory Create(SchoolApplication app, Guid actor, string action, string? reason, DateTime now) => new()
    {
        Id = Guid.NewGuid(), ApplicationId = app.Id, ActorUserId = actor, Action = action,
        Revision = app.Revision, Version = app.Version, Status = app.Status, Reason = reason, OccurredAtUtc = now,
        ProposedInitialManagerUserId = app.ProposedInitialManagerUserId, FinalInitialManagerUserId = app.FinalInitialManagerUserId
    };
}
