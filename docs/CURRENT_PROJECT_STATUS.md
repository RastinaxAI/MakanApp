# وضعیت جاری پروژه و مسیر مطالعه اسناد

بازبینی `2026-10-24` از `main@e525c78` به‌همراه تغییرات حذف عامل دوم؛ **318 عملیات HTTP در 56 Controller، 105 جدول و 49 Migration**. مدل و OpenAPI از working tree استخراج شده‌اند؛ آخرین Migration `20261024184613_RemovePlatformMfa` هنوز روی دیتابیس اصلی یا سرور اعمال نشده است. [اثر تغییر](platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md).

## تغییرات کد و بیزینس نسبت به بازبینی قبلی

| حوزه | تغییر مؤثر و مرجع |
|---|---|
| OTP و ورود | Real=true/false؛ Mock فقط Development/Test/Testing=false/true، بدون اثبات مالکیت شماره؛ Production/Staging fail-closed؛ [قرارداد](dev/CONFIGURABLE_OTP_FA.md) |
| Test/Production | profileهای جدا، JSON هم‌ساختار با مقادیر مستقل و حفظ secretها؛ [Publish Guide](production/PUBLISH_GUIDE_FA.md) و [شاهد انتشار](production/BACKEND_RELEASE_READINESS_FA.md) |
| Platform / readiness | MFA و key ring صرفاً مرتبط حذف شده‌اند؛ OTP/Session/permission و config/SQL/Storage در ready حفظ شده‌اند؛ [تصمیم جاری](platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) |
| workerها | claim/lease/fencing، repair محدود، completion budget، انتشار امن export؛ [بازیابی](operations/WORKER_RELIABILITY_AND_RECOVERY.md) |
| observability / IIS | log و exception امن، snapshot stale-aware، shutdown Host/ANCM/WAS مستقل؛ [Observability](production/OBSERVABILITY.md) و [IIS](production/DEPLOYMENT_IIS.md) |
| retention | مکانیزم پیش‌فرض خاموش برای bytes terminal بدون reference؛ بدون purge SQL؛ [سیاست](operations/RETENTION_AND_CLEANUP_POLICY.md) |
| Messenger Block | seek زوج فعال روی index موجود برای retry موازی، بدون تغییر policy/DTO/schema؛ [شاهد مرحله](production/BACKEND_RELEASE_READINESS_FA.md) |
| AI v1 | grounding/response سخت‌گیرانه، telemetry و probe امن؛ Service Auth قبلی PASS، Parent Provider Timeout و Full-12 Deferred؛ [وضعیت](ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) |

Identity/session، onboarding Organization/Guardian، Academic/Assessment، report/export، Notification، Commerce و Backoffice موجودند؛ عامل دوم حذف شده است. Messenger P0/P1/P2 تا Checklist و transport Web Push/FCM در کد هستند؛ وضعیت media P3 و delivery واقعی از وجود کد نتیجه نمی‌شوند.

## gateهای باز

| gate | وضعیت |
|---|---|
| Web Push/FCM روی دستگاه واقعی | P1_PUSH_RUNTIME_PENDING |
| backplane و state مشترک چند instance | PENDING_SCALE_INFRA |
| LiveKit/TURN و رسانه P3 | P3_S_INFRA_REQUIRED؛ rejoin NOT_TESTED و Production approval NOT_GRANTED |
| AI واقعی | Service Auth قبلی PASS؛ Parent Provider Timeout، Full-12 Deferred |
| RAG source lifecycle | Deferred |
| پرداخت واقعی / SMS عمومی اعلان | provider unavailable؛ SMS اعلان مستقل از OTP ملی‌پیامک است |
| TLS، recycle/reboot، backup/restore/load، secret custody و alert | پذیرش محیط هدف لازم است؛ regression/Publish محلی کافی نیست |

انتخاب قبلی Production/HTTP و اعتماد استثنایی certificate SQL، closure انتشار عمومی امن نیست. وضعیت دقیق در [ماتریس Production](production/PRODUCTION_READINESS.md) و شواهد پیشین در [Consolidation](release/BACKEND_FINAL_CONSOLIDATION_FA.md) حفظ شده است.

## مسیر مطالعه

۱. [راهنمای بک‌اند](backend-api-guide.md): مدل، معماری، ورود و عملیات.

۲. [مرجع API](backend-api-reference.md): تمام method/route، request/response و schema.

۳. [قرارداد فرانت](frontend-contract/README.md)، [Backoffice](platform/BACKOFFICE_FRONTEND_HANDOFF.md) و [Messenger](messaging/CURRENT_MESSENGER_STATUS.md).

۴. [فرهنگ دیتابیس](database-current-structure.md)، [استقرار](deployment-windows-linux-docker.md)، پیکربندی PH، worker و retention.

۵. [فهرست تمام اسناد](documentation-inventory.csv): مرجع جاری، قرارداد موضوعی، شاهد تاریخی و خروجی محلی را مشخص می‌کند. فایل‌های اصلی محصول و نتایج تاریخی بازنویسی نشده‌اند.

## حدود بازبینی

metadata از TestServer بدون hosted worker، SQL، SMS یا AI واقعی و مدل طراحی EF استخراج شده است. DOC-CONTRACT-01 در snapshot فعلی mismatch ندارد و CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS است؛ سند روی سرور جدا بررسی می‌شود. سه فایل Python محلی ابزار AI خارج تغییرات و commit این مرحله باقی می‌مانند. این کار deployment، migration سرور، provider پولی، فعال‌سازی cleanup یا پذیرش عملیاتی انجام نمی‌دهد.

## نتیجهٔ validation این بازبینی

Restore موفق بود؛ Buildهای Debug و Release صفر Warning/Error داشتند. در هر دو اجرای کامل، ۲۱۹۹ تست پاس شدند: ۸۶۶ Unit، ۱۳۱۹ Integration و ۱۴ Architecture؛ failed/skipped صفر. دوازده تست ابزار مستندسازی نیز موفق‌اند. Suite در مخزن اصلی و فقط روی LocalDB خودکار و ایزوله اجرا شد؛ Migration فقط در همین fixture با اجازهٔ صریح مالک اعمال شد.

مدل EF pending change ندارد؛ Migration روی دیتابیس اصلی یا سرور اعمال نشده است. کاتالوگ ۳۱۸ عملیات/۵۴۷ schema/۱۰۵ جدول/۴۹ Migration و ۱۳۹ ورودی inventory کنترل شد؛ لینک شکسته و اختلاف Enum/OpenAPI صفر است. PDFهای Guide/Reference/Frontend/Database به‌ترتیب ۷۱/۴۰۰/۴۰۵/۱۵۵ صفحه دارند؛ bookmark، hash HTML، صفحهٔ خالی و متن خارج صفحه با ابزار بررسی شدند.

**تصمیم، فایل‌های حذف/تغییرکرده، اثر مخرب Migration و گزارش آموزشی این مرحله در [حذف عامل دوم Platform](platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) ثبت شده‌اند.** سه فایل Python قبلی AI و دو JSON خصوصی با hash اولیه برابرند؛ Push و استقرار سرور در این مرحله انجام نمی‌شوند. نتایج جاری جای پذیرش Production، provider واقعی یا closure gateهای باز نیستند.
