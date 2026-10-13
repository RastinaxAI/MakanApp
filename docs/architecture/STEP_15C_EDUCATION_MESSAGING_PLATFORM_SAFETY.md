# STEP 15C — عملیات آموزشی، فراداده پیام‌رسان و ایمنی پلتفرم

## هدف

این مرحله read modelهای سراسری و صفحه‌بندی‌شده لازم برای عملیات آموزشی پلتفرم را فراهم می‌کند و صف پایدار `PlatformSafety` ایجادشده در STEP 14D را از طریق Backoffice قابل بررسی می‌سازد. مرز اصلی طراحی این است که دسترسی مدیریتی به فراداده پیام‌رسان یا یک پرونده تعدیل، مجوز عمومی خواندن تاریخچه گفت‌وگوهای خصوصی نیست.

## تصمیم‌های ثابت

- همه مسیرهای این مرحله زیر `/api/admin/v1` هستند و از IAM پلتفرم استفاده می‌کنند.
- read modelهای آموزشی، context سازمان را همراه هر ردیف برمی‌گردانند.
- اتصال‌های Class، Enrollment و TeacherAssignment با کلید مرکب `OrganizationId` انجام می‌شوند تا داده‌های دو سازمان مخلوط نشوند.
- در این مرحله mutation آموزشی جدیدی ارائه نشده است. use case موجودی برای عملیات مدیریتی cross-organization وجود نداشت که بدون دورزدن قواعد Domain قابل استفاده مجدد باشد.
- `platform.education.manage` در catalog و ماتریس نقش‌ها تثبیت شده، اما تا ایجاد یک use case صریح Domain/Application به endpoint متصل نشده است.
- API پیام‌رسان فقط فراداده امن را برمی‌گرداند؛ متن پیام، تاریخچه، اعضای مستقیم، attachmentها و pair کاربران Direct در آن وجود ندارند.
- مجوزی با نام `platform.private_chats.read_all` وجود ندارد و ایجاد نشده است.
- صف مدیریتی moderation فقط پرونده‌هایی را می‌خواند که `QueueScope = PlatformSafety` دارند.
- evidence فقط از مسیر `CaseId + FileAssetId` و رابطه پایدار `ModerationEvidenceAttachment` قابل دریافت است.
- حذف محتوا فقط برای پیام گزارش‌شده همان پرونده، پس از شروع review، با permission جداگانه و audit ممکن است.

## Permissionها و نقش‌ها

| Permission | PlatformOwner | Operations | Support | SecurityAudit | AbuseReviewer |
|---|---:|---:|---:|---:|---:|
| `platform.education.read` | بله | بله | بله | خیر | خیر |
| `platform.education.manage` | بله | بله | خیر | خیر | خیر |
| `platform.messaging.read_metadata` | بله | بله | بله | بله | بله |
| `platform.moderation.review` | بله | خیر | خیر | خیر | بله |
| `platform.moderation.enforce` | بله | خیر | خیر | خیر | خیر |

نقش `Manager` یک نقش سازمانی است و به‌تنهایی هیچ دسترسی Backoffice یا PlatformSafety ایجاد نمی‌کند. نقش `AbuseReviewer` نمی‌تواند سازمان، کاربر یا امور مالی را مدیریت کند. نقش `SecurityAudit` فقط فراداده پیام‌رسان و قابلیت‌های امنیت/ممیزی تثبیت‌شده قبلی را دارد و مجوز enforcement ندارد.

## Endpointهای آموزشی

همه endpointهای زیر به `platform.education.read` نیاز دارند:

- `GET /api/admin/v1/education/classes`
- `GET /api/admin/v1/education/classes/{classId}`
- `GET /api/admin/v1/education/teachers`
- `GET /api/admin/v1/education/enrollments`
- `GET /api/admin/v1/education/assignments`
- `GET /api/admin/v1/education/exams`

فهرست‌ها page و pageSize دارند و می‌توانند بر اساس `OrganizationId` و در موارد مرتبط `ClassId` فیلتر شوند. پاسخ جزئیات کلاس شامل اطلاعات امن کلاس، مدرس‌ها و فراگیران فعال، خلاصه Session، Assignment، Exam و Attendance است. شماره تلفن، credential، token، پاسخ امتحان، submission خصوصی یا جزئیات حساس حضور و غیاب در این read model وجود ندارد.

## فراداده پیام‌رسان

`GET /api/admin/v1/messaging/conversations` به `platform.messaging.read_metadata` نیاز دارد و فقط این داده‌ها را ارائه می‌کند:

- `ConversationId`
- `Type`
- `Scope`
- context سازمان در صورت سازمانی بودن
- `Visibility`
- `Status`
- تعداد اعضای فعال
- تعداد پرونده‌های moderation
- زمان ایجاد

این endpoint مسیر detail یا history ندارد. خواندن `/api/v1/conversations/{id}/messages` همچنان تابع عضویت و authorization عادی پیام‌رسان است و نقش پلتفرمی آن را دور نمی‌زند.

## صف PlatformSafety

endpointهای خواندن و review به `platform.moderation.review` نیاز دارند:

- `GET /api/admin/v1/moderation/cases`
- `GET /api/admin/v1/moderation/cases/{caseId}`
- `POST /api/admin/v1/moderation/cases/{caseId}/start-review`
- `POST /api/admin/v1/moderation/cases/{caseId}/resolve`
- `POST /api/admin/v1/moderation/cases/{caseId}/dismiss`
- `GET /api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}`

فهرست بر اساس status و priority فیلتر می‌شود. جزئیات بر حسب source فقط snapshot پایدار همان `AbuseReport` یا `SafetyIncident`، actionها و evidenceهای متصل به همان پرونده را برمی‌گرداند.

mutationها `ExpectedVersion` مبتنی بر `rowversion` و `Reason` الزامی دارند. state transition از متدهای خود `ModerationCase`، `AbuseReport` و `SafetyIncident` عبور می‌کند و عملیات در transaction با isolation سطح `Serializable` انجام می‌شود.

## Enforcement

`POST /api/admin/v1/moderation/cases/{caseId}/enforce-content-removal` هم‌زمان به دو permission زیر نیاز دارد:

- `platform.moderation.review`
- `platform.moderation.enforce`

این عملیات فقط وقتی مجاز است که:

- پرونده در صف `PlatformSafety` باشد؛
- source آن یک `AbuseReport` دارای پیام پایدار باشد؛
- پرونده قبلاً توسط همان actor وارد `InReview` شده باشد؛
- `ExpectedVersion` معتبر باشد؛
- پیام دقیقاً با `MessageId` و `ConversationId` گزارش متصل به Case پیدا شود.

عملیات، متن پیام را با رفتار Domain موجود `Message.Delete` حذف می‌کند، پرونده و گزارش را Resolve می‌کند، `ModerationActionType.ContentRemoved` می‌سازد و `AdminAuditLog` ثبت می‌کند. endpoint عمومی حذف با `MessageId` دلخواه ایجاد نشده است.

## ممیزی

تمام mutationهای مدیریتی moderation علاوه بر `ModerationAction` یک `AdminAuditLog` append-only ایجاد می‌کنند:

- `platform.moderation.review.start`
- `platform.moderation.resolve`
- `platform.moderation.dismiss`
- `platform.moderation.content.remove`

audit شامل actor، session، CaseId، OrganizationId در صورت وجود، reason، correlation id و خلاصه state قبل/بعد است. متن گزارش یا محتوای پیام در summary ممیزی ذخیره نمی‌شود.

## دیتابیس و Migration

Migration این مرحله:

```text
20261013062956_AddPlatformEducationSafetyOperations
```

تغییرات schema/data:

- افزودن پنج permission ثابت به `platform.PlatformPermissions`
- افزودن mappingهای نقش‌ها به `platform.PlatformRolePermissions`
- توسعه constraint جدول `messaging.ModerationActions` برای مقدار `ContentRemoved = 6`

جدول جدیدی ایجاد نشده است. read modelها از schemaهای موجود Academic، Assessment، Organization و Messaging استفاده می‌کنند.

## مرزهای فعلی

- mutation آموزشی Backoffice عمداً ارائه نشده است.
- Backoffice امکان impersonation ندارد.
- تاریخچه عمومی پیام خصوصی یا جست‌وجوی دلخواه بر اساس MessageId/FileAssetId وجود ندارد.
- enforcement برای `SafetyIncident` بدون پیام پایدار انجام نمی‌شود؛ این پرونده‌ها فقط Resolve یا Dismiss می‌شوند.
- محتوای کامل گفت‌وگو برای تحقیق moderation در این مرحله قابل پیمایش نیست؛ فقط evidence snapshot همان Case نمایش داده می‌شود.
