# برنامهٔ مهاجرت و Backfill کلاس مستقل نسخهٔ ۲

این مرحله Migration را فقط روی SQL ایزولهٔ خودکار آزمایش می‌کند. اجرای روی `MakanApp`، `MakanApp_Test` یا سرور، Publish و Deploy مجاز نیست و انجام نمی‌شود.

Migration پایهٔ مقایسه `20261101202642` در Source `154c437` است. دو Migration جدید `20261103073219_AddReviewedPersonalClassLifecycle` و `20261103074858_BindPersonalClassInvitationRoles` وجود دارند. اعتبارسنجی شامل empty→latest و baseline با دادهٔ Legacy→latest است.

## نگهداری داده

- شناسه، عنوان، Host، ClientOperationId و زمان‌های کلاس‌های موجود باقی می‌مانند.
- Host با حساب Active به Manager اولیه نگاشت می‌شود. اگر قبلاً عضو فعال باشد همان ردیف نقش Manager می‌گیرد؛ در غیر این صورت فقط عضویت Manager ایجاد می‌شود. Host نامعتبر/غیرفعال به `NeedsManagerMapping` می‌رود و مدیر حدس زده نمی‌شود.
- Active participantهای قدیمی Learner باقی می‌مانند؛ شناسه، دورهٔ عضویت و InvitationId حفظ می‌شود. ردیف duplicate برای آن‌ها ساخته نمی‌شود.
- دعوت‌های قدیمی نقش Learner و تفویض None دارند؛ مدت اعتبار و Accepted/Revoked قبلی rewrite نمی‌شود.
- Capacity برای Legacy برابر NULL و پذیرش Join برابر false است. عضو موجود حذف نمی‌شود؛ پذیرش تازه تا ثبت ظرفیت صریح مدیر بسته است.
- State کلاس فعال/بایگانی و MigrationState از وضعیت موجود استخراج می‌شود؛ تاریخ، قیمت، سهمیه یا مصرف تخمینی ساخته نمی‌شود.
- Migration هیچ Group/Channel، پیام یا activity تاریخی نمی‌سازد.

## طرح ارتباطی پیش از اجرا

`GET /api/v1/personal-classes/{id}/spaces/backfill-plan` برای مدیر، خروجی read-only دارد: classId، host/manager، تعداد عضویت فعال، binding موجود، candidateهای بدون binding، اقدام Group/Channel، Conflict و ManualDecision و PlanSha256.

binding نوع‌دار موجود reuse می‌شود. مقصد واقعاً مفقود `CreateMissing` است. گفتگوی شخصی مشابه از روی نام/ایجادکننده صرفاً candidate است و **خودکار متصل نمی‌شود**؛ چنین وضعیتی `UnboundConversationOwnership` و Conflict است. مدیر مفقود یا گفتگوی غیرقابل استفاده نیز نیازمند تصمیم صریح است. کلاس Archived فضای تازه دریافت نمی‌کند.

تنظیم ظرفیت Legacy به‌تنهایی Outbox backfill نمی‌سازد. Repair API نیازمند ExpectedVersion و Hash همان طرح خوانده‌شده است. طرح دوباره در تراکنش بررسی می‌شود. Worker همان provisioning موجود را اجرا می‌کند؛ مقصد موفق حفظ و فقط مقصد مفقود/نیازمند ترمیم بررسی می‌شود. GET spaces هیچ تغییری ندارد.

ابزار موجود `tools/organization/MakanApp.OfficialSpacesMaintenance` گزینه‌های `--personal-classes` و `--personal-class-id` دارد. ابتدا dry-run و سپس apply محدود به یک شناسه با Hash طرح مورد تأیید استفاده شود؛ قواعد واقعی CLI را از `Program.cs` بخوانید. در این مرحله فقط fixture ایزوله مجاز است. عملیات گسترده یا اتصال خودکار conversationهای مبهم مجاز نیست.

## برگشت و بازیابی

Down برای دادهٔ جدید حفاظ دارد: اگر درخواست، انتقال، Join، نقش غیرLegacy یا binding جدید وجود داشته باشد با SQL error مشخص متوقف می‌شود. این محافظ مانع حذف خاموش داده است؛ اجرای Down روی دادهٔ واقعی جایگزین برنامهٔ backup/restore نیست.

تست `PersonalClassV2MigrationTests` هر بار دیتابیس تازه با نام یکتای محافظت‌شده می‌سازد، مسیر مهاجرت را اجرا و فقط همان دیتابیس ساخته‌شده توسط آزمون را حذف می‌کند. تست Recovery، ساخت ناقص Group/Channel، حفظ binding، retry بدون duplicate و صفر بودن پیام تاریخی را اثبات می‌کند. Artifact محلی `PERSONAL_CLASS_V2_BACKFILL_DRY_RUN_FA.md` به fixture ساخته‌شده در این مرحله مربوط است؛ گزارش دادهٔ سرور نیست.
