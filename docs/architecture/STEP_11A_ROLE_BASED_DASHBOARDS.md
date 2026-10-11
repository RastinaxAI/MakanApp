# STEP 11A — داشبوردهای عملیاتی مبتنی بر نقش

در این مرحله چهار read model مستقل برای Student، Parent، Teacher و Manager پیاده‌سازی شده است. این قابلیت entity دامنه یا جدول dashboard ایجاد نمی‌کند؛ داده در زمان درخواست از وضعیت رسمی ماژول‌های Identity، Organization، Guardian، Academic و Assessment خوانده می‌شود.

## معماری

```text
GET /api/v1/dashboards/{audience}
-> DashboardsController
-> DashboardService
-> IAccessContextResolver
-> IDashboardReadService
-> EfDashboardReadService
-> SQL Server projections
-> audience-specific DTO
```

- `DashboardsController` فقط نشست احرازشده را به use case می‌دهد و شناسه سازمان، learner یا role را از Client نمی‌پذیرد.
- `DashboardService` نقش فعال و فضای سازمانی را از `AccessContext` معتبر می‌کند و `DataAsOfUtc` را یک بار از `TimeProvider` می‌گیرد.
- `EfDashboardReadService` projectionهای `AsNoTracking`، aggregationهای SQL-side و collectionهای محدودشده را اجرا می‌کند.
- Domain تغییری نکرده است، چون dashboard invariant تازه‌ای ایجاد نمی‌کند و فقط وضعیت معتبر موجود را می‌خواند.

## Endpointها و context مجاز

تمام مسیرها به Bearer session معتبر نیاز دارند.

| Method | Route | نقش فعال لازم | Scope |
|---|---|---|---|
| `GET` | `/api/v1/dashboards/student` | `Student` | learner متصل به `User.PersonId` در سازمان فعال |
| `GET` | `/api/v1/dashboards/parent` | `Parent` | فرزند انتخاب‌شده در workspace و `GuardianRelation` فعال |
| `GET` | `/api/v1/dashboards/teacher` | `Teacher` | membership فعال و `TeacherAssignment`های همان سازمان |
| `GET` | `/api/v1/dashboards/manager` | `Manager` | فقط سازمان workspace فعال |

فراخوانی مسیر یک نقش از workspace نقش دیگر با `WORKSPACE_NOT_ALLOWED` رد می‌شود. چون OrganizationId و LearnerId در route یا query وجود ندارند، Client نمی‌تواند با تغییر Guid محدوده دسترسی را جابه‌جا کند. Parent از همان child selector موجود استفاده می‌کند؛ اگر رابطه انتخاب‌شده بعداً revoke یا end شود، `GuardianAccessContextResolver` درخواست بعدی را پیش از اجرای query رد می‌کند.

## قراردادهای نقش‌محور

### Student

- نزدیک‌ترین Session برنامه‌ریزی‌شده در کلاس دارای Enrollment فعال
- نزدیک‌ترین تکلیف منتشرشده و actionable و تعداد تکالیف pending
- آخرین نمره‌ها و feedbackهای Assignment فقط با `GradeRelease`
- metadata امن آزمون، وضعیت attempt و نتیجه فقط با `ExamGradeRelease`
- خلاصه Attendance رسمی با تفکیک `Absent` از `NotRecorded`

### Parent

- مشخصات حداقلی فرزند انتخاب‌شده
- برنامه، وضعیت تکلیف، Attendance رسمی و نتایج منتشرشده همان فرزند
- فقط `GuardianVisibleFeedback` برای Assignment منتشر می‌شود
- در نبود فرزند انتخاب‌شده، پاسخ موفق با `selectedLearner: null`، آرایه‌های خالی و count صفر برمی‌گردد
- هیچ action برای submit/start/finalize، پیام خصوصی یا history حساب فرزند در DTO وجود ندارد

### Teacher

- کلاس‌های دارای `TeacherAssignment` فعال
- Sessionهای آینده همان کلاس‌ها
- Sessionهای پایان‌یافته‌ای که تعداد Attendance رسمی آنها از Enrollment فعال کمتر است
- شمارنده Submissionهای در انتظار Evaluation یا دارای Evaluation پیش‌نویس
- شمارنده Exam Attemptهای توصیفی در انتظار grading/manual review/release

یک Teacher حتی در همان سازمان داده کلاس بدون assignment را دریافت نمی‌کند. Teacher عضو دو مدرسه نیز فقط داده workspace فعال را می‌بیند.

### Manager

- تعداد کلاس فعال، learner ثبت‌نام‌شده متمایز و membershipهای Teacher فعال
- checklist آمادگی سازمان، دوره، درس، معلم، learner و کلاس
- Sessionهای آینده و Sessionهای فاقد meeting link
- Attendance ناقص، دعوت‌های pending و backlogهای Assignment/Exam

Manager فقط summary عملیاتی می‌گیرد؛ متن پیام، محتوای پاسخ آزمون، کلید پاسخ، یادداشت خصوصی ارزیاب و تاریخچه Copilot در این قرارداد وجود ندارد.

## تازگی و semantics زمان

هر پاسخ دارای `DataAsOfUtc` است. این زمان لحظه‌ای است که Application شروع خواندن dashboard را ثبت کرده است؛ تضمین snapshot ریاضی واحد میان تمام queryها نیست. مدل Organization هنوز timezone معتبر ندارد، بنابراین مفهوم «امروز محلی» ساخته نشده و فیلترهای این مرحله بر windowهای UTC مانند `StartUtc >= DataAsOfUtc` متکی‌اند.

## قواعد visibility

- Assignment فقط از نسخه منتشرشده و Recipient معتبر خوانده می‌شود. وضعیت `Closed` نیز برای تاریخچه قابل مشاهده باقی می‌ماند.
- Evaluation بدون `GradeRelease` در Student/Parent نمایش داده نمی‌شود.
- Exam DTO هیچ `QuestionVersion.Prompt`، option یا answer key ندارد.
- نمره Exam فقط پس از `ExamGradeRelease` مقدار می‌گیرد؛ Draft و ReadyForRelease امتیاز را افشا نمی‌کنند.
- Attendance فقط از `academic.Attendance` محاسبه می‌شود. نبود رکورد برابر Absent نیست و در `NotRecordedCount` قرار می‌گیرد.

## شکل query و کارایی

- همه listها با `Take` محدود شده‌اند: Session و Exam حداکثر ۱۰، feedback حداکثر ۵ و کلاس Teacher حداکثر ۲۰.
- queryها entity graph را `Include` نمی‌کنند و DTOها مستقیماً در SQL projection می‌شوند.
- countها و `Any`ها در SQL Server اجرا می‌شوند؛ loop دارای query و الگوی N+1 وجود ندارد.
- readها `AsNoTracking` هستند و transaction نوشتن یا cache جدیدی ندارند.
- در این مرحله index یا schema تازه‌ای برای correctness لازم نبود؛ بنابراین migration جدید ایجاد نشده است.

## نمونه پاسخ‌ها

Student:

```json
{
  "dataAsOfUtc": "2026-10-11T09:30:00Z",
  "organizationId": "00000000-0000-0000-0000-000000000001",
  "audience": "Student",
  "nextSession": null,
  "nearestAssignmentDeadline": null,
  "recentReleasedFeedback": [],
  "upcomingExams": [],
  "attendanceSummary": {
    "eligibleSessionCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "pendingAssignmentCount": 0
}
```

Parent بدون child context:

```json
{
  "dataAsOfUtc": "2026-10-11T09:30:00Z",
  "organizationId": "00000000-0000-0000-0000-000000000001",
  "audience": "Parent",
  "selectedLearner": null,
  "upcomingSessions": [],
  "assignments": [],
  "releasedAssignmentResults": [],
  "exams": [],
  "attendanceSummary": {
    "eligibleSessionCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "attentionItemCount": 0
}
```

## خارج از محدوده

- AI، Copilot، RAG، insight و recommendation در این مرحله وجود ندارند.
- widgetهای Plan، Quota، Subscription و ظرفیت Manager تا STEP 13 Commerce به تعویق افتاده‌اند.
- گزارش تفصیلی، export، PDF/Excel، job زمان‌بندی‌شده و materialized dashboard به STEP 11B یا مراحل بعدی تعلق دارند.
- Redis و cache برای این read modelها اضافه نشده است؛ correctness مجوز بر cache مقدم است.

## پوشش تست

`DashboardServiceTests` نقش فعال، workspace سازمانی، timestamp سرور و عدم serialization فیلدهای حساس را بررسی می‌کند. `DashboardEndpointsTests` با SQL Server واقعی، migrationهای موجود و HTTP host سناریوهای Student/Parent/Teacher/Manager، حالت خالی، child selection/revocation، visibility نمره منتشرشده، Attendance رسمی، grading backlog و جداسازی سازمان/کلاس را پوشش می‌دهد.
