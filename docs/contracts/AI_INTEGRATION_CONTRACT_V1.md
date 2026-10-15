# AI Integration Contract v1 — Frozen Backend Target

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

وضعیت: **Frozen**. Generic Chat و عملیات role-specific در Backend پیاده‌سازی شده‌اند. این سند target الزام‌آور handoff تیم AI است.

## مرز و شناسه‌ها

- Backend `OrganizationId` همان boundary مدرسه و مقدار AI `schoolId` است؛ School aggregate جدا ساخته نمی‌شود.
- `UserId` شناسه حساب ورود، `PersonId` شناسه شخص و `OrganizationPersonId` شناسه همان شخص در سازمان است؛ برابر فرض نمی‌شوند.
- شناسه Student در داده آموزشی `OrganizationPersonId` است. `actor.userId` فقط شناسه کاربر احراز‌شده است.
- `MembershipId` عضویت حساب در سازمان و `EnrollmentId` ثبت‌نام learner در کلاس است.
- role از `AccessContext.ActiveRole` می‌آید و client role authoritative نیست.

## Envelope مشترک درخواست

| Field | Type | الزام | معنا |
|---|---|---|---|
| `requestId` | string(1..128) | required | شناسه invocation ساخته‌شده در Backend |
| `correlationId` | string(1..128) | optional | correlation ورودی/سرور |
| `actor.userId` | string | required | User احراز‌شده |
| `actor.role` | `Student/Parent/Teacher/Manager` | required | نقش resolve‌شده سرور |
| `scope.organizationId` | string | per operation | Organization جاری |
| `scope.schoolId` | string | per operation | همان OrganizationId برای schema فعلی AI |
| `scope.classId` | string | per operation | کلاس مجاز |
| `scope.studentId` | string | per operation | OrganizationPersonId مجاز |

تمام JSONها camelCase و unknown fieldها در AI مردود هستند. timestampهای Backend در context به UTC ارسال می‌شوند.

## عملیات فعلی AI

| عملیات | Backend public route | AI internal route | Request اختصاصی | Response | Role |
|---|---|---|---|---|---|
| Generic Chat | `POST /api/v1/ai/chat` | `POST /api/v1/ai/chat` | `conversationId?`, `message`, `context` | `ChatResponse` | چهار نقش |
| Student Analyze | `POST /api/v1/ai/students/analyze` | همان | StudentScope + `studentData` | `StudentAnalysisResponse` | Student |
| Study Plan | `POST /api/v1/ai/study-plan` | همان | StudentScope + goals/preferences/studentData | `StudyPlanResponse` | Student |
| Homework Explain | `POST /api/v1/ai/homeworks/{assignmentId}/explain` | `POST /api/v1/ai/homeworks/explain` | StudentScope + homework/question/context | `HomeworkExplanationResponse` | Student |
| Exam Prepare | `POST /api/v1/ai/exams/{examId}/prepare` | `POST /api/v1/ai/exams/prepare` | StudentScope + exam/studentData/context | `ExamPreparationResponse` | Student |
| Parent Child Summary | `POST /api/v1/ai/parents/child-summary` | همان | Parent actor + ParentChildScope + `childData` | `ChildSummaryResponse` | Parent |
| Teacher Class Summary | `POST /api/v1/ai/teachers/classes/{classId}/summary` | `POST /api/v1/ai/teachers/class-summary` | TeacherClassScope + `classData` | `ClassSummaryResponse` | Teacher |
| Teacher Student Insight | `POST /api/v1/ai/teachers/classes/{classId}/students/{studentId}/insight` | `POST /api/v1/ai/teachers/student-insight` | TeacherStudentScope + `studentData` | `StudentInsightResponse` | Teacher |
| Teacher Activity Suggestions | `POST /api/v1/ai/teachers/classes/{classId}/activity-suggestions` | `POST /api/v1/ai/teachers/activity-suggestions` | TeacherClassScope + objective/constraints/classData | `ActivitySuggestionsResponse` | Teacher |
| Manager School Summary | `POST /api/v1/ai/managers/school-summary` | همان | ManagerSchoolScope + `schoolData` | `SchoolSummaryResponse` | Manager |
| Manager Class Insight | `POST /api/v1/ai/managers/classes/{classId}/insight` | `POST /api/v1/ai/managers/class-insight` | ManagerClassScope + `classData` | `ClassInsightResponse` | Manager |
| Manager Student Insight | `POST /api/v1/ai/managers/classes/{classId}/students/{studentId}/insight` | `POST /api/v1/ai/managers/student-insight` | ManagerStudentScope + `studentData` | `ManagerStudentInsightResponse` | Manager |

## ساختار context آموزشی

`studentData` و `childData` دقیقاً شامل `subjects`، `grades`، `homeworks`، `exams` و `activity` هستند. `classData` و `schoolData` همین مجموعه را همراه aggregateهای محدود و شناسه scope دارند. رکوردهای داخلی projection صریح Backend هستند و entity یا navigation graph نیستند.

- `grades[]`: فقط نتیجه release‌شده، شامل `score` و `maxScore` همان AssignmentVersion/ExamVersion.
- `homeworks[]`: فقط تکلیف published/closed با version منتشرشده.
- `exams[]`: فقط Exam و ExamVersion منتشرشده.
- `activity[]`: حضور/رویدادهای آموزشی bounded؛ بدون متن پیام یا تاریخچه AI.
- Teacher/Manager فقط context همان class/student مجاز را دریافت می‌کنند.

حدود v1: حداکثر ۱۰ subject، ۱۰ assignment، ۱۰ exam، ۱۰ grade، ۲۰ activity و ۳۰ student reference برای aggregate کلاس. متن آزاد حداکثر ۲۰۰۰ نویسه است.

Backend header `X-AI-Contract-Version: v1` و `X-Correlation-ID` را می‌فرستد. `requestId` و `correlationId` پاسخ باید دقیقاً با درخواست برابر باشند.

## ChatResponse

| Field | Type | الزام |
|---|---|---|
| `requestId` | string | required و باید با request برابر باشد |
| `correlationId` | string/null | optional |
| `conversationId` | string/null | optional |
| `answer.text` | non-blank string | required |
| `answer.language` | language code | required |
| `insights[]/risks[]` | type، severity enum، entity refs، confidence 0..1 | required collection |
| `recommendations[]` | title و description اختیاری | required collection |
| `sources[]` | type و id | required collection |
| `metadata.modelAlias` | non-blank string | required |
| `metadata.processingTimeMs` | integer >= 0 | required |

Severity enum: `Info`, `Low`, `Medium`, `High`, `Critical`.

## خطا

AI error body شکل `{ "error": { "code", "message", "requestId?" } }` دارد. Backend body خام را عبور نمی‌دهد.

| وضعیت/خرابی | نگاشت Backend |
|---|---|
| 400/422 | `AI_INVALID_REQUEST` |
| 401 | `AI_SERVICE_UNAUTHORIZED` |
| 403 | `AI_SERVICE_FORBIDDEN` |
| 404/409/500/502/503 یا connection failure | `AI_SERVICE_UNAVAILABLE` |
| 429 | `AI_RATE_LIMITED` و Retry-After محدود |
| 504/timeout | `AI_SERVICE_TIMEOUT` |
| JSON/schema/version mismatch | `AI_RESPONSE_INVALID` |

## داده مجاز و ممنوع

فقط projection محدود و مجاز شامل publication/release state، زمان UTC و score به همراه MaxScore همان version مجاز است. token/OTP/credential/full phone/Contacts/private Direct/moderation evidence/payment secret/raw private Student-AI history و داده سازمان دیگر ممنوع است.

## Service Auth v1

Backend JWT داخلی HS256 کوتاه‌عمر با `kid`, `iss`, `aud`, `sub`, `scope`, `iat`, `nbf`, `exp`, `jti` می‌سازد. token هیچ claim کاربری یا آموزشی ندارد. AI در HEAD ممیزی‌شده current و previous key، امضا، زمان، issuer، audience و scope را بررسی می‌کند. health/readiness عمومی و business routes محافظت‌شده‌اند.
