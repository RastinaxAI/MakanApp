# AI Integration Contract v1 — Freeze Candidate

وضعیت عملیات generic chat: **Frozen**. عملیات role-specific فعلاً route/schema استخراج‌شده دارند، اما Backend integration آن‌ها تا STEP 10E کامل نیست.

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

| عملیات | Method/Path | Request اختصاصی | Response | Role | وضعیت |
|---|---|---|---|---|---|
| Generic Chat | `POST /api/v1/ai/chat` | `conversationId?`, `message{text,language}`, `context{subjects,grades,homeworks,exams,activity}` | `ChatResponse` | هر چهار نقش | Frozen |
| Student Analyze | `POST /api/v1/ai/students/analyze` | StudentScope + `studentData` | `StudentAnalysisResponse` | Student | Backend mapping required |
| Study Plan | `POST /api/v1/ai/study-plan` | StudentScope + goal/preferences/studentData | `StudyPlanResponse` | Student | Backend mapping required |
| Homework Explain | `POST /api/v1/ai/homeworks/explain` | StudentScope + homework/question/context | `HomeworkExplanationResponse` | Student | Backend mapping required |
| Exam Prepare | `POST /api/v1/ai/exams/prepare` | StudentScope + exam/studentData/context | `ExamPreparationResponse` | Student | Backend mapping required |
| Parent Child Summary | `POST /api/v1/ai/parents/child-summary` | Parent actor + StudentScope + `childData` | `ChildSummaryResponse` | Parent | Backend mapping required |
| Teacher Class Summary | `POST /api/v1/ai/teachers/class-summary` | TeacherClassScope + `classData` | `ClassSummaryResponse` | Teacher | Backend mapping required |
| Teacher Student Insight | `POST /api/v1/ai/teachers/student-insight` | TeacherStudentScope + `studentData` | `StudentInsightResponse` | Teacher | Backend mapping required |
| Teacher Activity Suggestions | `POST /api/v1/ai/teachers/activity-suggestions` | TeacherClassScope + objective/constraints/classData | `ActivitySuggestionsResponse` | Teacher | Backend mapping required |
| Manager School Summary | `POST /api/v1/ai/managers/school-summary` | ManagerSchoolScope + `schoolData` | `SchoolSummaryResponse` | Manager | Backend mapping required |
| Manager Class Insight | `POST /api/v1/ai/managers/class-insight` | ManagerClassScope + `classData` | `ClassInsightResponse` | Manager | Backend mapping required |
| Manager Student Insight | `POST /api/v1/ai/managers/student-insight` | ManagerStudentScope + `studentData` | `ManagerStudentInsightResponse` | Manager | Backend mapping required |

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
