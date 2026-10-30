# برنامهٔ Migration، Dry-run و Backfill فضاهای رسمی

این مرحله مجوز اجرای SQL روی `MakanApp`، `MakanApp_Test` یا سرور ندارد. Commit/Push و ساخت Publish خصوصی در فاز نهایی‌سازی جداگانه مجاز شده‌اند؛ Deploy همچنان مجاز نیست. نام Migration جدید:

`20261030093927_AddOfficialOrganizationAndClassSpaces`

مبدأ ارتقای نسخهٔ قبلی: `20261030060025_AddExamAnswerAttachments`، شمارهٔ ۵۵ در زنجیره؛ مقصد شمارهٔ ۵۶ است.

## تغییر اسکیمای افزایشی

- `messaging.OfficialSpaceProvisions`: `Id`، `OrganizationId`، `ClassId?`، `Purpose`، `State`، زمان/تعداد تلاش، دستهٔ خطای محدود و `RowVersion`.
- `messaging.AcademicActivityPublications`: شناسهٔ event، Org/Class، نوع منبع، یکی از سه FK نوع‌دار `LessonId/AssignmentVersionId/ExamVersionId`، revision، User آغازکننده، title و زمان‌های امن UTC.
- `messaging.AcademicActivityDeliveries`: PK مرکب `PublicationId+Purpose`، Conversation/Message نهایی، State و اطلاعات تلاش/concurrency.
- ستون‌های `Conversations.IsOfficialOrganizationSpace=false`، `ConversationParticipants.EndedBySystem=false` و `Messages.AcademicActivityPublicationId=null` به رکوردهای قدیمی مقدار سازگار می‌دهند.

FKها Restrict هستند. CHECKها نوع purpose/state، منبع دقیقاً یکی، lifecycle مشارکت سیستم و shape تحویل را محدود می‌کنند. unique index قبلی کلاس به `(OrganizationId, AcademicClassId, Type)` تبدیل می‌شود تا دقیقاً یک Group و یک Channel ممکن باشد. فضای رسمی Organization نیز `(OrganizationId, Type)` با filter رسمی unique دارد. Publication بر حسب منبع/نسخه unique است؛ Delivery/Message نیز effect تکراری را در SQL منع می‌کنند.

این تغییر Up هیچ داده یا جدول قدیمی را حذف نمی‌کند، پیام کپی نمی‌کند و backfill تجاری/سن/هویتی انجام نمی‌دهد. نام، نوع و FK گروه کلاس قدیمی حفظ می‌شوند. عنوان گفتگو برای اثبات رسمی‌بودن استفاده نمی‌شود.

## Planner فقط خواندنی

`EfOfficialSpacesStore.PlanAsync(ct, organizationId?)` منابع را با ترتیب ثابت بررسی می‌کند و برای هر Organization/Class این موارد را می‌دهد:

```text
SourceKind / SourceId / OrganizationId
GroupCandidateId / ChannelCandidateId
MissingGroup / MissingChannel
Conflict / Action
```

Actionها:

- `NoChange`: هر دو مقصد معتبر موجود.
- `ReuseAndCreateMissing`: binding معتبر با type روشن موجود و فقط مقصد دیگر غایب.
- `CreatePairAfterApproval`: هیچ binding معتبر موجود نیست؛ اجرای عمومی backfill مجاز نشده است.
- `ManualDecision`: منبع غیرفعال، Conversation غیرActive یا SystemManaged سازمانیِ بدون binding صریح و مبهم.

اجرای planner هیچ `SaveChanges`، Outbox یا Ensure ندارد. `organizationId` برای Audit محدود یک سازمان قابل استفاده است. برای دیتابیس بزرگ، اجرای عملیاتی باید بخش‌بندی و با محدودیت بار انجام شود؛ endpoint عمومی اجرای همهٔ منابع اضافه نشده است.

## Reuse و Repair

CLI عملیاتی واقعی در `tools/organization/MakanApp.OfficialSpacesMaintenance` قرار دارد. با `ConnectionStrings__MakanDatabase` تنظیم‌شده در محیط امن اپراتور، اجرای بدون mode یا با `--dry-run` فقط گزارش می‌دهد. برای Apply باید `--organization-id` و `--approved-plan-sha256` همان طرح جاری ارائه شوند؛ hash اشتباه، mode متناقض یا هر conflict پیش از write رد می‌شود. خروجی قبلی بازنویسی نمی‌شود. ابزار schema را نصب نمی‌کند و نیازی به روشن‌کردن API، AI، SMS یا Payment ندارد.

```powershell
dotnet MakanApp.OfficialSpacesMaintenance.dll --dry-run --organization-id <organization-guid> --output plan.json
```

گزارش و hash فقط شناسه‌ها و actionهای لازم را دارند؛ خود hash مجوز کاربر یا جایگزین دسترسی SQL اپراتور نیست. اجرای Apply روی سرور نیازمند مجوز مستقل و طرح واقعی همان دیتابیس است؛ طرح fixture این Release قابل استفاده برای سرور نیست.

گروه قدیمی ساخته‌شده با `POST /api/v1/organization/classes/{classId}/conversation`، اگر `SystemManagedAcademic / OrganizationId / AcademicClassId / Type=Group` واقعی داشته باشد، همان Group می‌ماند. Channel جدا ساخته می‌شود؛ history به مقصد دیگر منتقل نمی‌شود. route قدیمی اکنون فقط Group را انتخاب می‌کند و با وجود Channel هم شناسهٔ قبلی را برمی‌گرداند.

Repair داخلی `EnsurePairAsync` با قفل source و uniqueness SQL کار می‌کند. مقصد Ready تکرار نمی‌شود. مقصد غایب مستقل تکمیل می‌شود؛ دادهٔ موفق rollback نمی‌شود. ابهام با تغییر title یا تغییر نوع حدسی حل نمی‌شود. درخواست/خطای ایمن در Provision و Outbox موجود باقی می‌ماند؛ retry و recovery همان زیرساخت موجود را دارند.

ایجاد خودکار جفت برای **منبع جدید** در transaction همان منبع intent می‌نویسد. Sync رابطه برای منبع قدیمی، مجوز backfill همهٔ کلاس‌ها نیست. این مرحله fixture ایزوله را اجرا کرده؛ اجرای عمومی backfill دادهٔ سرور، نیازمند درخواست و بازبینی جداست.

## تضمین عدم اسپم تاریخی

Migration و planner رویداد انتشار تاریخی نمی‌سازند. ساخت/Repair Conversation هم رویداد Lesson/Assignment/Exam قدیمی را scan نمی‌کند. تنها Publish واقعی بعدی در use case فعلی `AcademicActivityPublication` می‌نویسد. در fixture، گروه/پیام قدیمی و درس منتشرشدهٔ قدیمی حفظ می‌شوند و تعداد Card تاریخی صفر می‌ماند.

## SQL و Validation ایزوله

ابزار موجود `tools/production/export-migration-sql.ps1` با startup پروژه Infrastructure استفاده شده است؛ خروجی امن idempotent هر عملیات guarded را با کامپایل مؤخر اجرا می‌کند تا مشکل compile شدن زودهنگام ستون‌های تازه تکرار نشود.

Evidence این مرحله زیر `.artifacts/official-spaces-20261030-d637b7ce/sql` است:

- `full-safe.sql`: از صفر تا ۵۶.
- `from-attachments-safe.sql`: از ۵۵ تا ۵۶.
- `baseline-55-safe.sql`: فقط برای آماده‌سازی fixture ارتقا، نه اسکریپت انتشار جدید.
- `Validate-IsolatedSql.ps1` و `validation-full.json / validation-upgrade.json`.

هر دو مسیر SQL دو بار روی دیتابیس‌های تازه با نام تصادفی `MakanApp_OfficialSql_<guid>` اجرا شدند. History نهایی ۵۶، اجرای دوم no-op، گروه مصنوعی قدیمی محفوظ و انتشار تاریخی صفر بود. فقط همان دیتابیس‌های متعلق به runner حذف شدند؛ دیتابیس‌های عادی تغییر نکردند. Pending model changes نیز وجود ندارد.

## Rollout و rollback

این سند مجوز اجرا روی سرور نیست. برای انتشار آینده، ابتدا backup و verification history، سپس Migration بازبینی‌شده و binary هماهنگ لازم است. binary قدیمی با فرض «یک Conversation در Class» نباید در کنار فعال‌شدن Channel کلاس استفاده شود.

Down پس از وجود Provision، Publication، Delivery، Channel کلاس، فضای رسمی سازمان یا مشارکت پایان‌یافتهٔ سیستم، با SQL `51131` متوقف می‌شود. حذف history/binding برای برگشت binary مجاز نیست. پس از استفاده، برنامهٔ rollback باید حفظ داده، توقف worker و restore معتبر یا forward fix را جدا بررسی کند. rollback عملیاتی یا حذف دادهٔ کاربر در این مرحله اجرا نشده است.
