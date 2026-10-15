# قرارداد API داخلی Backend به AI

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

نسخه frozen: `v1`

Base path نمونه: `http://<ai-service>/api/v1`. تمام عملیات `POST` و تمام bodyها JSON با نام‌گذاری `camelCase` و `extra=forbid` هستند.

## header و envelope مشترک

Headerهای الزامی business route:

```text
Authorization: Bearer <short-lived-service-jwt>
X-AI-Contract-Version: v1
Content-Type: application/json
```

`X-Correlation-ID` وقتی `correlationId` body وجود دارد ارسال می‌شود و باید با آن برابر باشد.

در تمام requestها این fieldها required هستند: `requestId`، `actor` و context/data اختصاصی عملیات. `correlationId` optional و nullable است. `language` optional با default برابر `fa` است، اما Backend آن را صریح می‌فرستد. `requestId` و `correlationId` حداکثر 128 نویسه‌اند.

Enumها:

- role: `Student | Parent | Teacher | Manager`
- severity/priority: `Info | Low | Medium | High | Critical`
- progress/trend: `Improving | Stable | Declining | Unknown`

در تمام responseها `requestId` و `correlationId` باید دقیقاً با request برابر باشند و `metadata.modelAlias` non-blank و `metadata.processingTimeMs >= 0` باشد.

## 1. Generic Chat

- Internal path: `POST /api/v1/ai/chat`
- Public Backend path: `POST /api/v1/ai/chat`
- Role: هر یک از چهار role معتبر که Backend resolve کرده است.
- Optional: `correlationId`، `conversationId` و fieldهای nullable داخل insight.

Request:

```json
{
  "requestId": "req-chat-1",
  "correlationId": "corr-1",
  "conversationId": null,
  "actor": { "userId": "11111111-1111-1111-1111-111111111111", "role": "Student" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": null,
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "message": { "text": "چطور بهتر مطالعه کنم؟", "language": "fa" },
  "context": { "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [] }
}
```

Response:

```json
{
  "requestId": "req-chat-1",
  "correlationId": "corr-1",
  "conversationId": null,
  "answer": { "text": "یک برنامه کوتاه و منظم شروع کن.", "language": "fa" },
  "insights": [],
  "risks": [],
  "recommendations": [{ "title": "مرور روزانه", "description": null }],
  "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 12 }
}
```

## 2. Student Analyze

- Internal/Public path: `POST /api/v1/ai/students/analyze`
- Role: `Student`
- Required scope: `organizationId`, `schoolId`, `studentId`; `classId` optional.

Request:

```json
{
  "requestId": "req-student-1",
  "correlationId": "corr-2",
  "actor": { "userId": "11111111-1111-1111-1111-111111111111", "role": "Student" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "studentData": {
    "subjects": [{ "classId": "44444444-4444-4444-4444-444444444444", "classTitle": "هفتم الف", "courseTitle": "ریاضی" }],
    "grades": [{ "score": 17.25, "maxScore": 20, "releasedAtUtc": "2026-10-01T08:00:00Z" }],
    "homeworks": [], "exams": [], "activity": []
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-student-1", "correlationId": "corr-2",
  "summary": "روند کلی پایدار است.",
  "strengths": [], "weaknesses": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 15 }
}
```

## 3. Study Plan

- Internal/Public path: `POST /api/v1/ai/study-plan`
- Role: `Student`
- `goals` می‌تواند خالی باشد؛ `preferences.availableMinutes` و `notes` optional هستند.

Request:

```json
{
  "requestId": "req-plan-1", "correlationId": "corr-3",
  "actor": { "userId": "11111111-1111-1111-1111-111111111111", "role": "Student" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "studentData": { "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [] },
  "goals": ["مرور ریاضی"],
  "preferences": { "availableMinutes": 45, "notes": "تمرکز روی کسرها" },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-plan-1", "correlationId": "corr-3",
  "summary": "برنامه ۴۵ دقیقه‌ای آماده شد.",
  "priorities": ["ریاضی"],
  "planItems": [{ "title": "تمرین کسر", "reason": "هدف فعلی", "priority": "High", "estimatedMinutes": 45 }],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 18 }
}
```

## 4. Homework Explain

- Internal path: `POST /api/v1/ai/homeworks/explain`
- Public Backend path: `POST /api/v1/ai/homeworks/{assignmentId}/explain`
- Role: `Student`
- `question` required و non-blank است؛ Assignment ID عمومی در Backend authorization می‌شود و داخل `homework` authorized ارسال می‌شود.

Request:

```json
{
  "requestId": "req-homework-1", "correlationId": "corr-4",
  "actor": { "userId": "11111111-1111-1111-1111-111111111111", "role": "Student" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "homework": {
    "assignmentId": "55555555-5555-5555-5555-555555555555",
    "classId": "44444444-4444-4444-4444-444444444444",
    "title": "کسرها", "description": "تمرین فصل ۲",
    "dueAtUtc": "2026-10-20T12:00:00Z", "maxScore": 10
  },
  "question": "روش حل را توضیح بده.",
  "context": { "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [] },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-homework-1", "correlationId": "corr-4",
  "explanation": "ابتدا مخرج مشترک پیدا کن.",
  "steps": ["مخرج مشترک را پیدا کن", "صورت‌ها را جمع کن"],
  "hints": ["کوچک‌ترین مضرب مشترک را بررسی کن"],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 20 }
}
```

## 5. Exam Prepare

- Internal path: `POST /api/v1/ai/exams/prepare`
- Public Backend path: `POST /api/v1/ai/exams/{examId}/prepare`
- Role: `Student`
- Exam عمومی باید published باشد و payload authorized شامل MaxScore version باشد.

Request:

```json
{
  "requestId": "req-exam-1", "correlationId": "corr-5",
  "actor": { "userId": "11111111-1111-1111-1111-111111111111", "role": "Student" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "exam": {
    "examId": "66666666-6666-6666-6666-666666666666",
    "classId": "44444444-4444-4444-4444-444444444444",
    "title": "آزمون ریاضی", "description": null,
    "availableFromUtc": "2026-10-21T08:00:00Z",
    "availableUntilUtc": "2026-10-21T09:00:00Z",
    "durationMinutes": 45, "maxScore": 30
  },
  "studentData": { "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [] },
  "context": { "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [] },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-exam-1", "correlationId": "corr-5",
  "summary": "برای فصل‌های اصلی مرور انجام بده.",
  "priorityTopics": ["کسرها"],
  "preparationSteps": ["مرور مثال‌ها", "حل تمرین"],
  "risks": [], "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 22 }
}
```

## 6. Parent Child Summary

- Internal/Public path: `POST /api/v1/ai/parents/child-summary`
- Role: `Parent`
- scope required: organization/school/student؛ childData فقط داده آموزشی مجاز و بدون raw conversation است.

Request:

```json
{
  "requestId": "req-parent-1", "correlationId": "corr-6",
  "actor": { "userId": "77777777-7777-7777-7777-777777777777", "role": "Parent" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": null,
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "childData": {
    "subjects": [],
    "grades": [{ "score": 8.5, "maxScore": 10, "releasedAtUtc": "2026-10-01T08:00:00Z" }],
    "homeworks": [], "exams": [], "activity": []
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-parent-1", "correlationId": "corr-6",
  "academicSummary": "وضعیت تحصیلی پایدار است.",
  "progress": { "trend": "Stable", "description": "تغییر معناداری دیده نمی‌شود." },
  "attentionAreas": [], "upcomingItems": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 16 }
}
```

## 7. Teacher Class Summary

- Internal path: `POST /api/v1/ai/teachers/class-summary`
- Public Backend path: `POST /api/v1/ai/teachers/classes/{classId}/summary`
- Role: `Teacher`
- scope و `classData.classId` باید یکسان باشند.

Request:

```json
{
  "requestId": "req-teacher-class-1", "correlationId": "corr-7",
  "actor": { "userId": "88888888-8888-8888-8888-888888888888", "role": "Teacher" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444"
  },
  "classData": {
    "classId": "44444444-4444-4444-4444-444444444444",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [],
    "students": [{ "studentId": "33333333-3333-3333-3333-333333333333" }],
    "aggregates": { "averageScore": null, "scoreScale": null, "completionRate": null, "attendanceRate": null, "trend": null }
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-teacher-class-1", "correlationId": "corr-7",
  "summary": "خلاصه کلاس آماده شد.",
  "strengths": [], "attentionAreas": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 19 }
}
```

## 8. Teacher Student Insight

- Internal path: `POST /api/v1/ai/teachers/student-insight`
- Public Backend path: `POST /api/v1/ai/teachers/classes/{classId}/students/{studentId}/insight`
- Role: `Teacher`
- scope student و `studentData.studentId` باید برابر باشند.

Request:

```json
{
  "requestId": "req-teacher-student-1", "correlationId": "corr-8",
  "actor": { "userId": "88888888-8888-8888-8888-888888888888", "role": "Teacher" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "studentData": {
    "studentId": "33333333-3333-3333-3333-333333333333",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": []
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-teacher-student-1", "correlationId": "corr-8",
  "summary": "خلاصه دانش‌آموز آماده شد.",
  "strengths": [], "attentionAreas": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 18 }
}
```

## 9. Teacher Activity Suggestions

- Internal path: `POST /api/v1/ai/teachers/activity-suggestions`
- Public Backend path: `POST /api/v1/ai/teachers/classes/{classId}/activity-suggestions`
- Role: `Teacher`
- `objective` required؛ constraints fieldهای optional دارد.

Request:

```json
{
  "requestId": "req-activity-1", "correlationId": "corr-9",
  "actor": { "userId": "88888888-8888-8888-8888-888888888888", "role": "Teacher" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444"
  },
  "classData": {
    "classId": "44444444-4444-4444-4444-444444444444",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [],
    "students": [], "aggregates": null
  },
  "objective": "تمرین گروهی کسرها",
  "constraints": { "availableMinutes": 30, "materials": ["کاغذ"], "notes": null },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-activity-1", "correlationId": "corr-9",
  "summary": "یک فعالیت گروهی پیشنهاد شد.",
  "activities": [{
    "title": "کارت‌های کسر", "objective": "مقایسه کسرها", "reason": "تمرین مشارکتی",
    "steps": ["گروه‌بندی", "حل کارت‌ها"], "estimatedMinutes": 30
  }],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 21 }
}
```

## 10. Manager School Summary

- Internal/Public path: `POST /api/v1/ai/managers/school-summary`
- Role: `Manager`
- scope organization/school باید برابر باشند و `schoolData.schoolId` نیز همان مقدار است.

Request:

```json
{
  "requestId": "req-manager-school-1", "correlationId": "corr-10",
  "actor": { "userId": "99999999-9999-9999-9999-999999999999", "role": "Manager" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222"
  },
  "schoolData": {
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [],
    "classes": [{ "classId": "44444444-4444-4444-4444-444444444444" }],
    "aggregates": {
      "averageScore": null, "scoreScale": null, "completionRate": null,
      "attendanceRate": null, "trend": null, "studentCount": 1, "classCount": 1
    }
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-manager-school-1", "correlationId": "corr-10",
  "summary": "خلاصه مدرسه آماده شد.",
  "strengths": [], "attentionAreas": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 24 }
}
```

## 11. Manager Class Insight

- Internal path: `POST /api/v1/ai/managers/class-insight`
- Public Backend path: `POST /api/v1/ai/managers/classes/{classId}/insight`
- Role: `Manager`
- scope class و `classData.classId` باید برابر باشند.

Request:

```json
{
  "requestId": "req-manager-class-1", "correlationId": "corr-11",
  "actor": { "userId": "99999999-9999-9999-9999-999999999999", "role": "Manager" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444"
  },
  "classData": {
    "classId": "44444444-4444-4444-4444-444444444444",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": [],
    "students": [], "aggregates": null
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-manager-class-1", "correlationId": "corr-11",
  "summary": "خلاصه کلاس آماده شد.",
  "strengths": [], "attentionAreas": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 17 }
}
```

## 12. Manager Student Insight

- Internal path: `POST /api/v1/ai/managers/student-insight`
- Public Backend path: `POST /api/v1/ai/managers/classes/{classId}/students/{studentId}/insight`
- Role: `Manager`
- scope student و `studentData.studentId` باید برابر باشند.

Request:

```json
{
  "requestId": "req-manager-student-1", "correlationId": "corr-12",
  "actor": { "userId": "99999999-9999-9999-9999-999999999999", "role": "Manager" },
  "scope": {
    "organizationId": "22222222-2222-2222-2222-222222222222",
    "schoolId": "22222222-2222-2222-2222-222222222222",
    "classId": "44444444-4444-4444-4444-444444444444",
    "studentId": "33333333-3333-3333-3333-333333333333"
  },
  "studentData": {
    "studentId": "33333333-3333-3333-3333-333333333333",
    "subjects": [], "grades": [], "homeworks": [], "exams": [], "activity": []
  },
  "language": "fa"
}
```

Response:

```json
{
  "requestId": "req-manager-student-1", "correlationId": "corr-12",
  "summary": "خلاصه دانش‌آموز آماده شد.",
  "strengths": [], "attentionAreas": [], "risks": [],
  "recommendations": [], "sources": [],
  "metadata": { "modelAlias": "education-fast", "processingTimeMs": 18 }
}
```

## validation تکمیلی

- `schoolId` همیشه برابر Backend `OrganizationId` است.
- Student ID یک `OrganizationPersonId` است، نه UserId یا PersonId.
- collectionها باید array باشند؛ `null` به‌جای array معتبر نیست.
- rateها در بازه 0..1، confidence در بازه 0..1 و duration/minutes مثبت‌اند.
- `maxScore` version-specific است و نباید به 20 normalize شود.
- unknown request/response field، response بدون collection لازم، شناسه envelope متفاوت یا enum نامعتبر باید fail شود.
- AI error body مطابق `05_AI_ERROR_AND_RESILIENCE_CONTRACT.md` است.
