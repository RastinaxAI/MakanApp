# وضعیت جاری پروژه و مسیر مطالعه اسناد

بازبینی `2026-10-30` از `main@e11d4ec`؛ **365 عملیات HTTP در63 Controller و322 مسیر؛608 schema،113 جدول و55 migration**. آخرین migration `20261030060025_AddExamAnswerAttachments` است. شمارش از OpenAPI/ApiExplorer و مدل طراحی EF بدون اتصال SQL، worker، SMS یا AI استخراج شده است؛ applied schema یا پذیرش سرور از آن معلوم نمی‌شود.

## تغییرات کد و بیزینس

| حوزه | وضعیت جاری و مرجع |
|---|---|
| Identity / Platform | opaque session و refresh چرخشی؛ MFA/Step-Up حذف مانده، permission/reason/audit فعال؛ [قرارداد Platform](platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) |
| Profile / School | DOB خوداظهاری، Photo با grant احرازشده، Directory/roster، Guardian consent و Lesson موجود؛ [مدرسه](product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md) |
| Commerce | مالک Individual/Organization، PlanVersion immutable، Quote/Order، دورهٔ ماهانه و Upgrade/Downgrade؛ [Handoff کاربر](product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md) و [Admin](product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md) |
| Legacy | تا قرارداد معتبر Admin، AwaitingValidContract؛ LegacyPendingContract نامحدود نیست؛ enforcement از زمان واقعی ثبت و Grace هفت‌روزهٔ سازمان |
| Quota / Export | CurrentCapacity بدون reset ماهانه، reservation/ledger و اتصال واقعی ReportExport با settlement و recovery؛ Unknown نتیجهٔ مالی قطعی نیست |
| PersonalClass | میزبان User، دعوت/پذیرش/عضویت/archive و ظرفیت؛ CanCreateEducationalContent=false، بدون Assignment/Exam مستقل |
| Realtime | bootstrap پیش از Snapshot، account replay و ClientInvalidated با مجوز جاری؛ cursor24ساعت وابسته به Session/Context و Data Protection host؛ [قرارداد Event](product/REALTIME_EVENT_CONTRACT_FA.md) |
| پاسخ آموزشی | تکلیف workflow موجود را حفظ کرده؛ آزمون policy نسخه‌دار سؤال، Attach/Detach revision و Finalize/Download مجاز دارد؛ [قرارداد](product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md) |
| Safety Messenger |422 PendingReview/Rejected، NotApplied و KEEP_DRAFT_NO_AUTO_RETRY؛ workflow انتشار رسانه هنوز PRODUCT_DECISION_BLOCKED؛ [BE-01](messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md) |
| Hosting | CORS از config مستقل از محیط؛ OTP Test=false/true و Production=true/false؛ ready=config/SQL/Storage، تداوم کلید cursor جدا بررسی می‌شود؛ [استقرار](deployment-windows-linux-docker.md) |

خرید نقش آموزشی یا مجوز داده ایجاد نمی‌کند. Trial و AI preflight بنیاد قراردادند؛ Runtime AI فعال نشده است. meter ناموجود مصرف صفر یا Unlimited قطعی نیست. پایان نقش حساب، عکس یا پیام‌رسانی شخصی مجاز را حذف نمی‌کند.

## محدودیت‌ها و gateهای مستقل

| موضوع | وضعیت |
|---|---|
| درگاه پرداخت واقعی / قیمت و سقف تجاری | provider unavailable؛ مقدار تجاری حدسی Seed نشده است |
| Learning Profile / Badge / Source lifecycle / RAG | هنوز پیاده‌شده فرض نمی‌شوند؛ [تفکیک محصول](product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md) |
| AI واقعی | پیش‌فرض خاموش؛ Service Auth قبلی PASS، Parent Provider Timeout و Full-12 Deferred |
| Push دستگاه واقعی | P1_PUSH_RUNTIME_PENDING |
| چند replica، backplane، keyring مشترک و Load/SLO | PENDING_SCALE_INFRA؛ replay محلی اثبات topology هدف نیست |
| P3/LiveKit/TURN | P3_S_INFRA_REQUIRED؛ rejoin NOT_TESTED و Production approval NOT_GRANTED |
| IIS/TLS/Proxy، restart/reboot، backup/restore/alert | پذیرش محیط هدف جدا؛ این مرحله Deploy/Publish یا migration سرور نیست |

## دیتابیس و نگه‌داری

سه migration پس از CompleteSchoolManagement: AddCommercialOwnershipPeriodsAndPersonalClasses، BindReportExportsToCommercialReservations و AddExamAnswerAttachments. شش جدول افزوده: سه PersonalClass، UsageReservations، SubscriptionAmendments و AnswerRevisionAttachments. migration خودکار startup نداریم. RemovePlatformMfa قدیمی حذف داده دارد و Down داده را برنمی‌گرداند؛ rollout و backup هدف مستقل است.

DB و Storage پیوست آزمون/تکلیف/Lesson/Photo سازگار backup شوند؛ Detach blob تاریخی را حذف نمی‌کند. Outboxهای منبع replay خودکار purge نمی‌شوند و cleanup terminal همچنان opt-in و خاموش است. key ring اختصاصی MFA حذف است، ولی Data Protection استاندارد حفاظت cursor مصرف جدید دارد؛ Identity key جای آن نیست. [Runbook](operations/REALTIME_RUNBOOK_FA.md)، [retention](operations/RETENTION_AND_CLEANUP_POLICY.md) و [Backup](production/BACKUP_RESTORE.md).

## مسیر مطالعه

[راهنمای بک‌اند](backend-api-guide.md) → [مرجع API](backend-api-reference.md) → [قرارداد فرانت](frontend-contract/README.md) → Handoffهای تخصصی جدول بالا → [فرهنگ SQL](database-current-structure.md) → [استقرار](deployment-windows-linux-docker.md). HTML/PDF چهار کتاب از همین Markdownها ساخته می‌شوند؛ نسخهٔ Database مطابق ignore قبلی محلی است. [فهرست اسناد](documentation-inventory.csv) و [گزارش تغییرها](DOCUMENTATION_UPDATE_REPORT.md).

## Validation بازبینی جاری

Restore موفق؛ Build اولیه و نهایی هر دو با **صفر warning و صفر error** موفق شدند. suite کامل اولیه و نهایی هر کدام **2478 تست موفق** داشتند:922 Unit،1542 Integration و14 Architecture؛ failed/skipped صفر. شمارنده‌های شش TRX کنترل شدند.12 تست ابزار اسناد هم موفق‌اند؛ تست تازهٔ محصول به کار مستندات اضافه نشد.

اجرای محصول روی checkout مستقل و LocalDB یکتای `MakanApp_DocsSync_20261030_c817e6d9` بود؛ نام موقت fixture پس از suite نهایی به bytes اصلی برگشت و وارد تحویل نیست. EF has-pending-model-changes تغییر معلق ندارد؛ هیچ migration روی دیتابیس معمول یا سرور اجرا نشده است.

کنترل365 operation،608 schema،113 جدول،55 migration و154 ورودی inventory موفق است؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. PDFهای Guide/Reference/Frontend/Database به‌ترتیب 78 / 458 / 466 / 172 صفحه دارند؛ bookmark و SHA متناظر HTML کنترل شدند. کنترل صفحهٔ خالی و متن خارج صفحه موفق و صفحات Commerce/Replay/Attachment/SQL بصری بازبینی شدند. این‌ها پذیرش UI، دستگاه یا سرور واقعی نیستند.

فایل‌های محلی قبلی ابزار AI خارج scope و بدون stage/تغییر باقی‌اند. فایل خصوصی تنظیمات، کد Frontend، دیتابیس معمول توسعه و سرور تغییر نکرده‌اند. Source baseline گزارش با commit اسناد متفاوت است؛ نتیجهٔ Commit/Push پس از موفقیت validation در گزارش تحویل Git ارائه می‌شود.
