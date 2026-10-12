# تصمیم‌های محصول Messenger v2

این سند قرارداد محصول Messenger v2 را برای مراحل `14B` تا `14E` تثبیت می‌کند. این تصمیم‌ها رفتار فعلی کد را توصیف نمی‌کنند؛ اختلاف رفتار فعلی با این قرارداد در [ممیزی STEP 14A](STEP_14A_MESSENGER_PRD_V2_AUDIT.md) ثبت شده است. تغییر هر تصمیم در آینده به تصمیم صریح Product و ثبت ADR یا سند جایگزین نیاز دارد.

## مرز این سند

- هویت شخصی Messenger متعلق به حساب سراسری Makan است و به یک سازمان خاص تعلق ندارد.
- دفترچه Contacts شخصی است؛ عضویت مدرسه یا نقش سازمانی به مدیر، معلم یا والد اجازه مشاهده Contacts یا گفت‌وگوی خصوصی دیگران نمی‌دهد.
- Conversationهای `Organization` همچنان به عضویت معتبر همان سازمان و مجوز منبع وابسته‌اند.
- این سند الگوریتم یا provider تعدیل محتوا، policy حقوقی retention و schema نهایی را انتخاب نمی‌کند.

## تصمیم‌های تثبیت‌شده

1. **Personal Direct عمومی میان حساب‌های معتبر Makan:** ارتباط مستقیم شخصی اصولاً میان حساب‌های Makan مجاز است و به رابطه مدرسه، کلاس، معلم/دانش‌آموز، والد/معلم، عضویت مشترک سازمان یا `PersonalCommunicationGrant` وابسته نیست. Privacy، Discovery، Block، اعتبار حساب و Child Safety همچنان باید اعمال شوند.
2. **Contact شرط Direct نیست:** کاربر می‌تواند از Search یا Profile مجاز، پس از بررسی Privacy، Direct را شروع یا بازیابی کند. ذخیره Contact اختیاری است و به‌تنهایی مجوز پیام‌دادن ایجاد نمی‌کند.
3. **بدون سقف محصولی ثابت برای فایل:** برای اندازه یک فایل، عدد ثابت تجاری/محصولی تعریف نشده است. محدودیت فنی قابل‌تنظیم برای امنیت، ظرفیت HTTP و Storage الزامی و از Product Limit متمایز است.
4. **بدون سقف محصولی ثابت برای عضو:** برای تعداد عضو Group یا subscriber/member در Channel عدد تجاری hard-coded تعریف نشده است. safeguard فنی قابل‌تنظیم در صورت نیاز مجاز است.
5. **مهلت Edit:** فرستنده فقط تا `SentAtUtc + 1 hour` می‌تواند پیام خودش را به‌صورت عادی ویرایش کند.
6. **مهلت Delete For Everyone:** فرستنده فقط تا `SentAtUtc + 1 hour` می‌تواند پیام را برای همه حذف کند. `Delete For Me` رفتار مستقلی دارد و این سند مهلت آن را تعیین نمی‌کند.
7. **حفظ شاهد داخلی بدون افشای عمومی:** revision، tombstone و شاهد لازم برای audit می‌تواند داخلی باقی بماند، اما متن قبلی یا محتوای حذف‌شده نباید از API عادی، Search، Shared Media، Reply یا Delta به کاربر عادی نشت کند.
8. **Group History قابل تنظیم:** مشاهده پیام‌های پیش از عضویت توسط عضو جدید تابع setting گروه است. نام field و schema در این مرحله انتخاب نمی‌شود؛ پیاده‌سازی باید Search، Shared Media، Reply و Pin را نیز با همان مرز هماهنگ کند.
9. **Block فقط ارتباط خصوصی را متوقف می‌کند:** Block ارسال Direct جدید را متوقف می‌کند، اما عضویت مشترک Group/Channel را حذف نمی‌کند و پیام‌های مجاز گروه مشترک را خودکار پنهان نمی‌کند.
10. **Report پیام‌محور و کمینه:** Report می‌تواند روی پیام مشخص ثبت شود و فقط شاهد و context ضروری را نگه دارد. دسترسی reviewer باید به permission صریح و scope همان منبع وابسته باشد.
11. **Manager دسترسی سراسری به چت خصوصی ندارد:** `OrganizationRole.Manager` به‌تنهایی اجازه مشاهده Personal Direct، دفترچه Contacts یا شاهد Report خصوصی را ایجاد نمی‌کند.
12. **معماری پیشگیری و escalation برای Child Safety:** محتوای ممنوع یا پرخطر مربوط به ایمنی کودک نباید به‌صورت عادی publish/deliver شود و رخداد پرخطر باید سریع برای review/escalation ظاهر شود. provider، مدل و vendor هنوز انتخاب نشده‌اند.
13. **Personal Group/Channel مجاز است:** همه حساب‌های عادی، تابع policy حساب و ایمنی، می‌توانند Group و Channel شخصی `Public` یا `Private` بسازند. Organization context نباید آن‌ها را خودکار سازمانی کند.
14. **Organization badge:** DTO گفت‌وگوی سازمانی باید حداقل هویت امن و لازم سازمان را برای badge فرانت‌اند فراهم کند، بدون افشای جزئیات داخلی سازمان.
15. **Retention حل‌نشده است:** مدت نگه‌داری Report evidence، سیاست حذف داده کاربر، reviewer دقیق Personal Direct و چرخه appeal هنوز `UNRESOLVED_PRODUCT_POLICY` هستند.

## مرزهای امنیت و حریم خصوصی

- Phone visibility و discoverability by phone دو permission مستقل‌اند و باید در Contacts، Search، Invite و Contact Card یکسان اعمال شوند.
- دانستن `UserId`، Username، Phone، ConversationId، MessageId یا FileAssetId مدرک authorization نیست.
- Search و Shared Media در هر درخواست باید مجوز جاری را دوباره ارزیابی کنند؛ cache یا cursor قدیمی مجوز ایجاد نمی‌کند.
- Organization Group/Channel فقط در مرز همان Organization قابل دسترسی است و مدرسه دیگر یا کاربر خارجی با شناسه مستقیم نباید وارد شود.
- داده خام گفت‌وگوی خصوصی Student نباید وارد context هوش مصنوعی Parent، Teacher یا Manager شود. هیچ محتوای خصوصی Messenger در مراحل 14 به `MakanApp-AI` ارسال نمی‌شود مگر با تصمیم امنیتی مستقل آینده.

## سیاست‌های حل‌نشده

موارد زیر عمداً freeze نشده‌اند و نباید در پیاده‌سازی حدس زده شوند:

- مدت دقیق retention برای Report evidence، revision، tombstone، ChangeEvent و outbox؛
- policy حقوقی حذف/نگه‌داری داده حساب؛
- reviewer نهایی همه Reportهای Personal Direct؛
- appeal، escalation SLA و lifecycle کامل moderation؛
- provider/model/vendor تعدیل متن و attachment؛
- semantics شمارش view در Channel؛
- نام field و schema نهایی Group History؛
- policy دقیق حساب کودک برای ساخت Personal Group/Channel و افزودن عضو؛
- مهلت یا رفتار `Delete For Me`؛
- سقف‌های فنی محیطی برای member count، file size و request body.

## نگاشت به مراحل آینده

| مرحله | خروجی مورد انتظار |
|---|---|
| `14B` | Contacts، Profile Privacy، Discovery، Global Personal Direct، Block integration و foundation اختیاری Contact Sync |
| `14C` | Edit/Delete window، Delete For Me، Group History، Public/Private، organization badge، permissionهای granular، Archive/Mute و تکمیل chat list/media |
| `14D` | routing و authorization گزارش، حداقل evidence، `SafetyIncident`، queue و pre-delivery safety hook بدون انتخاب provider ساختگی |
| `14E` | اجرای دوباره ماتریس کامل `CNT/PRF/LST/DM/GRP/CHN/MSG/NTF/ORG/AC` و بستن gapهای باقی‌مانده |

## کنترل تغییر

این سند در STEP 14A فقط تصمیم محصول را ثبت می‌کند و هیچ migration، endpoint یا رفتار runtime ایجاد نمی‌کند. هر تغییر آینده باید:

1. تصمیم Product جدید را صریح ثبت کند؛
2. اثر حریم خصوصی، authorization، migration و قرارداد فرانت‌اند را مشخص کند؛
3. تست Unit/Integration متناسب و migration قابل بازتولید داشته باشد؛
4. رفتار ناسازگار قبلی را به‌صورت آگاهانه و قابل ردیابی جایگزین کند.
