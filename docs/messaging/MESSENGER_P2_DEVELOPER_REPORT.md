# گزارش آموزشی توسعه‌دهنده — Messenger P2

## فاز اول: P2-0 تا P2-4

### خلاصه و معماری

قبلاً Typing/Presence وجود نداشت؛ اکنون hint گذرا با TTL، چندنشستی و دو audience مستقل وجود دارد. Domain فقط قواعد تنظیم حریم خصوصی را نگه می‌دارد. `MessagingEphemeralService` در Application هماهنگ‌کنندهٔ مجوز جاری و انتشار است؛ پورت‌های آن به SQL یا SignalR وابسته نیستند. `InMemoryMessagingEphemeralState`، `EfMessagingStore.Ephemeral.cs` و `MessagingEphemeralDispatcher` در Infrastructure قرار دارند چون حافظهٔ process، query و worker جزئیات اجرا هستند. Hub، notifier مبتنی بر SignalR و Controller در Api هستند.

### فایل‌های مهم و جریان اجرا

- `MakanApp.Application/Messaging/MessagingEphemeralContracts.cs`: DTO و پورت state/access/notifier و سقف‌های معتبر؛ مرز Application و adapter را صریح می‌کند.
- `MakanApp.Application/Messaging/MessagingEphemeralService.cs`: Resolve نشست → مجوز جاری → state → بازبینی مجوز هنگام dispatch → notifier. hint قدیمی، مجوز جدید ایجاد نمی‌کند.
- `MakanApp.Infrastructure/Messaging/InMemoryMessagingEphemeralState.cs`: lock کوتاه برای mutation و aggregate چنداتصالی؛ I/O بیرون lock است. dispatch مجزا سریال شده تا hintها جابه‌جا ارسال نشوند.
- `MakanApp.Infrastructure/Messaging/EfMessagingStore.Ephemeral.cs`: reuse مجوزهای جاری GAP-05 و Peer Read؛ نه نسخهٔ موازی Child Safety و نه write به history.
- `MakanApp.Api/Realtime/SignalRMessagingEphemeralNotifier.cs`: انتشار به ConnectionIdهای دقیق مجاز، نه broadcast نامحدود group.
- `MakanApp.Api/Controllers/PeoplePresenceController.cs`: HTTP → Application → پورت EF/SQL و state → DTO → HTTP؛ هیچ قانون کسب‌وکار در Controller نیست.

`MessagingPrivacySettings` و مسیر Directory/Resolver/EF Configuration برای دو audience تغییر کردند. `MessagingService.SendMessageAsync` پس از موفقیت یا retry پذیرفته‌شده، Typing همان نشست را متوقف می‌کند. Hub lifecycle ثبت/حذف state را انجام می‌دهد؛ `Program.cs` و DI adapterها را متصل می‌کنند. fixture تست worker گذرا را دستی flush می‌کند تا ساعت و انتشار قطعی باشند؛ notifier آزمایشی انتشار واقعی SignalR را نیز اجرا می‌کند.

### دیتابیس و قرارداد

Migration `20261016163433_AddMessagingPresencePrivacy`: دو ستون int غیرnullable با Contacts=2، CHECK مقدار 1/2/3؛ PK/FK و rowversion موجود حفظ شده‌اند. این default تصمیم مصوب privacy است، نه زمان یا اطلاعات فعالیت. state حضور در SQL نیست. endpointها، DTO، مجوز و خطاهای واقعی در `MESSENGER_P2_FRONTEND_HANDOFF.md` آمده‌اند؛ همان سند بخشی از این گزارش است.

### تصمیم‌ها و trade-off

state تک‌process پشت پورت به‌جای Redis جدید انتخاب شد؛ ساده و bounded است اما چندسروری نیست. Last Seen حافظه‌ای بعد از restart/retention از بین می‌رود. audience مصوب Contacts با رابطهٔ معتبر ترکیب می‌شود؛ Everyone به‌تنهایی مجوز مشاهدهٔ غریبه نیست. انتشار Hidden ناشی از فعالیت مقصد حذف شده تا timing side-channel ایجاد نکند؛ در نتیجه کلاینت موظف به انقضای snapshot و refresh است. رویداد گذرا durable/Delta نیست و تحمل از‌دست‌رفتن آن بر TTL تکیه دارد.

### تست‌ها و نکات آموزشی

`MessagingEphemeralStateTests` با ساعت ورودی، aggregate، TTL، ظرفیت/بودجه، مالک اتصال و حفظ privacy در درخواست قدیمی را می‌سنجد؛ I/O ندارد. `MessagingP2EphemeralEndpointsTests` روی HTTP/Hub واقعی TestServer و SQL Server ایزوله، مجوز گیرنده، Block در dispatch، دو نشست، انقضای ناگهانی، لغو نشست، subscriber کانال، حذف عضو و لغو رابطهٔ Guardian برای Minor/Unknown را می‌سنجد؛ بنابراین Integration است. تست‌های Architecture موجود برای جهت وابستگی اجرا می‌شوند، نه تست بی‌معنای جدید صرفاً برای تعداد.

۱. عضویت SignalR group، مجوز امنیتی نیست. ۲. Online از lease معتبر می‌آید، نه User موجود. ۳. وضعیت چنداتصالی باید aggregate شود. ۴. فیلد اختیاری افزوده‌شده نباید PUT قدیمی را به reset مخفی تبدیل کند. ۵. زمان دقیق heartbeat می‌تواند privacy Online را دور بزند؛ این مسیر در DTO بسته است. ۶. تست اتصال باید پایان آماده‌شدن Hub را با یک فرمان تأیید کند، نه Sleep.

### مسیر مطالعه و ادامه

`PeoplePresenceController` → `MessagingHub` → `MessagingEphemeralService` → `MessagingPrivacySettings` → پورت‌های Ephemeral → state حافظه‌ای و partial EF → Configuration → Migration → Unit و Integration بالا. گام بعد سازمان‌دهی Saved و پوشه‌های شخصی است؛ هیچ پوشه‌ای نباید مجوز گفتگو ایجاد کند. Validation و Git هر فاز پس از اجرای واقعی به ممیزی و گزارش نهایی اضافه می‌شوند؛ این متن به‌تنهایی ادعای بسته‌شدن P2 نیست.

### Validation واقعی فاز اول

restore و build/final build موفق با صفر warning/error؛ Unit=507، Integration=820، Architecture=14، مجموع=1341 passed، failed/skipped=0. هشت Unit و نوزده Integration به baseline افزوده شدند. migration روی SQL Server تست ایزوله اجرا شد و `has-pending-model-changes` تغییری نیافت؛ روی سرور یا دیتابیس عادی اجرا نشده است. جزئیات دو دور و تشخیص فشار حافظه در ممیزی ثبت شده‌اند. پیام انتخاب‌شده برای commit متمرکز این فاز `feat(messaging): add private typing and presence leases` است؛ hash و نتیجهٔ push فقط پس از اجرای Git گزارش می‌شوند. محدودیت‌های فعلی: فقط تک‌process، Last Seen حافظه‌ای، بدون Recording، و باقی فازهای P2 هنوز در این commit نیستند.
