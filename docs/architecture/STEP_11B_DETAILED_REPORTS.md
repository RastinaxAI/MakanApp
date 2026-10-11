# STEP 11B — گزارش‌های تفصیلی نقش‌محور

این مرحله APIهای خواندنی، فیلترپذیر و صفحه‌بندی‌شده را برای Student، Parent، Teacher و Manager اضافه می‌کند. گزارش‌ها در لحظه درخواست از داده رسمی SQL Server ساخته می‌شوند و جدول، cache، materialized view یا background job جدیدی ندارند. داشبوردهای STEP 11A بدون تغییر باقی مانده‌اند؛ داشبورد برای نمای سریع و bounded است، اما گزارش تفصیلی برای مرور، drill-down، فیلتر و pagination استفاده می‌شود.

## معماری و dependency flow

```text
GET /api/v1/reports/...
-> ReportsController
-> DetailedReportService
-> IAccessContextResolver
-> IDetailedReportReadService
-> EfDetailedReportReadService
-> SQL Server projection / aggregate
-> audience-specific DTO
-> HTTP 200 / ProblemDetails
```

- `ReportsController` فقط ورودی HTTP، نشست احراز‌شده و query string را به Application می‌دهد.
- `DetailedReportService` نقش فعال، child context، pagination، بازه UTC و allowlist مرتب‌سازی را اعتبارسنجی می‌کند.
- `IDetailedReportReadService` مرز Onion برای read model است.
- `EfDetailedReportReadService` محدودیت tenant/resource را داخل query اعمال می‌کند و از `Where`، `OrderBy`، `Count`، `Skip`، `Take`، `GroupBy` و `Select` سمت SQL استفاده می‌کند.
- Domain تغییر نکرده است؛ این قابلیت invariant تازه‌ای برای نوشتن داده ایجاد نمی‌کند.

## قرارداد pagination و زمان

فهرست‌های بزرگ پاسخ زیر را دارند:

```json
{
  "dataAsOfUtc": "2026-10-11T09:30:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

- پیش‌فرض `pageNumber=1` و `pageSize=20` است و سقف `pageSize` برابر ۱۰۰ است.
- صفحه نامعتبر با `REPORT_PAGINATION_INVALID` رد می‌شود.
- `fromUtc` و `toUtc` اختیاری، inclusive و UTC هستند؛ `fromUtc > toUtc` یا مقدار غیر UTC با `REPORT_DATE_RANGE_INVALID` رد می‌شود.
- `dataAsOfUtc` یک بار در Application از ساعت سرور گرفته می‌شود. این مقدار snapshot تراکنشی میان چند query را تضمین نمی‌کند.
- timezone سازمان هنوز در مدل authoritative وجود ندارد؛ بنابراین این مرحله مفهوم روز محلی را حدس نمی‌زند.
- صفحه معتبر بدون نتیجه `200` با `totalCount=0` و `items=[]` می‌دهد.
- گزارش‌های attendance همین metadata را همراه `summary` شامل `eligibleCount`، `recordedCount`، شمارنده وضعیت‌های رسمی و `notRecordedCount` برمی‌گردانند؛ summary روی کل مجموعه فیلترشده محاسبه می‌شود، نه فقط صفحه جاری.

## Endpointها

همه مسیرها Bearer session و workspace سازمانی با نقش فعال متناظر می‌خواهند.

### Student

| Method | Route | داده |
|---|---|---|
| GET | `/api/v1/reports/student/attendance` | sessionهای پایان‌یافته، وضعیت رسمی یا `NotRecorded` |
| GET | `/api/v1/reports/student/assignments` | تکلیف منتشرشده/بسته، آخرین submission و نتیجه فقط پس از release |
| GET | `/api/v1/reports/student/exams` | metadata امن نسخه آزمون، attempt و نتیجه فقط پس از release |
| GET | `/api/v1/reports/student/grades` | نمره‌های رسمی Assignment/Exam همراه max score و درصد نرمال‌شده |

Student از `User.PersonId -> OrganizationPerson` در همان سازمان resolve می‌شود. `classId` فقط selector روی داده خود فراگیر است و مجوز تازه‌ای ایجاد نمی‌کند.

### Parent

| Method | Route | داده |
|---|---|---|
| GET | `/api/v1/reports/parent/attendance` | حضور و غیاب فرزند انتخاب‌شده |
| GET | `/api/v1/reports/parent/assignments` | وضعیت تکلیف و فقط `GuardianVisibleFeedback` منتشرشده |
| GET | `/api/v1/reports/parent/exams` | metadata امن آزمون و فقط نتیجه/feedback مجاز منتشرشده |

Parent باید پیش‌تر child context را با API Guardian انتخاب کرده باشد. `GuardianAccessContextResolver` رابطه فعال را در هر درخواست دوباره بررسی می‌کند. نبود child انتخاب‌شده `CHILD_CONTEXT_NOT_FOUND` و رابطه revoke/end شده `CHILD_CONTEXT_NOT_ALLOWED` می‌دهد. پاسخ شامل action، پیام خصوصی، session credential، پاسخ آزمون یا یادداشت خصوصی ارزیاب نیست.

### Teacher

| Method | Route | داده |
|---|---|---|
| GET | `/api/v1/reports/teacher/classes/{classId}/attendance` | ردیف هر learner/session و تفکیک `NotRecorded` |
| GET | `/api/v1/reports/teacher/classes/{classId}/assignments` | recipient، آخرین submission و وضعیت grading |
| GET | `/api/v1/reports/teacher/classes/{classId}/exams` | attempt و وضعیت grading بدون محتوای پاسخ |
| GET | `/api/v1/reports/teacher/classes/{classId}/learners/{learnerId}` | خلاصه تجمیعی حضور، تکلیف و آزمون learner همان کلاس |

هر درخواست به `TeacherAssignment` فعال برای همان membership، سازمان و کلاس نیاز دارد. کلاس یا learner حدس‌زده‌شده خارج از محدوده با `REPORT_RESOURCE_NOT_FOUND` مخفی می‌شود.

### Manager

| Method | Route | داده |
|---|---|---|
| GET | `/api/v1/reports/manager/enrollments` | ثبت‌نام‌ها با فیلتر کلاس، درس، دوره و وضعیت |
| GET | `/api/v1/reports/manager/attendance` | aggregate حضور بر اساس کلاس |
| GET | `/api/v1/reports/manager/assessments` | aggregate Assignment/Exam و backlog ارزیابی |
| GET | `/api/v1/reports/manager/classes/{classId}` | عملکرد کلاس با درصدهای نرمال‌شده جداگانه |
| GET | `/api/v1/reports/manager/teacher-workload` | کلاس، session، حضور ناقص و backlog ارزیابی هر معلم |

Manager فقط داده همان سازمان workspace را می‌بیند. workload هیچ معیار chat/message ندارد. عملکرد Assignment و Exam جدا نگه داشته می‌شود و میانگین raw score، GPA یا سقف سراسری ۲۰ ساخته نمی‌شود؛ درصد هر نتیجه از `score / version.MaxScore` محاسبه می‌شود.

## فیلتر و مرتب‌سازی

فیلترهای مشترک: `pageNumber`، `pageSize`، `fromUtc`، `toUtc`، `classId` و `sort`. گزارش enrollment/assessment/workload فیلترهای `courseId` و `academicPeriodId` نیز دارند. enrollment فیلتر `status` و assessment فیلتر `kind=Assignment|Exam` دارد.

مرتب‌سازی arbitrary یا dynamic SQL وجود ندارد. مقادیر مجاز:

- Attendance: `occurredDesc`، `occurredAsc`
- Assignment: `dueDesc`، `dueAsc`، `titleAsc`
- Exam: `availableDesc`، `availableAsc`، `titleAsc`
- Grade: `releasedDesc`، `releasedAsc`
- Enrollment: `enrolledDesc`، `enrolledAsc`، `learnerAsc`
- Manager attendance: `classTitleAsc`، `eligibleDesc`، `absenceRateDesc`
- Manager assessment: `deadlineDesc`، `deadlineAsc`، `titleAsc`، `pendingDesc`
- Teacher workload: `teacherAsc`، `classesDesc`، `pendingDesc`

مقدار خارج allowlist با `REPORT_SORT_INVALID` رد می‌شود. Guidهای فیلتر selector هستند و هرگز جای authorization را نمی‌گیرند.

### نکات دقیق مصرف فیلترها؛ بازبینی 2026-10-12

- `classId` در route گزارش Teacher منبع انتخاب کلاس است؛ property هم‌نام query آن را تغییر نمی‌دهد. دو endpoint خلاصه learner معلم و عملکرد کلاس مدیر query صفحه‌بندی یا بازه زمانی ندارند.
- fromUtc/toUtc در Attendance روی StartUtc جلسه، در Assignment روی DueAtUtc، در Exam Student/Parent/Teacher روی AvailableFromUtc، در نمره Student روی ReleasedAtUtc و در enrollment روی EnrolledAtUtc اعمال می‌شود. Manager assessments برای آزمون از AvailableUntilUtc و برای تکلیف از DueAtUtc استفاده می‌کند.
- در teacher-workload، بازه زمانی فقط completedSessionCount را محدود می‌کند؛ شمارنده کلاس، حضور ناقص و backlog نمره به همین بازه محدود نیستند. attendanceMissingCount تعداد **جلسه با ثبت ناقص** است، نه تعداد فراگیران غایب. scope کلاس/درس/دوره روی شمارنده‌ها اعمال می‌شود ولی معلم دارای count صفر الزاماً از فهرست حذف نمی‌شود.
- Dashboard والد بدون child context پاسخ خالی200 دارد، اما report والد در همین حالت404 با CHILD_CONTEXT_NOT_FOUND می‌دهد. فیلتر list خارج scope می‌تواند پاسخ خالی200 بدهد؛ 404 پنهان‌سازی منبع را برای تمام فیلترها تعمیم ندهید.
- Student/Parent exam/assignment report شناسه assessment و version را می‌دهد ولی examAttemptId یا submissionAttemptId ندارد؛ summary نتیجه را مستقیم نمایش دهید و برای drill-down شناسه فرضی نسازید. Export یا بازسازی پاسخ سؤال‌ها از این DTO مجاز/ممکن تلقی نشود.

## privacy و semantics داده

- نبود رکورد Attendance برابر `NotRecorded` است، نه `Absent`.
- Student/Parent فقط Evaluation دارای `GradeRelease` و Exam grade دارای `ExamGradeRelease` را به‌صورت score مشاهده می‌کنند.
- Parent فقط `GuardianVisibleFeedback` را می‌گیرد؛ Student فقط `LearnerFeedback` را می‌گیرد.
- DTO آزمون هیچ prompt، option، answer key، final answer یا evaluator private note ندارد.
- Teacher گزارش grading را می‌بیند، اما متن پاسخ آزمون و یادداشت خصوصی در قرارداد گزارش وجود ندارد.
- Manager فقط aggregate و metadata عملیاتی می‌گیرد و محتوای submission، answer، feedback خصوصی یا پیام را دریافت نمی‌کند.

## کارایی، ایندکس و migration

Queryها `AsNoTracking` هستند و entity graph را با `Include` بارگذاری نمی‌کنند. filter/count/order/pagination/projection در SQL Server اجرا می‌شود؛ materialize-before-filter و loop دارای N+1 وجود ندارد. workload معلم از correlated SQL subqueryهای محدودشده به tenant/assignment استفاده می‌کند.

مدل و schema در این مرحله تغییر نکرده‌اند و migration ساخته نشده است. ایندکس جدید نیز بدون evidence از execution plan اضافه نشده است. پس از مشاهده workload واقعی و execution plan می‌توان درباره indexهای covering تصمیم گرفت؛ حدس زدن index در این مرحله انجام نشده است.

## خطاهای پایدار

| Code | HTTP | معنا |
|---|---:|---|
| `REPORT_PAGINATION_INVALID` | 400 | شماره/اندازه صفحه نامعتبر |
| `REPORT_DATE_RANGE_INVALID` | 400 | بازه یا Kind زمانی نامعتبر |
| `REPORT_SORT_INVALID` | 400 | sort خارج allowlist |
| `REPORT_RESOURCE_NOT_FOUND` | 404 | منبع وجود ندارد یا برای جلوگیری از افشا قابل مشاهده نیست |
| `WORKSPACE_NOT_ALLOWED` | 403 | workspace یا نقش فعال متناظر نیست |
| `CHILD_CONTEXT_NOT_FOUND` | 404 | Parent فرزند انتخاب نکرده است |
| `CHILD_CONTEXT_NOT_ALLOWED` | 403 | رابطه فرزند انتخاب‌شده دیگر معتبر نیست |

## خارج از محدوده

Export فایل/PDF/Excel، گزارش زمان‌بندی‌شده، cache/Redis، materialized view، AI/Copilot/RAG، Commerce، پیام‌رسانی، frontend و migration در STEP 11B پیاده‌سازی نشده‌اند.
