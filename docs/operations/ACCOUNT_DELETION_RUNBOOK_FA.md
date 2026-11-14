# عملیات و بازیابی حذف حساب — BE-22

بازبینی `2026-11-14`. این راهنما، دستور فعال‌سازی خودکار یا شاهد Deploy نیست. روی `MakanApp`، `MakanApp_Test` یا هیچ سرور Migration/حذف اجرا نشده است. پذیرش فقط با SQL ایزوله، فایل موقت و Knowledge ساختگی انجام می‌شود.

## پیش‌شرط اجرا

1. Backup/Restore و سطح دسترسی را طبق [Runbook موجود](../production/BACKUP_RESTORE.md) بررسی کنید؛ Backup و keyring را حذف یا بازنویسی نکنید.
2. Migration `20261114140323_AddAccountDeletionLifecycle` را در برنامهٔ انتشار مستقل بازبینی و اعمال کنید؛ Startup برنامه آن را اجرا نمی‌کند. Up افزایشی است و requestهای قدیمی را `Requested` با هشت مرحلهٔ خالی نگه می‌دارد.
3. صف `GET /api/admin/v1/privacy/deletion-requests`، درخواست‌های قدیمی، مسئولیت مدیر/مالک و وابستگی پرونده‌های Paid را بررسی کنید. روشن‌کردن Worker، درخواست‌های قدیمیِ Requested را هم با همان زمان ثبت اصلی زمان‌بندی می‌کند.
4. مقدار حقوقی/محصولی مهلت را صریحاً تعیین کنید. تنظیمات پیش‌فرض `ExecutionEnabled=false` و `GracePeriodHours=null` هستند؛ محیط Production یا Test به‌خودی‌خود مجوز حذف نیست.
5. برای منابع شخصیِ دارای نسخه، مسیر داخلی Knowledge و قرارداد delete باید آماده باشند. این مرحله تنظیم AI، Service Auth، JWT، Timeout یا MeliPayamak را تغییر نمی‌دهد. غیرفعال‌بودن سرویس دانش، دلیل Completed نیست.

تنظیم‌ها `AccountDeletion:ExecutionEnabled` و `AccountDeletion:GracePeriodHours` هستند؛ در environment به‌ترتیب `AccountDeletion__ExecutionEnabled` و `AccountDeletion__GracePeriodHours`. مقدار مهلت باید عدد صحیح نامنفیِ مصوب باشد؛ `true` بدون مهلت معتبر Startup را رد می‌کند. صفر فقط در پذیرش ایزوله استفاده شده است؛ مدت Production از آن نتیجه نمی‌شود.

تنظیم پایه و دو فایل `.example` به‌روز شده‌اند. فایل‌های خصوصی Development/Test/Production، Secretها، اتصال SQL، Storage و Providerها حفظ شده‌اند. تنظیم پایه به هر دو محیط ارث می‌رسد؛ override خصوصی اپراتور باید جدا بررسی شود. خصوصی‌بودن config مجوز اشتراک‌گذاری آن در Git یا گزارش نیست.

## پیشرفت پایدار

`AccountDeletionDispatcher` فقط با opt-in اجرا می‌شود. `AccountDeletionProcessor` در هر claim یک مرحلهٔ SQL یا یک نسخهٔ Knowledge را پردازش می‌کند. Network داخل transaction SQL قرار ندارد.

```text
Requested -> Scheduled -> Processing -> Completed
                              |
                              v
                        NeedsAttention -> Retry مجاز -> Scheduled
```

هشت مرحله به‌ترتیب: `FreezeAccount`, `EndAccessRelationships`, `PurgePrivateAddressBook`, `RevokePersonalSources`, `DeletePersonalKnowledge`, `AnonymizeIdentity`, `ClosePersonalEntitlements`, `Finalize`.

Claim از SQL با `UPDLOCK/READPAST/READCOMMITTEDLOCK` گرفته می‌شود. lease فنی پنج دقیقه است، نه retention حقوقی. token claim و `rowversion`، Worker قدیمی را هنگام ثبت نتیجه متوقف می‌کنند. `READPAST` ممکن است موقتاً صف ظاهراً خالی برگرداند؛ دور بعدی دوباره بررسی می‌کند.

شروع و تلاش هر مرحله پیش از عمل پایدار می‌شود. تغییر SQL همان مرحله و checkpoint موفق با هم commit می‌شوند. در Knowledge ابتدا command ثابتِ هر SourceId/Version ثبت، سپس delete خارج SQL فراخوانی و پاسخ قطعی در transaction جدید با claim جاری ثبت می‌شود. قطع بعد از اثر خارجی و قبل از ack می‌تواند همان command را دوباره بفرستد؛ این مسیر exactly-once شبکه نیست و به delete idempotent قرارداد موجود متکی است.

شکست گذرا با backoff فنی از دو ثانیه تا سقف پنج دقیقه تکرار می‌شود. پس از ده شکست/claim رهاشدهٔ متوالی، `NeedsAttention` ثبت می‌شود. پیشرفت موفق بودجهٔ متوالی را صفر می‌کند. مرحلهٔ موفق تکرار نمی‌شود؛ نسخهٔ پاک‌شده دوباره در retry عادی به Knowledge فرستاده نمی‌شود. در shutdown، budget محدود پنج‌ثانیه‌ای فقط برای ثبت شکست/ادامه استفاده می‌شود؛ اگر ثبت ممکن نباشد، lease منقضی و recovery بعدی انجام می‌شود.

## رسیدگی به NeedsAttention

| کد امن | کار اپراتور |
|---|---|
| `ACCOUNT_DELETION_LAST_ORGANIZATION_MANAGER` | از مسیر موجود مدیر جایگزینِ فعال و دارای نقش معتبر ایجاد/تأیید شود؛ نقش صوری یا User غیرفعال کافی نیست. |
| `ACCOUNT_DELETION_LAST_PERSONAL_CLASS_MANAGER` | انتقال مدیریت با پذیرش معتبر یا Archive موجود؛ Legacy بدون Manager صریح نیز محافظت می‌شود. |
| `ACCOUNT_DELETION_LAST_PLATFORM_OWNER` | آخرین Owner فعال را حذف نکنید؛ مسیر IAM موجود و ممیزی‌شده مبناست. |
| `ACCOUNT_DELETION_CONVERSATION_OWNERSHIP` | Owner گفتگوی user-managed باید انتقال مالکیت موجود یا بایگانی را انجام دهد. |
| `ACCOUNT_DELETION_SCHOOL_MANAGER_DEPENDENCY` | پروندهٔ Paid بدون مدیر جایگزین onboardingشده را به عملیات ارجاع دهید؛ مدیر نهایی را با SQL یا جانشین خودکار عوض نکنید. |
| `ACCOUNT_DELETION_KNOWLEDGE_UNAVAILABLE` | availability/auth/contract شبکهٔ Knowledge را خارج این workflow بررسی کنید؛ body و JWT را log نکنید. فقط Deleted یا NotIngested قطعی پذیرفته می‌شوند؛ Superseded کافی نیست. |
| `ACCOUNT_DELETION_REFERENTIAL_CONFLICT`, `ACCOUNT_DELETION_INVARIANT_FAILED`, `ACCOUNT_DELETION_STEP_FAILED`, `ACCOUNT_DELETION_RECOVERY_EXHAUSTED` | با RequestId، correlation، مرحله و زمان، علت فنی را بررسی کنید؛ موفقیت یا وضعیت SQL را دستی جعل نکنید. |

Guardهای مدیر/مالک پیش از freeze بررسی می‌شوند و در این مانع‌ها حساب هنوز فعال می‌ماند تا انتقال مسئولیت از جریان موجود ممکن باشد. شکست پس از freeze، حساب را دوباره فعال نمی‌کند؛ مراحل موفق حفظ می‌شوند.

پس از رفع علت، Owner مجاز Detail جاری را می‌خواند و `POST /api/admin/v1/privacy/deletion-requests/{id}/retry` را با `clientOperationId` و `expectedVersion` می‌فرستد. permission مستقل Manage فقط برای Owner seed شده و داخل SQL دوباره بررسی می‌شود. Retry مهلت اولیه، اثر پرداخت یا مرحلهٔ موفق را بازنویسی نمی‌کند. endpoint Force Complete وجود ندارد. صف generic Job راه میان‌بر Privacy نیست.

## مشاهده‌پذیری و دادهٔ حساس

رویدادهای append-only: `privacy.DeletionRequested`, `DeletionScheduled`, `DeletionStarted`, `DeletionStepCompleted`, `DeletionNeedsAttention`, `DeletionResumed`, `DeletionCompleted`. Worker، actor/session جعلی تولید نمی‌کند؛ Audit اجرای سیستمی actor خالی و RequestId/PolicyVersion/مرحله/کد محدود دارد.

correlation پایدار `account-deletion-{RequestId:N}` است. log شکست سطح Error دارد؛ claim واگذارشده Warning است و موفقیت جعلی ثبت نمی‌شود. Exception خام، StackTrace، SQL، مسیر فایل، Token، شماره، نام قبلی، دفترچه، پیام، bytes و AI body ثبت نمی‌شوند. metricها فقط worker/stage/outcome محدود دارند و شناسهٔ درخواست یا User label نیست. نام Worker برابر `AccountDeletion` است.

در health محافظت‌شده، `AccountDeletion=Enabled/Disabled` فقط تنظیم را نشان می‌دهد. وضعیت واقعی از Detail و جدول checkpoint خوانده شود. `attemptCount` درخواست، تعداد claimهاست؛ با attempt مرحله یا نسخهٔ Knowledge یکی نیست. اطلاعات دفعات و کد امن، مجوز مرور دادهٔ خصوصی ایجاد نمی‌کنند.

## توقف، Restore و حدود بازگشت

خاموش‌کردن `ExecutionEnabled` و restart، claim تازه را متوقف می‌کند؛ اثر انجام‌شده یا ناشناس‌سازی را برنمی‌گرداند. account واردشده به freeze قابل Reactivate نیست. هیچ Rollback هویت، برگشت پول یا merge دوباره با حساب تازه وجود ندارد. Down پس از شروع حذف/جداشدن Credential محافظ دارد؛ برای rollback انتشار پس از اجرای داده‌ای، از اصلاح رو به جلو و برنامهٔ بازیابی مصوب استفاده کنید.

Backup ممکن است PII تاریخی داشته باشد؛ این workflow آن را پاک نمی‌کند. Restore به نقطهٔ قبل از حذف می‌تواند وضعیت قدیمی را برگرداند؛ قبل از بازکردن ترافیک، رسیدها و حذف‌های بعد از restore point باید طبق برنامهٔ عملیاتی معتبر تطبیق و بازاعمال شوند. دفتر ثبت مستقل و purge خودکار Backup در این مرحله ساخته نشده و برای آن ادعای پذیرش نداریم. مدت Backup/legal hold/replay و سایر retentionها همچنان نیازمند تصمیم رسمی‌اند.

پاک‌سازی bytes فقط از `TerminalFileCleanup` موجود، با flag/grace مستقل، status نهایی، نبود reference و نبود Retained انجام می‌شود. این مرحله آن را روشن نمی‌کند. هیچ sweep روی Storage، Ready-orphan reaper، حذف keyring یا پاک‌سازی تاریخچهٔ SQL ایجاد نشده است.

جزئیات داده: [ماتریس](../product-v6/ACCOUNT_DELETION_DATA_MATRIX_FA.md)، [قرارداد API](../product-v6/ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md)، [سیاست نگه‌داری](RETENTION_AND_CLEANUP_POLICY.md).
