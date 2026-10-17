using System.Globalization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    // کپی مستقل فقط پرسش و گزینه‌هاست؛ این مسیر اصلاً جدول رأی یا کلید Quiz را نمی‌خواند.
    private async Task<(MessageKind Kind, string? Text)> BuildForwardContentAsync(Message source, CancellationToken ct)
    {
        if (source.Kind == MessageKind.Checklist)
        {
            // تنها متن فعال؛ هیچ query وضعیت شخصی/مشترک، actor، نسخه یا تاریخچه در این مسیر نیست.
            var items = await dbContext.ConversationChecklistItems.AsNoTracking().Where(i => i.ChecklistId == source.Id && i.RetiredAtRevision == null)
                .OrderBy(i => i.Position).ThenBy(i => i.Id).Select(i => i.Text).ToArrayAsync(ct);
            if (items.Length == 0) throw ChecklistNotFound();
            return (MessageKind.Text, source.Text + "\n" + string.Join("\n", items.Select((text, index) => (index + 1).ToString(CultureInfo.InvariantCulture) + ". " + text)));
        }
        if (source.Kind != MessageKind.Poll) return (source.Kind, source.Text);
        var options = await dbContext.ConversationPollOptions.AsNoTracking().Where(o => o.PollId == source.Id)
            .OrderBy(o => o.Number).Select(o => new { o.Number, o.Text }).ToArrayAsync(ct);
        if (options.Length < 2) throw PollNotFound();
        return (MessageKind.Text, source.Text + "\n" + string.Join("\n", options.Select(o => o.Number.ToString(CultureInfo.InvariantCulture) + ". " + o.Text)));
    }
}
