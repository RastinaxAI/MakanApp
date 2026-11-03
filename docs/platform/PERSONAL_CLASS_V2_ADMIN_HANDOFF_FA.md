# تحویل Platform Admin — PersonalClass v2

مرجع قرارداد ۲۰۲۶-۱۱-۰۳: source واقعی، `/openapi/v1.json` و [راهنمای یکپارچه](../product/PERSONAL_CLASS_V2_COMPLETE_FRONTEND_HANDOFF_FA.md). Backend .NET 9، SQL Server، Onion و Modular Monolith باقی مانده است.

## تفکیک اختیار

| Permission | وظیفه | چیزی که اعطا نمی‌کند |
|---|---|---|
| `platform.personal_classes.read` | لیست/detail/history تشکیل و metadata امن تاریخچهٔ کلاس | پاسخ، draft grade یا یادداشت خصوصی |
| `platform.personal_classes.review` | NeedsChanges/Approved/Rejected درخواست | مدیریت خصوصی آموزش یا عضویت بی‌رضایت |
| `platform.personal_classes.academic_permissions.manage` | اعطا/لغو سه اختیار آموزشی به membership مدیر مشخص | دسترسی خود Admin به تصحیح/پاسخ |
| `platform.audit.read` | خواندن Audit مدیریتی با فیلترهای موجود | مجوز endpointهای آموزشی خصوصی |

Bearer از نوع Opaque، نشست معتبر Backend را معرفی می‌کند و به‌تنهایی permission ایجاد نمی‌کند. جریان ورود: نشست موجود → `GET /api/admin/v1/me` → نقش‌ها و permissionهای جاری. منبع دسترسی، assignment فعال Platform IAM است و هر endpoint permission را سمت سرور دوباره بررسی می‌کند؛ مخفی‌کردن UI امنیت ایجاد نمی‌کند. School Manager نقش Platform نیست. مجوز کلی review را به مرور arbitrary learner answer تبدیل نکنید.

## Queue، review و تشکیل

پیشوند `R=/api/admin/v1/personal-class-requests`:

| Method / Route | ورودی | خروجی |
|---|---|---|
| GET R | status?, page=1, pageSize=30 | PersonalClassPage<PersonalClassRequestDto> |
| GET R/{id} | شناسه | درخواست، قابلیت و progress |
| POST R/{id}/review | expectedVersion, decision, reason, finalManagerUserId? | درخواست به‌روز و CreatedClassId در صورت تأیید |
| GET R/{id}/history | page، pageSize | تاریخچهٔ تصمیمِ مجاز Platform |

review از state و rowversion جاری پیروی می‌کند؛ نسخهٔ کهنه Core برابر **409 PERSONAL_CLASS_VERSION_CONFLICT** است. approval هم‌زمان/retry نباید کلاس یا جفت space تکراری بسازد. پیشنهاد Teacher/Manager با عضویت/consent/اختیار آموزشی برابر نیست. اگر مدیر نهایی شخص دیگری باشد، وضعیت consent موجود را از progress بخوانید؛ Ready/Active را از Approved حدس نزنید.

## Provisioning و حالت کلاس

تأیید، provisioning و activation سه وضعیت متفاوت‌اند. Group/Channel اختصاصی از زیرساخت Official Spaces موجود ساخته می‌شوند. مدیر مجاز از `/api/v1/personal-classes/{id}/spaces` وضعیت واقعی هر purpose را می‌خواند؛ repair موجود `202` بدون بدنه دارد و نیازمند refetch است. وجود خطا مجوز ایجاد فضای موازی یا تغییر دستی دیتابیس نیست.

این مرحله Admin endpoint تازه‌ای برای suspend/archive خصوصی ایجاد نکرده است؛ مسیر موجود مدیر محلی و قواعدش حفظ شده‌اند. archive/suspend دسترسی عملیاتی را محافظه‌کارانه محدود می‌کند، اما سیاست نهایی برخورد با آزمون در حال اجرا/خاتمهٔ اضطراری هنوز Product-open است. record تاریخی را حذف نکنید و پایان مهلت حدسی نسازید.

## Grant آموزشی

```text
GET /api/admin/v1/personal-classes/{classId}/academic-permissions
PUT /api/admin/v1/personal-classes/{classId}/academic-permissions/{membershipId}
```

PUT: `SetPersonalClassAcademicPermissionCommand` با `capabilities`, `clientOperationId`, `expectedMembershipVersion`, `expectedGrantVersion?`, `reason`؛ پاسخ `PersonalClassAcademicPermissionDto` شامل `isEffective`, `parentGrantId`, `version` است.

سه capability: `AuthorAssignments`, `AuthorExams`, `Grade`. مدیر نیز به grant صریح نیاز دارد. تفویض به Teacher فقط از grant مؤثر مدیر و به‌صورت subset انجام می‌شود. انتقال مدیریت grant را منتقل نمی‌کند؛ لغو grant مبنا یا پایان عضویت، تفویض وابسته را بی‌اثر می‌کند. ارتباط authorization در زمان استفاده دوباره بررسی می‌شود.

## تاریخچه و Audit

مسیر تازهٔ metadata امن:

```http
GET /api/admin/v1/personal-classes/{classId}/history?page=1&pageSize=30
```

permission خواندن لازم است. response صفحه‌ای از `PersonalClassHistoryItem`: `id`, `action`, `resourceType`, `resourceId`, `occurredAtUtc`, `actorUserId?`, `correlationId?`.

Platform تاریخچهٔ تشکیل و metadata انتشار فعالیت را می‌بیند؛ رخداد خصوصی release/correction نتیجه به این view وارد نمی‌شود. نمایش metadata grant نیازمند permission مستقل مدیریت grant است. Reason و Before/AfterSummary به این projection وارد نمی‌شوند. مدیر محلی از route user تاریخچهٔ محدودتری می‌بیند؛ review داخلی Platform و actor/correlation داخلی برای او مخفی است.

Audit کامل در زیرساخت قبلی باقی است:

```http
GET /api/admin/v1/audit-logs?targetType=PersonalClass&targetId=<classId>
```

فیلترهای واقعی `actorUserId`, `action`, `targetType`, `targetId`, `result`, `fromUtc`, `toUtc`, `page`, `pageSize`, `sortBy`, `sortDirection` موجودند. default این endpoint عمومی Audit برابر pageSize=50 است؛ با default=30 تاریخچهٔ کلاس اشتباه نشود.

| رخداد | محل ثبت |
|---|---|
| submit/NeedsChanges/Approved/Rejected | AdminAuditLog موجود با target درخواست |
| نقش، انتقال، پایان عضویت، suspend/archive | Audit Core موجود؛ metadata شناسه و نقش برای trace بهتر کامل شده |
| ایجاد دعوت/پذیرش عضویت | Audit همان transaction؛ replay ثبت تکراری ایجاد نمی‌کند |
| grant/revoke/delegation | Audit grant موجود و رابطهٔ parent |
| AssignmentPublished/ExamPublished | writer آموزشی در transaction انتشار |
| AssignmentResultReleased/Corrected | target همان SubmissionAttempt، بدون نمره/متن |
| ExamResultReleased/Corrected | target همان ExamAttempt، بدون پاسخ/یادداشت |

`EfAcademicAuditWriter` فقط PersonalClass را ثبت می‌کند؛ transaction جدا نمی‌سازد. correlation امن درخواست در Audit حفظ می‌شود. reason آزاد اصلاح، answer، score، feedback و evaluator private note در summary جدید ذخیره نمی‌شوند. API Audit موجود با permission مستقل، جایگزین طراحی UI مرور خصوصی نیست.

## Realtime و خطا

Admin account stream همان Hub و replay موجود است. برای reviewerهای دارای read، تغییر درخواست غیردرفت `access` ایجاد می‌کند؛ UI Queue/detail/history را refetch کند. [کاتالوگ نسخهٔ 1](../product/PERSONAL_CLASS_V2_EVENT_CATALOG_FA.md) رفتار at-least-once، cursor و resync را توضیح می‌دهد.

401 نشست؛ 403 permission یا authorization جاری ناکافی؛ 404 concealment؛ 409 نسخه/state/capacity؛ 429 limiter؛ 503 وابستگی موقت. متن نمایشی را از code پایدار جدا کنید؛ exception خام/توکن/اطلاعات خصوصی در ticket پشتیبانی قرار ندهید.

## محدودیت‌ها

این مرحله migration جدید، deployment، AI runtime یا دسترسی ویژهٔ Parent ایجاد نکرده است. retention و دسترسی پس از خروج، running exam هنگام archive/suspend، emergency termination، سقف مدیر اضافی، قیمت/payer، Study Studio و گزارش‌های پیشرفته نیازمند تصمیم مستقل Product هستند. Age/DOB شرط کسب‌وکاری این مرحله نیست.
