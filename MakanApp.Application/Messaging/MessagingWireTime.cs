namespace MakanApp.Application.Messaging;

internal static class MessagingWireTime
{
    // زمان ذخیره‌شده UTC است؛ بازیابیِ Kind از دست‌رفته، ساعت را جابه‌جا نمی‌کند.
    internal static DateTime FromStoredUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal static DateTime? FromStoredUtc(DateTime? value) => value.HasValue ? FromStoredUtc(value.Value) : null;
}
