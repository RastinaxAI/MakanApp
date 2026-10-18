# ممیزی و شواهد تحویل Messenger P2

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## P2-0 — مبنای واقعی و منابع

شروع از `0a93e800cb5c91d0545ae6a11513fd2fed98f5a5` در worktree مستقل `D:\Project\MakanApp\MakanApp-Backend-Messenger-P0`، شاخهٔ `messenger/p0` با upstream موجود `origin/main`. workspace اصلی و dirtyهای AI دست‌نخورده‌اند. PDF مرجع جدید، ورودی صاحب پروژه و در شروع این اجرا untracked بود؛ bytes آن تغییر نمی‌کنند.

PDF چهارده‌صفحه‌ای `MakanApp_Messenger_Master_PRD_v3.0_FA.pdf` کامل خوانده شد؛ SHA256 برابر `22ADBBFE2DE53B168CDF964FB2FA3B907057CB9780237F011D8634921B8A1BBD` است. با وجود عبارت پیشنهادی داخل PDF، تأیید صریح مالک در درخواست جاری، آن را TARGET PRODUCT مصوب کرده است؛ این تأیید به معنی وجود runtime نیست. تصمیم‌های سخت‌گیرانهٔ جاری Messenger و GAP-05، از جمله پنجرهٔ یک‌ساعته Edit/Delete، رضایت کودک، منع کانال شخصی Minor/Unknown و مرز moderation حفظ می‌شوند.

baseline قبلی طبق دستور مالک تکرار نشد: restore/build موفق، ۰ warning/error؛ Unit=499، Integration=801، Architecture=14، Total=1314 passed، failed=0، skipped=0. TRX در `.artifacts/messenger-p2/tests/baseline` است. EF pending-model check موفق بود. تأخیر قبلی فقط دسترسی به همین PDF بود و با دریافت واقعی فایل رفع شد.

| حوزه | وضعیت قبل از تغییر P2 | شاهد و مرز |
|---|---|---|
| Typing / Recording | MISSING | Hub فقط subscribe/unsubscribe دارد؛ Message/Outbox جای وضعیت گذرا نیست |
| Presence / Last Seen | MISSING | lease/heartbeat/privacy مخصوص نداریم؛ LastSeenAtUtc دستگاه Push، حضور کاربر نیست |
| Saved | PARTIAL | canonical Saved یکتا، متن/فایل/جست‌وجو/Forward مستقل موجود؛ تگ و سازمان‌دهی پیشرفته نداریم |
| Folders | MISSING | مدل، override و API پوشه وجود ندارند |
| Group / Channel granular permissions | PARTIAL | ManageMembers/RemoveMembers/ManageAdmins/Publish/History/Visibility و rowversion اجرا شده‌اند؛ مدل موازی ساخته نشود |
| targeted invitation | IMPLEMENTED | ConversationInvitation مصوب GAP-05؛ همان رضایت/رابطه در acceptance |
| bearer invite links / Join Requests | MISSING | دعوت target-bound، معادل لینک چندبارمصرف یا درخواست عضویت نیست |
| Topics | MISSING | TopicId و lifecycle موضوع در مدل پیام نیست |
| Poll / Quiz / Checklist | MISSING | MessageKind فقط شش نوع پایه دارد؛ این تعامل‌ها در Assessment ساخته نشوند |
| Scheduled messages | MISSING | workerهای Notification/Outbox موجودند، اما scheduler پیام نیستند |
| Silent messages | MISSING | flag مستقل intent ارسال وجود ندارد؛ Mute گیرنده با Silent فرستنده فرق دارد |
| Advanced Search | PARTIAL | conversation/sender/date/kind و cursor مجاز موجود؛ hasAttachment و لینک فیلتر مستقل ندارند |
| shared ephemeral / backplane | DEFERRED_BY_POLICY | وابستگی جدید Redis صرفاً برای P2 مجاز نیست؛ تک‌instance صریح بماند |

## تصمیم جدید مصوب Presence

مالک در پاسخ جاری، پیش‌فرض **Contacts** برای OnlineAudience و LastSeenAudience را انتخاب کرد. Contacts از دفترچهٔ مقصد است؛ مشاهده در ارتباط مجاز موجود، بازبینی رابطهٔ جاری GAP-05، Block دوطرفه → Hidden، زمان دقیق فقط با مجوز Last Seen و وضعیت گذرا بدون تاریخچهٔ SQL باقی‌اند. هیچ تصمیم قبلی Child Safety دوباره باز نشده است.

## مرزهای باقی‌مانده

`P1_PUSH_RUNTIME_PENDING` حفظ می‌شود؛ دریافت واقعی Web Push/FCM، سرور، frontend، ملی‌پیامک و AI/RAG در این تحویل تغییر/تأیید نمی‌شوند. `DISTRIBUTED_EPHEMERAL_STATE=DEFERRED` و `MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA` تا وقتی فقط حافظهٔ یک process استفاده می‌شود برقرارند. PRD نام Checklist را دارد ولی قرارداد actor/item/ownership/version دقیق ندارد؛ طبق استثنای صریح پرامپت، قرارداد آن جدا بررسی و در نبود تصمیم، `P2_CHECKLIST_REQUIRES_PRODUCT_DECISION` ثبت می‌شود؛ بقیهٔ فازها متوقف نمی‌شوند.

شواهد implementation، آزمون، migration و Git هر فاز فقط پس از اجرای واقعی به این سند افزوده می‌شوند؛ جدول بالا وضعیت تاریخی شروع است، نه ادعای تحویل P2.

## P2-1 تا P2-4 — پیاده‌سازی و آزمون‌های گذرا

Typing با TTL شش ثانیه، refresh حداقل سه ثانیه، چنداتصالی، stop بعد از send/unsubscribe/disconnect و بازبینی current-state در command و dispatch پیاده شد. Presence با heartbeat ده‌ثانیه‌ای، lease ۴۵ ثانیه، نگهداری حافظه‌ای محدود، نشست فعال SQL و privacy مستقل پیاده شد. نه Hidden ناشی از فعالیت و نه Recently بدون timestamp ناشی از Online مخفی، broadcast نمی‌شوند؛ زمان رویداد هم نباید oracle شود. `GET /api/v1/people/{userId}/presence` rate-limited است.

Migration `20261016163433_AddMessagingPresencePrivacy` بررسی شد؛ فقط دو ستون audience، default مصوب Contacts و دو CHECK دارد. هیچ migration روی دیتابیس عادی یا سرور اجرا نشده؛ fixture به SQL Server LocalDB ایزولهٔ `MakanApp_MessagingSafety_IntegrationTests_Step7E` محدود است.

آزمون هدفمند نهایی: Unit=8 و Integration=19، همگی passed با failed/skipped=0؛ TRX هدفمند Integration در `.artifacts/messenger-p2/tests/ephemeral-final-focused` است. شامل گیرندهٔ دقیق Hub، نبود Message/Delta، TTL بدون disconnect، دو نشست، Block پس از enqueue، لغو نشست واقعی OTP محلی، کانال read-only، حذف عضو، لغو Guardian برای Minor/Unknown، privacy Contacts مطابق دفترچهٔ مقصد، حفظ تنظیم جدید با PUT قدیمی، timing privacy و 429 است. هیچ SMS واقعی ارسال نشده است.

restore موفق بود. build اول و build نهایی بدون warning/error موفق شدند. دور کامل اول: Unit=507، Integration=818، Architecture=14، Total=1339 passed، failed/skipped=0؛ `.artifacts/messenger-p2/tests/phase1-full`. دو تست مکمل بعد از این دور افزوده شدند. دور کامل نهایی روی نسخهٔ ثابت: **Unit=507، Integration=820، Architecture=14، Total=1341 passed، failed=0، skipped=0**؛ TRX در `.artifacts/messenger-p2/tests/phase1-final-validated`. EF pending model changes: NONE. اجرای نهایی قدیمی هنگام rebuild تست اصلاح‌شده متوقف شد و به‌عنوان PASS شمرده نمی‌شود.

دور نهایی SQL حدود ۱۱ دقیقه و ۲۶ ثانیه بود؛ بررسی فقط‌خواندنی دیتابیس تست `RESOURCE_SEMAPHORE` و علامت فشار حافظهٔ SQL را نشان داد. `dotnet build-server shutdown --msbuild --vbcscompiler` پس از اتمام build برای آزادسازی helperهای بلااستفاده اجرا شد؛ نه SQL restart شد، نه تست حذف/skip شد، نه تنظیم امنیتی عوض شد. نتیجهٔ واقعی suite موفق است؛ این زمان، benchmark ظرفیت Production نیست.

Git فاز گذرا: commit `8d0b92ae33718113d522827f561e57520f392651`، پیام `feat(messaging): add private typing and presence leases`، branch=`messenger/p0`، upstream=`origin/main`، origin=`https://github.com/RastinaxAI/MakanApp.git`. commit/push/fetch و HEAD==origin/main موفق شدند؛ worktree پس از آن تمیز بود. فقط ۳۶ فایل مرتبط و PDF مصوب بدون تغییر bytes commit شدند؛ AI/ملی‌پیامک/frontend/config/server دخیل نبودند.

## P2-5/P2-6 — Saved Labels و Chat Folders

canonical Saved، media و Forward قبلی reuse شدند. label-set فقط روی Message قابل‌مشاهدهٔ Saved خود مالک است؛ source metadata جدید ساخته نشده است. پوشه‌ها مالک‌محور، دارای فیلتر type/unread، override include/exclude، ترتیب و pin مستقل‌اند؛ query پایهٔ مجاز پیش از pagination فیلتر می‌شود. حذف پوشه هیچ conversation/member/message را حذف نمی‌کند. تغییر چنددستگاهی با rowversion و REST همگراست؛ event ساختگی ساخته نشده است.

Migration بررسی‌شده `20261016170915_AddMessagingPersonalOrganization` چهار جدول presentation و PK/FK/index/CHECK/rowversion واقعی دارد؛ فقط LocalDB تست migrate شده است. batch authorization برای حداکثر ۲۰۰ override جلوی N+1 جدید را می‌گیرد. mutationها Serializable، GET مجموعه/نسخه RepeatableRead، و replace فرزندان با دو Save در یک transaction هستند.

هدفمند: Integration=12 passed، failed/skipped=0، TRX در `.artifacts/messenger-p2/tests/presentation-focused`. Unit شش حالت جدید دارد. restore و build/final build موفق با صفر warning/error. هر دو دور کامل اول و نهایی: **Unit=513، Integration=832، Architecture=14، Total=1359 passed، failed/skipped=0**؛ `.artifacts/messenger-p2/tests/phase2-full` و `.artifacts/messenger-p2/tests/phase2-final`. EF pending model changes: NONE. دور نهایی Integration حدود ۱۱ دقیقه و ۳۶ ثانیه بود؛ این smoke/validation محلی است، نه benchmark Production.

Git فاز شخصی: `d317aa446443dc58811cfaa04970612c468f375d`، پیام `feat(messaging): add folders and advanced saved organization`؛ ۳۳ فایل مرتبط، push معمولی به origin/main و fetch/برابری HEAD با remote موفق و worktree تمیز بود. branch/upstream و مرز workspace همان فاز اول است؛ فایل AI، frontend، config یا ملی‌پیامک وارد commit نشد.

## P2-7 تا P2-10 — مدیریت، لینک و درخواست عضویت

سه permission واقعی ManageInvites/BanMembers/ChangeInfo، token تصادفی hash-only، لینک idempotent با token یک‌بارقابل‌دریافت، expiry/revoke/max-use، JoinRequest خصوصی و محدودیت/ban نسخه‌دار روی Group/Channel مدیریت‌شدهٔ کاربر اضافه شدند. Owner/Admin/Member، دعوت target-bound، history policy، مالکیت، کلاس رسمی و GAP-05 reuse شدند؛ نقش مدرسه مجوز private Direct نیست. actor/role/session و context هر دو طرف لینک سازمانی داخل transaction بازبینی می‌شوند. انقضای زمانی با Clock سرور است و CurrentRole سازنده با نقش مدیر approval جایگزین نمی‌شود.

Migration `20261016174429_AddConversationInvitesAndRestrictions` سه جدول و CHECK permission=4095 دارد؛ backfill خودکار Admin permission وجود ندارد. FK مرکب، اتصال درخواست به **همان** لینک/گفتگو، unique filtered Pending، rowversion و CHECK سقف استفاده واقعی‌اند. Down قبل از حذف جدول در صورت استفاده از flag تازه توقف می‌کند؛ permission تاریخی خودکار کم نمی‌شود. فقط fixture LocalDB migrate شده؛ سرور/دیتابیس عادی تغییر نکرده است.

هدفمند نهایی: **Integration=26 و Unit=5 passed، failed/skipped=0**؛ TRX Integration در `.artifacts/messenger-p2/tests/management-final-focused`. تست FK در suite کامل، SQL error=547 و نام constraint دقیق را نیز بررسی می‌کند. coverage شامل دو نوع Group/Channel، hash/token/audit، retry، آخرین مصرف هم‌زمان، approval×revoke، revoke-permission×create، جداسازی contextهای سازمانی، نقش منقضی سازنده، نسخهٔ info/restriction، Ban در Join/Add/Link/دعوت قبلی، انقضا، FromJoin، نشست لغوشده و Minor/Unknown/stranger است.

دو شکست اولیهٔ Child Safety، fixture را آشکار کردند: helper از ابتدا Owner را در دفترچهٔ target محافظت‌شده ذخیره می‌کرد و این طبق GAP-05 رابطهٔ رضایتی است. تست با حذف Contact، Everyone صرفاً برای جداسازی guard رابطه، سپس ثبت Contact خود target و حذف مجدد آن اصلاح شد؛ policy ضعیف نشد. یک خطای واقعی نیز پیدا شد: bit ناشناخته در مسیر role به exception دامنه/500 می‌رسید؛ validation Application اکنون RoleInvalid/400 می‌دهد و عدم تغییر role تست شده است.

restore و build/final build موفق با صفر warning/error؛ هر دو دور کامل اول و نهایی **Unit=518، Integration=858، Architecture=14، Total=1390 passed، failed/skipped=0** در `.artifacts/messenger-p2/tests/phase3-full` و `.artifacts/messenger-p2/tests/phase3-final`. دور نهایی Integration حدود ۱۱ دقیقه و ۳۴ ثانیه بود؛ EF pending model changes: NONE. P2 کامل و real Push از این بخش نتیجه نمی‌شوند. پیام commit متمرکز `feat(messaging): add invite links and join requests` است؛ نتیجهٔ Git فقط پس از اجرای واقعی ثبت می‌شود.

Git فاز سوم: `f93a9ed98bd2674b257accd03324b8d797eaec56` با همان پیام؛ ۳۷ فایل مرتبط، branch=`messenger/p0`، upstream=`origin/main`. push معمولی، fetch و HEAD==origin/main موفق شدند و worktree تمیز بود. AI، ملی‌پیامک، appsettings، frontend و سرور تغییری نداشتند.

## P2-11 — Topics

مدل Topic مختص Group، Open/Closed و بازگشایی صریح، عنوان، سازنده، UTC و rowversion پیاده شد. ManageTopics مستقل و بدون backfill permission است؛ authority معلم کلاس از assignment فعلی خوانده می‌شود، نه تغییر نقش یا عضویت Academic. پیام/Forward از همان مسیر قبلی با Topic مقصد صریح و FK مرکب هم‌گروه می‌گذرند. close/send قفل Conversation مشترک دارند. Message history، Clear/DeleteForMe، FromJoin و revalidation گیرندهٔ Delta پابرجاست؛ metadata قدیمی برای FromJoin تا محتوای مجاز پنهان می‌ماند.

گیت فعلی Content Safety به helper داخلی Application استخراج شد تا عنوان Topic بدون Message ساختگی، با همان fail-closed و پروندهٔ moderation بررسی شود. provider بیرون transaction، permission/session/relationship و audience دوباره داخل transaction کنترل می‌شوند. hash درخواست قدیمیِ بدون Topic ثابت مانده است. Migration `20261016183900_AddConversationTopics` بررسی شده و فقط دیتابیس تست migrate می‌شود؛ Down در حضور داده/رویداد/permission تازه از ابتدا توقف ایمن دارد.

هدفمند: ۴ Unit و ۱۳ Integration passed با صفر failed/skipped، شامل دو نشست واقعی، FK با SQL error=547 و نام دقیق، race Close/Send، assignment، revocation هنگام provider، safety unavailable/blocked برای Minor/Unknown، FromJoin و Forward. TRX هدفمند `.artifacts/messenger-p2/tests/topics-final-focused`. build مجدد صفر warning/error بود؛ خطای اولیه نام متد در تست و سه هشدار blocking با اصلاح کد تست رفع شدند، نه suppression. فرمان اولیهٔ EF با startup=Api به دلیل نبود Design در آن پروژه اجرا نشد؛ از factory و startup صحیح Infrastructure استفاده شد، بدون نصب package یا تغییر csproj. suite کامل نهایی/Git این فاز هنوز در حال انجام‌اند.

Validation نهایی روی source ثابت: restore موفق، build و final build صفر warning/error؛ هر دو suite کامل **Unit=522، Integration=871، Architecture=14، Total=1407 passed، failed/skipped=0**. TRX در `.artifacts/messenger-p2/tests/phase4-full` و `phase4-final`؛ Integration اول حدود ۷ دقیقه و نهایی ۴ دقیقه و ۳۲ ثانیه بود. build-serverهای بلااستفاده پس از build خاموش و test با MSBuild تک‌گرهی اجرا شد؛ هیچ تست حذف/skip و هیچ تنظیم SQL/امنیتی تغییر نکرد. EF pending model changes: NONE. پیام commit انتخاب‌شده `feat(messaging): add versioned group topics` است؛ نتیجهٔ remote فقط پس از Git ثبت می‌شود.

Git فاز چهارم انجام شد: `7ebe826cd78ef4a49518e9c05e54c88836393675`، همان پیام، ۳۶ فایل مرتبط؛ push/fetch و HEAD==origin/main موفق و worktree تمیز بود. branch/upstream و مرز workspace همان فازهای قبل است.

## P2-15 تا P2-17 — Scheduled / Silent

قصد ارسال private و durable با Typed payload، context سروری، UTC، rowversion، رزرو ClientMessageId، cancel، lease/backoff و source-of-truth SQL پیاده شد. worker هر بار یک کار را claim می‌کند و حداکثر بیست کار پردازش می‌کند؛ نه Timer حافظه‌ای و نه fake Message. dispatch همان گیت Safety و Store ارسال را reuse می‌کند. ثبت Message/وضعیت Dispatched/Outbox یک transaction است؛ لغو و مصرف claim با ترتیب Conversation→Scheduled کنترل می‌شوند. worker قدیمی token/context/body اشتباه را مصرف نمی‌کند. قبل از تصمیم Failure، tracker rollbackشده پاک می‌شود تا entity پیام تصادفاً دوباره ذخیره نشود.

Silent flag immutableِ send است؛ normal history/Unread/SignalR و اعلان داخلی باقی‌اند و فقط external delivery هم در intent و هم در current delivery gate suppress می‌شود. OTP/MeliPayamak/config تغییر نکرده‌اند. API و محدودیت واقعی cancel/recreate به‌جای edit اختیاری در Handoff آمده‌اند؛ وضعیت صف خصوصی REST-owned است و broadcast گروهی ندارد.

Migration `20261016190927_AddScheduledAndSilentMessages` بررسی شد: جدول اصلی و دو جدول attachment/mention، FK/unique/CHECK/index/rowversion و IsSilent=false برای سابقهٔ قبلی. Down guard پیش از حذف داده دارد؛ فقط fixture تست migrate می‌شود. build اولیهٔ این فاز خطای namespace/type Organization و سپس نام دو property در تست داشت؛ اصلاح شدند، warning مخفی نشد. ۱۷/۱۸ تست هدفمند اول passed بودند؛ fixture شکست‌خورده حق ارتباط Manager→Student را فرض کرده بود. با TeacherAssignment معتبر و گروه رسمی اصلاح شد، نه تضعیف policy. پس از دو تست race و تست rollback Outbox، هدفمند نهایی **۲۱ Integration و ۶ Unit passed، failed/skipped=0**؛ TRX Integration در `.artifacts/messenger-p2/tests/scheduled-final-focused-v3` است. suiteهای کامل/Git این فاز در حال اجرا هستند و هنوز PASS/commit ادعا نمی‌شوند.

دور کامل اولِ همین نسخه واقعاً شکست خورد: Unit=528 passed، Integration=891 passed/1 failed از 892، Architecture=14 passed، skipped=0؛ TRX `phase5-full`، Integration حدود ۱۲ دقیقه و ۴ ثانیه. race Claim×Cancel به SQL deadlock=1205 در Save claim رسید. claim قبلی مستقیم از index صف قفل می‌گرفت، ولی Cancel مسیر Conversation/خواندن row/upgrade را داشت. اصلاح: انتخاب readonly candidate فقط hint است؛ همهٔ claim/cancel/send/failure اکنون ترتیب Conversation→Scheduled با UPDLOCK/HOLDLOCK و revalidation زمان/lease/status زیر قفل دارند. Cancel نیز read قفل update دارد؛ global retry، skip یا ضعیف‌کردن assertion استفاده نشد. race به پنج round با start barrier تقویت شد. این دور ناموفق PASS نیست؛ validation کامل اصلاح‌شده باید دوباره اجرا شود و تا آن زمان commit/push نداریم.

هدفمند پس از اصلاح قفل: **۲۵ Integration passed، failed/skipped=0** در `.artifacts/messenger-p2/tests/scheduled-lockorder-focused`؛ build صفر warning/error. suite کامل اصلاح‌شده در حال اجراست؛ نتیجهٔ آن جای نتیجهٔ ناموفق قبلی جعل یا حدس زده نمی‌شود.

دور کامل اولِ اصلاح‌شده: **Unit=528، Integration=896، Architecture=14، Total=1438 passed، failed/skipped=0** در `.artifacts/messenger-p2/tests/phase5-fixed-full`؛ Integration حدود ۱۰ دقیقه و ۴۳ ثانیه بود. restore/build و final build صفر warning/error؛ final suite هنوز در اجراست. شمار بیشتر Integration به پنج round واقعی regression قفل مربوط است؛ تست ناموفق قبلی حذف یا PASS شمرده نشده است.

دور کامل نهایی اصلاح‌شده نیز **Unit=528، Integration=896، Architecture=14، Total=1438 passed، failed/skipped=0** در `.artifacts/messenger-p2/tests/phase5-fixed-final`؛ Integration حدود ۱۰ دقیقه و ۱۳ ثانیه. EF pending model changes: NONE؛ buildهای نهایی صفر warning/error. ۶ Unit و ۲۵ Integration به فاز قبل افزوده شدند. پیام commit متمرکز این فاز `feat(messaging): add durable scheduled and silent messages` است؛ hash/push فقط پس از Git واقعی ثبت می‌شود. دور ناموفق قبل بخشی از شواهد fix است، نه PASS یا skip.

Git فاز پنجم انجام و remote تأیید شد: `4de7237233dcc8865672849f35e0cad06d6010b7`، همان پیام، ۳۶ فایل مرتبط؛ push/fetch و HEAD==origin/main و worktree تمیز. هیچ فایل AI، frontend، appsettings یا ملی‌پیامک در commit نبود.

## P2-18 و سخت‌سازی مستقل — فاز ششم

فیلترهای HasAttachment/HasLinks/TopicId واقعی، count/cursor داخل authorization و حذف N+1 sender پیاده شدند. query-counter فقط tag ثابت را می‌شمارد و هیچ SQL/پارامتر/دادهٔ شخصی log نمی‌کند. Contact Card به predicate فعلی کشف پروفایل متصل و برای تمام مخاطبان جاری/Forward/dispatch بازبینی شد؛ policy جدید رضایت یا ادغام Person ساخته نشد. Send/Edit/Forward و ثبت incident پس از provider نشست واقعی را داخل transaction دوباره می‌سنجند؛ job پایدار فقط با claim معتبر، نه نشست جعلی، از ثبت قصد قبلی استفاده می‌کند.

وضعیت محدودیت خود عضو endpoint کمینه دارد؛ reason مدیریتی/محدودیت دیگران افشا نمی‌شود. انقضای Until میان validation و lock به 400 پایدار نگاشت شد. خواندن و تعویض مجموعه‌های Folder/Label، parent/version/children یکپارچه و ترتیب قفل مشترک دارند؛ raw normalization/body محدودند. مشخصات جدید گفتگو از همان Content Safety عبور می‌کند؛ Topic retry دقیق به availability provider وابسته نیست. schema جدید یا migration لازم نشد.

۲۸ Integration تازه افزوده شدند: شش Search، نه revalidation، چهار coherence، دو restriction و هفت metadata/OpenAPI. هدفمندهای `hardening-focused` (۲۱) و `metadata-openapi-focused` (۷) همگی passed، failed/skipped=0؛ build صفر warning/error. Search اولیه ۵ passed/۱ failed بود چون fixture تلاش داشت فایل retained را Deleted کند و CHECK نگه‌داری SQL درست جلویش را گرفت؛ تست اصلاح‌شده خود همین invariant و concealment را اثبات می‌کند. خطای compile تست body attribute با interface واقعی IRequestSizeLimitMetadata اصلاح شد؛ source امنیتی/constraint برای سبزشدن تست تضعیف نشد. suiteهای کامل، EF و Git بعد از اجرای واقعی ثبت می‌شوند؛ نتیجهٔ هدفمند به معنی تکمیل کل P2 نیست.

قرارداد نهایی Poll/Quiz از مالک دریافت و Freeze شد: Anonymous/NonAnonymous انتخاب سازنده، هویت فقط در NonAnonymous برای عضو مجاز؛ Poll قابل تغییر تا Close نهایی؛ Quiz تک‌گزینه‌ای با یک CorrectOption، پاسخ نهایی هر User immutable و عدم افشای correct/explanation پیش از پاسخ؛ بدون Exam/grade و Reopen. این تصمیم **مدرک پیاده‌سازی Poll/Quiz نیست**؛ فاز مستقل آن ادامه دارد. Checklist و scale-out و P1 runtime همان deferredهای صریح‌اند.

دو suite نخست فاز ششم واقعاً Unit=528، Integration=924، Architecture=14، Total=1466 passed، failed/skipped=0 و EF بدون تغییر معوق داشتند (`phase6-full` و `phase6-final`؛ Integration حدود ۵:۵۱ و ۸:۵۲). بااین‌حال review پیش از commit یک خلأ مخاطب کلاس رسمی پیدا کرد: participant، projection همگام‌شونده است و عضو تازهٔ Enrollment می‌تواند هنوز سطر ذخیره‌شده نداشته باشد. دو regression جدید با ثبت‌نام قبل درخواست و در provider، **هر دو واقعاً شکست خوردند**: انتظار 404، پاسخ 200 (`official-card-before-fix`). بنابراین آن دو نتیجهٔ سبز به‌جای validation نسخهٔ اصلاح‌شده استفاده نمی‌شوند.

اصلاح، مخاطب کلاس را مستقیماً از ClassMembers جاری (Enrollment/TeacherAssignment) می‌گیرد؛ peer ساخته‌نشده یا در ChangeTracker ذخیره‌نشده هم از guard جا نمی‌ماند. گروه عادی همان participant جاری را دارد. دو regression حذف/ضعیف نشده‌اند، policy consent هم تغییر نکرده و شمار Integration تازهٔ فاز اکنون **۳۰** است. validation اصلاح‌شده باید دوباره اجرا و سپس Git بسته شود.

Validation اصلاح‌شده انجام شد: هدفمند P2 برابر ۱۲۵ Integration passed؛ restore، build و final build موفق با صفر warning/error. هر دو suite کامل روی source ثابت **Unit=528، Integration=926، Architecture=14، Total=1468 passed، failed/skipped=0** در `.artifacts/messenger-p2/tests/phase6-fixed-full` و `phase6-fixed-final`؛ Integration حدود ۱۰:۰۵ و ۵:۵۱ بود. EF pending model changes: NONE؛ migration تازه ندارد. این نتایج جای خطاهای واقعی قبلی را حذف نمی‌کنند. پیام commit متمرکز `fix(messaging): harden advanced search and current authorization` است؛ نتیجهٔ Git بعد از اجرای واقعی ثبت خواهد شد.

Git فاز ششم: `bf133ffbf5d9f4bfb2804035657e4c60cda27f5a` با همان پیام؛ فقط ۴۰ فایل مرتبط، commit/push/fetch موفق و HEAD==origin/main و worktree تمیز. مرز AI/MeliPayamak/appsettings/frontend/server حفظ شد.

## P2-12/P2-13 و پذیرش نهایی — فاز هفتم

Poll/Quiz با تصمیم Freezeشده پیاده شد: انتخاب صریح Anonymous/NonAnonymous، رأی mutable Poll با rowversion خود رأی، یک پاسخ immutable Quiz، کلید جدا و عدم query آن برای پاسخ‌نداده حتی بعد از Close، Close نهایی با rowversion Poll و reauthorization جاری، و Forward متنی مستقلِ فقط پرسش/گزینه‌ها. نوع عمومی MessageKind.Poll=7 و PollChanged=14 اضافه شدند؛ سایر نوع‌ها و hash درخواست قدیمی تغییر نکردند. رأی/کلید در پیام، Revision، Search، Reply یا event payload وارد نمی‌شوند. Topic، Silent، Block، محدودهٔ تاریخچه و Consent جاری reuse شده‌اند؛ رأی، Message/Notification تازه نمی‌سازد. Creator با حق ارسال یا Owner/Admin با DeleteMessages می‌بندد؛ subscriber کانال فقط حق رأی دارد.

Migration بررسی‌شده `20261016211112_AddConversationPollsAndQuiz` پنج جدول واقعی و PK/FK مرکب/unique/index/CHECK/rowversion دارد. parent رأی هر User/Poll یکتا، انتخاب فقط برای گزینهٔ همان Poll و QuizKey فقط روی Quiz و گزینهٔ واقعی است. ایجاد اتمیک Definition/Options/Key/Message/Revision/Outbox و ترتیب قفل Conversation در Vote/Get/Close، از half-state و Vote×Close مبهم جلوگیری می‌کند. تک‌گزینه‌ای/immutable بودن Quiz قواعد Domain/transaction هستند؛ ادعای CHECK بین چند سطر نمی‌شود. Down قبل از حذف، در حضور داده/رویداد/شواهد moderation تازه ایمن می‌ایستد. فقط LocalDB fixture migrate می‌شود؛ EF pending model changes: NONE.

دو CHECK قبلی AbuseReport/SafetyIncident هنوز فقط Kindهای ۱ تا ۵ را قبول می‌کردند، با وجود ContactCard=6 موجود. توسعهٔ همین constraint برای Kind=6/7 با تست SQL/API واقعی انجام شد؛ provider واقعی Internal Makan برای Card همچنان ReviewRequired می‌دهد، نه allow ضعیف‌شده. پروندهٔ Poll فقط پرسش و گزینه‌ها را snapshot می‌کند؛ private browsing یا bypass MFA جدیدی ایجاد نشد.

هدفمند Domain: **۱۶ passed** در `poll-unit`. دور اول هدفمند SQL واقعاً ۱۳ passed/۱ failed از ۱۴ بود: تست برای MessageNotEditable انتظار 403 داشت، درحالی‌که قرارداد موجود 409 بود. دور بعد ۳۱ passed/۲ failed از ۳۳ داشت: fixture روی settings از PUT به‌جای PATCH استفاده کرد و assertion همهٔ resource eventها را منع می‌کرد، حتی MessageHiddenForUser مجاز خود کاربر. method و assertion به قرارداد واقعی اصلاح شدند؛ authorization ضعیف نشد. یک خطای compile تست نیز property ناموجود ContentKind در database-record helper بود؛ assertion با entity واقعی SQL انجام شد. اجرای بعدی ۴۴ passed و صفر failed/skipped بود (`poll-delivery-focused`).

هدفمند نهایی **۱۷۰ Integration P2 passed، failed/skipped=0** در `phase7-p2-focused`، حدود ۱:۲۹. فاز هفتم **۱۶ Unit و ۴۵ Integration تازه** دارد: ۴۱ Poll/Quiz و چهار smoke نهایی. شامل دو دستگاه واقعی یک account، دوازده رأی‌دهندهٔ مستقل هم‌زمان/یک identity batch، چهار round Vote×Close، SQL 2601/2627 و 547، rollback Outbox و retry، عدم side effect به ExamAttempt/GradeRevision/GradeRelease، provider واقعی برای گزینهٔ unsafe، current revocation و Minor/Unknown، Report/Forward/Delta/SignalR و query-counter کلید بدون متن SQL است. smokeهای ۹ اتصال/۱۴۴ فرمان Typing، batch بیست‌تایی روی ۲۱ task، ۳۵ پیام Topic با صفحه‌های ده‌تایی و Search sender/date/media نیز واقعی‌اند؛ benchmark Production نیستند. suite کامل و Git فقط پس از اتمام واقعی ثبت می‌شوند.

## نگاشت پذیرش P2-T01 تا P2-T50

همهٔ نام‌های کوتاه زیر متد واقعی در `tests/MakanApp.IntegrationTests` هستند؛ theoryها دادهٔ مستقل اجرا دارند. وجود این نگاشت به‌تنهایی PASS نیست؛ TRX اجرای نهایی مرجع نتیجه است. T34 طبق استثنای صریح prompt deferred است، نه تست ساختگی یا skipped.

| Gate | شاهد واقعی |
|---|---|
| T01 | P2EphemeralTypingReachesOnlyAuthorizedSubscribedPeerWithoutMessageOrDelta |
| T02 | P2EphemeralTypingExpiresWithoutDisconnectAndSendStopsCurrentSessionTyping |
| T03 | P2EphemeralBlockedPublisherAndPendingHintAreDeniedWithoutDeletingHistory؛ P2EphemeralRemovedMemberCannotPublishOrReceiveTyping |
| T04 | P2EphemeralOneSessionDisconnectKeepsPresenceOnlineAndLastDisconnectGivesLastSeen |
| T05 | P2EphemeralAbruptLeaseExpiryDoesNotLeavePermanentOnline |
| T06 | P2EphemeralNobodyPrivacyAndLegacyUpdateDoNotRevealOrResetPresence؛ P2EphemeralOnlineNobodyDoesNotLeakHeartbeatTimestampThroughLastSeen |
| T07 | P2EphemeralPresenceContactsUsesTargetsContactNotViewersContactAndBlockHidesIt |
| T08 | P2EphemeralPresenceRealtimeIsReauthorizedAfterBlockAndDoesNotEnumerateStrangers |
| T09 | P2PresentationLabelsUseCanonicalSavedAndConvergeAcrossSessions؛ unique Saved موجود |
| T10 | P2PresentationSavedForwardAfterSourceRemovalDoesNotExposeSourceMetadata |
| T11–T12 | P2PresentationFolderCrudIsOwnerOnlyAndDeletionKeepsConversation |
| T13 | P2PresentationFolderOverrideDoesNotGrantAccessOrKeepRemovedGroupVisible |
| T14–T15 | P2ManagementInviteCapabilityDoesNotGrantPromotionInfoOrChannelPublication |
| T16 | P2ManagementLinkUsesHashAndExplicitAcceptanceWithoutDuplicateUse |
| T17 | P2ManagementExpiredLinkRejectsJoinUsingServerClock |
| T18 | P2ManagementRevocationBlocksFutureJoinButDoesNotRemoveExistingMember |
| T19 | P2ManagementConcurrentLastLinkUseHasExactlyOneNewMember |
| T20 | P2ManagementAdultCannotUseLinkToBypassExistingProtectedGroupAudience |
| T21/T23 | P2ManagementPendingRequestIsNotMembershipAndDuplicateApprovalHasOneEffect |
| T22 | P2ManagementProtectedJoinRequiresRelationAndRechecksItAtApproval؛ P2ManagementOrganizationApprovalUsesCreatorsAndRequestersOwnCurrentRoles |
| T24 | P2TopicsCrossConversationReferenceFailsInApiAndSql |
| T25–T26 | P2TopicsClosePreservesHistoryRejectsSendAndExplicitReopenWorks |
| T27 | P2TopicsGranularPermissionAndRevocationApplyToEveryMutation؛ P2TopicsOfficialClassRequiresCurrentTeacherNotManagerOrStudent |
| T28–T29 | P2PollVoteMutationUsesOwnVersionAndExactRetryDoesNotDuplicate؛ P2PollConcurrentSameAccountVotesHaveOneFinalSqlEffect |
| T30 | P2PollMultiChoiceCountsVotersOnceAndRejectsInvalidSelections |
| T31 | P2PollVoteRacingFinalCloseIsTransactionalAndDeterministic؛ چهار round واقعی |
| T32 | P2AnonymousPollNeverExposesVoterIdentitiesEvenToCreator؛ P2NonAnonymousVotersArePagedSafeAndVisibleOnlyToCurrentMembers |
| T33 | P2QuizHidesAnswerUntilOwnFinalVoteAndNeverCreatesEducationalGrade |
| T34 | DEFERRED_PRODUCT_DECISION؛ Checklist پیاده نشده، تست ساختگی ندارد |
| T35 | P2ScheduledExpiredClaimIsRecoveredByNewScopeAndOldWorkerCannotOverwriteReceipt؛ recovery با scope/processor تازه، نه ادعای reboot سرور |
| T36 | P2ScheduledConcurrentCreatesAndWorkersDoNotDuplicate؛ P2ScheduledOutboxFailureRollsBackMessageAndReceiptBeforeSafeRetry |
| T37 | P2ScheduledBlockAtDispatchDoesNotUseCreationTimePermission |
| T38 | P2ScheduledRemovedGroupMemberCannotDispatchOldIntent |
| T39 | P2ScheduledSafetyUnavailableIsRetryableButNeverSent؛ P2ScheduledOrganizationMembershipRemovedDuringSafetyCannotDispatch |
| T40 | P2ScheduledConcurrentCancelAndDispatchCannotBothTakeEffect؛ پنج round واقعی |
| T41–T43 | P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery؛ silent=true/false |
| T44–T46 | P2AcceptanceSearchSenderUtcWindowAndMediaFiltersStayWithinVisibleScope |
| T47 | P2SearchFiltersCannotRestoreDeleteForMeClearOrInaccessibleCount؛ P2SearchTopicFilterCannotEscapeGroupOrRemovedMembership |
| T48 | P2EphemeralRevokedSessionLosesRestTypingHeartbeatAndOnlineContribution؛ P2PollRemovedMemberCannotReadVoteOrListVotersAndRevokedSessionIsUnauthorized؛ P2ManagementRevokedSessionCannotUseLinksOrPendingRequestActions؛ P2ScheduledRevokedSessionLosesMutationWithoutCreatingFakeWorkerSession |
| T49 | P2EphemeralRemovedMemberCannotPublishOrReceiveTyping؛ P2TopicsRemovedMemberCannotReadWriteOrReceiveFutureTopicDelta؛ P2PollFromJoinAndPersonalClearCannotLeakOldPollOrItsDelta؛ تست‌های revalidation گیرندهٔ موجود |
| T50 | اجرای کامل Solution شامل تمام suiteهای P0/P1/GAP-05، نه فقط filter P2؛ نتیجه پس از اجرا ثبت می‌شود |

کارایی محدود: smoke پوشهٔ چندگفتگویی `P2PresentationBoundedFolderSmokeReturnsAllAuthorizedConversationsInStablePages`، لینک آخرین slot، Poll دوازده‌نفره، صف ۲۱تایی، Topic ۳۵پیامی، نُه اتصال و Search چندصفحه‌ای پوشش دارند. identity Search و voters هرکدام یک query batch هستند؛ query کلید پاسخ برای unvoted صفر است. اندازهٔ صفحه/گزینه/override/lease/queue bounded است. این شواهد ظرفیت چندگرهی یا SLO Production نیستند.

مدت واقعی کل test method در TRX هدفمند `phase7-p2-focused` (شامل setup/HTTP/assert، نه latency خالص query): صف ۲۱تایی ۰٫۳۸۲ ثانیه؛ آخرین مصرف لینک ۰٫۳۷۰؛ sender/date/media برابر۰٫۴۸۶؛ پوشه چندگفتگویی ۰٫۳۴۲؛ دوازده رأی هم‌زمان و identity batch برابر۵٫۶۳۲؛ Topic ۳۵پیامی/صفحه‌بندی ۰٫۸۵۰؛ Search batch برابر۰٫۳۲۸؛ نُه اتصال و ۱۴۴ فرمان Typing برابر۴٫۳۵۴ ثانیه. این اعداد فقط نمونهٔ محلی همین دور و بدون SLA/تعمیم ظرفیت‌اند.

دور کامل نخست فاز هفتم واقعاً **Unit=544، Integration=971، Architecture=14، Total=1529 passed، failed/skipped=0** در `phase7-full` بود؛ restore/build صفر warning/error و Integration حدود۸:۴۱. review بعدی یک edge واقعی SQL CHECK یافت: در branch Closed، عبارت مقایسهٔ ClosedAtUtc=NULL مقدار UNKNOWN داشت و SQL آن را رد نمی‌کرد. دو تست تکمیلی افزوده شدند؛ `poll-null-before-fix` واقعاً **۱ passed/۱ failed** داشت: تست وضعیت Closed بدون timestamp هیچ exception نگرفت، درحالی‌که آزمون اتصال QuizKey به Poll عادی با FK موجود درست رد شد. این نقص از مسیر Domain/API تولید نمی‌شد، اما integrity جدول جدید باید خودش نیز محافظت کند.

اصلاح: `ClosedAtUtc IS NOT NULL` صریح در configuration، migration تازهٔ همین فاز، Designer و snapshot یکسان شد؛ migration منتشرشدهٔ قبلی تغییر نکرد. تست انتظار SQL547 و نام CHECK را نگه می‌دارد، نه bypass یا suppression. شمار تازهٔ این فاز اکنون **۱۶ Unit و ۴۷ Integration** (۴۳ Poll/Quiz و چهار smoke) است. نتیجهٔ ۱۵۲۹تایی قبل، جای دو suite کامل روی نسخهٔ اصلاح‌شده نخواهد نشست؛ commit/push هنوز انجام نشده است.

بازآزمایی مستقیم `poll-null-after-fix`: **۲ passed، failed/skipped=0** و EF pending model changes: NONE. restore/build موفق با صفر warning/error؛ دور کامل اول اصلاح‌شده `phase7-fixed-full` برابر **Unit=544، Integration=973، Architecture=14، Total=1531 passed، failed/skipped=0** بود؛ Integration حدود۷:۴۴. final build نیز صفر warning/error موفق شد و final suite روی همان source ثابت در اجراست. نتیجهٔ آن و Git پس از پایان واقعی ثبت می‌شوند.

Validation نهایی فاز هفتم تکمیل شد: دور دوم `phase7-fixed-final` نیز **Unit=544، Integration=973، Architecture=14، Total=1531 passed، failed/skipped=0**؛ Integration حدود۱۲:۳۲. restore و هر دو build موفق با صفر warning/error؛ EF pending model changes: NONE. هر ۱۷۲ تست P2 در full suite وجود دارند. PDF مرجع دوباره hash شد و دقیقاً همان SHA256 ابتدایی را دارد. بررسی کمکی فقط‌خواندنی sqlcmd با timeout پنج‌ثانیه‌ای نتیجه نداد؛ علت کندی به‌طور قطعی نسبت داده نمی‌شود و این diagnostic نه تست موفق/ناموفق محسوب شد، نه با تغییر SQL/service/config رفع ظاهری شد. خود suite کامل سرانجام موفق بود؛ هیچ تست حذف/skip و هیچ تغییر کد میان دو دور اصلاح‌شده وجود نداشت.

از baseline تا این نقطه، ۴۵ Unit و۱۷۲ Integration معنادار افزوده شده‌اند؛ جمع افزایش۲۱۷ و Architecture همان۱۴ است. فقط۵۳ مسیر مرتبط این فاز با فهرست مجاز تطبیق داده شدند؛ unexpected/missing هر دو صفر. source اصلی AI و فایل‌های config/MeliPayamak/frontend/Solution دست‌نخورده‌اند. پیام commit نهایی کد `feat(messaging): add polls quizzes and p2 acceptance` است؛ نتیجهٔ commit/push/remote باید پس از اجرای واقعی ثبت شود.

Git فاز هفتم کامل شد: `9e35bb3ba53ee93cfa555a024792ce50fcef6c86` با همان پیام؛۵۳ فایل مرتبط، commit/push معمولی/fetch و HEAD==origin/main موفق، worktree تمیز. این SHA کد نهاییِ دو بار full-tested است. ثبت پس از آن فقط تکمیل شواهد و وضعیت اسناد است؛ هیچ source/config/migration تازه‌ای به آن افزوده نمی‌شود. SHA commit همین ثبتِ مستندات از `git log` و گزارش نهایی قابل دریافت است؛ self-hash داخل فایل جعل یا amend نمی‌شود.

## نتیجهٔ نهایی Gateهای P2

Gateها فقط به code/product و آزمون محلی موجود اشاره دارند؛ دریافت Push واقعی، frontend، deployment و ظرفیت چندگرهی ادعا نمی‌شوند. Checklist طبق استثنای صریح مرحله deferred است و blocker بقیهٔ P2 نیست. هیچ تصمیم Child Safety بازگشایی/تضعیف نشده است.

```text
MESSENGER_P2_TYPING=PASS
MESSENGER_P2_PRESENCE=PASS
MESSENGER_P2_LAST_SEEN_PRIVACY=PASS
MESSENGER_P2_SAVED=PASS
MESSENGER_P2_FOLDERS=PASS
MESSENGER_P2_GROUP_MANAGEMENT=PASS
MESSENGER_P2_CHANNEL_MANAGEMENT=PASS
MESSENGER_P2_INVITES=PASS
MESSENGER_P2_JOIN_REQUESTS=PASS
MESSENGER_P2_TOPICS=PASS
MESSENGER_P2_POLL=PASS
MESSENGER_P2_QUIZ=PASS
MESSENGER_P2_SCHEDULED_MESSAGES=PASS
MESSENGER_P2_SILENT_MESSAGES=PASS
MESSENGER_P2_ADVANCED_SEARCH=PASS
MESSENGER_P2_REALTIME_SECURITY=PASS
MESSENGER_P2_CHILD_SAFETY=PASS
MESSENGER_P2_OPENAPI=PASS
MESSENGER_P2_CHECKLIST=DEFERRED_PRODUCT_DECISION
MESSENGER_P1_REAL_PUSH=PENDING_ENVIRONMENT
MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA
```

حکم تحویل کد: `MESSENGER_P2_CODE_COMPLETE`؛ محدودیت‌های زیرساختی و Checklist به معنی وجود پیاده‌سازیِ نداشته نیستند. مانع حل‌نشده‌ای برای scope کد P2 باقی نمانده است. P1 همچنان `MESSENGER_P1_CODE_COMPLETE` و `P1_PUSH_RUNTIME_PENDING` است. نقطهٔ شروع `0a93e800cb5c91d0545ae6a11513fd2fed98f5a5`، شاخه `messenger/p0`، upstream `origin/main` و remote `https://github.com/RastinaxAI/MakanApp.git` است؛ تمام هفت commit کد/فاز بالا جدا push و با remote تطبیق داده شدند. هیچ force-push یا بازنویسی تاریخچه انجام نشد.

شش migration این تحویل به ترتیب: `20261016163433_AddMessagingPresencePrivacy`، `20261016170915_AddMessagingPersonalOrganization`، `20261016174429_AddConversationInvitesAndRestrictions`، `20261016183900_AddConversationTopics`، `20261016190927_AddScheduledAndSilentMessages` و `20261016211112_AddConversationPollsAndQuiz`. pending model changes=NONE؛ migration خودکار startup، apply روی سرور یا دیتابیس عادی انجام نشده است. workspace تحویل همین worktree مستقل است؛ workspace اصلی دارای dirtyهای AI نه تغییر داده شد و نه وارد index/commit شد.

اعتبارسنجی ثبتِ صرفاً مستندات: restore، build و final build موفق با صفر warning/error؛ دو دور هدفمند، هرکدام **۳ Integration قرارداد OpenAPI/body + ۱۴ Architecture = ۱۷ passed** و failed/skipped=0 در `final-doc-contracts` و `final-doc-contracts-repeat`. این ۱۷ مورد جای دو suite کامل۱۵۳۱تایی کد نهایی نیستند و با آن جمع زده نمی‌شوند؛ بعد از SHA کد بالا فقط همین سه فایل سند تغییر کرده‌اند. پیام ثبت نهایی `docs(messaging): finalize p2 delivery evidence` است؛ SHA و تأیید مستقیم remote پس از اجرای Git در گزارش تحویل اعلام می‌شوند.

## تحویل مستقل Checklist v1 — ۲۰۲۶-۱۰-۱۷

مرجع جدید، درخواست صریح «MakanApp Messenger — P2 CHECKLIST CLOSURE» است؛ تعویق T34 و تصمیم Checklist در گزارش تاریخی بالا اکنون با قرارداد APPROVED جایگزین شده‌اند. نقطهٔ شروع دقیق `0cefab535f01fd7e3e47569bcfe18d999b223bca`، branch=`messenger/p0`، upstream=`origin/main` است. fetch و برابری HEAD/remote و worktree تمیز پیش از تغییر تأیید شدند؛ workspace اصلی AI خوانده/ویرایش/stage نشده است. baseline دوباره اجرا شد، نه از گزارش قبلی کپی: restore/build موفق، صفر warning/error، **544 Unit + 973 Integration + 14 Architecture = 1531 passed**، failed/skipped=0؛ TRX در `.artifacts/messenger-p2-checklist/baseline`، Integration حدود۱۰:۰۳.

سه جدول مستقل Checklist/Items/PersonalStates با mode key مرکب، PK/FK Restrict، rowversion و CHECK اضافه شدند؛ Poll، QuizKey یا Assessment جدول مشترک ندارند. تعریف با state نسخهٔ مستقل دارد؛ تیک شخصی هیچ نسخه/Notification عمومیِ Checklist را عوض نمی‌کند. hash ساخت ثابت، state شخصی User-owned، Collaborative روی آیتم، completion مشتق، soft-retire و متن ثابت ItemId معنای تاریخچه را نگه می‌دارند. Mode از انتشار ثابت است تا تبدیل مالکیت، حریم خصوصی/سابقه را بازنویسی نکند. Creator با Send جاری و پنجرهٔ موجود، Owner/Admin غیرسازنده با DeleteMessages و Send جاری، و کانال با PublishMessages تعریف را تغییر می‌دهند؛ audit و Safety موجود reuse شده‌اند.

کنترلر Poll قبل از طراحی بازبینی شد؛ اضافه‌کردن ownership و structural editing به DTO/Controller رأی‌گیری تمیز نبود، بنابراین Controller/Service/port باریک مستقل و مشترک‌بودن سیاست تعامل/Safety/Forward/Delta انتخاب شد. هیچ permission موازی، provider یا تنظیم تازه نیست. خواندن دوبارهٔ actor و Conversation پس از provider، identity map قدیمی EF را مرجع authorization نمی‌گیرد؛ تست revoke permission داخل provider این مرز را اثبات می‌کند.

Migration بررسی‌شده `20261017033804_AddConversationChecklists`: نصب تازه در fixture، ارتقا از `20261016211112_AddConversationPollsAndQuiz` با پیام قدیمی واقعی، Down فقط وقتی جداول/شواهد جدید خالی‌اند، ارتقای مجدد و رد Down با state واقعی (SQL51071) تست شدند. CHECK صریح Text IS NOT NULL جلوی UNKNOWN را می‌گیرد؛ FK mode مانع state شخصی روی Collaborative و PK مانع duplicate هستند. تست ارتقا DB یکتای خود را پاک می‌کند، نه DB fixture/عادی/سرور. EF pending model changes=NONE. هیچ startup migration یا apply روی سرور انجام نشد.

### نگاشت پذیرش C01 تا C35

نام‌ها متدهای واقعی در `tests/MakanApp.IntegrationTests/MessagingP2Checklist*.cs` هستند؛ Theoryهای Mode/age/group/channel هرکدام چند case واقعی دارند، نه تست skip یا ادعای محیط.

| Gate | شاهد واقعی |
|---|---|
| C01–C02 | P2ChecklistCreateHasExplicitModeStableItemsAndIdempotentReceipt، Personal/Collaborative |
| C03–C05 | P2ChecklistPersonalStateAndDerivedCompletionArePrivateAndConvergeAcrossSessions؛ P2ChecklistClientSuppliedUserIdCannotSelectOrOverwriteAnotherUsersPersonalState |
| C06–C07 | P2ChecklistCollaborativeCheckUncheckAndCompletionUseOneSharedState |
| C08–C09 | P2ChecklistEquivalentCheckAndUncheckHaveNoDuplicateEventOrVersion |
| C10 | همان تست stale state؛ P2ChecklistAddingAndReorderingItemsAreVersionSafeAndPreserveOwnState برای stale definition |
| C11 | P2ChecklistCheckVersusUncheckCannotSilentlyRestoreStaleState؛ هر دو Mode |
| C12 | P2ChecklistRemovedAndBannedMembersCannotMutateOldIdsAndRevokedSessionIsUnauthorized؛ P2ChecklistRemovalRacingMutationAlwaysRevokesFutureWrites |
| C13–C14 | همان تست REST؛ P2ChecklistCollaborativeRealtimeAndReplayRecheckRevocationRemovalAndVisibility |
| C15–C16 | P2ChecklistDefinitionRequiresCurrentSendAndContentManagementPermission؛ P2ChecklistAdminPermissionRevokedDuringSafetyCannotUseTrackedPreflightAuthority |
| C17 | P2ChecklistAddingAndReorderingItemsAreVersionSafeAndPreserveOwnState |
| C18 | P2ChecklistVersionedReplacementRetainsHistoryWithoutReusingMeaningOrExposingPrivateActivity؛ P2ChecklistDefinitionRetirementRacingInteractionNeverOrphansOrReattributesState |
| C19–C20 | تست‌های PersonalStateAndDerivedCompletion و CollaborativeCheckUncheckAndCompletion با دو User |
| C21 | P2ChecklistNeverCreatesPollQuizAssessmentOrGradeArtifacts؛ count تمام جدول‌های schema assessment و Poll/Quiz قبل/بعد |
| C22 | P2ChecklistProtectedCreationAndDefinitionSafetyInspectAllTextAndFailClosed؛ P2ChecklistRealInternalSafetyRejectsUnsafeItemInsteadOfOnlyCheckingTitle |
| C23 | P2ChecklistProtectedRelationshipAndBlockAreReevaluatedWithoutErasingHistory؛ P2ChecklistOfficialClassUsesCurrentEnrollmentNotGuardianOrManagerPrivateAccess؛ P2ChecklistCreationRevalidatesSessionAndBlockAfterSafety |
| C24–C26 | P2ChecklistForwardIsIndependentTextWithoutAnyStateIdentityVersionOrInteractiveRow؛ هر دو Mode و عدم row زندهٔ مقصد |
| C27–C28 | P2ChecklistDeleteForMeIsPrivateAndDeleteForEveryoneRetainsHistoryButConcealsAllDetailsAndDelta |
| C29–C30 | P2ChecklistUnavailableReplySearchAndGenericEditNeverLeakItemsOrPersonalState؛ P2ChecklistFromJoinAndClearHistoryHideOldDefinitionItemsAndChanges |
| C31 | P2ChecklistPersonalRealtimeReachesSameUsersSecondDeviceButNotPeerEvenWithBroadAudienceHint |
| C32 | P2ChecklistCollaborativeRealtimeAndReplayRecheckRevocationRemovalAndVisibility؛ P2ChecklistDefinitionEventIsSharedButNeverIncludesPersonalInteractionState |
| C33 | تست realtime دستگاه دوم، بازیابی REST و P2ChecklistConcurrentTwoDeviceDoubleCheckHasOneSqlEffect |
| C34 | P2ChecklistCurrentMemberCanInteractButChannelSubscriberCannotPublish؛ تعریف group/channel و suite موجود |
| C35 | اجرای ۲۲۲ Integration تمام P2 شامل Poll/Quiz موجود؛ اجرای کامل Solution نیز gate جداست |

شواهد تکمیلی: P2ChecklistSqlUniquenessAndModeKeysPreventDuplicateOrCrossModePersonalState (SQL2601/2627/547)، P2ChecklistMigrationUpgradesBaselinePreservesDataAndRefusesDestructiveRollbackAfterUse، P2ChecklistOutboxFailureRollsBackMessageDefinitionAndItemsBeforeSafeRetry، دو تست قرارداد OpenAPI/unknown value و Topic بسته. ۱۴ Unit خالص در `ConversationChecklistTests.cs` برای تعریف immutable، enum/ID/حدود ورودی، مالکیت، mode، shared/private idempotency، retirement، UTC و منع generic edit وجود دارند؛ Architecture موجود dependency flow را بررسی می‌کند.

Validation هدفمند واقعی: ۱۴ Unit passed؛ دور اول API **۳۲ passed / ۱ failed** بود چون تست پس از Ban (که عضویت را پایان می‌دهد) حذف دوم را موفق فرض کرده بود؛ تست به دو حالت مستقل Ban/Remove اصلاح شد، production behavior ضعیف نشد. دور بعد **۴۹ Integration Checklist passed**؛ سپس تست spoofed UserId و شمار همهٔ جدول‌های Assessment افزوده و **تمام ۲۲۲ Integration P2 passed** (۵۰ Checklist + ۱۷۲ قبلی)، failed/skipped=0، حدود۲:۲۲. warning EF1002 در query تست با پارامتر واقعی و QUOTENAME سمت SQL رفع شد، نه suppress. build بعدی صفر warning/error است. نتایج کامل/نهایی و Git فقط پس از پایان واقعی در افزودهٔ بعدی ثبت می‌شوند.

دور کامل اول Checklist واقعاً پایان یافت: restore/build موفق با صفر warning/error؛ **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، failed/skipped=0، Integration حدود۱۴:۳۲، TRX در `.artifacts/messenger-p2-checklist/full`. تمام regressionهای P0/P1/GAP-05/P2 و code مستقل دیگر در همین Solution اجرا شدند، نه فقط filter. Source از شروع همین دور عوض نشده؛ فقط شواهد سند تکمیل شدند. final build نیز zero warning/error موفق است؛ runner نهایی باید پیش از commit/push پایان یابد. گزارش نهایی تحویل SHA واقعی و نتیجهٔ remote را اعلام می‌کند.

### نتیجهٔ نهایی پذیرش Checklist

runner نهایی نیز واقعاً پایان یافت: **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، failed=0، skipped=0؛ Integration حدود۵:۴۱، شواهد `.artifacts/messenger-p2-checklist/final`. دو دور کامل روی Source/Test ثابت انجام شدند؛ فقط متن شواهد سند در طول آن تکمیل شد. restore، build و final build همگی موفق با صفر warning/error، و EF pending model changes=NONE است. ۶۴ تست معنادار تازه (۱۴ Unit،۵۰ Integration)، regression تمام P0/P1/P2، و ماتریس C01–C35 موفق‌اند؛ هدفمند ۲۲۲تایی با suite کامل جمع زده نمی‌شود. Checklist v1 اکنون **APPROVED و implemented/tested** است؛ T34 در تحویل قبلی deferred بود و در این تحویل مستقل رفع شد.

```text
MESSENGER_P2_CHECKLIST=PASS
MESSENGER_P1_REAL_PUSH=PENDING_ENVIRONMENT
MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA
```

حدود تحویل: ۵۰ مسیر فقط همین قابلیت/شش سند؛ بدون appsettings، تغییر sln/csproj/package، AI/RAG، ملی‌پیامک، Commerce، frontend یا سرور. branch=`messenger/p0` و upstream=`origin/main`؛ پیام commit `feat(messaging): add collaborative and personal checklists` است. index باید با allowlist صریح همین ۵۰ مسیر برابر باشد. hash commit و HEAD/remote پس از commit/push/fetch از خروجی واقعی Git در گزارش نهایی اعلام می‌شوند؛ این سند self-hash یا موفقیت Gitِ اجرا‌نشده جعل نمی‌کند. وضعیت نهایی MESSENGER_P2_COMPLETE فقط پس از تأیید remote در گزارش تحویل معتبر است؛ operational pendingها همچنان باقی‌اند.
