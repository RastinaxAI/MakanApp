# قرارداد Context، Authorization و Privacy

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## semantics شناسه‌ها

- `UserId`: حساب احراز هویت‌شده.
- `PersonId`: شخص متصل به حساب؛ با UserId برابر فرض نمی‌شود.
- `OrganizationId`: boundary مدرسه در Backend.
- AI `schoolId`: دقیقاً همان `OrganizationId`؛ School entity دوم ساخته نمی‌شود.
- `OrganizationPersonId`: هویت آموزشی شخص در یک سازمان و شناسه Student در payload آموزشی.
- `MembershipId`: عضویت حساب در سازمان.
- `EnrollmentId`: ثبت‌نام OrganizationPerson در Class.
- `classId`: کلاس مجاز داخل همان Organization.

`actor.userId` همیشه User احراز‌شده است؛ `scope.studentId` همیشه OrganizationPerson مجاز است.

## multi-role

role از `AccessContext.ActiveRole` و workspace انتخاب‌شده و اعتبارسنجی‌شده سرور می‌آید. role یا organization ارسالی client authority نیست. یک کاربر چندنقشی فقط context نقش فعال همان request را دریافت می‌کند. `PlatformOwner` به‌تنهایی Manager آموزشی نیست.

## Student

`StudentContextBuilder` این زنجیره را enforce می‌کند:

```text
User -> Person -> OrganizationPerson(active) -> Enrollment(active) -> Class(active)
```

Student فقط داده خود را می‌بیند. homework/exam انتخابی باید برای همان learner و کلاس منتشر شده باشد.

## Parent

`ParentChildContextBuilder` از child انتخاب‌شده در context معتبر استفاده می‌کند و resolver در هر request رابطه فعال Guardian را دوباره بررسی می‌کند:

```text
Parent Membership/Role -> GuardianRelation(active) -> Child OrganizationPerson
```

childId تنها selector است و مجوز نیست. هیچ raw child AI chat یا Messenger Direct به Parent ارسال نمی‌شود.

## Teacher

- `TeacherClassContextBuilder`: active Teacher membership/role + active TeacherAssignment به class.
- `TeacherStudentContextBuilder`: علاوه بر assignment فعال، student باید active Enrollment در همان class داشته باشد.
- شناسه class/student دلخواه client هرگز query جهانی ایجاد نمی‌کند.

## Manager

- `ManagerSchoolContextBuilder`: active Manager membership/role در Organization جاری.
- `ManagerClassContextBuilder`: class باید متعلق به همان Organization باشد.
- `ManagerStudentContextBuilder`: student از مسیر Organization -> Class -> active Enrollment resolve می‌شود.

## publication و release

- Assignment فقط در حالت published/closed و با version منتشرشده وارد context می‌شود.
- Exam و ExamVersion باید published باشند.
- score فقط بعد از grade/evaluation release ارسال می‌شود.
- draft، نمره release‌نشده و private teacher note ارسال نمی‌شوند.
- هر score همراه `maxScore` همان AssignmentVersion یا ExamVersion است؛ هیچ مقیاس سراسری 20 فرض نمی‌شود.

## limits نسخه v1

- subjects: 10
- assignments/homeworks: 10
- exams: 10
- grades: 10
- activity items: 20
- class student references: 30
- goals: 10
- materials: 10
- role-operation free text: 2000 character
- Generic Chat message: 4000 character

## داده ممنوع

هیچ payload نباید شامل این داده‌ها باشد:

- access/refresh/session token یا OTP
- full phone number یا credential
- Contacts
- Messenger Direct content/history/attachment
- moderation یا child-safety evidence
- payment/finance secret
- platform audit content
- raw private Student AI history برای Parent/Teacher/Manager
- EF entity، navigation graph یا داده سازمان دیگر

AI باید schemaهای `extra=forbid` و role-specific validation را نگه دارد. AI defense-in-depth است؛ authority اصلی authorization همچنان Backend است.
