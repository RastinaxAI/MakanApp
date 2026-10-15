# GAP-05 — قرارداد مصوب ایمنی کودک پیام‌رسان، نسخه ۱

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ: ۲۰۲۶-۱۰-۱۵. سیاست: `Makan Child Safety Policy v1 / APPROVED_FOR_BACKEND_IMPLEMENTATION`.
این سند رفتار Backend را توضیح می‌دهد؛ تأیید حقوقی همه کشورها، دقت معنایی AI، استقرار Production یا آماده‌بودن تیم عملیاتی ادعا نمی‌شود.

## ۱. منابع و تغییر وضعیت

- `MESSENGER_V2_PRODUCT_DECISIONS.md` و اسناد `STEP_14A` تا `STEP_14E` در همین پوشه؛
- `BACKOFFICE_PRODUCT_DECISIONS.md` برای Permission، reviewer و مرز مدیریت پلتفرم؛
- `GAP_01_ORGANIZATION_PERSON_GUARDIAN_ONBOARDING.md` برای رابطه والد و اتصال حساب؛
- `../release/GAP_04_BACKOFFICE_TOTP_MFA.md` برای MFA و freshness؛
- متن کامل درخواست «GAP-05 — FINAL APPROVED IMPLEMENTATION» و تأیید صریح بعدی مالک پروژه، منبع معادل سیاست فارسی نسخه ۲ هستند. PDF با نام `Makan_GAP05_Child_Safety_Developer_Policy_FA_v2.pdf` در مخزن موجود نبود؛ منبع یا محتوای آن اختراع نشده است.

وضعیت تاریخی `BLOCKED_BY_PRODUCT_POLICY` با این تصمیم محصول رفع شده است. اسناد تاریخی 14A/14E، شواهد زمان خود را حفظ می‌کنند؛ addendum سند تصمیم محصول، قرارداد جاری را مشخص می‌کند.

## ۲. مدل سن و تأیید

مدل سنی دوم ایجاد نشده است. `User.CommunicationAgeCategory` موجود، با همان مقدارهای `Unknown=0`، `Minor=1` و `Adult=2` حفظ شد؛ `Unknown` در قرارداد حاضر معادل `UnknownAge` است. نقش مدرسه، نداشتن عضویت و شماره تلفن هیچ‌کدام سن را اثبات نمی‌کنند.

`CommunicationAgeVerificationStatus` شامل `Unverified / Verified / Conflict` است. Adult فقط همراه Verified، Adult مؤثر است؛ مقدار قدیمی Adult بدون شاهد تأیید، در تصمیم حساس مثل Unknown محافظت می‌شود. طبقه‌بندی از تاریخ تولد بررسی‌شده و تاریخ UTC سرور محاسبه می‌شود: کمتر از ۱۸ Minor و از ۱۸ Adult. با رسیدن خودکار به تولد، اختیار جدید خودکار صادر نمی‌شود؛ بازتأیید معتبر لازم است.

مدیر جاری فقط برای `OrganizationPerson` فعال همان سازمان که به حساب موجود وصل است می‌تواند شاهد مدرسه ثبت کند. پرونده بدون حساب Messenger ندارد و این API برای آن حساب یا state سنی موازی نمی‌سازد. سازمان از workspace معتبر گرفته می‌شود؛ `UserId` دلخواه و role ورودی مجوز نیستند. School Manager با Platform Admin برابر نیست.

تاریخ تولد در جدول شاهد محدود ذخیره می‌شود، ولی در Discovery، DTO عمومی، audit عمومی یا پاسخ تأیید بازگردانده نمی‌شود. هر شاهد شامل طبقه قبلی/پیشنهادی، منبع، وضعیت، actor، سازمان، UTC و دلیل است و append-only است. `AdminAuditLog` فقط خلاصه امن تغییر طبقه و وضعیت را می‌گیرد؛ نه تاریخ تولد یا تلفن. اپراتور نباید PII را در reason بنویسد.

اختلاف تاریخ تولد با آخرین شاهد معتبر، شاهد قبلی را overwrite نمی‌کند: شاهد Conflict ثبت، طبقه مؤثر Unknown و پاسخ 409 می‌شود. این تغییر محافظتی در همان تراکنش commit می‌شود؛ خطای HTTP نباید شاهد تعارض را rollback کند. فقط PlatformOwner با `platform.users.manage`، نشست MFA و Step-up تازه، reason و نسخه جاری می‌تواند تعارض را حل کند. تاریخچه شاهدها باقی می‌ماند. Self-assertion پروفایل، سن تأییدشده نمی‌سازد.

## ۳. ماتریس ارتباط و Discovery

| وضعیت مقصد/رابطه | FindUser | CreateDirect | SendMessage | ReadConversation موجود |
|---|---|---|---|---|
| Adult معتبر | طبق Privacy موجود | Privacy + حساب فعال + نبود Block | مجوز جاری دوباره بررسی می‌شود | فقط participant و scope مجاز |
| Minor/Unknown، غریبه | Discovery عمومی خاموش | 404 عمومی، حتی با شناسه/username/phone | شناسه قدیمی مجوز نیست؛ ارسال جدید رد | عضویت مجاز تاریخی مستقل است؛ تاریخچه خودکار حذف نمی‌شود |
| Minor/Unknown، رابطه مجاز | نمایش محدود همان context | رابطه جاری + Privacy + نبود Block | رابطه جاری + Permission + Safety | رابطه به‌تنهایی اجازه خواندن گفت‌وگوی دیگران نیست |

رابطه مجاز از کلاس فعال با Enrollment/TeacherAssignment، رابطه Guardian معتبر و نقش/عضویت مربوط، یا رضایت مخاطب محافظت‌شده به دست می‌آید. ذخیره یک‌طرفه کودک توسط غریبه مجوز نیست. در رابطه Contact، هر طرف محافظت‌شده باید طرف دیگر را ذخیره کرده باشد؛ دو طرف محافظت‌شده به رضایت هر دو نیاز دارند. عضویت گروه یا صرف وجود Conversation رابطه عمومی ایجاد نمی‌کند.

رابطه آموزشی به همان organization/class محدود است. Teacher/Manager صرف عنوان نقش به همه دانش‌آموزان دسترسی خصوصی نمی‌گیرد. Parent برای Child A، اختیار Child B را نمی‌گیرد. در Discovery عمومی حساب محافظت‌شده تلفن، سن دقیق، Guardian و تاریخچه مدرسه نمایش داده نمی‌شوند؛ hidden و nonexistent علت حساس متفاوت ندارند.

ماتریس نقش‌های Organization Direct موجود حفظ شده است؛ رابطه ایمنی شرط اضافه است، نه اعطای عمومی نقش/مجوز جدید. رابطه معتبر کلاس یا Guardian در Personal Direct و گروه رسمی طبق قواعد همان سطح مصرف می‌شود.

## ۴. گروه، کانال و رضایت

| عملیات شخصی | Adult | Minor / Unknown |
|---|---|---|
| ساخت Group خصوصی | مجاز با قواعد حساب | مجاز با قواعد حساب |
| ساخت Group عمومی | مجاز | ممنوع |
| ساخت/انتشار/دریافت ownership کانال شخصی | طبق Permission | ممنوع |
| افزودن فوری عضو | فقط وقتی دو طرف Adult و Privacy و کل audience مجازند | دعوت و پذیرش صریح لازم است |

برای دعوت یا join، رابطه مقصد با تمام اعضای محافظت‌شده نیز بررسی می‌شود. گروه راه فرعی ارتباط با کودک غریبه نیست. initial member محافظت‌شده در create به‌طور پنهان Active نمی‌شود؛ ابتدا گروه ساخته و سپس دعوت ارسال می‌شود. دعوت شخصی، عضویت مدرسه یا نقش ایجاد نمی‌کند.

`ConversationInvitation` به Conversation، inviter و target متصل است. فقط target احراز‌شده می‌تواند accept/decline کند؛ inviter مجاز می‌تواند revoke کند. پذیرش، نقش جاری inviter، Privacy، رابطه جاری، scope، وضعیت و expiry را دوباره بررسی می‌کند. انقضا ورودی صریح UTC و حداکثر ۳۰ روز است؛ فهرست حداکثر ۱۰۰ دعوت Pending و منقضی‌نشده است. پذیرفتن هم‌زمان یا retry عضویت دوم نمی‌سازد. دعوت revoked/expired/غیرمجاز مجوز نیست.

تغییر سن در Send، invite/member، visibility، اعطای publish و انتقال ownership دوباره اعمال می‌شود. مالک قدیمی کانال/گروه عمومی با طبقه محافظت‌شده اختیار انتشار قدیمی را حفظ نمی‌کند؛ داده قدیمی حذف نمی‌شود. انتقال مجاز یا خصوصی‌کردن گروه از مسیر موجود ممکن است.

## ۵. گروه رسمی کلاس

`Conversation.AcademicClassId` با FK مرکب سازمان/کلاس، اتصال رسمی را ثبت می‌کند. `POST /api/v1/organization/classes/{classId}/conversation` گروه رسمی یکتا را lazy resolve و projection اعضا را با منابع آموزشی هماهنگ می‌کند.

- Student: Enrollment فعال، پرونده فعال، عضویت/نقش Student و کلاس/سازمان فعال؛
- Teacher: TeacherAssignment فعال همان کلاس، عضویت/نقش Teacher و کلاس/سازمان فعال؛
- Manager مجاز می‌تواند گروه را provision کند؛ این کار او را participant یا مجاز به خواندن متن نمی‌کند.

ردیف Messenger مرجع حقیقت رسمی نیست. History، list، members، Search، Media/File، Delta، realtime، send و گیرندگان اعلان مجوز جاری آموزشی را بررسی می‌کنند؛ پایان کلاس/assignment/enrollment با شناسه cacheشده دور زده نمی‌شود. resolve و final send، projection participantها را در تراکنش به‌روز می‌کنند؛ تغییر سازمان A، گروه/نقش سازمان B یا گروه شخصی را حذف نمی‌کند. گروه رسمی legacy بدون ClassId قابل اعتماد برای دسترسی رسمی نیست؛ کلاس آن حدس زده یا با migration خودکار متصل نمی‌شود.

## ۶. ارسال جاری، Block و تاریخچه

`ConversationExists != SendAllowed` و `FindUser != CreateDirectAllowed`. هر Send/Forward/Edit وضعیت حساب، محدودیت پیام‌رسانی، scope، membership، Block در Direct، رابطه سنی جاری و Permission را دوباره بررسی می‌کند. Child Safety نیز پیش از تغییر محتوای قابل انتشار اجرا می‌شود.

Block ارسال Direct جدید را در هر دو جهت مؤثر متوقف می‌کند، ولی Enrollment، نمرات، دیگر نقش‌ها، عضویت Group مشترک یا داده تاریخی را حذف نمی‌کند. پایان Guardian، assignment یا رابطه ارتباطی تاریخچه را فیزیکی پاک نمی‌کند. دسترسی جاری به گروه رسمی/سازمانی ممکن است قطع شود؛ حفظ داده به معنی مجوز خواندن همیشگی نیست. Guardian هیچ مجوز خواندن private chat کودک با دیگران نمی‌دهد.

## ۷. Internal Makan Content Safety

همان `IContentSafetyProvider` استفاده می‌شود؛ provider موازی یا microservice ساخته نشده است. انتخاب پیش‌فرض و نمونه Production اکنون `Internal` است؛ گزینه صریح `Unavailable` fail-closed و DevelopmentAllow فقط در Development/Testing مجاز است. تنظیم OTP/SMS تغییر نکرده است.

`InternalMakanContentSafetyProvider` یک موتور قطعی قواعد متن، با نسخه `makan-child-safety-v1-rules-1` است؛ AI/ML یا تشخیص معنایی نیست. normalization حروف عربی/فارسی، ارقام، فاصله و برخی نشانه‌های نامرئی اعمال می‌شود. دسته‌ها:

- Harassment/Bullying؛ Threat/Violence؛ SexualSafety/Grooming؛ ChildAbuseRisk؛ SensitivePersonalData؛ Spam/Scam؛ Other.

خروجی typed برابر `Allowed / ReviewRequired / Blocked / Unavailable` همراه `ReasonCode / PolicyVersion / CorrelationId` است. قواعد پرخطر مشخص Block و قواعد نیازمند بررسی Review می‌دهند؛ نبود تطبیق یک قاعده به معنی تضمین بی‌خطر بودن متن نیست. false-positive/false-negative، زبان‌های دیگر و دورزدن معنایی محدودیت واقعی این baseline هستند.

| محتوای مسیر حساس | رفتار واقعی |
|---|---|
| متن بدون تطبیق قواعد | Allowed؛ سپس بازبینی مجوز و commit پایدار |
| عبارت پرخطر پوشش‌داده‌شده | Blocked؛ بدون Message معمولی |
| عبارت نیازمند بررسی یا داده حساس پوشش‌داده‌شده | ReviewRequired؛ بدون Message معمولی |
| Image / Video / Voice / File / ContactCard | ReviewRequired؛ هیچ ادعای اسکن بایت یا تشخیص معنایی وجود ندارد |
| لینک HTTP/HTTPS/WWW در متن | ReviewRequired؛ URL دانلود یا بررسی معنایی نمی‌شود |
| unavailable، exception، timeout یا نتیجه نامعتبر | 503 fail-closed برای Minor/Unknown؛ بدون انتشار |

برای هر Conversation دارای حداقل یک participant مؤثر Minor/Unknown بررسی اجباری است؛ برای گروه رسمی audience از منبع کلاس به دست می‌آید، نه cache قدیمی. Saved حساس هم از pipeline عادی ایمنی می‌گذرد. Adult-only شخصی در v1 الزام Child Safety pre-send ندارد؛ Block/Report برقرارند.

Blocked/ReviewRequired، `SafetyIncident` و پرونده Critical در PlatformSafety می‌سازند و 422 امن برمی‌گردانند؛ این وضعیت Sent نیست و به Notification/Delivery معمولی راه پیدا نمی‌کند. ReviewRequired هرگز خودکار متن قبلی را منتشر نمی‌کند. Unavailable، Message یا موفقیت جعلی نمی‌سازد. retry incident از کلید/اثر انگشت قبلی استفاده می‌کند. Edit نیز پیش از تغییر متن، با شناسه قطعی همان پیام/نسخه/محتوا بررسی می‌شود؛ revision منتشرشده قبلی در شکست حفظ می‌شود.

متن خصوصی، bytes فایل، DOB، تلفن کودک، OTP/MFA یا token وارد log عمومی نمی‌شوند. audit فقط شناسه، نسخه سیاست، category/outcome یا شمار context امن دارد. snapshot پرونده در Persistence خصوصی مجاز است و Log عمومی نیست. هیچ محتوای Messenger به AI/RAG یا vendor خارجی ارسال نمی‌شود.

## ۸. Moderation و MFA

بازبین پلتفرم از `AbuseReviewer` و `platform.moderation.review` موجود استفاده می‌کند. خواندن شاهد case-scoped و audited است؛ مسیر مرور unrestricted private chat وجود ندارد و School Manager هیچ permission جدیدی نمی‌گیرد. reviewer پرونده مربوط به خودش را بررسی نمی‌کند.

GET عادی، reported item و حداکثر ۲ پیام قبل/۲ پیام بعد همان Conversation را می‌دهد؛ snapshot گزارش بعد از حذف/ویرایش منبع حفظ می‌شود، ولی revision تاریخی همسایه از API استخراج نمی‌شود. فایل فقط از همان پرونده و با مجوز جاری قابل دریافت است.

گسترش context یک POST صریح، فقط پس از Escalate، claim بازبین قوی‌تر، `platform.moderation.enforce`، reason، نسخه جاری و Step-up تازه است. از GAP-04 و `IPlatformStepUpAuthorization` واقعی استفاده می‌شود. سقف فنی گسترش ۲۰ پیام هر طرف همان reported item است؛ مجوز مرور کل Conversation نیست. هر read/action حساس AdminAudit دارد و محتوا در summary کپی نمی‌شود.

اقدامات:

- CloseReport با Resolve/Dismiss موجود؛
- Warned به‌صورت ثبت اخطار در پرونده و Audit؛ تحویل پیامک/Push اخطار ادعا نمی‌شود؛
- MessagingRestricted با پایان UTC صریح؛ Send/Forward/Edit بعدی منع می‌شود، ولی حساب، نمرات، عضویت و خواندن مجاز حذف نمی‌شوند؛
- Escalated، پرونده را Critical/Pending می‌کند تا بازبین مجاز مجدداً claim کند؛
- RemoveContent از endpoint enforce موجود با permission قوی‌تر، reason و Step-up تازه؛
- SuspendAccount از workflow موجود `/api/admin/v1/users/{userId}/suspend`، با `platform.users.manage`، Step-up تازه، Audit و محافظ آخرین Owner/Manager؛ AbuseReviewer این اختیار را ندارد. سیستم suspension موازی ساخته نشده است.

## ۹. API و اثر قرارداد

همه routeهای جدید Bearer معتبر می‌خواهند. JSON مانند قرارداد موجود camelCase و enum رشته‌ای است. DTO عمومی سن یا علت رد حساس اضافه نشده است.

| Method/Route | Request / Response | مجوز و خطای مهم |
|---|---|---|
| GET `/api/v1/organization/people/{id}/communication-age` | وضعیت طبقه مؤثر، VerificationStatus و Version | Manager جاری همان سازمان؛ منبع غیرمجاز 404 |
| POST همان مسیر + `/verify` | VerifiedBirthDate, ExpectedVersion, Reason → وضعیت | نقش/نشست جاری، شاهد/تاریخ معتبر؛ 400/403/404/409/412 |
| POST `/api/admin/v1/users/{id}/communication-age/resolve` | همان command → وضعیت | Owner، UsersManage، MFA تازه، تعارض موجود |
| POST `/api/v1/conversations/{id}/invitations` | TargetUserId, ExpiresAtUtc → دعوت Pending | Permission دعوت، رابطه، Privacy و audience جاری |
| GET `/api/v1/messaging/invitations` | فهرست محدود دعوت‌های خود | فقط target؛ بدون متن private chat |
| POST `/api/v1/messaging/invitations/{id}/accept|decline|revoke` | بدون body → وضعیت | target یا inviter مجاز؛ 404 عمومی؛ retry امن |
| POST `/api/v1/organization/classes/{id}/conversation` | بدون body → ConversationId/OrganizationId/ClassId/ActiveMemberCount | منبع آموزشی یا Manager جاری برای provision؛ 404 غیرمجاز |
| GET `/api/admin/v1/moderation/cases/{id}` | detail قبلی + ContextMessages محدود | MFA و ModerationReview؛ audited |
| POST همان مسیر + `/expanded-context` | ExpectedVersion, Reason → detail محدود گسترش‌یافته | ارجاع/claim، ModerationEnforce و MFA تازه |
| POST همان مسیر + `/actions` | ExpectedVersion, Reason, Action, RestrictedUntilUtc اختیاری → detail | بازبین assigned؛ Warned/MessagingRestricted/Escalated |

کدهای افزوده: `AGE_VERIFICATION_INVALID`=400، `AGE_SUBJECT_NOT_FOUND`=404، `AGE_VERIFICATION_CONFLICT`=409 و `CONVERSATION_INVITATION_REQUIRED`=409. کدهای موجود `DIRECT_RECIPIENT_NOT_AVAILABLE`، `MESSAGE_NOT_ALLOWED`، `MESSAGE_SAFETY_BLOCKED/REVIEW_REQUIRED/UNAVAILABLE`، `MFA_REQUIRED` و `PLATFORM_STEP_UP_REQUIRED` حفظ شده‌اند. UserId/ConversationId/MessageId selector هستند. قراردادهای Controller/DTO از OpenAPI تولید می‌شوند؛ frontend تغییر نکرده است.

## ۱۰. Persistence، تراکنش و Race

Migration: `20261015091336_EnforceApprovedMessengerChildSafety`، شماره ۳۷.

- `identity.Users`: ستون VerificationStatus و MessagingRestrictedUntilUtc؛ `rowversion` قبلی حفظ می‌شود. مقدار پیش‌فرض backfill وضعیت ۰، تأیید تاریخی اختراع نمی‌کند.
- `identity.CommunicationAgeVerifications`: شاهد append-only، PK Guid، FKهای User/Actor/Organization با Restrict، DOB از نوع date، زمان datetime2، reason محدود، CHECKهای دسته/وضعیت/منبع و index User/Time/Id.
- `messaging.ConversationInvitations`: PK، FKهای محدودکننده، expiry/status/completion، rowversion، filtered unique دعوت Pending برای Conversation/Target و index inbox.
- `messaging.Conversations`: AcademicClassId nullable، composite FK به Class هم‌سازمان و filtered unique سازمان/کلاس. index عمومی OrganizationId حفظ می‌شود. کلاس رکوردهای قدیمی حدس زده نمی‌شود.
- Constraint اقدام Moderation برای Warned و MessagingRestricted توسعه می‌یابد؛ جدول Moderation یا MFA موازی ساخته نشده است.

ارسال نهایی و تغییر عضویت، تراکنش کوتاه Serializable و قفل Conversation دارند؛ خواندن وضعیت رابطه/Block/Privacy/سن تا commit از اثر check-then-write محافظت می‌کند. پاسخ provider خارج تراکنش است و final mutation مجوز را دوباره می‌سنجد. سن از ابتدا با UPDLOCK/HOLDLOCK و نسخه جاری خوانده می‌شود؛ شاهد، classification و Audit اتمیک‌اند. پذیرش دعوت ترتیب قفل Conversation → invitation دارد تا دو accept اثر دوم نسازند. RowVersion، اقدام reviewer هم‌زمان/قدیمی را رد می‌کند.

هیچ history یا رکورد کاربر در Up حذف نمی‌شود. Migration فقط در SQL Server ایزوله تست می‌شود؛ روی developer/Production اجرا نشده و Startup auto-migrate ندارد. Down به‌عنوان راهبرد rollback کور توصیه نشده است.

## ۱۱. تست و Validation

تست‌های جدید در `ApprovedChildSafetyPolicyTests`، پنج فایل `MessagingChildSafety*`/`MessagingInternalContentSafetyEndpointsTests` و `PlatformMfaChildSafetyEndpointsTests` قرار دارند. fixture جدید فقط آماده‌سازی/خواندن داده تست را انجام می‌دهد؛ workflowهای جدید سن، دعوت، کلاس و moderation از API اجرا می‌شوند. تست MFA از پیاده‌سازی واقعی TOTP/session assurance استفاده می‌کند؛ fake فقط برای جداکردن تست policyهای دیگر است.

پوشش: سه طبقه، آستانه تولد و شاهد/تعارض/نسخه، منع self/cross-org، Discovery امن، رابطه و لغو، private/public/channel، رضایت و پذیرش هم‌زمان، Guardian و دو فرزند، دو مدرسه، کلاس رسمی، Block حین Safety، چهار outcome، رسانه پشتیبانی‌نشده با provider واقعی داخلی، Edit، پنجره reviewer، escalation/Reason/freshness، اخطار/محدودیت و منع suspension برای reviewer. regressionهای 14B–14E با قرارداد جدید تطبیق داده شده‌اند؛ تستی غیرفعال نشده است.

Validation کامل پس از پیاده‌سازی: restore موفق؛ build با صفر warning/error؛ Unit=413، Integration=674، Architecture=14، مجموع 1101 passed، صفر failed و صفر skipped. نسبت به baseline برابر 1050، ۲۱ Unit و ۳۰ Integration افزوده شده‌اند. final build/test نیز پیش از commit تکرار و با همین تعداد موفق شد؛ نتیجه واقعی Git در گزارش نهایی می‌آید. EF pending-model check با startup پروژه Infrastructure موفق بود. تست زنده SMS، AI، provider خارجی یا load/staging در این مرحله انجام نشده است.

## ۱۲. معماری، تصمیم و مسیر مطالعه

Domain قواعد سن، دعوت و انتقال پرونده را نگه می‌دارد. Application قرارداد/validation و سیاست رابطه را مالک است. Infrastructure query منبع رابطه، EF/SQL و موتور داخلی را پیاده می‌کند. Api transport، احراز و composition را نگه می‌دارد. .NET 9، SQL Server، EF Core SQL Server، Onion، Modular Monolith و Controllers بدون تغییرند؛ dependency یا framework جدیدی نصب نشده است.

`CommunicationAgeController → CommunicationAgeService → CommunicationAgeVerification → EfCommunicationAgeStore → SQL/Audit`

`ConversationsController → MessagingService → IContentSafetyProvider → InternalMakanContentSafetyProvider → EfMessagingStore final authorization → SQL/Outbox → receipt`

`PlatformModerationController → PlatformEducationSafetyService → EfPlatformEducationSafetyStore → Permission/MFA/Case → SQL/Audit → context محدود`

ترتیب مطالعه پیشنهادی: Controller مربوط → Application service/contracts → Domain entity → `MessengerRelationshipQuery` → store مربوط → EF configuration → migration → تست‌های نام‌برده. نکات آموزشی: نقش معادل سن نیست؛ رابطه معادل Read نیست؛ مجوز قبلی دائمی نیست؛ approval و atomicity جداگانه لازم‌اند؛ provider نتیجه می‌دهد، authorization نمی‌دهد؛ حفظ تاریخچه با حفظ اختیار ارسال متفاوت است.

## ۱۳. محدودیت‌ها و مرحله بعد

قواعد متن محدودند، رسانه معناشناسی نمی‌شود، Review خودکار publish ندارد، appeal/SLA/retention حقوقی و richer age assurance/provider آینده‌اند. صف انسانی و محدودیت فایل می‌توانند به عملیات/ظرفیت نیاز داشته باشند؛ این مرحله آن شواهد محیطی را جعل نمی‌کند. register معوق، رفتار فعلی را از upgrade آینده جدا می‌کند.

ملی‌پیامک و تنظیمات OTP، فایل‌های قبلی AI و GAP-03 دست‌نخورده‌اند. تنها تغییر appsettings این قابلیت انتخاب `Messaging:ContentSafetyProvider=Internal` در نمونه Production است. Git پس از validation باید فقط همین gap را commit/push کند؛ hash واقعی در گزارش نهایی می‌آید. closure کد GAP-05، GAP-02 یا readiness عمومی را خودکار نمی‌بندد؛ gap بعدی پیشنهادی audit، GAP-08 observability، و تأیید محیط امن GAP-02 همچنان لازم است.
