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

| Method | Route | نقش فعال لازم | Scope | پاسخ 200 |
|---|---|---|---|---|
| `GET` | `/api/v1/dashboards/student` | `Student` | learner متصل به `User.PersonId` در سازمان فعال | `StudentDashboardDto` |
| `GET` | `/api/v1/dashboards/parent` | `Parent` | فرزند انتخاب‌شده در workspace و `GuardianRelation` فعال | `ParentDashboardDto` |
| `GET` | `/api/v1/dashboards/teacher` | `Teacher` | membership فعال و `TeacherAssignment`های همان سازمان | `TeacherDashboardDto` |
| `GET` | `/api/v1/dashboards/manager` | `Manager` | فقط سازمان workspace فعال | `ManagerDashboardDto` |

فراخوانی مسیر یک نقش از workspace نقش دیگر با `WORKSPACE_NOT_ALLOWED` رد می‌شود. چون OrganizationId و LearnerId در route یا query وجود ندارند، Client نمی‌تواند با تغییر Guid محدوده دسترسی را جابه‌جا کند. Parent از همان child selector موجود استفاده می‌کند؛ اگر رابطه انتخاب‌شده بعداً revoke یا end شود، `GuardianAccessContextResolver` درخواست بعدی را پیش از اجرای query رد می‌کند.

درخواست‌ها body یا selector سازمان/learner ندارند. افزودن query stringهایی مانند `organizationId`، `learnerId`، `classId` یا `role` منبع مجوز نیست و context معتبر session را تغییر نمی‌دهد. انتخاب membership متعلق به کاربر دیگر با `404 / WORKSPACE_NOT_FOUND` رد می‌شود؛ نقش یا workspace نامعتبر `403` و session غایب/نامعتبر `401` می‌دهد. Parent رابطه را در هر درخواست دوباره بررسی می‌کند: `Active`، بدون `EndedAtUtc`، شروع اعتبار گذشته و learner/سازمان فعال. مدل فعلی تاریخ انقضای مستقل برای GuardianRelation ندارد؛ رفتار expiry جدیدی ساخته نشده است.

## قراردادهای نقش‌محور

### Student

- نزدیک‌ترین Session برنامه‌ریزی‌شده در کلاس دارای Enrollment فعال
- نزدیک‌ترین تکلیف منتشرشده و actionable و تعداد تکالیف pending
- آخرین نمره‌ها و feedbackهای Assignment فقط با `GradeRelease`
- metadata امن آزمون، وضعیت attempt و نتیجه فقط با `ExamGradeRelease`
- خلاصه Attendance رسمی با تفکیک `Absent` از `NotRecorded`

در بازبینی `2026-10-12`، actionable دقیقاً به Assignment با وضعیت Published، موعد آینده، بدون submission و بدون grade محدود است. `Closed` در تاریخچه باقی می‌ماند ولی در nearest deadline و pending count نیست؛ allowLateSubmission به‌تنهایی تکلیف گذشته را به این شمارنده اضافه نمی‌کند.

### Parent

- مشخصات حداقلی فرزند انتخاب‌شده
- برنامه، وضعیت تکلیف، Attendance رسمی و نتایج منتشرشده همان فرزند
- فقط `GuardianVisibleFeedback` برای Assignment منتشر می‌شود
- در نبود فرزند انتخاب‌شده، پاسخ موفق با `selectedLearner: null`، آرایه‌های خالی و count صفر برمی‌گردد
- هیچ action برای submit/start/finalize، پیام خصوصی یا history حساب فرزند در DTO وجود ندارد
- فرزند مجاز بدون Enrollment نیز با مشخصات حداقلی، آرایه خالی و شمارنده صفر برمی‌گردد؛ نبود Enrollment خطای500 نیست
- `AttentionItemCount` فقط تکلیف Published، موعد آینده و بدون submission/grade را می‌شمارد؛ Closed ممکن است در تاریخچه بماند ولی قابل‌اقدام نیست

### Teacher

- کلاس‌های دارای `TeacherAssignment` فعال
- Sessionهای آینده همان کلاس‌ها
- Sessionهای پایان‌یافته‌ای که تعداد Attendance رسمی آنها از Enrollment فعال کمتر است
- شمارنده Submissionهای در انتظار Evaluation یا دارای Evaluation پیش‌نویس
- شمارنده همه Exam Attemptهای نهایی‌شده در انتظار grading/manual review/release؛ آزمون objective نیز ممکن است در انتظار شروع grading یا انتشار باشد

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
- metadata، وضعیت attempt، نمره و سقف آن بر اساس همان `ExamVersionId` join می‌شوند؛ انتشار نسخه جدید با سقف10، نمره18/20 نسخه قبلی را به18/10 تبدیل نمی‌کند. فهرست bounded می‌تواند هم نسخه جاریِ هنوز باز و هم نسخه تاریخیِ دارای نتیجه منتشرشده را داشته باشد؛ کلید هر ردیف `ExamVersionId` است، نه فقط `ExamId`. نسخه تاریخی بدون release وارد این فهرست نمی‌شود.
- Attendance فقط از `academic.Attendance` محاسبه می‌شود. نبود رکورد برابر Absent نیست و در `NotRecordedCount` قرار می‌گیرد.
- در backlog عملیاتی، Expected و Recorded هر دو بر Enrollmentهای فعال همان کلاس محاسبه می‌شوند؛ حضور ثبت‌شده برای Enrollment پایان‌یافته، ثبت‌نشدن حضور learner فعال دیگری را پنهان نمی‌کند. این تعریف current-roster است، نه بازسازی roster تاریخی زمان جلسه.

## شکل query و کارایی

- همه listها با `Take` محدود شده‌اند: Session و Exam حداکثر ۱۰، feedback حداکثر ۵ و کلاس Teacher حداکثر ۲۰.
- queryها entity graph را `Include` نمی‌کنند و DTOها مستقیماً در SQL projection می‌شوند.
- countها و `Any`ها در SQL Server اجرا می‌شوند؛ loop دارای query و الگوی N+1 وجود ندارد.
- خلاصه Attendance با یک count جلسات واجد شرایط و یک aggregate شرطی SQL برای همه وضعیت‌ها خوانده می‌شود؛ برای هر وضعیت round-trip جدا ایجاد نمی‌شود.
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
- گزارش تفصیلی اکنون در [STEP 11B](STEP_11B_DETAILED_REPORTS.md) پیاده‌سازی شده است؛ export، PDF/Excel، job زمان‌بندی‌شده و materialized dashboard همچنان خارج از قابلیت فعلی‌اند.
- Redis و cache برای این read modelها اضافه نشده است؛ correctness مجوز بر cache مقدم است.

## پوشش تست

`DashboardServiceTests` نقش فعال، workspace سازمانی، timestamp سرور و عدم serialization فیلدهای حساس را بررسی می‌کند. `DashboardEndpointsTests` با SQL Server واقعی، migrationهای موجود و HTTP host سناریوهای Student/Parent/Teacher/Manager، حالت خالی، child selection/revocation، visibility نمره منتشرشده، Attendance رسمی، grading backlog و جداسازی سازمان/کلاس را پوشش می‌دهد.

## تکمیل و بازبینی 2026-10-15

پیاده‌سازی اصلی از commit `5ad9130` موجود بود؛ این بازبینی آن را تکمیل می‌کند و abstraction، Controller، route یا DTO موازی ایجاد نمی‌کند. چهار regression در هفت case ابتدا با شکست بازتولید شدند: سقف نمره نسخه تاریخی، شمارنده تکلیف Closed والد، حذف backlog آزمون objective و پنهان‌شدن نقص حضور به‌خاطر Enrollment پایان‌یافته.

تست‌های تکمیلی در `DashboardEndpointsTests.Hardening.cs` هستند و helperها/fixture موجود را با partial class دوباره استفاده می‌کنند. علاوه بر آن هفت case، دست‌کاری selector برای چهار نقش، child بدون Enrollment و End شدن رابطه، و هر چهار وضعیت حضور رسمی به‌همراه NotRecorded بررسی می‌شوند. تست Unit جدید، DTOهای پُر هر چهار نقش و اشیای nested را serialize می‌کند تا نبود فیلد خصوصی فقط روی آرایه خالی سنجیده نشود.

Schema، table، index، rowversion، transaction نوشتن، migration یا package جدید وجود ندارد. تست‌ها migrationهای موجود را فقط روی دیتابیس اختصاصی fixture اجرا می‌کنند؛ دیتابیس عادی توسعه و production تغییر نمی‌کنند. قابلیت‌های AI، Commerce، Notification، Backoffice و export که ممکن است در بخش‌های دیگر مخزن موجود باشند، dependency این داشبوردها نیستند و در این مرحله توسعه یا حذف نشده‌اند.
