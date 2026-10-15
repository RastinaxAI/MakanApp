# ماتریس Context مجاز برای AI

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## اصول

- selector ورودی client مدرک مجوز نیست.
- Backend ابتدا session، workspace، membership و role را resolve می‌کند.
- فقط aggregateهای محدود، publication/release شده و موردنیاز use case ارسال می‌شوند.
- `DataAsOfUtc` زمان سرور است.
- private messages، secrets، OTP، payment data، answer keys و raw entities ممنوع‌اند.

| نقش/سناریو | مرجع مجوز | scope خروجی | داده مجاز | داده ممنوع |
|---|---|---|---|---|
| Student | active session + Student role + OrganizationPerson/enrollment فعال | organization، student، classهای enrolled | برنامه کلاس، تکلیف/آزمون منتشرشده، حضور خود، grade آزادشده با MaxScore version | grade آزادنشده، دانش‌آموز دیگر، answer key |
| ParentChild | Parent role + selected child + GuardianRelation فعال و معتبر | organization و learner انتخاب‌شده | dashboard/report فرزند، feedback مخصوص guardian، grade آزادشده | learner دیگر، learner feedback خصوصی، private chat |
| TeacherClass | Teacher role + TeacherAssignment فعال | organization و class مجاز | aggregate کلاس، برنامه، صف ارزیابی، داده منتشرشده | کلاس دیگر، private messages، داده payment |
| TeacherStudent | TeacherAssignment فعال + Enrollment فعال learner در همان class | organization، class و learner | attendance و assessment همان learner در class، state لازم برای insight | learner بیرون class، داده organization دیگر |
| ManagerSchool | Manager role در membership فعال | organization جاری | aggregate عملیاتی مدرسه | PlatformOwner privileges، organization دیگر |
| ManagerClass | Manager role + class در organization جاری | organization و class | aggregate/گزارش همان class | class سازمان دیگر |
| ManagerStudent | Manager role + learner در organization جاری | organization و learner | aggregate آموزشی bounded | guardian/private chat/payment details |

## نگاشت شناسه‌ها

- `actor.userId` = `AccessContext.UserId`؛ برای audit است، نه مجوز AI.
- `scope.organizationId` = `AccessContext.OrganizationId` و هرگز مقدار آزاد client نیست.
- learner = `OrganizationPerson.Id`، نه `User.Id` یا `Person.Id`.
- Parent learner = `AccessContext.SubjectOrganizationPersonId` پس از revalidation.
- Teacher scope فقط از `TeacherAssignment.TeacherMembershipId` فعال ساخته می‌شود.
- Student class فقط از `Enrollment` فعال ساخته می‌شود.
- Manager با `OrganizationRole.Manager` برابر PlatformOwner نیست.

## وضعیت‌های business

| داده | شرط ارسال |
|---|---|
| Assignment | status منتشرشده/بسته و version منتشرشده |
| Assignment grade | evaluation آزادشده و GradeRelease موجود |
| Exam | Exam و ExamVersion منتشرشده |
| Exam grade | revision آزادشده و ExamGradeRelease موجود |
| Score | decimal به همراه `MaxScore` همان version |
| Time/deadline | UTC از سرور؛ زمان device معتبر نیست |
| File | صرف Ready بودن FileAsset کافی نیست؛ باید knowledge source lifecycle مستقل داشته باشد |

## حداقل‌سازی

Context builderها باید projection صریح بسازند، تعداد آیتم‌ها را محدود کنند، متن آزاد را truncate کنند و null/empty state را آگاهانه نمایش دهند. serialization مستقیم DbContext entity ممنوع است.
