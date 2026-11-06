# پذیرش Gapهای Backend و قرارداد Summary و فایل — ۲۰۲۶-۱۱-۰۶

این سند ادامهٔ [قرارداد Gapها](BACKEND_GAP_RESOLUTION_20261104_FA.md) و [مدیریت نقش سازمانی](ORGANIZATION_ROLE_MANAGEMENT_20261105_FA.md) است. مبنای بازبینی `main@73d5edcd3101de8f50a6fdb6c2fa1967d0431d2e` بود. وضعیت کد، پذیرش محلی و پذیرش سرور سه مدرک مستقل هستند. وجود API یا تست با Provider آزمایشی، موفقیت سرویس واقعی را ثابت نمی‌کند.

## Baseline و محدوده

Restore و Build مبنا موفق، Warning/Error صفر؛ Unit برابر ۱۰۸۶، Integration برابر ۱۷۵۴ و Architecture برابر ۱۴، مجموع ۲۸۵۴ تست موفق با Failed/Skipped صفر بود. OpenAPI تولیدشده از همین Source دارای ۳۹۱ Path، ۴۴۲ Operation و ۷۱۷ Schema بود؛ SHA-256 فایل مبنا `0936590b2e943564db00b63f85a9a893819a71791e82c11c9eb31ab411f3c4e7` است.

سه فایل Untracked قبلی `tools/ai/provider_diagnostic.py`، `tools/ai/run_exact_ai.py` و `tools/ai/test_provider_diagnostic.py` و چهار appsettings خصوصی و User Secrets خارج از تغییرات این مرحله هستند. Frontend، سرویس AI، MeliPayamak و دیتابیس‌های `MakanApp` و `MakanApp_Test` تغییر نمی‌کنند. آزمون SQL فقط در پایگاه‌های خودکار ایزوله اجرا می‌شود.

## PHASE 5 — قرارداد نهایی BE-404-A

`GET /api/v1/conversations/{conversationId}/summary` با همان Bearer/session موجود و همان `ConversationSummaryResult` پاسخ می‌دهد. Route، DTO و schema جدید ساخته نشده است.

علت 404 قبلی این بود که `MessagingService.GetConversationSummaryAsync` از Workspace انتخاب‌شده مجوز می‌گرفت، در حالی که Global Inbox از مجوز حساب استفاده می‌کرد. اکنون همان `AccountInboxAccessQuery.Conversations` و همان projection/mapping صندوق حساب برای lookup یک شناسه استفاده می‌شود. خواندن Summary هیچ Workspace یا Membership تازه‌ای ایجاد نمی‌کند.

مجوز لازم: حساب و نشست فعال، پروفایل لازم فعلی، Conversation فعال، Participant فعال؛ برای گفتگوی سازمانی، سازمان فعال و Membership و Role جاری **همان سازمان گفتگو**؛ برای فضای رسمی، دلیل دسترسی جاری کلاس/سازمان نیز لازم است. لغو تخصیص مدرس یا Enrollment بدون انتظار برای Worker بعدی، دسترسی را قطع می‌کند. Archive مانع deep-link مجاز نیست. نشست قبل و بعد از lookup دوباره بررسی می‌شود و پاسخ `Cache-Control: no-store` دارد.

انتخاب سازمان B، Summary سازمان A را فقط وقتی مجاز می‌کند که حساب از قبل مجوز واقعی A داشته باشد؛ صرف نقش مشابه در B هیچ دسترسی نمی‌سازد. حساب نامرتبط، شناسهٔ حدسی، عضویت پایان‌یافته، Role/Participant لغوشده یا فضای رسمی بدون رابطهٔ جاری: `404 CONVERSATION_NOT_FOUND`؛ نشست نامعتبر: ۴۰۱.

Summary همان دادهٔ از قبل مجاز Global Inbox را دارد: شناسه/نوع/scope/عنوان/سازمان، هویت امن طرف مقابل، preview محدود فعلی، شمارنده‌ها، وضعیت شخصی، metadata فضای رسمی و قابلیت‌های محاسبه‌شده. تاریخچه، بدنهٔ کامل، فهرست اعضا یا فایل از این مسیر برنمی‌گردد. قابلیت‌های نمایشی اثبات مجوز mutation نیستند؛ عملیات مقصد دوباره مجوز می‌گیرد.

| مسیر | رفتار Workspace |
|---|---|
| `/conversations/account` و `/{id}/summary` | metadata مجاز در سطح حساب، مستقل از Workspace انتخاب‌شده |
| `/{id}` | جزئیات مدیریتی Group/Channel؛ Context کامل فعلی لازم؛ Direct/Saved در این مسیر 404 هستند |
| `/{id}/messages`، ارسال، اعضا، تنظیمات، Moderation | مجوز و Context کامل فعلی حفظ شده است |
| `SubscribeConversation` روی `/hubs/messaging` | نشست و Context جاری؛ Summary موفق مجوز Subscribe نیست |
| فایل سازمانی متصل به منبع | ACL جاری همان منبع و Context لازم؛ URL قدیمی مجوز دائمی نیست |
| فایل سازمانی متصل‌نشدهٔ خود uploader | Context ناسازگار طبق قرارداد موجود `403 FILE_NOT_ALLOWED`؛ فایل کاربر دیگر 404 |

کلاینت پس از Summary سازمانی می‌تواند مسیر انتخاب Workspace موجود را نشان دهد، سپس `POST /api/v1/workspaces/select` و refetch محتوای کامل انجام دهد. تست همان نشست و همان اتصال Hub، تغییر Personal → سازمان دیگر → سازمان صحیح را بررسی می‌کند. Token تازه برای این تغییر الزامی نیست. قرارداد fallback جدید 409 ساخته نشده است.

جریان واقعی:

```text
ConversationsController.GetSummary
→ MessagingService.GetConversationSummaryAsync
→ AccountInboxService.ReadSummaryAsync
→ MessagingOperationContextResolver.PersonalAsync
→ IAccountInboxStore.ReadConversationSummaryAsync
→ EfMessagingStore.ReadConversationsAsync(accountWide: true)
→ AccountInboxAccessQuery / OfficialSpaceAccessQuery / SQL Server
→ ConversationSummaryMapping → HTTP 200 یا خطای موجود
```

Application مالک orchestration و Port است؛ Infrastructure مالک query/ACL داده‌ای موجود و EF است؛ Api فقط transport و no-store/OpenAPI را تعیین می‌کند. Domain تغییر نکرده است. متد Read جدید، mutation یا transaction طولانی اضافه نمی‌کند.

## PHASE 6 — قرارداد و پذیرش فایل

| عملیات | ورودی/خروجی و محافظ |
|---|---|
| `POST /api/v1/files` | `multipart/form-data`، field الزامی `File`؛ `PersonalClassId` اختیاری در form، `workspaceScope` اختیاری در query؛ 201 و `FileAssetResult` |
| `GET /api/v1/files/{fileId}` | metadata مجاز شامل نام، نوع، اندازه، وضعیت، زمان‌ها، uploader/context، SHA-256 و Base64 rowversion؛ بدون StorageKey یا مسیر دیسک |
| `GET /api/v1/files/{fileId}/content` | بایت خام با MIME واقعی، Content-Length و Content-Disposition؛ `private, no-store` و `Vary: Authorization` |
| Range | Range معتبر 206 و Content-Range؛ بازهٔ خارج محدوده 416؛ JSON خطا فایل موفق محسوب نمی‌شود |
| `DELETE /api/v1/files/{fileId}?expectedRowVersion=...` | مالک مجاز و نسخهٔ جاری؛ نسخه باید URL-encode شود؛ 200 و Deleted؛ خواندن/دانلود بعدی 404 |

حد مؤثر تنظیمات این انتشار ۱۰٬۴۸۵٬۷۶۰ بایت است. `Storage:MaxFileSizeBytes` کنترل Application است و FormOptions برابر آن به‌علاوهٔ ۶۵٬۵۳۶ بایت تنظیم می‌شود. IIS/reverse proxy ممکن است محدودیت کوچک‌تری داشته باشد؛ مقدار مؤثر سرور باید جداگانه کنترل شود. سقف پیش‌فرض request filtering IIS برابر ۳۰٬۰۰۰٬۰۰۰ بایت برای این اندازه کافی است، ولی این سند اثبات تنظیم فعلی IIS نیست.

MIMEهای فعلی: `text/plain` با `.txt`، `image/png` با `.png`، `image/jpeg` با `.jpg/.jpeg` و `application/pdf` با `.pdf`. وجود شاخهٔ MP4/MP3 در کد policy به معنی فعال بودن آن‌ها در config نیست. تطابق MIME/extension و signature اولیه بررسی می‌شود؛ اعتبارسنجی کامل ساختار PDF/تصویر یا Antivirus عمومی ادعا نمی‌شود. سقف نام ذخیره‌شده ۲۵۵ نویسه است؛ نام بسیار بلند ممکن است پسوند را پس از کوتاه‌سازی از دست بدهد و 415 بگیرد. نام Client کلید ذخیره‌سازی نیست؛ path traversal و نام تکراری مسیر blob را تعیین نمی‌کنند.

| خطا | رفتار |
|---|---|
| 400 `FILE_EMPTY` | فایل صفر بایت |
| 401 | نشست غایب، نامعتبر یا لغوشده |
| 403 `FILE_NOT_ALLOWED` | scope نامناسب فایل خود uploader، طبق قرارداد فعلی |
| 404 `FILE_NOT_FOUND` | فایل ناموجود/حذف‌شده یا دسترسی نامجاز؛ خطا را به فایل ذخیره نکنید |
| 409 `FILE_NOT_READY` | فایل آمادهٔ دریافت نیست |
| 409 `FILE_IN_USE` | مدرک نگه‌داری‌شده یا اتصال رسمی اجازهٔ حذف ندارد |
| 409 `FILE_ALREADY_DELETED` | حذف مجدد همان رکورد |
| 412 `CONCURRENCY_CONFLICT` | rowversion قدیمی؛ metadata را refetch کنید |
| 413 `FILE_TOO_LARGE` | سقف اندازهٔ Application |
| 415 `FILE_TYPE_NOT_ALLOWED` | MIME، extension یا signature نامعتبر |
| 503 `FILE_STORAGE_FAILED` | شکست واقعی storage؛ موفقیت ساختگی برنگردد |

پوشش آزمون‌ها:

- `FileAcceptanceTests`: فایل واقعی TXT/PNG/PDF، metadata، SHA-256 قبل/بعد، headers، Range/416، حذف و خواندن بعدی، MIME/extension، نام فارسی/تکراری، مرز اندازه و نشست لغوشده.
- `FileStorageEndpointsTests`: no-token، کاربر دیگر، traversal، storage failure، storage key یکتا، نسخهٔ قدیمی حذف، scope سروری و فایل شخصی مستقل از Workspace.
- `AcademicAssignmentAttachmentAcceptanceTests`: upload → draft → submit → reload → دانلود بایت یکسان؛ لغو Enrollment/Guardian و فایل Pending.
- `AcademicExamAttachmentTests` و `AcademicExamAttachmentSecurityTests`: Attempt، lease، revision، deadline، Finalize، race، cross-attempt/cross-org، وضعیت فایل و revocation؛ داشتن FileAssetId هیچ محافظی را دور نمی‌زند.
- `ConversationSummaryWorkspaceTests`: موفقیت Summary به معنی مجوز فایل/تاریخچه/Hub نیست؛ لغو واقعی SQL بلافاصله پنهان می‌شود.

این تست‌ها SQL واقعی ایزوله دارند؛ EF InMemory شاهد SQL نیست. harness پذیرش Kestrel علاوه بر این‌ها سقف واقعی ۱۰ MiB، دانلود پیوست Assignment/Exam و قطع دانلود قدیمی پس از لغو تخصیص مدرس را بررسی می‌کند. نتایج نهایی و تعداد اجرا در گزارش همان بسته ثبت می‌شود.

### مانع قطعی رسانهٔ Messenger

در تنظیمات فعلی هر دو محیط `Messaging:ContentSafetyProvider=Internal` است. `InternalMakanContentSafetyProvider` برای media/attachment تصمیم `ReviewRequired` می‌دهد؛ موتور فعلی متن‌محور است. `Ready` وضعیت storage است و به معنی `Approved/Attachable` برای Messenger نیست.

پاسخ واقعی `422 MESSAGE_SAFETY_REVIEW_REQUIRED` دارای `PendingReview` و `NotApplied` است؛ پیام جدید، binding جدید و مجوز delivery/download گیرنده ساخته نمی‌شود. Retry همان قصد همان reference ایمنی را می‌دهد؛ payload دیگر با همان شناسه Conflict است. مدرک Safety می‌تواند Retained شود و Delete آن `FILE_IN_USE` بدهد؛ این نگه‌داری مجوز گیرنده نیست. هیچ workflow جدید Human Review یا موتور جایگزین اختراع نشده است. تست Adult نیز مانند Minor/Unknown این وضعیت واقعی را تأیید می‌کند؛ سن معیار bypass نیست.

```text
BE01_ROOT_CAUSE_IDENTIFIED=true
BE01_SAFETY_BYPASSED=false
BE01_HUMAN_REVIEW_IMPLEMENTED=false
BE01_PRODUCT_DECISION_REQUIRED=true
BE01_RELEASE_BLOCKING_FOR_ATTACHMENT=true
```

بنابراین PHASE 6 برای **Happy Path کامل رسانهٔ Messenger BLOCKED** است، حتی اگر همهٔ تست‌های حفظ این محافظ سبز باشند. Product باید دقیقاً یکی از مسیرهای تأیید انسانی، موتور async یا رد محصولی رسانه را مصوب کند. تا آن زمان پذیرش کامل Release اعلام نمی‌شود.

## ماتریس باقی فازها

در ردیف‌های بازاستفاده‌شده Source جدید تکراری، Endpoint موازی یا Migration بی‌نیاز ایجاد نشده است. DONE در این جدول به معنی پیاده‌سازی و بررسی Backend محلی است؛ اجرای سرور Test جداگانه لازم است.

| Phase | وضعیت و علت/تغییر واقعی | مرز امنیت/هم‌زمانی و شاهد |
|---|---|---|
| 0 | DONE؛ baseline واقعی ثبت شد | Git، Build، تست و OpenAPI فوق؛ dirtyهای قبلی محفوظ |
| 1 B01 | DONE؛ read model موجود بازاستفاده شد | `EfPersonalClassAdminMembershipStore`، نسخهٔ Membership مستقل از Grant؛ `PlatformPersonalClassMembershipReadTests`؛ Platform فقط به Manager، تفویض subset به Teacher |
| 2 B02 | DONE؛ lookup مستقیم موجود | `PlatformOperationsService` و `EfPlatformOperationsStore`؛ equality پیش از paging، GET بدون write؛ `PlatformProvisioningLookupTests` و replay قصد |
| 3 OPS-01 | DONE در ابزار/SQL ایزوله؛ دادهٔ تاریخی سرور بررسی نشده | `MakanApp.OfficialSpacesMaintenance`، Dry-run/hash/Apply و قفل موجود؛ `OfficialSpacesMaintenanceTests`؛ بدون duplicate، احیای revoke یا انتشار محتوای تاریخی |
| 4 OPS-02 | DONE؛ revision اختیاری و receipt فعلی حفظ شد | `EfOfficialSpacesStore.ReadSourceDeliveryAsync`؛ منبع مجاز بدون event کد PublicationMissing دارد؛ `OfficialSpacesProvisioningTests` و delivery واقعی دو مقصد محلی |
| 5 Messaging | DONE؛ BE-404-A این مرحله | query مشترک مجوز حساب، no-store، آزمون تغییر Workspace و revoke؛ قرارداد بالا |
| 6 Files | BLOCKED برای happy path رسانه؛ سایر مسیرهای فایل مستقل بررسی شدند | `ReviewRequired` علت مصوب؛ تست محافظ موفق، ارسال واقعی رسانه موفق محسوب نمی‌شود |
| 7 دعوت سازمان | DONE؛ read موجود | `SchoolInvitationReadService` و `EfSchoolInvitationReadStore`؛ Manager همان سازمان، pagination و SafeIdentity؛ رضایت Accept مستقل |
| 8 نقش سازمان | DONE؛ پیاده‌سازی قبلی حفظ شد | `SchoolMembershipManagementTests`، Manager→Manager با محافظ آخرین مدیر، قفل مشترک Platform/School، رسید retry و نسخه‌های مستقل |
| 9 کلاس سازمانی | DONE؛ رفتار موجود | `SchoolClassHardeningTests`؛ قصد ساخت یکتا، قفل ظرفیت و rowversion، عدم حذف خودکار Learner |
| 10 B04 | DONE؛ DTO نام‌دار موجود | `PlatformClassDetail.activeLearners` از `PlatformClassPerson`؛ userId nullable برای شخص بدون حساب و flag truncated؛ OpenAPI بررسی می‌شود |
| 11 دعوت PersonalClass | DONE؛ identity موجود | `EfPersonalClassStore.Reads`؛ عنوان کلاس و inviter مبتنی بر Audit؛ هویت Legacy حدس زده نمی‌شود |
| 12 Guardian | DONE؛ identity موجود | رابطه/سازمان جاری؛ هویت امن مجوز چت خصوصی یا نمرهٔ منتشرنشده نمی‌سازد |
| 13 فعالیت مدرس | DONE؛ منبع منتشرشده مرجع است | `EfSchoolAcademicDiscoveryStore.ActivitiesAsync`؛ Manager همان سازمان، بدون body/answer/private content؛ فقدان event تاریخی منبع را حذف نمی‌کند |
| 14 Exam discovery | DONE؛ query موجود | `EfSchoolAcademicDiscoveryStore.ExamsAsync`؛ مدرس با Assignment جاری، فیلتر قبل paging، بدون answer key؛ آرایهٔ قبلی حفظ شده |
| 15 Result | DONE برای قرارداد فعلی؛ گسترش جزئیات Product لازم دارد | release و correction موجود؛ draft correction نمرهٔ منتشرشده را بازنویسی نمی‌کند؛ note خصوصی/پاسخ کودک افشا نمی‌شود |
| 16 Commerce | DONE برای قرارداد Backend؛ درگاه واقعی پذیرش نشده | `EfOrderPaymentStore.Quotes`؛ قیمت/سررسید سروری، PreviewOnly و paymentAvailable از provider؛ redirect موفقیت پرداخت نیست |
| 17 Support/Delete | BLOCKED — PRODUCT DECISION REQUIRED | retention قانونی و workflow تعیین نشده؛ حذف حساب/API فرضی ساخته نشد |
| 18 Web Push | BLOCKED در runtime واقعی | زیرساخت موجود، نبود device/subscription/credential و HTTPS تأییدشده؛ `P1_PUSH_RUNTIME_PENDING` حفظ می‌شود |
| 19 AI | DONE برای حفظ خاموشی | `AiService:Enabled=false` در بسته؛ هیچ Probe، retry یا تماس provider اجرا نمی‌شود |
| 20 Dashboard | BLOCKED برای optimization تازه بدون SLA | readهای موجود حفظ شده؛ summary موازی و cache بدون سیاست تازگی ساخته نمی‌شود |

## OpenAPI، مدل و انتشار

تغییر عمومی این مرحله فقط توضیح رفتار Summary و header عدم cache است؛ DTO، Route، enum، خطای تازه یا request جدید ندارد. Backend تولیدکنندهٔ TypeScript SDK در این مخزن ندارد؛ بنابراین Regenerate/Build یک SDK ناموجود ادعا نمی‌شود و Frontend تغییر نمی‌کند. schema نام‌دار Summary، فایل، ProblemDetails، pagination و activeLearners در OpenAPI و تست قرارداد بررسی می‌شوند.

Model و Migration جدید لازم نیست. آخرین Migration همان `20261104224922_AddSchoolMembershipManagementOperations` و تعداد موجود ۶۴ است. SQL قابل تکرار با exporter موجود تولید و روی LocalDB موقت در حالت fresh و upgrade و اجرای دوم no-op آزمایش می‌شود. اگر سرور همین ۶۴ Migration را دارد، این اصلاح Summary به تغییر schema نیاز ندارد.

Test candidate با `Test`، Mock OTP، Swagger روشن و AI خاموش آماده می‌شود. Production باید Real OTP، Swagger روشن و AI خاموش داشته باشد؛ بستهٔ Production جدید طبق gate این درخواست **پس از پذیرش واقعی Test** تهیه می‌شود. فایل‌های config خصوصی Source دست‌نخورده‌اند؛ خروجی Test خصوصی است و نباید در Git یا دانلود عمومی قرار گیرد. دستورهای اپراتور در [راهنمای انتشار این مرحله](../production/BACKEND_MASTER_TEST_DEPLOYMENT_20261106_FA.md) آمده است.
