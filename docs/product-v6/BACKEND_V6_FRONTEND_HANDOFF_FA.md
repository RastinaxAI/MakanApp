# تحویل قرارداد Backend v6 به Frontend

> بازبینی `2026-11-14`: [قرارداد Personal AI و Knowledge/RAG](PERSONAL_AI_FRONTEND_HANDOFF_FA.md) انتخاب مستقل Assistant، Payer فردی/Trial، منابع و Retry را مشخص می‌کند. Generic Personal Chat با Actor رسمی `Personal` و Scope تهی فعال است؛ پذیرش واقعی Backend→AI Production با200 و یک مصرف انجام شد. Knowledge مسیر مستقل موجود را حفظ می‌کند؛ نقش سازمانی یا Service JWT تازه ساخته نشده است. Backend Deploy نشده است.

بازبینی `2026-11-12`؛ Source مبنا `e5bd3eae658991585ca09f581f6236351857b3b8` به‌همراه تغییرات شاخهٔ `feature/product-v6-backend-alignment`. این سند در مخزن Backend است؛ هیچ Frontend تغییر نکرده است. قرارداد کامل فیلدها و literalهای Enum در [مرجع API](../backend-api-reference.md) و [قرارداد مشترک](../frontend-contract/README.md) ثبت می‌شود. وجود API با آمادگی عملیاتی Provider یا مجوز محصول برابر نیست.

## هویت، Bio و تغییر شماره

`PATCH /api/v1/me/profile` فیلد اختیاری `bio` دارد: حداکثر ۵۰۰ نویسه، Trim، متن ساده؛ حذف فیلد یا `null` مقدار قبلی را حفظ و رشتهٔ خالی آن را پاک می‌کند. پاسخ `CurrentUserResult` و پروفایل عمومی مجاز، Bio را برمی‌گردانند. آن را به‌صورت متن نمایش دهید، نه HTML؛ ACL پروفایل و Block پابرجا هستند.

| Method / Route | ورودی مهم | پاسخ / حالت Client |
|---|---|---|
| `POST /api/v1/me/phone-change/requests` | `clientOperationId`, `newPhoneNumber` | `200 PhoneChangeResult`: `requestId`, `state`, `expiresAtUtc`, `resendAvailableAtUtc`, `completedAtUtc` |
| `POST /api/v1/me/phone-change/requests/{requestId}/verify` | `code` | فقط پس از OTP واقعی شمارهٔ جدید، وضعیت `Completed`؛ سپس `GET /api/v1/me` برای شمارهٔ تازه |

نشست Bearer فعال لازم است. شمارهٔ ورود ایران طبق normalizer فعلی است؛ پذیرش بین‌المللی Address Book را به Login تعمیم ندهید. همان عملیات Request با شناسهٔ ثابت SMS تازه یا تمدید مهلت نمی‌سازد. `PHONE_CHANGE_DELIVERY_UNCONFIRMED` برابر ۴۰۹ یعنی نتیجهٔ تحویل قطعی نیست؛ خودکار loop نکنید. درخواست جدید فقط با شناسهٔ جدید و رعایت محدودیت درخواست ساخته می‌شود. کد غلط ۴۰۰، منقضی ۴۱۰، سقف تلاش/درخواست ۴۲۹، تعارض شماره/نسخه ۴۰۹ و درخواست متعلق به نشست دیگر ۴۰۴ است. هیچ کد یا توکن را Log نکنید. پس از موفقیت، سایر نشست‌ها و Refreshهایشان باطل‌اند؛ نشست جاری و UserId حفظ می‌شوند. Mock Login این Verify را دور نمی‌زند.

## دفترچهٔ ابری خصوصی

دفترچهٔ جدید از `UserContact` حساب‌های پیام‌رسان جداست؛ شمارهٔ غیرعضو به معنی ساخت حساب نیست. هیچ تطبیق خودکار شماره یا merge وجود ندارد.

1. `PUT /api/v1/address-book/devices/consent` با `deviceId`, `consentVersion`, `scope`, `syncEnabled`؛ انتخاب رضایت باید صریح باشد. پاسخ `lastSourceRevision` برای ادامهٔ همان دستگاه است.
2. `POST /api/v1/address-book/sync` با `deviceId`, `clientOperationId`, `sourceRevision`, `entries`؛ حداکثر ۱۰۰ تغییر. پاسخ هر ردیف مستقل `status/serverVersion/errorCode` دارد. موفقیت HTTP به معنی پذیرش همهٔ ردیف‌ها نیست.
3. `GET /api/v1/address-book/delta?deviceId=...&limit=100&cursor=...`؛ `upserts`, `tombstones`, `nextCursor`, `serverVersion`, `hasMore`. شروع بدون cursor برای بازسازی؛ cursor به حساب/دستگاه/رضایت بسته است و نباید میان حساب‌ها جابه‌جا شود.

`EntryId` را Client ثابت می‌سازد؛ تغییر موجود نیازمند `expectedServerVersion` است. هر Entry یک تا ده شماره دارد؛ Unicode digit، قالب ایران و شمارهٔ بین‌المللی معتبر canonical می‌شوند. نام `Manual` از Sync دستگاه بازنویسی نمی‌شود. در `Delete` شناسه Tombstone می‌شود و دستگاه قدیمی حق احیای همان شناسه را ندارد. نسخهٔ مبدا باید برای هر batch جدید افزایش یابد؛ retry دقیق باید همان operation، revision و payload را نگه دارد. رضایت خاموش دادهٔ خصوصی را حذف نمی‌کند، ولی Sync/Delta را می‌بندد. تغییر رضایت cursor قبلی را نامعتبر می‌کند.

`LinkAccount` عملیات جدا و صریح روی Entry موجود است؛ فقط `entryId`, `kind`, `expectedServerVersion`, `linkedAccountUserId` را بفرستید. برای unlink، همان Kind با مقدار `null`؛ Phones/SavedName در این عملیات ارسال نشوند. `Upsert` با حساب مقصد پذیرفته نمی‌شود. این فقط Bookmark خصوصی است و `UserContact`، ContactsAudience، مجوز Direct، Membership یا User/Person را تغییر نمی‌دهد. ACL پروفایل و Block هنگام Link و Delta دوباره بررسی می‌شوند؛ ممکن است `linkCurrentlyVisible=false` و شناسهٔ لینک در پاسخ پنهان شود. نام و شمارهٔ خصوصی را در Analytics/Log نفرستید.

## استقلال زمینه‌ها

Inbox حساب، Contacts، دفترچه و PersonalClass با تغییر School Workspace از بین نمی‌روند. APIهای account-inbox/replay فعلی reuse می‌شوند؛ قابلیت Official Space هنوز به عضویت منبع، Guardian/Teacher assignment و مجوز جاری همان منبع وابسته است. Guardian حق مرور Direct خصوصی کودک ندارد.

`GET/PUT /api/v1/ai/assistant-context` انتخاب مستقل Assistant را می‌خواند/ثبت می‌کند. `kind` یکی از `Personal`, `Organization`, `OrganizationClass` است. PUT شامل `clientOperationId`, `membershipId?`, `role?`, `subjectOrganizationPersonId?`, `classId?`, `expectedVersion?` است. پاسخ `version` برای update بعدی لازم است. نبود انتخاب در GET برابر۲۰۴ است. انتخاب کلاس فقط با مجوز جاری `CanManageClass` یا `CanStudyClass` ممکن است؛ Parent از زمینهٔ Organization و کودک مجاز استفاده می‌کند. عملیات نقش‌محور فاقد scope کلاس، مجاز به استفاده از زمینهٔ کلاس انتخابی نیست. اولین اجرای Client قدیمی زمینهٔ مجاز جاری را یک بار pin می‌کند؛ تعویض مدرسه آن را عوض نمی‌کند. Context لغوشده خطا می‌دهد و fallback نمی‌کند. Payer مجوز داده نمی‌دهد. انتخاب Personal اکنون `upstreamContextSupported=true` و `limitation=null` دارد؛ سرور Actor رسمی Personal را می‌سازد، نه Frontend. Runtime و سهمیه جدا بررسی می‌شوند. PersonalClass AI اضافه نشده و Service Auth دو سرویس خاموش است.

## School Application: انتخاب مدیر و Provisioning متصل

`POST/GET /api/v1/school-applications` و `GET/PUT /api/v1/school-applications/{id}` برای Draft خصوصی دو نوع `LegalOrganization` و `IndividualTeacher` هستند. Create با `clientOperationId`, `kind`, `data`, `documents?` و Update با `expectedVersion`؛ پاسخ `revision/version` و snapshot واقعی همان نسخه را دارد. `documents=null` در Update مدارک قبلی را نگه می‌دارد؛ آرایهٔ خالی مدارک نسخهٔ جاری را حذف می‌کند، نه تاریخچه را. فایل باید از Upload فعلی، متعلق به متقاضی، آماده و در همین پرونده مجاز باشد.

به‌روزرسانی `2026-11-13`: حداقل الزامات هر دو فرم مصوب و Submit واقعی فعال است؛ ظرفیت صفر معتبر است. Cancel/History، Review/NeedsChanges/resubmit و Quote/Order با قیمت سرور موجودند. متقاضی فقط مدیر پیشنهادی است؛ Reviewer هنگام Approved، حساب واجد شرایط را صریحاً انتخاب می‌کند. پرداخت همان Zibal/Verify/Outbox موجود است و پس از آن سازمان، اشتراک معتبر و دعوت مدیر از Provisioning مرحله‌ای ساخته می‌شوند. شکست آماده‌سازی Paid را تغییر نمی‌دهد و پرداخت دوباره لازم نیست. نبود نگاشت قیمت، `SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE` می‌دهد؛ قیمت واقعی هنوز نیازمند تنظیم Admin است. قرارداد دقیق، DTO، وضعیت‌ها و Retry در [Handoff آموزشگاه](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) است. `canSubmit` از داده/مدرک جاری مشتق می‌شود و ثابت false نیست.

## پشتیبانی، حذف حساب و اعلان

| مسیر | قرارداد قابل‌استفاده |
|---|---|
| `POST /api/v1/support/tickets` | `clientOperationId`, `subject` حداکثر۲۰۰، `category` برچسب حداکثر۶۴، `body` حداکثر۸۰۰۰، `fileAssetIds?` حداکثر۱۰؛ ۲۰۱ |
| `GET /api/v1/support/tickets` | فقط پرونده‌های مالک؛ `status?`, `page`, `pageSize` تا۱۰۰ |
| `GET /api/v1/support/tickets/{id}` | پیام‌ها تا۱۰۰؛ ادامه با `afterMessageVersion=nextMessageVersion` و `hasMoreMessages` |
| `POST /api/v1/support/tickets/{id}/messages` | `clientOperationId`, `expectedVersion`, `body`, `fileAssetIds?`؛ تعارض۴۰۹، پروندهٔ غیرمجاز۴۰۴ |
| `POST /api/v1/me/deletion-request` | `clientOperationId`؛ ۲۰۲ ثبت درخواست با `executionEnabled=false` و بدون تاریخ اجرای حدسی |
| `GET /api/v1/me/deletion-request` | وضعیت موجود یا۲۰۴؛ حالت `PRODUCT_LEGAL_DECISION_REQUIRED` به معنی حذف انجام‌شده نیست |
| `GET /api/v1/notifications/{id}/target` | target تایپ‌شده، `apiRoute`, `resourceId`, `requiredOrganizationId?` پس از بررسی مجدد ACL؛ URL آزاد نیست |

Reply روی تیکت Resolved بازگشایی نمی‌کند. Support اجازهٔ مرور دفترچه/پیام خصوصی نمی‌دهد. اعلان عمومی فقط خبر به‌روزرسانی است و متن تیکت/مدرک در Push نمایش داده نمی‌شود. برای target آموزشی، زمینهٔ سازمان/نقش/فرزند صحیح از مسیر معمول انتخاب شود؛ target خودش Workspace را تغییر نمی‌دهد. دسترسی هدف ممکن است بعداً لغو و پاسخ۴۰۴ شود؛ آن را قابل‌دورزدن با Route قدیمی فرض نکنید. Push واقعی هنوز نیازمند credential/device و پذیرش Runtime است.

## قابلیت پیشنهادی خاموش و مرز پذیرش

`Features:PersonalClass:AssignmentsEnabled` و `ExamsEnabled` پیش‌فرض `false` هستند؛ capability مؤثر آن‌ها را خاموش نشان می‌دهد. مسیر generic با resourceId نیز Gate را دور نمی‌زند. Error403 با کد موجود `FEATURE_NOT_ENTITLED` را مطابق قرارداد Commerce نشان دهید. engine، سوابق و آزمون‌های opt-in حفظ شده‌اند. Assignment/Exam سازمانی فعال می‌مانند.

CafeBazaar/Myket به Contract رسمی و تنظیم/mapping معتبر وابسته‌اند؛ iOS و حذف واقعی حساب تصمیم باز دارند. media `ReviewRequired` همچنان برای ارسال/دانلود گیرنده قابل استفاده نیست. هیچ Ready بودن UI، Provider، Deploy یا Full Product از وجود Schema استنتاج نشود. [تصمیم‌های باز](BACKEND_V6_OPEN_DECISIONS_FA.md) مرجع محدودیت‌های فعلی است.
