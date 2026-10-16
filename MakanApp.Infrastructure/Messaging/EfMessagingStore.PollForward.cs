using System.Globalization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    // کپی مستقل فقط پرسش و گزینه‌هاست؛ این مسیر اصلاً جدول رأی یا کلید Quiz را نمی‌خواند.
    private async Task<(MessageKind Kind, string? Text)> BuildForwardContentAsync(Message source, CancellationToken ct)
    {
        if (source.Kind != MessageKind.Poll) return (source.Kind, source.Text);
        var options = await dbContext.ConversationPollOptions.AsNoTracking().Where(o => o.PollId == source.Id)
            .OrderBy(o => o.Number).Select(o => new { o.Number, o.Text }).ToArrayAsync(ct);
        if (options.Length < 2) throw PollNotFound();
        return (MessageKind.Text, source.Text + "\n" + string.Join("\n", options.Select(o => o.Number.ToString(CultureInfo.InvariantCulture) + ". " + o.Text)));
    }
}
