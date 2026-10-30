using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class OfficialSpacesWorkHandler(EfOfficialSpacesStore spaces, EfMessagingStore messages) : IOfficialSpacesWorkHandler
{
    public bool Handles(string messageType) => messageType is OfficialSpaceEventWriter.EnsureType or OfficialSpaceEventWriter.SyncType or OfficialSpaceEventWriter.PublicationType;

    public async Task HandleAsync(Guid eventId, string messageType, string payload, CancellationToken ct)
    {
        if (messageType == OfficialSpaceEventWriter.PublicationType)
        {
            var group = await messages.DeliverAcademicActivityAsync(eventId, ConversationType.Group, ct);
            var channel = await messages.DeliverAcademicActivityAsync(eventId, ConversationType.Channel, ct);
            if (!group || !channel) throw new InvalidOperationException("تحویل فعالیت منتظر فضای رسمی است.");
            return;
        }
        var work = JsonSerializer.Deserialize<OfficialSpaceWork>(payload) ?? throw new JsonException("منبع عملیات رسمی مشخص نیست.");
        if (messageType == OfficialSpaceEventWriter.EnsureType)
        {
            if (!await spaces.EnsurePairAsync(work.OrganizationId, work.ClassId, ct))
                throw new InvalidOperationException("فضای رسمی نیازمند بررسی است.");
        }
        else if (messageType == OfficialSpaceEventWriter.SyncType)
            await spaces.SynchronizeExistingAsync(work.OrganizationId, work.ClassId, ct);
        else throw new InvalidOperationException("نوع عملیات رسمی معتبر نیست.");
    }
}
