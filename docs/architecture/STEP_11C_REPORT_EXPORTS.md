# STEP 11C — خروجی غیرهم‌زمان گزارش و چرخه دانلود

این مرحله روی read modelهای مجاز STEP 11B یک چرخه پایدار برای ساخت و دریافت خروجی گزارش ایجاد می‌کند. درخواست HTTP فقط اعتبارسنجی، authorization و ثبت job را انجام می‌دهد و با `202 Accepted` برمی‌گردد؛ تولید فایل در `ReportExportDispatcher` انجام می‌شود.

## تصمیم فرمت

اسناد محصول فرمت الزامی PDF/Excel/CSV تعیین نکرده‌اند. در این مرحله فقط `Csv` پیاده‌سازی شده است چون:

- برای گزارش‌های جدولی فعلی کافی و deterministic است؛
- وابستگی third-party تازه ندارد؛
- به‌صورت UTF-8 همراه BOM تولید می‌شود و متن فارسی را حفظ می‌کند؛
- تولید آن صفحه‌به‌صفحه و buffered است و کل داده را در حافظه جمع نمی‌کند.

PDF و Excel تا زمان وجود نیاز واقعی محصول خارج محدوده‌اند.

## جریان اجرا

```text
POST /api/v1/reports/exports
-> ReportsController
-> ReportExportService
-> DetailedReportService.PrepareAsync
-> IAccessContextResolver + STEP 11B read model
-> reports.ReportExportJobs (Queued)
-> 202 Accepted

ReportExportDispatcher
-> EfReportExportJobStore.TryClaimNextAsync
-> SQL Server UPDLOCK / READPAST / ROWLOCK
-> DetailedReportService.ReadPageAsync
-> EfDetailedReportReadService
-> CsvReportExporter
-> ReportExportOutputStore
-> storage.FileAssets + IFileStorage
-> ReportExportJob.Completed

GET status/download
-> current AccessContext revalidation
-> current GuardianRelation / TeacherAssignment where relevant
-> authorized private stream
```

## نوع‌های گزارش پشتیبانی‌شده

`ReportExportType` یک enum بسته است و ورودی SQL، نام جدول یا expression نمی‌پذیرد:

- Student: `StudentAttendance`، `StudentAssignments`، `StudentExams`، `StudentGrades`
- Parent: `ParentChildAttendance`، `ParentChildAssignments`، `ParentChildExams`
- Teacher: `TeacherClassAttendance`، `TeacherAssignmentGrading`، `TeacherExamGrading`
- Manager: `ManagerEnrollments`، `ManagerAttendance`، `ManagerAssessments`، `ManagerTeacherWorkload`

دو read model تک‌منبعی `TeacherLearnerOverview` و `ManagerClassPerformance` در این مرحله export مستقل ندارند. فیلترها به ستون‌های typed در job تبدیل می‌شوند و query string یا metadata نوع CLR ذخیره نمی‌شود.

## API

### ایجاد

```http
POST /api/v1/reports/exports
Authorization: Bearer <session-token>
Content-Type: application/json

{
  "reportType": "StudentGrades",
  "format": "Csv",
  "filters": {
    "fromUtc": "2026-09-01T00:00:00Z",
    "toUtc": "2026-10-01T00:00:00Z",
    "classId": null,
    "sort": "releasedDesc"
  }
}
```

پاسخ `202` شامل `jobId`، status، زمان snapshot و تعداد سطر مورد انتظار است. پیش از ثبت، نقش فعال، workspace، child context یا TeacherAssignment، sort/date/filter و سقف سطر کنترل می‌شوند.

### وضعیت

```http
GET /api/v1/reports/exports/{jobId}
```

فقط مالک job در همان محدوده سازمانی، membership، نقش و subject فعلی می‌تواند وضعیت را بخواند. job کاربر دیگر با `REPORT_EXPORT_NOT_FOUND` پنهان می‌شود. `FailureCode` فقط یک کد امن است و exception یا stack trace ذخیره/منتشر نمی‌شود.

### دانلود

```http
GET /api/v1/reports/exports/{jobId}/download
```

فقط job با status برابر `Completed` دانلود می‌شود. در `Queued`، `Processing` یا `Failed` خطای `REPORT_EXPORT_NOT_READY` برمی‌گردد. مسیر فیزیکی storage هرگز در response قرار نمی‌گیرد.

## authorization و revocation

- ایجاد job فقط پس از resolve شدن `AccessContext` معتبر انجام می‌شود.
- `OrganizationId`، role، membership، child و class از context و policy سرور ثبت می‌شوند؛ client آن‌ها را proof مجوز نمی‌کند.
- پردازش با session ایجادکننده دوباره context را resolve و با snapshot job مقایسه می‌کند.
- هر صفحه export پیش از query دوباره همین دسترسی را کنترل می‌کند.
- دانلود با session جاری دوباره authorization را ارزیابی می‌کند.
- Parent به `SubjectOrganizationPersonId` انتخاب‌شده و `GuardianRelation` فعلی محدود است؛ revoke شدن رابطه، وضعیت و دانلود بعدی را مسدود می‌کند.
- Teacher علاوه بر membership/role به `TeacherAssignment` فعال همان `ClassId` نیاز دارد.
- Student هیچ `studentId` ورودی ندارد و read model فقط شخص متناظر حساب را resolve می‌کند.
- Manager فقط tenant جاری را می‌بیند و Platform Admin محسوب نمی‌شود.

همان DTOهای audience-specific STEP 11B مبنای خروجی‌اند. بنابراین نمره منتشرنشده، private evaluator note، answer key، پاسخ آزمون، پیام خصوصی، Copilot، OTP و credential وارد CSV نمی‌شوند.

## CSV و امنیت spreadsheet

`CsvReportExporter` comma، quote و newline را طبق quoting استاندارد CSV escape می‌کند. ستون‌های متنی کنترل‌شده توسط کاربر با `ReportExportCell.IsUserControlledText` مشخص می‌شوند. اگر نخستین نویسه غیر whitespace یکی از `=`، `+`، `-` یا `@` باشد، یک apostrophe پیشوند می‌گیرد تا spreadsheet آن را formula اجرا نکند. مقدارهای عددی typed مانند نمره منفی به‌عنوان متن user-controlled پردازش نمی‌شوند.

نام فایل کاملاً سمت سرور از report type، زمان و GUID job ساخته می‌شود؛ filename یا path دلخواه client پذیرفته نمی‌شود.

## مدل job و SQL Server

جدول `reports.ReportExportJobs` توسط migration `AddAsynchronousReportExports` ساخته می‌شود. ستون‌های مهم:

- scope: `OrganizationId`، `RequestedByUserId`، `RequestedBySessionId`، `MembershipId`، `Audience` و `SubjectOrganizationPersonId`
- allowlist/filter snapshot: `ReportType`، `Format`، بازه UTC، شناسه‌های filter typed، enumهای typed و `Sort`
- lifecycle: `Status`، `AttemptCount`، زمان‌های requested/started/completed/failed، lease و next attempt
- output: `OutputFileAssetId` و `FailureCode`
- concurrency: `RowVersion`

statusها `Queued -> Processing -> Completed|Failed` هستند. retry صریحاً `Processing -> Queued -> Processing` است. transitionهای terminal برگشت‌پذیر نیستند.

index polling روی `(Status, NextAttemptAtUtc, RequestedAtUtc)` و index مالک/سازمان روی `(RequestedByUserId, OrganizationId, RequestedAtUtc)` قرار دارد. `OutputFileAssetId` یک filtered unique index و FK به `storage.FileAssets` دارد. check constraintها status، format، report type، شمارنده‌ها، بازه تاریخ و سازگاری حالت terminal را حفاظت می‌کنند.

## claim، recovery و retry

`EfReportExportJobStore` job بعدی را در transaction کوتاه با `UPDLOCK, READPAST, ROWLOCK` claim می‌کند. بنابراین دو processor در مدت lease یک job را هم‌زمان دریافت نمی‌کنند. SQL Server منبع حقیقت است و wake signal حافظه‌ای فقط latency را کم می‌کند؛ restart باعث حذف `Queued` نمی‌شود.

`Processing` با lease منقضی دوباره claim می‌شود. retry حداکثر سه attempt دارد؛ job منقضی با attempt کامل به `REPORT_GENERATION_FAILED` می‌رود. خطای authorization دائمی retry نمی‌شود و `REPORT_DATA_NO_LONGER_AUTHORIZED` ثبت می‌کند. خطای storage/غیرمنتظره تا سقف محدود retry می‌شود.

سیستم ادعای exactly-once ندارد. شناسه FileAsset برابر شناسه job و کلید storage ثبت‌شده پایدار است. اگر process بعد از ذخیره فایل و پیش از Completed از بین برود، retry فایل `Ready` موجود را reuse می‌کند؛ برای `Pending` همان رکورد/کلید repair می‌شود. به این ترتیب برای هر job خروجی‌های orphan نامحدود ساخته نمی‌شود.

## محدودیت منابع و retention

- سقف پیش‌فرض: `10000` سطر
- page داخلی: `100` سطر
- سقف فایل: `20 MiB`
- poll idle: `2` ثانیه
- lease: `120` ثانیه
- retry: سه attempt با فاصله پیش‌فرض `10` ثانیه

مقادیر در `Reports:Exports` قابل تنظیم‌اند. POST برای شمارش و authorization یک صفحه کوچک از read model را می‌خواند و گزارش بالاتر از سقف را با `REPORT_EXPORT_ROW_LIMIT_EXCEEDED` رد می‌کند. هنگام تولید نیز تغییر تعداد داده دوباره با همان سقف کنترل می‌شود.

فایل به `FileAsset` خصوصی متصل است، اما expiration/purge خودکار برای گزارش در این مرحله پیاده‌سازی نشده است. تعیین retention production و cleanup فایل‌های قدیمی یک تصمیم عملیاتی آینده است. endpoint لیست job، retry دستی، گزارش زمان‌بندی‌شده، distributed queue و scale-out coordinator نیز خارج محدوده‌اند.

## تست

Unit Testها transitionهای job، recovery و escape/Formula Injection را پوشش می‌دهند. Integration Testها روی SQL Server LocalDB migration واقعی، create/status/download، privacy فایل واقعی، scope چهار نقش، revocation والد، TeacherAssignment، tenant isolation، guessed job، claim هم‌زمان، lease recovery، failure امن و عدم بازتولید job کامل‌شده را بررسی می‌کنند.

## تنظیمات

```json
{
  "Reports": {
    "Exports": {
      "MaximumRows": 10000,
      "PageSize": 100,
      "MaximumFileSizeBytes": 20971520,
      "ClaimLeaseSeconds": 120,
      "IdlePollingSeconds": 2,
      "RetryDelaySeconds": 10,
      "MaximumAttempts": 3
    }
  }
}
```

هیچ migration در startup به‌صورت خودکار اجرا نمی‌شود و migration این مرحله نیز روی دیتابیس production اعمال نشده است.
