# تصمیم‌های محصول Messenger v2

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

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

## Addendum تصمیم‌های STEP 14E

این بخش تصمیم‌های تکمیلی و صریح STEP 14E را ثبت می‌کند و تصمیم‌های قبلی را
بازنویسی نمی‌کند.

1. **Saved Messages یک Conversation شخصی یکتا است:** برای هر User حداکثر یک
   Conversation از نوع `Saved` به‌صورت lazy ساخته می‌شود. فقط همان مالک participant
   آن است؛ Organization، عضو دوم و notification پیام ندارد. متن، فایل و Forward در
   آن از همان pipeline امن پیام استفاده می‌کنند و Forward یک copy مستقل می‌سازد.
2. **Clear History حذف فیزیکی نیست:** این عملیات یک watermark خصوصی و monotonic
   برای User/Conversation ثبت می‌کند. پیام‌های قبلی از History، Search، Shared Media،
   File، Reply، Forward، Delta و unread همان کاربر خارج می‌شوند؛ copy دیگران و rows
   مشترک حذف نمی‌شوند و پیام‌های بعدی قابل مشاهده می‌مانند. retry idempotent است.
3. **اعلان پیام durable و کمینه است:** بعد از commit پیام، intent همان transaction
   در Outbox ثبت می‌شود. فرستنده اعلان نمی‌گیرد، participant لغوشده گیرنده نیست و
   برای هر `(MessageId, RecipientUserId)` فقط یک Notification ساخته می‌شود. اولویت
   type برابر `Mention > Reply > MessageReceived` است.
4. **Mute فقط delivery خارجی را suppress می‌کند:** پیام، unread، Notification داخلی،
   Delta و SignalR همچنان ثبت می‌شوند. preview بیرونی هیچ متن پیام، نام فایل، شماره
   تلفن یا metadata حساس ندارد.
5. **Contact Card snapshot امن است:** payload ذخیره‌شده فقط `UserId`، `Username` و
   `DisplayName` دارد. Phone Number ذخیره یا برگردانده نمی‌شود و دریافت card به‌تنهایی
   Contact یا Direct Conversation نمی‌سازد.
6. **Contact Sync هنوز مجوز پیاده‌سازی ندارد:** تا زمانی که consent، پروتکل upload
   حداقلی/hashed، provenance، revoke/delete، collision و failure isolation به‌صورت
   end-to-end تصویب نشوند، endpoint یا table آن ساخته نمی‌شود.
7. **Avatar و Presence فعلاً deferred هستند:** Avatar به قرارداد lifecycle/authorization
   فایل و Presence/LastSeen/Typing به semantics چند-instance، privacy و expiry نیاز
   دارند. نبود این تصمیم‌ها با مقدار ساختگی یا state پایدار حدس زده نمی‌شود.

نتیجه release در STEP 14E برای production دارای کودک همچنان
`BLOCKED_BY_PRODUCT_POLICY` است: adapter واقعی Child Safety و policy دقیق ساخت/عضویت
Personal Group/Channel برای حساب کودک تصویب نشده‌اند. runtime فعلی در نبود provider
واقعی برای Conversation حساس به کودک fail-closed است.

## Addendum مصوب GAP-05 — Child Safety Policy v1

تاریخ ۲۰۲۶-۱۰-۱۵: مالک پروژه همه تصمیم‌های P0 این قابلیت و اجرای امنیتی آن را صریحاً تأیید کرده است. `BLOCKED_BY_PRODUCT_POLICY` در بند تاریخی STEP 14E دیگر وضعیت جاری GAP-05 نیست؛ متن تاریخی بالا شواهد زمان خود را حفظ می‌کند. مرجع اجرای فعلی، [قرارداد GAP-05](GAP_05_MESSENGER_CHILD_SAFETY.md) است.

| تصمیم P0 | وضعیت و قرارداد مصوب جاری |
|---|---|
| ۱. طبقه سن | حل‌شده؛ Minor کمتر از ۱۸، Adult از ۱۸ پس از تأیید، Unknown مستقل از نقش و مانند Minor محافظت می‌شود |
| ۲. منبع/تأیید سن | حل‌شده؛ مدیر فقط با رابطه سازمانی معتبر، شاهد و audit؛ تعارض بدون overwrite و حل Owner با MFA تازه |
| ۳. Discovery | حل‌شده؛ Minor/Unknown عمومی کشف نمی‌شوند؛ context مجاز محدود و بدون PII حساس |
| ۴. Stranger Direct | حل‌شده؛ ارتباط غریبه با Minor/Unknown، حتی با شناسه، ممنوع است |
| ۵. جدایی مجوزها | حل‌شده؛ Find/Create/Send/Read مستقل و مجوز جاری سرور مرجع است |
| ۶. رابطه مجاز | حل‌شده؛ کلاس/assignment/enrollment/Guardian/رضایت Contact معتبر؛ بدون دسترسی والد یا مدیر به private chat دیگران |
| ۷. گروه/کانال شخصی | حل‌شده؛ Minor/Unknown فقط گروه خصوصی؛ دعوت رابطه‌دار با پذیرش صریح؛ کانال و ساخت عمومی ممنوع |
| ۸. گروه رسمی | حل‌شده؛ کلاس/سازمان و Enrollment/TeacherAssignment مرجع، نه participant دلخواه Messenger |
| ۹. تاریخچه | حل‌شده؛ پایان رابطه یا تغییر سن حذف مخرب خودکار ایجاد نمی‌کند |
| ۱۰. ارسال جدید | حل‌شده؛ حساب/محدودیت/Block/سن/رابطه/Permission/Safety در هر mutation دوباره بررسی می‌شوند |
| ۱۱. Block | حل‌شده؛ Direct جدید منع می‌شود، عضویت/نمرات/تاریخچه و اعلان رسمی خودکار حذف نمی‌شوند |
| ۱۲. تغییر سن | حل‌شده؛ اختیار آینده Discovery/Direct/Send/member/channel دوباره محاسبه می‌شود |
| ۱۳. Content Safety | حل‌شده؛ Internal Makan قاعده‌محور نسخه‌دار پشت IContentSafetyProvider؛ نه vendor یا AI ساختگی |
| ۱۴. نتیجه Safety | حل‌شده؛ Allowed، ReviewRequired، Blocked و Unavailable؛ مسیر محافظت‌شده در شکست fail-closed |

قرارداد تکمیلی مصوب: AbuseReviewer پرونده‌محور، پنجره عادی reported item و ۲ قبل/۲ بعد؛ گسترش محدود فقط با ارجاع، permission قوی‌تر، reason، MFA تازه و audit. School Manager یا Owner مجوز unrestricted private browsing ندارند. رسانه‌ای که موتور نمی‌تواند معناشناسی کند ReviewRequired است و Sent اعلام نمی‌شود. ضعف معنایی موتور، richer age assurance، appeal/SLA/retention و گزینه‌های آینده در register معوق ثبت می‌شوند؛ این‌ها اجازه کنارگذاشتن رفتار محافظتی فعلی نیستند.

## Addendum مصوب P0 — حساب شخصی و تأیید اولیه

در درخواست «MakanApp Messenger — COMPLETE P0 DELIVERY» مورخ ۲۰۲۶-۱۰-۱۶، مالک مسیر حداقلی تأیید اولیهٔ User شخصی را صریحاً مجاز کرد. OTP، تکمیل Profile و تاریخ تولد خوداظهاری به‌تنهایی Verified نمی‌سازند. اپراتور Platform با `platform.users.manage`، MFA و Step-up تازه، شاهد مجاز، reason و rowversion می‌تواند وضعیت `Unverified` را تأیید کند؛ School membership یا OrganizationPerson ساختگی شرط یا side effect نیست.

Initial verification، School verification و conflict resolution یکی نیستند. initial، `Verified/Conflict` را بازنویسی نمی‌کند و stale version رد می‌شود؛ conflict resolution همچنان Owner و workflow مصوب GAP-05 را می‌خواهد. شاهد جدید source صریح `PlatformVerification` دارد؛ شاهدها و طبقه قبلی حفظ می‌شوند. DOB در پاسخ عمومی یا Audit summary درج نمی‌شود.

`GET /api/v1/me/messaging-status` فقط راهنمای self با verification/effective category/action/next action/version است؛ authorization token یا روش پرس‌وجوی وضعیت فرد دیگر نیست. actionهای protected به رابطهٔ جاری وابسته‌اند؛ minor/unknown همچنان عمومی کشف نمی‌شوند و Group خصوصی به معنی مجوز Channel یا Group عمومی نیست. هر endpoint مقصد، policy جاری را دوباره بررسی می‌کند.

مالک آزمون پذیرش این تحویل را صریحاً **فقط محلی** خواسته است. هیچ تغییر یا تست روی سرور مجاز این مرحله نیست؛ شواهد LocalDB/TestServer/Kestrel محلی نباید به‌عنوان REAL SERVER PASS یا استقرار Production گزارش شوند. این تصمیم، سیاست امنیتی GAP-05 را عوض نمی‌کند.

## نتیجهٔ تکمیلی P0 — قرارداد و مجوز رویداد

- تست دو Verified Adult با تأیید اولیه و بدون school membership نشان داد جریان discover → Contact اختیاری → canonical Direct برقرار است؛ B02 به مدل Message Request جدید نیاز ندارد. مسیر protected همچنان رابطه/رضایت مصوب می‌خواهد و eligibility هر دو طرف سنجیده می‌شود.
- Username search/resolve حتی در صورت عمومی‌بودن visibility شماره، شماره نمی‌دهند. exact phone resolve و profile/contact مجاز همچنان privacy مستقل مقصد را رعایت می‌کنند. محدودیت ارسال actor در ساخت Direct هم اعمال می‌شود؛ history حذف نمی‌شود.
- مجوز conversation به‌تنهایی مجوز هر event نیست. Realtime باید visibility همان Delta را برای cursor/type/resource داشته باشد؛ FromJoin، Clear، DeleteForMe و state خصوصی در replay نیز معتبرند. audience hint فقط candidateها را محدود می‌کند و جای authorization نیست.
- JSON canonical رشته‌ای MVC/SignalR حفظ می‌شود؛ OpenAPI باید همان string/nullable/Flags را بگوید، نه اینکه runtime صحیح برای schema عددی تغییر کند. مقادیر/bit ناشناخته مجوز نمی‌دهند؛ parser عدد معتبر قدیمی فعلاً سازگاری دارد.
- read/delivered فعلی private self-state باقی‌اند؛ بستن نشت یک event خصوصی، Peer Read Receipt عمومی ایجاد نمی‌کند. B04، Push، Typing، Presence و قابلیت‌های پیشرفتهٔ ثبت‌شده در register، خارج P0 هستند.

این افزوده رفتار امنیتی مصوب را روشن می‌کند؛ Role مدرسه مجوز private chat نمی‌شود، GuardianRelation private history کودک را نمی‌گشاید، و Minor/Unknown یا failure Safety با verified adult آزاد جایگزین نشده‌اند. [قرارداد مصرف جاری](../messaging/MESSENGER_P0_FRONTEND_HANDOFF.md).

## تصمیم مصوب P2 — Presence و Last Seen

مالک در ۲۰۲۶-۱۰-۱۶، Master PRD v3.0 را target مصوب و **Contacts** را پیش‌فرض OnlineAudience و LastSeenAudience تأیید کرد. عبارت deferred قبلی Presence مربوط به scope تاریخی P0/P1 است؛ این تصمیم فقط P2-1 تا P2-4 را باز می‌کند. Contacts مطابق دفترچهٔ مقصد است، نه viewer؛ رابطهٔ Direct مجاز موجود و eligibility جاری GAP-05 لازم‌اند. Block در هر جهت Hidden می‌کند. زمان دقیق فقط با مجوز Last Seen و وقتی target Online نیست برمی‌گردد؛ heartbeat، راه فرعی افشای Online مخفی نیست. مجوز Guardian به چت شخص ثالث یا مدیر به private Direct گسترش نمی‌یابد.

Typing و Presence گذرا و تک‌process هستند؛ تاریخچهٔ SQL/Delta/Outbox ندارند. default دو audience در migration روی Contacts backfill می‌شود؛ درخواست قدیمی privacy که دو فیلد را ندارد، مقدار جاری آن‌ها را حفظ می‌کند. state مشترک چندگرهی، Recording و Checklist از این تصمیم مجوز ضمنی نمی‌گیرند. [قرارداد و محدودیت واقعی](../messaging/MESSENGER_P2_FRONTEND_HANDOFF.md).

## اجرای P2 — مدیریت پیشرفته بدون گسترش اختیار مدرسه

Group/Channel مدیریت‌شدهٔ کاربر از Owner/Admin/Member و granular permission موجود استفاده می‌کنند؛ flag تازه به Adminهای قدیمی خودکار داده نمی‌شود. لینک bearer رضایت و current-state GAP-05 را دور نمی‌زند؛ source context سازنده و context درخواست‌کننده جداگانه و دوباره سنجیده می‌شوند. approval queue با عضویت فعال یکی نیست. Ban از مسیرهای Add/Join/دعوت قبلی/لینک اعمال و Unban بدون بازگردانی خودکار عضویت است. لینک، نام کاربری، Membership و GuardianRelation مجوز مرور private Direct نمی‌سازند؛ مرز کلاس رسمی و moderation قبلی حفظ است. جزئیات پیاده‌شده و شواهد آزمون در ممیزی P2 ثبت‌اند؛ این افزوده ادعای بسته‌شدن همهٔ P2 نیست.

## قرارداد نهایی مصوب Poll / Quiz در P2 — ۲۰۲۶-۱۰-۱۶

مالک قرارداد جایگزین را صریحاً Freeze کرد؛ پیشنهاد aggregate-only قبلی تأیید نشد و مرجع نیست. سازندهٔ Poll هنگام ساخت Anonymous یا NonAnonymous انتخاب می‌کند. Anonymous هویت رأی‌دهندگان را در API/UI عمومی نمایش نمی‌دهد؛ NonAnonymous فقط برای اعضای دارای مجوز همان Conversation قابل مشاهده است. رأی Poll تا Close قابل تغییر است؛ Close نهایی و بدون Reopen در P2، و پس از آن ثبت یا تغییر رأی ممنوع است. بستن توسط Creator با اختیار جاری یا Owner/Admin دارای permission مناسب همان resource است؛ دانستن شناسه مجوز نیست.

Quiz فقط SingleChoice، دقیقاً یک CorrectOption و برای هر User یک پاسخ نهایی غیرقابل تغییر دارد. CorrectOption پیش از پاسخ خود کاربر افشا نمی‌شود؛ Explanation اختیاری نیز راه فرعی افشای پاسخ نیست. بسته‌شدن مجوز پاسخ تازه یا افشای correct به کاربرِ پاسخ‌نداده ایجاد نمی‌کند. Close همان مرز اختیار جاری را دارد. Quiz مستقل Messenger است و Exam/Assessment/Enrollment/نمره ایجاد نمی‌کند. SQL منبع نهایی Vote، unique constraint مانع duplicate و Vote×Close تراکنشی و deterministic است. این بخش تصمیم target است؛ وجود code/migration/tests و ممیزی فاز مستقل، تنها شاهد تحویل اجرایی خواهند بود. سیاست stricter GAP-05 و history/Block/session/MFA/moderation موجود پابرجاست.

Forward Poll/Quiz با تأیید صریح بعدی مالک: نسخهٔ متنی مستقل پرسش و گزینه‌ها، بدون رأی، پاسخ صحیح و Explanation؛ مقصد رأی‌گیری زنده نمی‌شود. این تصمیم جای گزینه‌های پیشنهادشدهٔ قبلی را می‌گیرد. در اجرای P2، تعریف منتشرشده immutable، رأی mutable Poll با OwnVote rowversion و Close با Poll rowversion است؛ Creator باید اختیار جاری و Owner/Admin باید DeleteMessages جاری داشته باشند. Quiz GET برای creator پاسخ‌نداده نیز استثنای افشا ندارد؛ Close پاسخ را برای بی‌پاسخ‌ها باز نمی‌کند. تاریخچه و revocation از قرارداد موجود تبعیت می‌کنند؛ رأی یا شمارش مجوز مرور private conversation نیست.
