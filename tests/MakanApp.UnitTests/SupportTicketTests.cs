using MakanApp.Domain.Support;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class SupportTicketTests
{
    [Fact]
    public void SupportThreadKeepsAssignmentAndReplyStateSeparateAndDoesNotInventReopen()
    {
        var now = DateTime.UtcNow; var owner = Guid.NewGuid(); var staff = Guid.NewGuid();
        var ticket = SupportTicket.Create(owner, " پرسش ", "General", now);
        Assert.Equal(SupportTicketStatus.Open, ticket.Status); Assert.Equal("پرسش", ticket.Subject);
        ticket.Assign(staff, now); Assert.Equal(staff, ticket.AssignedStaffUserId); Assert.Equal(SupportTicketStatus.InProgress, ticket.Status);
        ticket.Reply(true, now); Assert.Equal(SupportTicketStatus.WaitingForUser, ticket.Status);
        ticket.Reply(false, now); Assert.Equal(SupportTicketStatus.WaitingForSupport, ticket.Status);
        ticket.Resolve(now); Assert.Equal(SupportTicketStatus.Resolved, ticket.Status);
        Assert.Throws<InvalidOperationException>(() => ticket.Reply(false, now));
        Assert.Equal(owner, ticket.OwnerUserId);
    }

    [Fact]
    public void TicketMessageHasBoundedPlainTextContent()
    {
        Assert.Throws<ArgumentException>(() => SupportTicketMessage.Create(Guid.NewGuid(), Guid.NewGuid(), false, " ", 1, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => SupportTicketMessage.Create(Guid.NewGuid(), Guid.NewGuid(), false, new string('x', 8001), 1, DateTime.UtcNow));
    }
}
