# ممیزی و شواهد تحویل Messenger P2

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
