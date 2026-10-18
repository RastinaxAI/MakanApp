# STEP 10E — Role-specific AI Contexts

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## نتیجه

Backend عملیات Student، Parent، Teacher و Manager را با scope و داده‌ای که خودش authorize کرده به AI می‌فرستد. client فقط selectorهای لازم مانند `classId`، `studentId`، `assignmentId` و `examId` را می‌دهد؛ هیچ‌کدام مدرک مجوز نیستند.

## Builderها

- `StudentContextBuilder`: User → Person → OrganizationPerson → Enrollment فعال → Class فعال.
- `ParentChildContextBuilder`: Parent workspace → selected child که `GuardianAccessContextResolver` رابطه فعال آن را در هر درخواست دوباره بررسی می‌کند.
- `TeacherClassContextBuilder`: Teacher membership → TeacherAssignment فعال → Class همان Organization.
- `TeacherStudentContextBuilder`: مسیر قبل + Enrollment فعال learner در همان Class.
- `ManagerSchoolContextBuilder`: Manager membership فعال → Organization جاری.
- `ManagerClassContextBuilder`: Manager Organization → Class همان Organization.
- `ManagerStudentContextBuilder`: Manager Organization → Class → Enrollment فعال learner.

هر Builder چهار مسئولیت را انجام می‌دهد: authorization، فراخوانی read-model/query، minimization و mapping به DTO قرارداد AI.

## publication و score

- Assignment فقط در وضعیت `Published/Closed` و با `AssignmentVersion.PublishedAtUtc` معتبر وارد context می‌شود.
- Exam و ExamVersion هر دو باید `Published` باشند.
- Grade فقط با `GradeRelease` یا `ExamGradeRelease` و revision دارای وضعیت release ارسال می‌شود.
- هر score همراه `MaxScore` همان version ارسال می‌شود؛ نمره سراسری ۲۰ فرض نشده است.
- private note ارزیاب/معلم و answer key ارسال نمی‌شود.

## حداقل‌سازی

| داده | حد |
|---|---:|
| Subject | 10 |
| Assignment | 10 |
| Exam | 10 |
| Grade | 10 |
| Activity | 20 |
| Student reference در aggregate کلاس | 30 |
| متن آزاد | 2000 نویسه |

لیست‌ها از read-modelهای گزارش موجود ساخته می‌شوند تا publication/release semantics یک منبع واحد داشته باشد. نام/شماره تلفن، token، Contact، Messenger Direct، moderation evidence، payment و raw AI history بخشی از DTOها یا query sourceهای این مرحله نیستند.

## API عمومی Backend

- `POST /api/v1/ai/students/analyze`
- `POST /api/v1/ai/study-plan`
- `POST /api/v1/ai/homeworks/{assignmentId}/explain`
- `POST /api/v1/ai/exams/{examId}/prepare`
- `POST /api/v1/ai/parents/child-summary`
- `POST /api/v1/ai/teachers/classes/{classId}/summary`
- `POST /api/v1/ai/teachers/classes/{classId}/students/{studentId}/insight`
- `POST /api/v1/ai/teachers/classes/{classId}/activity-suggestions`
- `POST /api/v1/ai/managers/school-summary`
- `POST /api/v1/ai/managers/classes/{classId}/insight`
- `POST /api/v1/ai/managers/classes/{classId}/students/{studentId}/insight`

همه endpointها session احراز‌شده و policy اختصاصی `ai-chat` را لازم دارند. role فعال از `AccessContext` گرفته می‌شود. `PlatformOwner` به‌تنهایی نقش آموزشی نیست.

## سازگاری AI فعلی

DTOهای Backend با schemaهای Pydantic در AI commit `c80fdb90c2fe5101a7b3ff193a02d3120bf7e5f9` هم‌نام‌اند. scopeهای Teacher/Manager به‌صورت اختصاصی تعریف شده‌اند تا با `extra=forbid` فیلد null اضافی تولید نشود. Backend همچنان target امن v1 را مستقل از زمان‌بندی تیم AI با Fake AI تست می‌کند.

## تست‌ها

- contract serialization برای هر یازده request role-specific
- Student identity، active enrollment و `MaxScore`
- Parent relation revoked
- Teacher unassigned class و learner خارج از class
- Manager cross-organization class/student
- PlatformOwner بدون Manager membership
- حذف فیلدهای ممنوع از payload
- endpointهای واقعی Student/Parent/Teacher/Manager
- Fake-AI E2E شامل Controller، Builder، HMAC service JWT، HTTP adapter و response validation

این مرحله migration یا تغییر schema دیتابیس ندارد.
