# استقرار Test candidate رفع Gapها — ۲۰۲۶-۱۱-۰۶

این راهنما برای اجرای مالک روی Windows/IIS است؛ Codex روی سرور عملیاتی اجرا یا استقراری انجام نداده است. بستهٔ این مرحله کاندید Test است و پذیرش کامل فایل Messenger تا تصمیم Product دربارهٔ `ReviewRequired` مسدود است. [قرارداد و وضعیت فازها](../product/BACKEND_FINAL_GAP_ACCEPTANCE_20261106_FA.md) را همراه گزارش Validation همان بسته بخوانید.

## پیش از استقرار

1. فایل ZIP و `SHA256SUMS` را از مسیر خصوصی Release بگیرید. Hash را با `Get-FileHash -Algorithm SHA256 -LiteralPath '<zip-path>'` مقایسه کنید. ZIP حاوی config خصوصی است؛ در Git یا محل دانلود عمومی نگذارید.
2. از فایل‌های فعلی سایت Test، config خصوصی، SQL `MakanApp_Test`، Storage و کلیدهای پایدار Data Protection طبق runbook موجود Backup بگیرید. Backup فقط وجود فایل نیست؛ قابلیت Restore آن لازم است.
3. محیط این سایت فقط `Test` است. `web.config` باید هر دو `ASPNETCORE_ENVIRONMENT` و `DOTNET_ENVIRONMENT` را `Test` بگذارد؛ `stdoutLogEnabled=false` باقی بماند.
4. در فایل خصوصی خروجی مقصد را بررسی کنید: Database=`MakanApp_Test`، `Otp:Enabled=false` و `Otp:AllowMockAuthentication=true`، `Swagger:Enabled=true`، `AiService:Enabled=false`. config محیط Production را به سایت Test منتقل نکنید. مقدار Secret را در ترمینال، گزارش یا چت چاپ نکنید.
5. .NET 9 Hosting Bundle، App Pool مناسب، ACL نوشتن Storage/Data Protection و HTTP binding فعلی `http://94.184.32.84:8090` را اپراتور کنترل کند. HTTP استثنای قبلی این محیط است؛ این مرحله TLS یا firewall را تغییر نمی‌دهد.

## SQL

در SSMS روی **MakanApp_Test** و اتصال تازه:

```sql
SELECT DB_NAME() AS DatabaseName, @@TRANCOUNT AS OpenTransactions, XACT_STATE() AS TransactionState;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
```

آخرین نسخهٔ مورد انتظار `20261104224922_AddSchoolMembershipManagementOperations` است. اگر حاضر است، این مرحله Migration تازه ندارد. اگر عقب است، پس از Backup و بازبینی history، اپراتور SQL کامل `migrations-idempotent-safe.sql` بسته را **یکجا** اجرا کند؛ نه raw EF SQL و نه تکه‌ای از میانهٔ فایل. اسکریپت از dynamic compilation برای ستون‌های تازه و توقف/rollback در خطا استفاده می‌کند. فایل upgrade فقط برای مبنای دقیق `20261104164717_HardenOrganizationClassCreationIntent` مناسب است. فایل کامل به معنی مجوز اجرای حدسی روی دیتابیس نامعلوم نیست.

پس از اجرا، history و نبود transaction باز را کنترل کنید. خطا را success فرض نکنید. Rollback دیتابیس با حذف دستی Migration یا اجرای Down حدسی انجام نشود؛ از طرح Restore معتبر و نسخهٔ سازگار برنامه استفاده شود. دیتابیس Production در این مرحله تغییر نمی‌کند.

## انتقال بسته

نام سایت/App Pool و مسیر فیزیکی واقعی را اپراتور از IIS بگیرد؛ در این راهنما نام حدسی داده نشده است. سایت Test را در پنجرهٔ نگه‌داری متوقف کنید، فایل‌های برنامه را از ZIP تأییدشده منتقل کنید و Storage/Data Protection را در مسیر پایدار قبلی نگه دارید. appsettings خصوصی مقصد و web.config را پیش از Start دوباره کنترل کنید. فایل‌های user content و کلیدها را حذف یا با فایل خالی جایگزین نکنید. `appsettings.Production.json` نباید همراه بستهٔ Test باشد.

پس از Start:

```powershell
Invoke-WebRequest 'http://94.184.32.84:8090/health/live' -UseBasicParsing
Invoke-WebRequest 'http://94.184.32.84:8090/health/ready' -UseBasicParsing
Invoke-WebRequest 'http://94.184.32.84:8090/openapi/v1.json' -UseBasicParsing -OutFile '.\test-openapi.json'
```

از Swagger موجود سایت و دو حساب مجازِ آزمایشی استفاده کنید؛ token را در فایل یا command history ذخیره نکنید. سلامت عمومی جای پذیرش business flow نیست. اگر IIS upload را زودتر رد کرد، `requestFiltering/requestLimits/maxAllowedContentLength` و لایهٔ proxy را بررسی کنید؛ سقف ۱۰ MiB برنامه به‌علاوهٔ multipart باید عبور کند و محدودیت بی‌حد تنظیم نشود.

## پذیرش اجباری مالک روی سرور Test

- Global Inbox معلم → همان Summary در Personal موفق؛ detail/history/file/Hub سازمانی همچنان Context صحیح بخواهد؛ پس از انتخاب سازمان صحیح همان نشست بدون stale cache موفق شود. حساب نامرتبط و نقش مشابه در سازمان دیگر 404 بگیرند.
- TXT/PNG/PDF واقعی → upload → metadata → download؛ SHA-256 بایت‌ها برابر، Content-Type/Length/Disposition صحیح؛ Range 206 و بازهٔ نامعتبر 416. خطای ProblemDetails را به‌عنوان فایل ذخیره نکنید.
- فایل خود کاربر و کاربر دیگر، scope سازمان دیگر، URL قبلی پس از revoke، فایل غیرآماده، MIME ناسازگار، ۱۰ MiB و یک بایت بیشتر بررسی شوند. حذف فایل مجاز و نسخهٔ قدیمی 412؛ فایل رسمی/مدرک ایمنی `FILE_IN_USE` دارد.
- Assignment: Attempt → فایل → draft → submit → reload → دانلود مدرس مجاز؛ revoke دسترسی را قطع کند.
- Exam: Attempt/lease → فایل → revision → reload → finalize؛ lease/نسخه/deadline/مالکیت قابل دورزدن نباشد.
- Messenger با Internal فعلی: media باید 422 PendingReview بماند؛ هیچ پیام/attachment/delivery/download جدید برای گیرنده ایجاد نشود؛ retry همان reference، بدون مصرف یا پیام تکراری. این تست محافظ است و **Happy Path ارسال رسانه نیست**.
- B01 خواندن MembershipVersion و Grant به Manager/تفویض subset؛ B02 lookup همان ClientOperationId پس از lost response؛ OfficialSpaces dry-run/repair فقط بعد تأیید طرح؛ فعالیت منتشرشده در هر دو مقصد و بدون افشای Draft.
- Manager همان سازمان، مدیر دیگر را مدیریت کند؛ حذف/پایان آخرین مدیر 409؛ cross-org و stale version رد؛ retry قصد یکسان اثر تکراری نداشته باشد.
- کلاس create/retry، ظرفیت و concurrent enrollment، sent invitations، typed activeLearners، هویت امن Guardian/Invitation، draft discovery و privacy نمرهٔ released/correction بررسی شوند.
- Quote سروری Preview است و redirect را پرداخت موفق فرض نکنید. Push واقعی فقط با دستگاه و subscription/HTTPS معتبر پذیرش می‌شود. AI در این مرحله خاموش بماند.

شواهد ارسالی مالک فقط شامل نسخهٔ بسته، status/code، زمان، correlationId و نتیجهٔ سناریو باشد؛ phone/token/OTP/بدنهٔ خصوصی/connection string ارسال نشود. زمان، actor آزمایشی و Context هر شکست را مشخص کنید. موفقیت محلی، موفقیت IIS یا شبکهٔ عمومی اعلام نمی‌شود.

## Gate بعدی

بستهٔ Production جدید پس از پذیرش واقعی Test و تعیین تکلیف مانع رسانه تهیه می‌شود. Production هرگز Mock Authentication نگیرد. تصمیم‌های Support/Delete/retention، دسترسی تفصیلی پاسخ آزمون/والد، pricing جدید و Dashboard SLA جدا هستند. وجود این موارد مجوز ساخت API فرضی یا bypass امنیت نیست.
