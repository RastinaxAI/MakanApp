# سند سناریوهای دستی پذیرش API — MakanApp

**تاریخ:** 2026-10-18 · **مبنا:** `main@49cb86ca897301c80c771d170825c1aec4a99226` · **وضعیت اجرای واقعی:** `NOT_RUN`.

این سند با `API_INVENTORY.csv` و Collection همان Branch همراه است. endpointها از کاتالوگ واقعی `docs/backend-api-reference.md` و Controllerهای موجود گرفته شده‌اند. وضعیت‌های HTTP ذکرشده بر اساس تعریف Controller و ErrorHandler است؛ هر شرط کسب‌وکاری باید از خروجی واقعی تأیید شود. سناریوهای پیچیده لازم نیست پشت سرهم بدون آماده‌سازی داده اجرا شوند.

## مراحل مشترک پیش از هر سناریو

**پیش‌نیاز:** محیط تست مجاز با TLS معتبر، artifact مطابق commit، SQL migration صحیح، حساب‌های مستقل دارای نقش واقعی `Manager`، `Teacher`، `Student`، `Parent` و `Platform` با permission/MFA، حداقل دو سازمان مستقل، چند کلاس/دوره، یک PlanVersion قابل‌خرید، و منابع قابل‌ویرایش مخصوص آزمایش. ایجاد دادهٔ پایه باید تحت مجوز اپراتور انجام شود؛ pack حساب/نقش ساختگی یا bypass تعریف نمی‌کند. سناریوهای destructive را روی دادهٔ Production اجرا نکنید.

**متغیرهای Postman:** `baseUrl`، `accessToken`، `refreshToken`، `phoneNumber`، `otpCode`، `challengeId`، `workspaceType`، `membershipId`، `organizationRole`، `classId`، `conversationId`، `orderId`، `expectedVersion`، `expectedRowVersion` و IDs مربوطه. هر ID باید از response واقعی یا fixture مجاز بیاید. پیش از هر mutation، `allowMutations=true` را فقط برای همان نوبت تنظیم و بعد دوباره false کنید.

**ثبت شاهد:** UTC، محیط/نسخه deployment، Actor/Workspace، method/path، ورودی با حذف token/PII، HTTP status، `code` و `traceId` برای خطا، نتیجه، تعداد رکوردهای قبل/بعد، و screenshot/response سانسورشده. مقدارهای `phone`، `OTP`، `accessToken`، `refreshToken`، MFA/recovery، invitation token، کلید AI و فایل شخصی را ثبت نکنید.

---

## A01 — Health و تشخیص نسخه

**هدف:** اطمینان از بالا بودن host و readiness؛ این endpointها در ۳۲۵ عملیات Controller شمرده نشده‌اند.

| گام | متد و مسیر واقعی | انتظار |
|---|---|---|
| A01.1 | `GET /health` | `200` در میزبان سالم |
| A01.2 | `GET /health/live` | `200` در میزبان زنده |
| A01.3 | `GET /health/ready` | `200` فقط با readiness؛ عدم آمادگی را جداگانه تحلیل کنید |
| A01.4 | `GET /api/v1/me` بدون Bearer | `401`؛ این درخواست نباید اطلاعات کاربر بدهد |
| A01.5 | `GET /openapi/v1.json` | روی Production با Swagger غیرفعال `404` طبیعی است؛ برای comparison فقط محیط مجاز با Swagger روشن |

**نتیجه:** Health موفق به‌تنهایی احراز هویت، SQL schema، رسانه، SMS/AI/Push یا نسخهٔ artifact هدف را اثبات نمی‌کند.

## A02 — OTP، هویت و session

**مرجع:** `AuthController.cs` و `IdentityContracts.cs`.

1. `POST /api/v1/auth/otp/challenges` با `{"phoneNumber":"<TEST_PHONE>"}`. پاسخ موفق `202` و فیلدهای `challengeId`، `expiresAtUtc`، `resendAvailableAtUtc`. شرط: SMS واقعی فقط با opt-in اپراتور، هزینه و rate-limit.
2. `POST /api/v1/auth/otp/verify` با `{"challengeId":"<ID_FROM_A02.1>","phoneNumber":"<SAME_PHONE>","code":"<RECEIVED_CODE>","deviceName":"manual-acceptance"}`. پاسخ `200` و `accessToken`، `refreshToken`، `expiresAtUtc` و `user`.
3. `GET /api/v1/me` با Bearer؛ `200`. `GET /api/v1/auth/sessions`؛ `200` و نشست جاری در خروجی.
4. `POST /api/v1/auth/refresh` با `{"refreshToken":"<REFRESH_FROM_PREVIOUS_RESPONSE>"}`. `200` با توکن‌های چرخشی؛ refresh قدیمی را پس از refresh موفق دوباره امتحان و `401` کد `REFRESH_TOKEN_REUSED` یا کد معتبر مربوطه را با handler ثبت کنید.
5. `POST /api/v1/auth/sessions/revoke-others`؛ `204`، سپس اعتبار نشست دوم را تست کنید.
6. `POST /api/v1/auth/logout`؛ `204`. درخواست `GET /api/v1/me` با توکن نشست باطل‌شده باید فاقد دسترسی باشد.
7. منفی: OTP اشتباه، استفادهٔ دوباره، OTP منقضی و rate-limit فقط با مجوز تست و بدون فشار روی SMS/provider. status `409`، `410`، `429` یا خطای متناظر را براساس case واقعی بررسی کنید.

**سیاست:** هرگز OTP یا token زنده روی HTTP عمومی ارسال نکنید؛ حتی اگر تنظیم موقت Production اجازهٔ HTTP را بدهد.

## A03 — Profile و انتخاب Workspace

1. `GET /api/v1/me` و در صورت نیاز `PATCH /api/v1/me/profile` با `CompleteProfileCommand` واقعی (رجوع به template).
2. `GET /api/v1/workspaces/me`، `200`؛ `workspaceType`، `membershipId` و `role` معتبر را از همان پاسخ بخوانید.
3. `POST /api/v1/workspaces/select` با `{"workspaceType":"<REAL_ENUM_LITERAL>","membershipId":"<MEMBERSHIP_ID_OR_NULL>","role":"<REAL_ROLE_OR_NULL>"}`. `200` و `AccessContext` برگشتی.
4. `GET /api/v1/workspaces/current` و مقایسه با انتخاب قبلی.
5. منفی: membership سازمان دوم، role انتخاب‌نشده، membership ابطال‌شده و کاربر بدون مجوز؛ انتظار رد مجوز (معمولاً `403`/`404`) و عدم جابه‌جایی مجاز context. **هیچ role اختیاری از client پذیرفته نشود.**

## A04 — Organization، پرونده فرد، دعوت و Guardian

**Actor:** Manager سازمان تست؛ Parent/Student جدا.

1. `POST /api/v1/organization/people` با `{"clientOperationId":"<UUID>","firstName":"Test","lastName":"Learner","displayName":"Manual Test","reason":"acceptance fixture"}`؛ `201` برای ایجاد و `200` در replay معتبر.
2. همان `clientOperationId` و همان payload را دوباره بفرستید؛ `200` و همان شناسه شخص، بدون پروندهٔ جدید. payload متفاوت با همان شناسه باید `409` باشد (ثبت code).
3. `GET /api/v1/organization/people` و `GET /api/v1/organization/people/{id}`؛ رکورد ساخته‌شده را تأیید کنید.
4. `PATCH /api/v1/organization/people/{id}` با `UpdateOrganizationPersonNamesCommand` و rowversion معتبر؛ سپس اجرای مجدد با نسخهٔ قدیمی و انتظار `409`.
5. `POST /api/v1/organization/people/{id}/invitations`؛ سپس `GET /api/v1/invitations` در حساب مقصد؛ `POST /api/v1/invitations/{invitationId}/accept` فقط توسط صاحب حساب با confirmation معتبر contract، نه اتصال خودکار.
6. `POST /api/v1/organization/guardian-relations`، `GET /api/v1/organization/guardian-relations/{id}`، و `POST /api/v1/organization/guardian-relations/{id}/activate` با version واقعی؛ Parent سپس `GET /api/v1/guardian/children`.
7. منفی: تلاش Parent فاقد رابطه، Manager سازمان دیگر، پذیرش دعوت برای شخص دیگر و مشاهدهٔ Direct خصوصی کودک. دسترسی خوداظهاری ممنوع است.

## A05 — Academic (Course / Class / Teacher / Enrollment / Schedule / Session)

**Actor:** Manager با Workspace معتبر.

1. `POST /api/v1/academic/courses` با `CreateCourseCommand`.
2. `POST /api/v1/academic/periods` با `CreateAcademicPeriodCommand`.
3. `POST /api/v1/academic/classes`، سپس `GET /api/v1/academic/classes`؛ ارتباط class، course و period را کنترل کنید.
4. `POST /api/v1/academic/classes/{classId}/teachers` و `POST /api/v1/academic/classes/{classId}/enrollments` با payloadهای واقعی DTO.
5. `POST /api/v1/academic/classes/{classId}/schedule-rules` سپس `GET /api/v1/academic/schedule`.
6. `POST /api/v1/academic/classes/{classId}/sessions` و `GET /api/v1/academic/sessions/{sessionId}`؛ آزمون ساعت UTC و timezone.
7. `PUT /api/v1/academic/sessions/{sessionId}/attendance`، `PATCH /api/v1/academic/sessions/{sessionId}/attendance/{attendanceId}`؛ اصلاح با version معتبر و ناسازگار.
8. `POST /api/v1/academic/sessions/{sessionId}/complete` یا `.../cancel` روی دو session جدا؛ ممنوعیت تغییر وضعیت ناسازگار و تعارض teacher/schedule را ثبت کنید.

## A06 — Assignment، Submission و Evaluation

**Actor:** Teacher، Student و Parent با رابطهٔ معتبر.

1. `POST /api/v1/academic/classes/{classId}/assignments`؛ پاسخ ایجاد را به‌عنوان fixture بگیرید.
2. `PATCH /api/v1/academic/assignments/{assignmentId}` با نسخهٔ جاری؛ `POST /api/v1/academic/assignments/{assignmentId}/publish`.
3. با Student: `GET /api/v1/academic/assignments/{assignmentId}`؛ `POST /api/v1/academic/assignments/{assignmentId}/attempts`؛ `PATCH /api/v1/academic/submission-attempts/{attemptId}/draft`.
4. `POST /api/v1/academic/submission-attempts/{attemptId}/submit`؛ سپس وضعیت، deadline و تعداد attempts را کنترل کنید.
5. با Teacher: `GET /api/v1/academic/evaluations/queue`، `PUT /api/v1/academic/submission-attempts/{attemptId}/evaluation`، `POST /api/v1/academic/submission-attempts/{attemptId}/evaluation/release`.
6. با Student/Parent مجاز: `GET /api/v1/academic/submission-attempts/{attemptId}/result` و `.../guardian-result`.
7. منفی: Student دیگر، submission بعد deadline، submission duplicate با شرایط ممنوع، update با rowversion قدیمی و نشت نتیجه پیش از release.

## A07 — Exam / Answer Revisions / Grading / Finalization

1. Teacher: `POST /api/v1/academic/classes/{classId}/exams`، `POST /api/v1/academic/exams/{examId}/questions`، `POST /api/v1/academic/exams/{examId}/versions`، `POST /api/v1/academic/exams/{examId}/publish`.
2. Student: `POST /api/v1/academic/exams/{examId}/attempts/start`، `POST /api/v1/academic/exam-attempts/{attemptId}/write-lease`.
3. `PUT /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}` با `expectedVersion`/lease واقعی (از response)؛ اجرای دوباره با version قدیمی. بررسی عدم گم‌شدن update.
4. `POST /api/v1/academic/exam-attempts/{attemptId}/finalize`؛ `GET /api/v1/academic/exam-attempts/{attemptId}/receipt`.
5. Teacher: `POST /api/v1/academic/exam-attempts/{attemptId}/grading`، `POST /api/v1/academic/exam-attempts/{attemptId}/grading/complete`، `POST /api/v1/academic/exam-attempts/{attemptId}/grade/release`.
6. Student/Parent مجاز: `.../result` و `.../guardian-result`; منفی: پاسخ بعد از finalize، lease نشست دیگری، grade پیش از release.

## A08 — Messaging P0 (Direct، Send، Idempotency، Sync)

**حساب‌ها:** دو Adult مجاز و مستقل، یک Minor/Unknown و دو سازمان تست.

1. `POST /api/v1/conversations/direct` با `StartDirectConversationCommand` و peer مجاز؛ `201` یا در replay معتبر `200`.
2. `GET /api/v1/conversations` و `GET /api/v1/conversations/{conversationId}/messages`.
3. `POST /api/v1/conversations/{conversationId}/messages` با `{"clientMessageId":"<UUID>","text":"acceptance-p0","isSilent":false}`؛ `200` با `MessageReceiptResult`.
4. همان body با همان `clientMessageId` را تکرار کنید؛ تنها یک پیام پایدار، همان شناسه و عدم تکرار unread/notification. برای payload متفاوت با شناسهٔ یکسان وضعیت conflict/validation مطابق service را بررسی کنید.
5. `GET /api/v1/conversations/{conversationId}/changes` با cursor واقعی، `POST /api/v1/conversations/{conversationId}/read` و `GET /api/v1/conversations/{conversationId}/read-states`.
6. سوییچ به peer، Block، revoke session یا حذف membership؛ تلاش read/send پس از سلب مجوز باید رد شود. برای Minor/Unknown هیچ رابطهٔ اختیاری ایجاد نشود.

## A09 — Messaging P1/P2 (مدیریت، Poll، Checklist، Scheduled، Privacy)

1. گروه/کانال: `POST /api/v1/conversations/groups` و `POST /api/v1/conversations/channels`، join/invitation، حذف عضو، انتقال مالکیت و کنترل authorization فعلی.
2. Poll: `POST /api/v1/conversations/{conversationId}/polls`؛ `GET /api/v1/conversations/{conversationId}/messages/{messageId}/poll`؛ `POST .../poll/vote` و `.../poll/close`. شرط quiz anonymity و answer visibility.
3. Checklist: `POST /api/v1/conversations/{conversationId}/checklists`، `GET .../messages/{messageId}/checklist`، `PUT .../checklist/items/{itemId}`. آزمون optimistic concurrency، وضعیت شخصی در مقابل collaborative.
4. Scheduled: `POST /api/v1/conversations/{conversationId}/scheduled-messages`، `GET .../scheduled-messages`، `POST .../scheduled-messages/{id}/cancel`. `Scheduled` را به معنی `Dispatched` نگیرید؛ worker/revalidation را جدا مشاهده کنید.
5. `GET /api/v1/messaging/privacy` و `PUT /api/v1/messaging/privacy`؛ `POST /api/v1/messaging/blocks/{userId}`; جست‌وجوی `GET /api/v1/messages/search` باید privacy و محدودیت scope را حفظ کند.
6. SignalR مستقل از HTTP catalog: اتصال `/hubs/messaging`، subscribe/reconnect و دریافت Delta؛ برای تست WebSocket/IIS نیاز به client واقعی و شواهد جدا است.
7. منفی: poll/quiz بعد close، stale `expectedVersion`، مدیر گروه فاقد مجوز، deleted topic، bypass `FromJoin`، read receipts و disclosure `Hidden`.

## A10 — فایل و ACL

1. `POST /api/v1/files` با `multipart/form-data` فیلد دقیق `File`؛ `201` و `FileAssetResult.id`.
2. `GET /api/v1/files/{fileId}`؛ `200` و metadata. `GET /api/v1/files/{fileId}/content`؛ byte-for-byte مقایسه با فایل مرجع.
3. `DELETE /api/v1/files/{fileId}?expectedRowVersion=<CURRENT_BASE64>`؛ `200`، سپس درخواست با نسخهٔ کهنه و انتظار conflict معتبر.
4. کاربر/سازمان دیگر نباید فایل خصوصی را دانلود کند؛ malware/oversize/unsupported content و مجوز ACL IIS را مجزا بررسی کنید.

## A11 — Commerce (Idempotency، Order، Payment)

**پیش‌نیاز:** PlanVersion واقعی قابل‌خرید؛ محیط تست سفارش، نه پرداخت Production.

1. `POST /api/v1/commerce/orders` با `{"planVersionId":"<REAL_PLAN_VERSION_ID>","clientOperationId":"<UUID>"}`; ایجاد جدید `201`، `replayed=false`.
2. دقیقاً همان درخواست را تکرار کنید؛ `200`، `replayed=true`، همان `order.orderId`، بدون سفارش دوم.
3. همان `clientOperationId` و `planVersionId` متفاوت: `409` با `ORDER_IDEMPOTENCY_CONFLICT` و بدون سفارش اضافه.
4. `GET /api/v1/commerce/orders/{orderId}` و `GET /api/v1/commerce/orders`؛ تطابق وضعیت، مبلغ/ارز و Workspace.
5. `POST /api/v1/commerce/orders/{orderId}/payments` با `InitiatePaymentCommand` واقعی فقط در محیط تأییدشده. در پیکربندی gateway=Unavailable، status `503` و `PAYMENT_PROVIDER_UNAVAILABLE` نتیجهٔ موردانتظار **سناریوی منفی** است، نه پذیرش پرداخت واقعی.
6. منفی: Order سازمان دیگر، checkout هم‌زمان با یک operationId، plan non-purchasable، idempotency collision، rate-limit؛ status و audit پایدار بررسی شوند.

## A12 — Notification / Device

1. `GET /api/v1/notifications` و `GET /api/v1/notifications/unread-count`؛ `POST /api/v1/notifications/{notificationId}/read`، مطابقت unread count.
2. `GET /api/v1/notification-preferences`؛ `PUT /api/v1/notification-preferences/{category}` با rowversion واقعی؛ آزمون تعارض همزمان و دسته mandatory.
3. `GET /api/v1/notifications/devices/web-push/configuration`؛ امکان بررسی configuration، نه اثبات delivery.
4. `POST /api/v1/notifications/devices/web-push` و `POST /api/v1/notifications/devices` فقط با credentials دستگاه تست؛ `DELETE /api/v1/notifications/devices/{registrationId}`.
5. دریافت Web Push/FCM روی دستگاه واقعی، بستن/بازکردن app و revoked token باید جداگانه ثبت شود: فعلاً **BLOCKED** بدون device/provider evidence.

## A13 — Dashboard / Reports / Exports

1. نقش‌های واقعی: `GET /api/v1/dashboards/manager`، `.../teacher`، `.../student`، `.../parent` با role مناسب و دادهٔ پایه.
2. `GET /api/v1/reports/manager/attendance` و `GET /api/v1/reports/student/grades`؛ مقایسهٔ جمع‌ها با SQL fixture مجاز، نه صرف HTTP `200`.
3. `POST /api/v1/reports/exports` با `CreateReportExportCommand`/DTO واقعی، `GET /api/v1/reports/exports/{jobId}` و پس از آماده‌شدن `GET /api/v1/reports/exports/{jobId}/download`.
4. آزمون export قبل آماده‌شدن، permission role دیگر، limit/rate-limit، فایل ناقص و نگهداری دادهٔ حساس.

## A14 — Platform Backoffice / MFA

1. حساب Platform معتبر: `GET /api/admin/v1/me` و `GET /api/admin/v1/roles`، `GET /api/admin/v1/mfa`.
2. راه‌اندازی MFA تنها روی حساب آزمایش: `POST /api/admin/v1/mfa/enrollment`، `POST /api/admin/v1/mfa/enrollment/confirm`، `POST /api/admin/v1/mfa/verify`; recovery را بدون ثبت secret بررسی کنید.
3. `GET /api/admin/v1/users`، `GET /api/admin/v1/organizations`، `GET /api/admin/v1/audit-logs`، `GET /api/admin/v1/operations/health`.
4. mutationهای Platform روی سازمان/Role/User/Commerce فقط با مجوز، MFA fresh، reason و دادهٔ تست. `POST /api/admin/v1/users/{userId}/suspend` یا reset MFA در Production **اجرا نشود**.
5. منفی: Manager مدرسه روی `/api/admin/v1/*`، MFA غیرفعال/قدیمی، cross-org، درخواست بدون permission و بررسی append-only audit.

## A15 — AI Boundary و Providers

1. `POST /api/v1/ai/chat` با `{"message":"<SAFE_TEST_PROMPT>","conversationId":null}` در حساب مجاز و محیط AI-enabled آزموده شود؛ هیچ chat متن خصوصی را خارج scope نفرستید.
2. `POST /api/v1/ai/exams/{examId}/prepare` و `POST /api/v1/ai/teachers/classes/{classId}/summary` با منابع/role واقعی، خروجی صرف JSON کافی نیست: privacy/grounding/tenant/correlation و واقعی‌بودن provider باید تأیید شوند.
3. در حالت AI disabled انتظار `503` با `AI_FEATURE_DISABLED` یا کد دقیق منتشرشدهٔ provider. timeout/service unavailable/unauthorized جداگانه بررسی شوند.
4. تست smoke محلی، mock یا health سرویس AI به معنی real E2E نیست؛ RAG source lifecycle را ایجاد/اجرا نکنید.

## A16 — بازبینی امنیت و Concurrency عمومی

- **401**: بدون Bearer/نشست باطل‌شده برای endpoint محافظت‌شده.
- **403 یا 404**: User دیگر، Workspace اشتباه، مالکیت نامعتبر، Parent/Manager فاقد مجوز؛ هیچ leak از Person/Message/Exam/Finance.
- **409**: `expectedRowVersion` یا `expectedVersion` کهنه، `clientOperationId` با payload متفاوت؛ دو نفر با Snapshot یکسان و ویرایش همزمان را روی دو session مستقل بازتولید کنید.
- **429**: rate-limit فقط در محیط کم‌هزینهٔ ایزوله و بدون flooding؛ بررسی `Retry-After` برای سیاست‌هایی که آن را برمی‌گردانند.
- **5xx**: Provider ناموجود یا timeout با response code واقعی؛ ادعای success ممنوع. traceId و عدم نمایش stack/secret را چک کنید.
- **Idempotency**: شناسهٔ هر use-case متفاوت است؛ `Idempotency-Key` عمومی در این Backend وجود ندارد.
- **محرمانگی**: هیچ secret یا پیام خصوصی در Postman export، CI logs، فایل سند یا screenshots ذخیره نشود.

---

## معیار نهایی پذیرش

سرویس‌های Core را پس از اجرای **موفق** سناریوهای الزامی، سلامت محیط، تطبیق نسخه و نبود نقص P0/P1 امضا کنید. برای AI واقعی، SMS OTP، Push/FCM، payment gateway، SignalR IIS، TLS/Backup/Restore، زیرساخت scale و Media P3 که شاهد واقعی ندارند به ترتیب **BLOCKED / NOT_RUN / NOT_APPROVED** ثبت کنید. صرف وجود Route یا IntegrationTest به هیچ‌وجه Sign-off Production نیست.
