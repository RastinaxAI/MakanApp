# گزارش پیاده‌سازی و یادگیری Backend v6

> آخرین بخش «Personal AI Activation — 2026-11-14» است: [Handoff شخصی](PERSONAL_AI_FRONTEND_HANDOFF_FA.md)، Actor رسمی Personal و پذیرش واقعی Backend→AI Production. بخش BE19 مورخ2026-11-13 و گزارش‌های School با Commit/محدودیت تاریخی خود حفظ‌اند؛ مانع upstream آن روز با release تازه برطرف شده است. قرارداد School: [Frontend آموزشگاه](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md)، [Admin آموزشگاه](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md) و [تنظیم تجاری](SCHOOL_COMMERCIAL_CONFIGURATION_FA.md).

تاریخ `2026-11-12`؛ مبنا `e5bd3eae658991585ca09f581f6236351857b3b8`، شاخهٔ `feature/product-v6-backend-alignment`. مرجع نیاز: Product Architecture v6، شرح Backend Tasks v6 و override همین Task. سند محصول هدف است؛ این گزارش فقط Source، تست و محدودیت واقعی را بیان می‌کند.

Commit کدِ این گزارش: `e440c0143abee7cc40fb18223922cd3c06e22b26`؛ اسناد در Commit جداگانه نهایی می‌شوند. تمام فایل‌های Runtime در دو دور نهایی یکسان بوده‌اند.

## ۱. خلاصهٔ مرحله

پیش از این مرحله Feature Gate قطعی برای آموزش PersonalClass، Bio، تغییر شمارهٔ امن، دفترچهٔ ابری غیرعضو، پروندهٔ Support، درخواست حذف و زمینهٔ مستقلِ ذخیره‌شدهٔ Assistant وجود نداشت. اکنون این بنیادها و قراردادهای لازم اضافه شده‌اند. ماژول‌های قدیمیِ سالم، engine آموزشی سازمانی، Zibal، Outbox و Storage reuse شدند.

درخواست آموزشگاه اکنون Draft خصوصی، نسخه/مدرک، تصمیم و ACL دارد؛ Submit عمداً تا فهرست الزامی جداگانهٔ مالک بسته است. در نتیجه این مرحله **تمام Product v6 را Product-ready نمی‌کند**: اتصال پروندهٔ تأسیس به Quote/Payment/Provisioning، حذف واقعی، Generic Personal AI و خرید Store هنوز محدودند. جزئیات در [تصمیم‌های باز](BACKEND_V6_OPEN_DECISIONS_FA.md) است.

## ۲. معماری

Domain قوانین وضعیت/نسخه و موجودیت‌ها را نگه می‌دارد و هیچ EF/HTTP/SMS dependency نمی‌گیرد. Application شامل DTO، validation، use case و port اختصاصی هر قابلیت است. Infrastructure قفل و transaction SQL، EF mapping/migration، رمزنگاری، تحویل OTP واقعی و اتصال به Outbox موجود را اجرا می‌کند. Api فقط نشست/HTTP را به use case و نتیجه تبدیل می‌کند.

Dependency flow همان `Api → Application → Domain` و `Infrastructure → Application/Domain` باقی ماند؛ Composition Root در `DependencyInjection` پیاده‌سازی portها را وصل می‌کند. نه Generic Repository، MediatR، CQRS، microservice یا پایگاه دادهٔ دیگری اضافه شد. .NET9/EF9/SQL Server ثابت‌اند.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و دلیل ایجاد | نکتهٔ یادگیری |
|---|---|---|
| `MakanApp.Application/Assessment/PersonalClassAcademicFeatures.cs` | Gate مرکزی دو قابلیت پیشنهادی | Permission داده‌ای جای فعال‌بودن Feature را نمی‌گیرد |
| `MakanApp.Domain/Messaging/AddressBookEntry.cs` | Entry، Phone index، Device/Consent، Binding، Sync state/receipt | غیرعضو نیازمند User ساختگی نیست؛ tombstone با حذف فیزیکی فرق دارد |
| `MakanApp.Application/Messaging/AddressBookService.cs` | قرارداد و validation Sync/Delta/Consent | conflict هر Entry از موفقیت کل batch جداست |
| `MakanApp.Infrastructure/Messaging/AddressBookProtection.cs` | Data Protection موجود، HMAC و canonicalization با libphonenumber | رمزنگاری محتوا و شاخص جستجو دو مسئولیت متفاوت‌اند |
| `MakanApp.Infrastructure/Messaging/EfAddressBookStore.cs` | transaction per-owner، نسخه، cursor، replay و Link خصوصی | ترتیب commit باید مبنای نسخهٔ سرور باشد |
| `MakanApp.Domain/Identity/PhoneChangeRequest.cs` | Challenge مستقلِ purpose-bound و وضعیت Preparing/Ready/Completed/Superseded | OTP ورود/Mock نباید در تغییر Credential قابل‌استفاده باشد |
| `MakanApp.Application/Identity/PhoneChangeService.cs` | orchestration تحویل واقعی خارج transaction و Verify | نتیجهٔ نامطمئن شبکه مجوز resend خودکار نیست |
| `MakanApp.Infrastructure/Identity/EfPhoneChangeStore.cs` | تعویض اتمیک شماره، یکتایی و ابطال سایر نشست/Refreshها | race را با unique index، range lock و rollback حل کنید |
| `MakanApp.Domain/Support/SupportTicket.cs` و `MakanApp.Application/Support/SupportTicketService.cs` | پرونده، پیام، attachment، رسید و use case پشتیبانی | Support با Moderation یا مرور پیام خصوصی یکی نیست |
| `MakanApp.Infrastructure/Support/EfSupportTicketStore.cs` | ACL جاریِ Owner/Support، نسخه و audit | Replay باید مجوز فعلی را هم رعایت کند |
| `MakanApp.Domain/Organization/SchoolApplication.cs` و `MakanApp.Application/Organization/SchoolApplicationService.cs` | دو نوع متقاضی، revision و decision، Submit بسته | state machine موجود به‌معنی تصویب Policy ناتمام نیست |
| `MakanApp.Infrastructure/Organization/EfSchoolApplicationStore.cs` | snapshot رمز‌شده، مدارک و ACL Reviewer | تاریخچهٔ مدرک و نسخه برای ممیزی immutable می‌ماند |
| `MakanApp.Domain/Intelligence/AssistantContextSelection.cs`، `MakanApp.Application/Ai/AssistantContextResolver.cs`، `MakanApp.Infrastructure/Ai/EfAssistantContextStore.cs` | انتخاب صریح مستقل هر نشست با نسخه | School View، Data Context و Payer را یکی نگیرید |
| `MakanApp.Domain/Identity/AccountDeletionRequest.cs`، `MakanApp.Application/Identity/AccountDeletionService.cs`، `MakanApp.Infrastructure/Identity/EfAccountDeletionStore.cs` | ثبت/پیگیری و صفِ فقط‌خواندنی درخواست حذف | بنیاد حذف نباید Retention یا زمان اجرا اختراع کند |
| `MakanApp.Infrastructure/Notification/ProductNotificationWriter.cs` و `ProductNotificationAccess.cs` | اعلان‌های پنج‌گانه روی Outbox فعلی و بازبینی ACL | شناسهٔ dedup باید Resource/Recipient را هم جدا کند |
| `MakanApp.Infrastructure/Notification/EfNotificationTargetResolver.cs` | target داخلی تایپ‌شده و مجاز | Deep link قدیمی مجوز دسترسی فعلی نیست |
| `MakanApp.Api/Controllers/AddressBookController.cs`، `PhoneChangeController.cs`، `AssistantContextController.cs`، `SchoolApplicationsController.cs`، `SupportTicketsController.cs`، `AccountDeletionController.cs` | transport نازک API و صف‌های Admin | Controller هیچ SQL یا OTP rule ندارد |

EF Configurationهای متناظر در `MakanApp.Infrastructure/Persistence/Configurations` و تست‌های `ProductV6*`/Unit در پوشهٔ سه پروژهٔ آزمون هستند؛ schema/route کامل از Snapshot واقعی در [مرجع API](../backend-api-reference.md) و [مدل دیتابیس](../database-current-structure.md) بازتولید شده است.

## ۴. فایل‌های مهم تغییرکرده

- `User`, `IdentityContracts`, `IdentityService`, `UserConfiguration`: Bio اختیاری بدون شکستن request قبلی؛ نبود/Null فیلد، مقدار قدیمی را حفظ می‌کند.
- `UserCredential`: تنها متد کنترل‌شدهٔ تغییر verified phone؛ UserId و سایر هویت/عضویت‌ها عوض نمی‌شوند.
- `AcademicContextResolver`, `PersonalAcademicReadService`, `EfPersonalClassStore`, Permission/History store و worker/Realtime/Notification مرتبط: Gate تنها به مسیرهای PersonalClass، حتی generic resource-id، اعمال شد؛ سوابق و engine حذف نشدند.
- `DependencyInjection`: اتصال portهای جدید، ثبت config Gate و انتخاب جداگانهٔ resolver برای AI؛ resolver عمومی School Workspace عوض نشد. Adapter اصلی ملی‌پیامک و کانفیگ آن تغییر نکردند؛ مسیر phone-change با keyed registration فقط adapter واقعی موجود یا unavailable می‌گیرد.
- `AiChatService`, `AiRoleContextBuilders`, `AiInvocationExecutor`, `AiSourceSelectionService`, `KnowledgeAuthorizationService` و projection/integration: استفاده از context مستقل و revalidation، scope کلاس انتخابی و Payer جدا. `CommercialSubscriptionService` فقط port داخلی Assistant را اضافه کرد؛ API عمومی تجاری همچنان زمینهٔ خودش را resolve می‌کند.
- Storeهای مشخص Organization/Person/Membership/Platform/Order: اعلان را در transaction و Save موجود ثبت می‌کنند؛ hook عمومی DbContext یا Outbox موازی وجود ندارد.
- `EfFileAssetBoundAccessResolver`: ACL پرونده پیش از fallback uploader بررسی می‌شود؛ پس از revoke، uploader بودنِ پشتیبان مجوز باقی‌مانده نمی‌سازد.
- `GlobalExceptionHandler`: کدهای پایدار افزوده شدند؛ Exception/Secret/body خصوصی به قرارداد عمومی یا Log اضافه نشد.
- `appsettings.json` و دو example: فقط Gateهای پیش‌فرض false افزوده شد. Actual Development/Test/Production و secretها تغییر نکردند.
- fixtureهای Migration تاریخی: برای نوشتن User در schema قدیمی از helper موجودِ شناخت ستون‌های همان schema استفاده شد؛ Migrationهای قبلی دست‌نخورده‌اند.
- ابزار docs: برچسب `source@` جای فرض همیشگی `main@` را گرفت؛ source شاخهٔ Feature به‌اشتباه main گزارش نمی‌شود. HTML/PDF و کاتالوگ Markdown بازتولید شدند.

## ۵. جریان اجرای قابلیت‌ها

```text
HTTP → PhoneChangeController → PhoneChangeService → IPhoneChangeStore
→ EfPhoneChangeStore: Prepare/commit → MeliPayamakOtpProvider خارج transaction
→ Ready با hash/salt → Verify و PhoneChangeRequest.Check
→ UserCredential.ChangeVerifiedPhone + Revoke سایر Session/Refresh در یک transaction
→ SQL Server/unique index/rowversion → PhoneChangeResult → HTTP

HTTP → AddressBookController → AddressBookService → EfAddressBookStore
→ Consent/owner lock → canonicalization/AddressBookProtection
→ AddressBookEntry + SyncOperation → SQL commit → safe per-item receipt/Delta

HTTP → AssistantContextController/AI Controller → AssistantContextResolver
→ مجوز جاری Membership/Guardian/Class → scope انتخاب‌شده
→ AiInvocationExecutor/CommercialSubscriptionService → reservation با payer مستقل
→ adapter AI موجود → نتیجهٔ مجاز/grounded و ledger، بدون JWT تازه

HTTP → SupportTicketsController → SupportTicketService → EfSupportTicketStore
→ SupportTicket rule + ACL/ExpectedVersion → FileAsset binding/Audit/Outbox
→ SQL commit → dispatcher موجود → ACL دوباره → Notification/target امن
```

هیچ تماس طولانی SMS یا AI داخل transaction جدید نیست. پیامک phone-change هیچ `OtpChallenge` ورود نمی‌سازد و هیچ مقدار ثابت OTP ندارد. خطای تحویل Prepared باقی می‌ماند و همان operation تماس مجدد نمی‌گیرد؛ با درخواست تازه و محدودیت rate می‌توان مجدداً اقدام کرد.

## ۶. دیتابیس و Migration

شش Migration جدید، بدون backfill حدسیِ تجاری/حقوقی/Retention:

1. `20261112145324_AddProductV6ProfileAndAddressBook`: `Users.Bio` nullable با طول۵۰۰ و شش جدول دفترچه.
2. `20261112151714_AddSupportPrivacyAndAssistantContexts`: چهار جدول Support، درخواست حذف، انتخاب Assistant و پنج permission لازم.
3. `20261112152027_EnforceAssistantAndDeletionSessionOwnership`: FK ترکیبی Session/User برای جلوگیری از اتصال نشست حساب دیگر.
4. `20261112153121_AddVersionedSchoolApplications`: پرونده، revision، مدرک، decision و operation.
5. `20261112160026_ExtendProductNotificationTargets`: گسترش CHECKهای نوع/منبع/دستهٔ اعلان؛ حذف داده ندارد.
6. `20261112165826_AddVerifiedPhoneChangeRequests`: Challenge تغییر شماره، FK هویت/نشست/Credential، hash/salt و snapshot نسخهٔ Credential.

مدل ۱۴۶ جدول و ۷۱ Migration دارد. PKهای مالک/Entry/Device و uniqueهای owner/version، operation و phone index با rowversion و transaction per-owner، از تکرار/گم‌شدن Sync جلوگیری می‌کنند. phone index HMAC است و محتوای دفترچه رمز‌شده؛ مدرک پرونده همان FileAsset موجود است. CHECK وضعیت/انقضا/سقف تلاش، unique pending-user و unique Credential مقصد پشتوانهٔ phone-change هستند. FK ترکیبی نشست، account ownership را در SQL هم تثبیت می‌کند. جهت dependency لایه‌ها از FK دیتابیس استنتاج نمی‌شود.

پذیرش Migration شامل Fresh و Upgrade از Migration65 زیبال، اجرای مجدد، حفظ User/Contact/rowversion قدیمی، خالی‌ماندن دادهٔ تجاری تازه و رد FK نامعتبر است و در هر دو دور نهایی PASS شد. Pending model changes وجود ندارد. فقط LocalDB مخصوص fixture و دیتابیس‌های GUIDدار disposable به‌کار رفته‌اند؛ روی `MakanApp`، `MakanApp_Test` یا سرور چیزی اعمال نشده است. Startup migration نداریم. SQL افزایشی idempotent از Migration65 تا71 در `.artifacts/product-v6-20261112-e5bd3ea/product-v6-from-zibal-idempotent.sql` صرفاً تولید و مرور شد: ۱۸ Create Table، Bio nullable و بدون Drop Table، Delete یا Update تجاری؛ این فایل اجرا نشده است.

## ۷. Endpointها

قرارداد دقیق Method/Route/Request/Response/validation/ACL/error در [Handoff Frontend](BACKEND_V6_FRONTEND_HANDOFF_FA.md)، [Handoff Admin](BACKEND_V6_ADMIN_HANDOFF_FA.md) و [مرجع API](../backend-api-reference.md) آمده است. همهٔ endpointهای جدید نشست معتبر دارند؛ Owner و permission جاری جای selector ارسالی Client را نمی‌گیرند. مهم‌ترین حالت‌ها: conflict نسخه۴۰۹، resource غیرمجاز۴۰۴، challenge منقضی۴۱۰، rate/attempt۴۲۹، provider واقعی unavailable۵۰۳؛ Submit آموزشگاه عمداً۴۰۹ است. ثبت حذف۲۰۲، نبود درخواست/انتخاب Assistant۲۰۴ و Create پرونده۲۰۱ است؛ OpenAPI همین statusهای واقعی را دارد.

## ۸. مفاهیم فنی استفاده‌شده

Purpose separation برای hash تغییر شماره، keyed DI برای جلوگیری از reuse provider Mock، SQL serializable/range lock، optimistic version و rowversion، idempotency receipt، encrypted cursor، tombstone، HMAC index، authorization جاریِ Resource، Outbox پس از commit و Feature Gate مرکزی واقعاً در همین کد استفاده شده‌اند. هیچ Exactly-once delivery یا موفقیت Provider واقعی از تست محلی استنتاج نمی‌شود.

## ۹. تصمیم‌های طراحی و Trade-off

- UserContact ثبت‌شده بازنویسی نشد؛ دفترچهٔ غیرعضو مدل جدا دارد. این کار دادهٔ جدید را از مجوز ContactsAudience جدا نگه می‌دارد، با هزینهٔ persistence و Sync مستقل.
- برای Account Link نه matching شماره و نه mutation هویت انتخاب شد؛ فقط reference خصوصی مصوب و current profile ACL. کاربر هدف هیچ نقش/رابطهٔ تازه نمی‌گیرد.
- برای phone-change، Prepared/Ready به‌جای نگه‌داشتن transaction هنگام SMS انتخاب شد. تحویل نامطمئن به معنی resend خودکار نیست؛ Client باید با وضعیت صریح و محدودیت درخواست کنار بیاید.
- Actor/Session/UserId ثابت و فقط Credential معتبر عوض می‌شود؛ سایر نشست‌ها اتمیک revoke می‌شوند. Unique index و conflict امن از تغییر نیمه‌تمام جلوگیری می‌کنند.
- Assistant context هر نشست جدا pin می‌شود؛ context نامعتبر fallback ندارد. سازگاری Client قدیمی با pin یک‌باره حفظ می‌شود، ولی Generic Personal تا Contract سازگار Upstream باز نمی‌شود.
- Gateها engine را حذف نمی‌کنند. تست اختیاری engine با opt-in صریح باقی ماند تا خاموشی محصول به معنای فرسودگی کد نباشد.
- Requiredness آموزشگاه، قیمت/ظرفیت Legacy، Shared quota و Retention حدس زده نشدند؛ محدودیت صریح بر پیش‌فرض ظاهراً راحت ولی نامعتبر ترجیح داده شد.

## ۱۰. تست‌ها

Unitهای تازه: `PersonalClassAcademicFeaturesTests`, `ProfileBioTests`, `AddressBookTests`, `PhoneChangeRequestTests`, `SupportTicketTests`, `SchoolApplicationTests`, `ProductNotificationTests`. قوانین خالصِ Gate، Bio، canonicalization، hash/انقضا/attempt، state transition و تفکیک dedup را اثبات می‌کنند.

Integrationهای `ProductV6*` با API/EF/SQL واقعی تست می‌کنند: School سالم و Personal خاموش؛ تغییر چهار Role و ثبات Inbox/Contact/Class؛ cloud consent/version/replay/tombstone/IDOR؛ reference خصوصی و Block؛ Bio بدون شکستن request قدیمی؛ تغییر شماره، Mock ممنوع، expiry/attempt/provider failure، رقابت تصاحب و duplicate Verify؛ ACL فایل Support/School و revoke؛ صف حذف بدون execution؛ Assistant pin/class/source/payer و مصرف درست؛ اعلان بدون متن خصوصی/rollback/dedup/target revoke؛ Fresh/Upgrade migration. Assertionهای Log عدم نشت نام/شماره/کد synthetic را هم می‌سنجند. Adapter SMS/AI/Payment در fixture کنترل‌شده است؛ هیچ SMS/پرداخت/AI واقعی اجرا نشده است.

Architecture suite موجود dependencyهای Onion را حفظ می‌کند؛ تست صوری تازه برای افزایش عدد اضافه نشد. تست‌های تاریخی و opt-in engine حذف یا disable نشدند. خطاهای schema قدیمی با helper همان schema حل شدند.

## ۱۱. Validation

نتیجهٔ واقعی نهایی:

| بررسی | دور کامل `verified` | دور تکراری `final` |
|---|---|---|
| Restore | PASS | Source/Package بدون تغییر؛ Restore همان workflow |
| Debug Build | PASS؛ صفر Warning/Error | PASS؛ صفر Warning/Error |
| Release Build | PASS؛ صفر Warning/Error | همان Source ثابت، Release build در workflow اجرا شد |
| Unit | ۱۲۸۵ PASS | ۱۲۸۵ PASS |
| Integration | ۱۸۵۸ PASS؛ ۲۳دقیقه۳۹ثانیه | ۱۸۵۸ PASS؛ ۲۴دقیقه۸ثانیه |
| Architecture | ۱۴ PASS | ۱۴ PASS |
| مجموع | ۳۱۵۷؛ Failed=0، Skipped=0 | ۳۱۵۷؛ Failed=0، Skipped=0 |

افزایش نسبت به baseline تأییدشدهٔ ۳۰۹۲: ۳۵ Unit و۳۰ Integration؛ Architecture موجود حفظ شد. Source/Test/Config طی Validation با SHA-256 ثابت بودند. فرمان‌ها واقعاً روی `MakanApp.sln` اجرا شدند: Restore، Buildهای Release/Debug و Test با `--configuration Debug --nologo --no-build --logger trx`، سپس Final Build و Final Test کامل. `--no-build` از DLLهای همان Build موفق استفاده می‌کند، نه نسخهٔ قدیمی. TRX و logها در `verified-results` و `final-results` نگه‌داری می‌شوند. Zibal و Migration regression جزئی از هر دو suite موفق‌اند؛ تست واقعی خرید/Provider از این نتیجه استنتاج نمی‌شود. ۱۳ تست خالص ابزار اسناد نیز PASS هستند و جدا از۳۱۵۷ شمارش می‌شوند.

در تشخیص‌های قبلی، fixture تاریخی برای ستون Bio به helper schema قدیمی اصلاح شد؛ سپس دو فرض قدیمی تست دربارهٔ «نبود MeliOptions در Mock» با آزمون صحیح جدایی Login و phone-change جایگزین شدند و focused۲۱/۲۱ PASS شد. کنترل عدم ارسال SMS در Login Mock و guard Production حذف نشد. یک اجرای موازی اشتباه focused SQL با suite اصلی نیز باعث حذف دیتابیس ثابت fixture و خطای اتصال شد؛ آن اجرا PASS محسوب نشد. دو دور نهایی بدون fixture موازی تکرار و موفق شدند. افزایش timeout، retry شبکه، تغییر SQL عادی، suppress warning یا disable تست برای این اصلاحات انجام نشده است.

Snapshot نهایی محلی: ۴۲۷ path، ۴۸۳ operation، ۷۷۲ schema، ۹۱ Controller، ۱۴۶ table، ۷۱ Migration؛ SQL/Hosted Worker/SMS/AI خاموش. فایل‌های metadata و log فرمان‌ها زیر `.artifacts/product-v6-20261112-e5bd3ea` خصوصی/ignored باقی می‌مانند.

Validation اسناد نهایی هم PASS شد: پوشش تمام۴۸۳ operation و۷۷۲ schema، بدون لینک شکسته یا اختلاف Enum؛ تطبیق SHA هر PDF با HTML منبع، یکتایی anchorها و نبود صفحهٔ خالی/متن بیرون صفحه کنترل شد. `backend-api-guide`، `backend-api-reference` و `MakanApp-Frontend-Backend-Contract` با Source Commit واقعی بازتولید شدند؛ پنج سند v6 نیز لینک معتبر دارند. این بررسی بدون SQL، SMS یا AI واقعی اجرا شد.

## ۱۲. Git و حفاظت کار قبلی

شاخهٔ پایه `feature/zibal-ipg` و HEAD اولیه برابر `origin/feature/zibal-ipg` در `e5bd3ea` بود. شاخهٔ مجاز `feature/product-v6-backend-alignment` از آن ساخته شد. قبل از تغییر، سه فایل untracked قبلی AI و چهار config واقعی با manifest هفت‌فایلی SHA-256 خصوصی پشتیبان‌گیری شدند. شش فایل عیناً حفظ‌اند؛ `appsettings.json` فقط بلوک Features مصوب را دارد و JSON باقی‌مانده دقیقاً baseline است. فایل‌های AI زیر خارج از scope و Commit می‌مانند:

```text
tools/ai/provider_diagnostic.py
tools/ai/run_exact_ai.py
tools/ai/test_provider_diagnostic.py
```

پس از دو دور کامل موفق و مرور status/diff/cached diff، Commit کد `e440c0143abee7cc40fb18223922cd3c06e22b26` با پیام `feat(product-v6): align backend foundations and authorization` ساخته شد: ۱۳۹ فایل مرتبط از لایه‌ها، migrationها، packageها، config پایه/exampleها و تست‌ها. دو مسیر با اختلاف صرفاً قابل‌نرمال‌سازی پس از Stage هیچ diff واقعی نداشتند و وارد Commit نشدند. هیچ فایل AI قبلی، private config، artifact، bin/obj یا Frontend در Commit نیست.

Remote برابر `origin` با مخزن `https://github.com/RastinaxAI/MakanApp.git` است. Commit اسناد جداست تا Snapshot به Commit واقعی Runtime اشاره کند؛ SHA نهایی شاخه، نتیجهٔ واقعی Push و تطبیق remote در گزارش نهایی Git تحویل می‌شود. این فایل برای جلوگیری از ارجاع دوری، SHA خودش یا Push اجرا‌نشده را پیشاپیش ادعا نمی‌کند. `main` merge نمی‌شود، Force/Reset/Clean/Deploy نداریم و Backup حذف نمی‌شود.

## ۱۳. محدودیت‌های فعلی

Submit آموزشگاه و اتصال تجاری بعدی وابسته به D01؛ حذف واقعی وابسته به سیاست قانونی؛ Generic Personal AI وابسته به Upstream؛ خرید CafeBazaar/Myket فاقد قرارداد server verification/mapping/config تأییدشده؛ iOS باز؛ Shared multi-child quota باز. Gateهای BE25 خاموش، Support reopen و Class restore فرض نشده‌اند. Push Runtime و keyring/backup/restore سرور پذیرش واقعی نشده‌اند. Media ReviewRequired و Library download عادی با سیاست قبلی محفوظ‌اند. هیچ پیامک واقعی، Store purchase، AI runtime یا استقرار سرور PASS گزارش نمی‌شود.

## ۱۴. نکات آموزشی

1. فعال‌بودن Feature، عضویت، permission و quota چهار تصمیم مستقل‌اند؛ فقط یک Role شرط کافی نیست.
2. Idempotency یعنی اثر پایدار همان عملیات؛ در نتیجهٔ نامطمئن شبکه درخواست را کورکورانه تکرار نکنید.
3. Data context مجوز را از payer یا view انتخاب‌شدهٔ UI قرض نمی‌گیرد.
4. Snapshot رمز‌شده و Tombstone ارزش خود را فقط با نگه‌داری کلید و نسخهٔ معتبر حفظ می‌کنند؛ TTL را بدون سیاست نسازید.
5. دادهٔ خصوصی را در event عمومی، Audit summary و Log تکرار نکنید؛ فقط شناسه/کد/correlation لازم را نگه دارید.
6. تست Migration تاریخی باید schema تاریخی را بنویسد؛ ORM جدید برای ستون اضافه‌شده شاهد سازگاری گذشته نیست.
7. خروجی build جدا به‌معنی دیتابیس تست جدا نیست؛ fixtureهای دارای نام DB ثابت را هم‌زمان در چند process اجرا نکنید.

## ۱۵. مسیر پیشنهادی مطالعهٔ کد

برای یک slice کامل: `PhoneChangeController` → `PhoneChangeService`/`IPhoneChangeStore` → `PhoneChangeRequest`/`UserCredential` → `EfPhoneChangeStore` → `PhoneChangeRequestConfiguration` → `AddVerifiedPhoneChangeRequests` → `PhoneChangeRequestTests`/`ProductV6PhoneChangeTests`.

سپس `AddressBookController` → `AddressBookService` → `AddressBookEntry` → `EfAddressBookStore`/`AddressBookProtection` → Configuration/Migration → `ProductV6AddressBookTests`/`ProductV6AccountContextTests`. پس از آن Assistant resolver/executor و تست استقلال Payer، و در پایان Support/Notification/Outbox را بخوانید.

## ۱۶. مرحلهٔ بعد

اول فهرست الزامی دقیق دو نوع School Submit را دریافت کنید؛ سپس در Task محدود، validation مصوب و اتصال Approval→Quote→Payment→Provisioning را روی ماژول موجود بسازید. هم‌زمان‌کردن این کار با فعال‌سازی BE25، حذف واقعی، Store billing یا تغییر AI Contract مجازِ ضمنی نیست. پیش از انتشار بعدی، Migration SQL/Backup و keyring جداگانه بررسی و محیط واقعی با دسترسی مالک پذیرش شود؛ این مرحله Deploy ندارد.

## تکمیل درخواست آموزشگاه — 2026-11-13

### ۱. خلاصهٔ مرحله

مبنای مرحله `1fd122c2bfa7930dbbf0a1325a3c360a2fa451a5` است. پیش از این کار Draft/Revision/Document/Review وجود داشت ولی Submit به تصمیم D01 مسدود و پرونده به Commerce متصل نبود. اکنون حداقل فرم و مدرک دو نوع مصوب اعمال می‌شود؛ Submit/Cancel/History، اصلاح و resubmit همان پرونده، تصمیم دارای دلیل و اتصال server-owned به Quote/Order/پرداخت و intent پایدار آماده‌سازی وجود دارند.

این تحویل تکمیل فنی تا مرز صریح تصمیم/تنظیم است؛ قیمت واقعی و سیاست مدیر اولیه هنوز آماده نیستند. `Approved != Paid != Provisioned` حفظ شده و از متقاضی مدیر فرضی یا اشتراک فردی ساخته نمی‌شود.

### ۲. معماری

Domain: `SchoolApplication` قوانین فرم/مدرک/ظرفیت/وضعیت و `SchoolApplicationHistory` سابقهٔ append-only را دارد؛ `SchoolEstablishmentOffering` نگاشت تجاری و `SchoolOfferingOperation` رسید همان نگاشت‌اند. `Order` و `PlatformProvisioningRequest` فقط مرجع nullable پرونده/پرداخت می‌گیرند؛ Provider/EF/HTTP به Domain وارد نشده‌اند.

Application: `SchoolApplicationService` ورودی، نشست و orchestration را روی portهای موجود انجام می‌دهد و برای تجارت به `IOrderPaymentService` موجود می‌رود. Infrastructure قفل/transaction/EF، binding فایل، snapshot رمز‌شده، notification و fulfillment را اجرا می‌کند. Api فقط Controller و ProblemDetails را تکمیل می‌کند. Dependency همان Api→Application→Domain و Infrastructure→Application/Domain است؛ .NET9/EF9/SQL Server/Controller و Modular Monolith ثابت‌اند.

### ۳. فایل‌های مهم ایجادشده

| فایل | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Domain/Organization/SchoolApplicationHistory.cs` | رویداد واقعی پرونده با actor/revision/version/status/reason/UTC | history با بازسازی حدسی گذشته فرق دارد |
| `MakanApp.Domain/Commerce/SchoolEstablishmentOffering.cs` | نگاشت نوع خدمت و رسید mutation آن | قیمت از Published PlanVersion می‌آید، نه ورودی متقاضی |
| `MakanApp.Infrastructure/Organization/EfSchoolApplicationStore.Workflow.cs` | Submit/Cancel/History/progress | قفل پرونده و ExpectedVersion در همان transaction هستند |
| `MakanApp.Infrastructure/Organization/EfSchoolApplicationStore.Offerings.cs` | تنظیم و خواندن نگاشت با permission School و Commerce | Role سازمانی مجوز فروش/بررسی Platform نیست |
| `MakanApp.Infrastructure/Commerce/EfOrderPaymentStore.SchoolApplications.cs` | resolve پروندهٔ Approved و نگاشت داخل transaction ساخت Order | check بیرون transaction برای قیمت/یکتایی کافی نیست |
| `tests/MakanApp.IntegrationTests/SchoolEstablishmentWorkflowTests.cs` | پذیرش API/ACL/نسخه/تجارت | آزمون با HTTP و SQL واقعی، قرارداد را اثبات می‌کند |
| `tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.SchoolApplications.cs` | adapter واقعی زیبال با HTTP provider ساختگی | خرابی fulfillment نباید پرداخت را دوباره شارژ کند |
| `tests/MakanApp.IntegrationTests/SchoolEstablishmentMigrationAcceptanceTests.cs` | Fresh/Upgrade و حفظ Draft/rowversion قدیمی | migration افزودنی، دادهٔ تجاری/تاریخچهٔ ساختگی نمی‌سازد |

Configuration و Migration متناظر زیر `MakanApp.Infrastructure/Persistence` قرار دارند. دو Handoff جدید مسیر عملی Client/Admin را مستند می‌کنند و هیچ Frontend ساخته یا تغییر نکرده است.

### ۴. فایل‌های مهم تغییرکرده

`SchoolApplication` اکنون ظرفیت منفی را رد می‌کند، صفر می‌پذیرد و Draft نامعتبر را نیمه‌کاره mutate نمی‌کند. `SchoolApplicationService` Submit واقعی و Cancel/History/Quote/Order/Offering را expose می‌کند. `EfSchoolApplicationStore` رویداد/اعلان امن، validation و projection جاری/تاریخی را اضافه می‌کند.

`OrderPaymentService` و partialهای `EfOrderPaymentStore` همان engine موجود را با correlation پرونده گسترش می‌دهند. `EfCommercialStore` نسخه‌های خدمت تأسیس را از خرید عادی جدا می‌کند تا Approval دور زده نشود و pending پرداخت تأسیس، اشتراک فردی حساب را pending نشان ندهد. `EfSubscriptionActivationHandler` در همان transaction Outbox، intent موجود Platform را ثبت می‌کند. `EfPlatformOperationsStore` مسیر عمومی resume این intentِ فاقد سیاست مدیر را می‌بندد. Finance fulfillment، Controller و Error mapping نیز با این قرارداد هماهنگ شده‌اند.

`EfFileAssetBoundAccessResolver` و `ProductNotificationAccess` تضمین می‌کنند Cancel یک Draft هرگز Submitنشده، دسترسی Reviewer به مدرک ایجاد نکند. MeliPayamak، AI، Auth، Private Config، feature gateها و frontend دست‌نخورده‌اند.

### ۵. جریان اجرای قابلیت

```text
HTTP → SchoolApplicationsController → SchoolApplicationService
→ ISchoolApplicationStore / EfSchoolApplicationStore
→ SchoolApplication.SubmissionRequirements/Submit/Cancel
→ SQL Server + revision/history/operation/audit/Outbox → SchoolApplicationResult

HTTP Approved application → SchoolApplicationService.OrderAsync
→ IOrderPaymentService / OrderPaymentService
→ EfOrderPaymentStore.ResolveSchoolPlanAsync → Published PlanVersion
→ Order.BindSchoolApplication + frozen terms → SQL commit
→ PaymentAttempt → ZibalPaymentGateway → callback → server Verify
→ Payment + Order.Paid + Outbox موجود در یک commit
→ EfSubscriptionActivationHandler → PlatformProvisioningRequest.AwaitSchoolManagerPolicy
→ NeedsAttention/INITIAL_SCHOOL_MANAGER_POLICY_REQUIRED
```

هیچ تماس Zibal داخل transaction جدید پرونده نیست. intent مدیر اولیه بدون سیاست جلو نمی‌رود؛ Paid حتی پس از خطای Worker محفوظ و قابل recovery است.

### ۶. دیتابیس

یک Migration: `20261113065124_CompleteSchoolEstablishmentWorkflow`، سه جدول تازه:

- `organization.SchoolApplicationHistory`: PK Id، FK پرونده و actor، unique `(ApplicationId,Version)`؛ readonly در SaveChanges.
- `commerce.SchoolEstablishmentOfferings`: PK نوع پرونده، FK نسخهٔ Plan، Version و rowversion؛ CHECK نوع/نسخه.
- `commerce.SchoolOfferingOperations`: PK `(ActorUserId,ClientOperationId)`، FK User/PlanVersion، fingerprint HMAC و نسخهٔ نتیجه؛ append-only.

ستون nullable `Orders.SchoolApplicationId` و `ProvisioningRequests.SchoolApplicationId/SourceOrderId` بدون backfill اضافه می‌شوند. unique filtered روی Order جاری پرونده، و unique مرجع پرونده/سفارش intent، اثر دوم را می‌بندند. Order لغوشده خارج index جاری است؛ فقط intent منقضی بدون هیچ PaymentAttempt قابل جایگزینی است. تلاش بانکی یا Paid بودن جایگزینی را می‌بندد. CHECK ظرفیت null یا نامنفی و CHECKهای وضعیت/SubmissionPolicy به‌روز شده‌اند؛ Draft قدیمی ناقص به‌صورت حدسی کامل نمی‌شود.

اگر دادهٔ قدیمی ظرفیت منفی داشته باشد، CHECK باید ارتقا را متوقف کند؛ Migration آن داده را بی‌اجازه اصلاح نمی‌کند. هیچ جدول قدیمی حذف، Identity بازنویسی یا قیمت/مدیر/سهمیه‌ای Seed نشده است. فقط disposable LocalDB تست می‌شود؛ SQL تجمعی و Delta این مرحله صرفاً Artifact هستند و روی دیتابیس عادی/سرور اجرا نمی‌شوند.

### ۷. Endpointها

مسیرهای جدید متقاضی: `POST /api/v1/school-applications/{id}/cancel`، `GET /{id}/history`، `POST /{id}/quote`، `POST /{id}/orders`. Submit قبلی اکنون۲۰۰ واقعی و ورودی ناقص۴۰۰ دارد. مسیرهای Admin: `GET /api/admin/v1/school-applications/{id}/history`، `GET /offerings` و `PUT /offerings/{kind}`. DTO، validation، response و permission کامل در دو Handoff آمده‌اند؛ مسیرهای قدیمی پرداخت تغییر نکرده‌اند.

تمام mutationهای پرونده و نگاشت receipt و version دارند. ۴۰۱ نشست، ۴۰۳ permission، ۴۰۴ resource مخفی و ۴۰۹ وضعیت/نسخه/operation/تنظیم تجاری است. کد خانوادهٔ School موجود حفظ و `SUBMISSION_INCOMPLETE`، `NOT_APPROVED` و `COMMERCIAL_TERMS_UNAVAILABLE` افزوده شده‌اند.

### ۸. مفاهیم استفاده‌شده

Optimistic Version همراه SQL UPDLOCK/HOLDLOCK؛ transaction کوتاه Serializable؛ HMAC fingerprint برای payload idempotency؛ snapshot رمز‌شدهٔ فرم؛ FK/CHECK/unique filtered؛ Outbox transaction مشترک؛ تفکیک payment/fulfillment؛ و authorization جاری برای resource واقعی. سیستم جدید repository عمومی، CQRS، broker یا زیرساخت موازی ایجاد نشده است.

### ۹. تصمیم‌های طراحی

نگاشت صریح نوع خدمت به نسخهٔ منتشرشده به‌جای دریافت Price/PlanVersion از متقاضی انتخاب شد. تغییر Enum Purpose ضروری نبود: همان Purchase با correlation اختصاصی و جداسازی نسخه‌های خدمت، engine قدیمی را reuse می‌کند. Quote همچنان Preview موجود است، Order سند frozen است.

سفارش منقضی بدون هیچ تلاش بانکی می‌تواند با intent تازه جایگزین شود؛ حذف سفارش یا تغییر تاریخچه انتخاب نشد. برای هر نوع نتیجهٔ نامطمئن بانکی، recovery بر ایجاد خرید جدید مقدم است. به‌جای فرض متقاضی=مدیر، intent موجود Platform با blocker مشخص نگه داشته شد؛ این رفتار پرداخت را حفظ می‌کند ولی تحویل کامل خدمت به تصمیم Product وابسته می‌ماند.

### ۱۰. تست‌ها

Unitها قوانین فرم/مدرک، ظرفیت منفی و صفر، state transition، لغو نهایی، عدم mutation نیمه‌کاره و intent فاقد سیاست مدیر را می‌سنجند. Integrationها HTTP/SQL/rowversion/receipt، دو نوع، private documents، revoke، حساب معلق/نشست stale، NeedsChanges/resubmit، approve/reject concurrent، قیمت سرور، تغییر نگاشت با Order تاریخی، جداسازی خرید عادی، انقضای بی‌تلاش/دارای تلاش و Outbox failure/retry را پوشش می‌دهند. Migration fresh/upgrade با GUID DB و constraints واقعی آزموده شده است.

تست‌های قدیمی حذف/Skip نشده‌اند؛ انتظار قدیمی Submitِ مسدود به تصمیم، با validation مصوب جدید جایگزین شد. assertion حذف فایل نیز request معتبر RowVersion می‌فرستد تا واقعاً `FILE_IN_USE` را اثبات کند، نه صرفاً خطای ورودی ناقص. آزمون زیبال از adapter واقعی و HTTP handler ساختگی استفاده می‌کند؛ پرداخت واقعی انجام نمی‌شود.

### ۱۱. Validation

Clean Debug/Release، Restore و Buildهای Debug/Release واقعاً موفق شدند؛ Buildهای نهایی منبع آخر نیز Warning=0 و Error=0 دارند. focused نسخهٔ آخر۱۵ Unit و۲۶ Integration موفق است؛ Architecture در filter محدود اجرا نشده و نتیجهٔ واقعی آن از suite کامل پایین می‌آید.

| بررسی | نتیجهٔ منبع آخر `latest-final-results` |
|---|---|
| Restore | PASS |
| Build / Final Build — Debug و Release | PASS؛ صفر Warning/Error |
| Unit | ۱۲۹۸ PASS |
| Integration | ۱۸۸۴ PASS؛۱۸دقیقه۸ثانیه |
| Architecture | ۱۴ PASS |
| مجموع | ۳۱۹۶؛ Failed=0، Skipped=0 |
| Zibal در همین regression | ۱۱۴ PASS؛ شامل دو پذیرش جدید تأسیس |
| ابزار اسناد | ۱۳ Unit PASS؛ جدا از۳۱۹۶ |
| EF model / Migration | pending model changes ندارد؛ Fresh/Upgrade/Delta SQL دوباره در disposable DB پاس شدند |

افزایش نسبت به baseline۳۱۵۷: سیزده Unit و۲۶ Integration، مجموع۳۹ تست. دور کامل اول۳۱۹۴ و دور بعدی۳۱۹۶ PASS شدند؛ تست نوع نامعتبر، دو Reviewer مستقل و replay SQL پیش از دور دوم تقویت شدند. پس از اصلاح نهایی validation طول Submit، Restore→Build→focused tests→Final Build→Full Test دوباره روی منبع ثابت اجرا شد. **همسان‌بودن سورس تمام دورهای قبلی ادعا نمی‌شود**؛ hash چهل فایل منبعِ نسخهٔ آخر، قبل و بعد هر فرمان نهایی برابر است. هیچ SMS/AI/پرداخت واقعی یا موفقیت روی سرور از این تست‌ها استنتاج نمی‌شود.

رگرسیون گستردهٔ ابتدایی سه شکست در تست تاریخی زیبال داشت: Entity کامل Order جدید ستونی را در schema قدیمی۶۵ می‌خواند که هنوز وجود نداشت. projection فقط ستون‌های همان schema اصلاح شد و همهٔ assertionهای مالی/مرجع/rowversion حفظ شدند؛ retest هر سه و full regressionهای بعدی PASS شدند. یک gap validation نیز در review پیدا شد: Submit توضیح بلند را به History با max length۲۰۰۰ می‌فرستاد؛ اکنون همان `Action` مشترک پیش از Store آن را۴۰۰ می‌کند و ورودی۲۰۰۱کاراکتری تست شده است. هیچ تست حذف، Skip یا خطای Provider به موفقیت تبدیل نشد.

Snapshot نهایی از Commit واقعی کد:۴۳۴ path،۴۹۰ operation،۷۷۸ schema،۹۱ Controller،۱۴۹ table،۷۲ Migration؛ SHA256 OpenAPI برابر `c63f7b3f954eb815aa43eab750fef307badf184d23cbc45260285666bb64f860` است. استخراج بدون SQL/HostedWorker/SMS/AI انجام شده است. SQL تجمعی از زیبال و Delta این مرحله در `.artifacts/school-establishment-20261113-1fd122c` قرار دارند؛ این artifactها روی دیتابیس عادی/سرور اجرا نشده‌اند. اجرای SQL در پذیرش Migration فقط داخل GUID LocalDB موقت انجام شده است.

اعتبارسنجی اسناد نهایی PASS است: پوشش همهٔ۴۹۰ operation و۷۷۸ schema، هیچ لینک شکسته یا اختلاف Enum، تطبیق SHA هر چهار PDF با HTML و نبود صفحهٔ خالی/متن بیرون صفحه. `backend-api-guide`، `backend-api-reference`، مدل دیتابیس و `MakanApp-Frontend-Backend-Contract` از همین Snapshot بازتولید شده‌اند؛ دو Handoff تازه نیز به‌صورت مستقل از نظر لینک بررسی شده‌اند. سیزده تست ابزار اسناد در پایان مجدداً PASS شدند.

### ۱۲. Git

شاخهٔ مجاز `feature/product-v6-backend-alignment` و مبنا `1fd122c2bfa7930dbbf0a1325a3c360a2fa451a5` است. Commit کد `b7ac095c3f5c0175254edadd6d5ef35f7b24215d` با پیام `feat(schools): add establishment workflow and commerce boundary` پس از validation و review/cached diff ساخته شد؛۴۰ فایل مرتبط Domain/Application/Infrastructure/Api، Migration و تست را شامل می‌شود. Commit اسناد جداست تا Snapshot به Commit واقعی Runtime اشاره کند؛ SHA نهایی اسناد و نتیجهٔ واقعی Push/remote verification در گزارش نهایی Git اعلام می‌شود، نه پیشاپیش در این فایل.

Remote/upstream جاری `origin/feature/product-v6-backend-alignment` است. سه فایل AI و چهار تنظیم واقعی با manifest خصوصی هفت‌فایلی SHA-256 حفظ شده‌اند؛ artifact، private config، AI قبلی یا Frontend در Commit کد نیست. main merge و Deploy انجام نشده است.

### ۱۳. محدودیت‌ها

`SCHOOL_APPLICATION_COMMERCIAL_TERMS=BLOCKED_CONFIGURATION` و `INITIAL_SCHOOL_MANAGER_POLICY=BLOCKED_PRODUCT_DECISION`. نگاشت/قیمت واقعی تنظیم نشده و مدیر اولیه انتخاب نشده است. بنابراین BE03 در هدف کامل خدمت `PARTIAL` و BE24 کل Product v6 همچنان FAIL است؛ بنیادها و پرداخت/intent در SQL تست شده‌اند، نه ساخت نهایی Organization/Subscription/Manager برای این workflow. ظرفیت مثبت اجباری، فیلد حقوقی/مدرک دقیق‌تر و correction پس از تصمیم نهایی حدس زده نشده‌اند.

### ۱۴. نکات آموزشی

۱. Approval، Verified Payment و Provisioning سه حقیقت مستقل‌اند.
۲. ClientOperationId یک payload مشخص را نمایندگی می‌کند؛ تغییر آن payload conflict است.
۳. محدودیت unique پایگاه داده پشتوانهٔ check نرم‌افزار در خرید/intent هم‌زمان است.
۴. Retry بعد از تغییر قیمت باید رسید تاریخی Order را حفظ کند، نه قیمت روز را جایگزین کند.
۵. لغو Draft نباید آن را به سند قابل مرور Reviewer تبدیل کند؛ state جدید را در همهٔ ACLها بررسی کنید.
۶. تصمیم باز Product را با دادهٔ حدسی یا موفقیت ساختگی حل نکنید؛ boundary امن و قابل recovery بسازید.
۷. خروجی build جدا، دیتابیس تست جدا نیست؛ fixture SQL با نام ثابت در processهای موازی اجرا نشود.

### ۱۵. مسیر مطالعه

`SchoolApplicationsController` → `SchoolApplicationService` → `SchoolApplication` → `EfSchoolApplicationStore`/Workflow/Offerings → `OrderPaymentService` → partialهای `EfOrderPaymentStore` → `EfSubscriptionActivationHandler` → `PlatformProvisioningRequest` → Configuration/Migration → `SchoolApplicationTests` → `SchoolEstablishmentWorkflowTests` → تست‌های Zibal و Migration.

### ۱۶. مرحلهٔ بعد

قیمت/PlanVersion/Quota معتبر دو خدمت با مجوز Admin ثبت و سیاست انتخاب مدیر اولیه تصویب شود. سپس همین intent/Order موجود، بدون شارژ دوم و با دعوت/پذیرش واقعی مدیر، تا Organization/Subscription/Ready تکمیل و پذیرش شود. فعال‌سازی PersonalClass academics، تغییر AI یا حذف واقعی حساب جزو این کار نیست.

## نهایی‌سازی مدیر و Provisioning آموزشگاه

مبنای این بخش `d9f493858b1ae3d1badceb6818d56aa1e34bb1cd` و پرامپت School Application Finalization مورخ `2026-11-13` است؛ محدودیت‌های گزارش قبلی دربارهٔ D17 مربوط به تحویل قبلی‌اند. Commit Runtime: `1804f402be32e4d776216c17fcd76555fc5cf29f`؛ کد این Commit همان نسخهٔ آزمون نهایی است.

### ۱. خلاصه مرحله

پیش از این مرحله، Submit/Review/Quote/Order/Zibal و intent پایدار پرداخت‌شده وجود داشت، اما انتخاب مدیر اولیه مصوب نبود و سازمان/اشتراک/دعوت ساخته نمی‌شد. اکنون Reviewer هنگام Approval حساب نهایی را صریح انتخاب می‌کند و Paid از Outbox موجود به همان Provisioning مرحله‌ای می‌رسد. متقاضی فقط مدیر پیشنهادی است؛ انتخاب دیگری مجاز است و متقاضی خودکار Membership نمی‌گیرد. قیمت واقعی Production همچنان Configuration است؛ دادهٔ تجاری حدسی ساخته نشده است.

### ۲. معماری

Domain در `SchoolApplication` انتخاب نهایی و immutability را حفظ می‌کند؛ `SchoolInitialManagerPolicy` eligibility حساب فعال با پروفایل کامل را تعریف می‌کند. `PlatformProvisioningRequest` correlation و checkpointهای موجود را نگه می‌دارد. Domain وابستگی EF/HTTP ندارد.

Application در `SchoolApplicationService` ورودی/نشست و قراردادهای خواندن و بازیابی را تعریف می‌کند. `ISchoolProvisioningWorkHandler` port هماهنگی کار durable است؛ سیستم Provisioning دوم نیست. Infrastructure همان `EfPlatformOperationsStore` را با partial اختصاصی، SQL lock/transaction و Outbox موجود توسعه می‌دهد. Api فقط دو action مدیریتی و نگاشت خطای پایدار را اضافه می‌کند. جهت وابستگی Onion و .NET9/EF9/SQL Server/Controller/Modular Monolith تغییر نکرده‌اند.

### ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Domain/Organization/SchoolInitialManagerPolicy.cs` | eligibility مشترک انتخاب مدیر | نقش قبلی سازمانی شرط ایجاد onboarding نیست؛ وضعیت جاری حساب مهم است |
| `MakanApp.Application/Platform/ISchoolProvisioningWorkHandler.cs` | قرارداد ثبت intent، صف‌بندی بازیابی و اجرای work | port، وابستگی Application به Worker/EF را حذف می‌کند |
| `MakanApp.Infrastructure/Platform/EfPlatformOperationsStore.SchoolProvisioning.cs` | اتصال Paid به همان درخواست و مرحله‌های موجود | payload فقط RequestId دارد؛ منبع حقیقت دوباره از SQL خوانده می‌شود |
| `MakanApp.Infrastructure/Platform/SchoolProvisioningOptions.cs` | مدت دعوت مدیر با default۷ و validation۱..۳۰ روز | policy قابل تنظیم است و با دعوت Messenger/Guardian مشترک فرض نشده است |
| `MakanApp.Infrastructure/Organization/EfSchoolApplicationStore.Recovery.cs` | تکمیل انتخاب خالی Legacy و retry مجاز | recovery نمی‌تواند Paid بسازد، پلن/قیمت عوض کند یا مدیر نهایی را جایگزین کند |
| `tests/MakanApp.UnitTests/SchoolInitialManagerTests.cs` | قواعد انتخاب/تاریخچه/مهلت/fingerprint | آزمون خالص بدون SQL برای state transition و سازگاری قرارداد |
| `tests/MakanApp.IntegrationTests/SchoolInitialManagerProvisioningTests.cs` | انتخاب جایگزین، شکست واقعی SaveChanges، دو Worker و repair | failure injection باید پس از checkpoint واقعی باشد، نه صرفاً Mock کل قابلیت |
| `docs/product-v6/SCHOOL_COMMERCIAL_CONFIGURATION_FA.md` | مراحل واقعی Catalog/قیمت/Publish/نگاشت | واحد قیمت در DTOهای مختلف را از روی نام endpoint حدس نزنید |

Migration و Designer جدید نیز در `MakanApp.Infrastructure/Persistence/Migrations/20261113103326_FinalizeSchoolManagerAndPaidProvisioning*` هستند.

### ۴. فایل‌های مهم تغییرکرده

`SchoolApplication.cs` مدیر پیشنهادیِ مشتق از متقاضی، مدیر نهایی، تأییدکننده و UTC را دارد؛ Decision/History انتخاب واقعی را همراه revision/version نگه می‌دارند. `SchoolApplicationService.cs` انتخاب صریح را هنگام Approved لازم می‌داند؛ NeedsChanges/Rejected مدیر نمی‌خواهند. فیلد اختیاری مدیر در حالت null از serialization داخلی حذف می‌شود تا fingerprint رسیدهای قبلی عملیاتِ بدون مدیر تغییر نکند.

`EfSchoolApplicationStore` حساب انتخابی و permission/version را در تراکنش بررسی می‌کند؛ deadlock دو Reviewer که حساب یکدیگر را انتخاب می‌کنند، به Conflict۴۰۹ِ بدون اثر تبدیل می‌شود. Workflow/progress اطلاعات Plan/Order/Payment/Provisioning/دعوت و failure را جدا می‌دهند. `EfOrderPaymentStore` eligibility مدیر را در Quote/Order بازبینی و وضعیت Receipt و فهرست سفارش را با Provisioning همسو می‌کند.

`EfSubscriptionActivationHandler` فقط برای سفارش آموزشگاه به port موجودِ جدید متصل می‌شود؛ مسیر عادی اشتراک حفظ است. `EfPlatformOperationsStore.cs` همان مراحل سازمان/اشتراک/دعوت/تکمیل را برای actor سیستمی نیز اجرا می‌کند؛ session جعلی ساخته نمی‌شود. اشتراک آموزشگاه از `Subscription.CreateMonthly` و PlanVersion پرداخت‌شده ساخته می‌شود و Legacy نامحدود نیست. `OutboxDispatcher.cs` همان processor/claim/retry/fence را reuse می‌کند؛ dispatcher یا جدول موازی وجود ندارد. DI همهٔ portهای Platform را به همان instance scoped متصل می‌کند.

### ۵. جریان اجرای قابلیت

```text
HTTP تصمیم → PlatformSchoolApplicationsController.Decide
→ SchoolApplicationService.ReviewAsync → EfSchoolApplicationStore.ReviewLockedAsync
→ SchoolInitialManagerPolicy / SchoolApplication.Decide
→ SQL Server: manager + Decision + History + operation + audit

HTTP Quote/Order → SchoolApplicationService → OrderPaymentService
→ EfOrderPaymentStore.ResolveSchoolPlanAsync → Published PlanVersion → frozen Order
→ Zibal callback → server Verify موجود → Payment + Paid + Outbox
→ OutboxProcessor → EfSubscriptionActivationHandler
→ ISchoolProvisioningWorkHandler.PreparePaidOrderAsync
→ PlatformProvisioningRequest + school.provisioning-requested.v1 در همان تراکنش
→ Commit → OutboxProcessor → EfPlatformOperationsStore.HandleAsync
→ EnsureOrganizationStageAsync → EnsureSubscriptionStageAsync
→ EnsureManagerInvitationStageAsync → EnsureProvisioningCompletedAsync
→ SQL Server + Result/progress → HTTP Response

مدیر منتخب → POST /api/v1/invitations/{id}/accept
→ جریان موجود رضایت/Invitation → Membership + RoleAssignment
```

### ۶. دیتابیس

Migration جدید۹ ستون nullable اضافه می‌کند: سه ستون انتخاب/تأیید مدیر در `organization.SchoolApplications`؛ Proposed/Final در `SchoolApplicationHistory`؛ Proposed/Final/ApplicationVersion در `SchoolApplicationDecisions`؛ و `SourcePaymentId` در `platform.ProvisioningRequests`. nullable بودن برای نگه‌داشتن تاریخچهٔ قدیمی است؛ انتخاب متقاضی یا تاریخ تأیید به‌صورت حدسی backfill نمی‌شود. انتخاب پیشنهادی root محاسبه‌شده است و ستون تکراری ندارد.

FKهای جدید با Restrict به User/Payment هستند. unique index موجود SchoolApplicationId/SourceOrderId درخواست حفظ می‌شود؛ SourcePaymentId نیز filtered unique است. index OrganizationId برای درخواست‌های School به unique filtered تبدیل می‌شود؛ داده یا جدول حذف نمی‌شود. FKهای Organization/Subscription/Invitation و unique SourceOrderId اشتراک قبلی حفظ‌اند. Rowversionهای موجود تغییر ماهیت نمی‌دهند و History همچنان append-only است.

هر مرحله request را با `UPDLOCK,HOLDLOCK` و Serializable می‌گیرد، ref ساخته‌شده را در همان commit ثبت و tracker را پاک می‌کند. Worker دوم ref همان مرحله را می‌بیند و اثر تازه نمی‌سازد. ثبت intent/work با تراکنش Outbox فعلی اتمیک است؛ مرحله‌های طولانی به تراکنش پیام متصل نشده‌اند. هیچ تماس شبکه داخل این مرحله‌های SQL نیست. SQL delta و cumulative فقط تولید و در DB disposable آزموده می‌شوند؛ دیتابیس‌های عادی/سرور تغییر نمی‌کنند.

### ۷. Endpointها

| Method / Route | ورودی و خروجی | validation / authorization / خطا |
|---|---|---|
| `POST /api/admin/v1/school-applications/{id}/decisions` | ActionCommand با `initialManagerUserId` برای Approved؛ SchoolApplicationResult | نشست فعال، SchoolRead+Review، reason، ExpectedVersion، حساب فعال/پروفایل کامل؛ مدیر نامعتبر۴۰۰، stale۴۰۹ |
| `POST /api/admin/v1/school-applications/{id}/initial-manager/confirm` | SchoolInitialManagerCommand؛ detail جاری | فقط Approved قدیمی با Final=null، Read+Review+ProvisioningManage؛ تعویض انتخاب ثبت‌شده۴۰۹ |
| `POST /api/admin/v1/school-applications/{id}/provisioning/retry` | SchoolProvisioningRetryCommand؛ detail جاری | Paid تأییدشده، همان permissionها، reason/version/operation؛ بدون ورودی پلن/مدیر/قیمت؛ unpaid۴۰۹ |
| `POST /api/v1/school-applications/{id}/quote` و `/orders` | SchoolApplicationCommerceCommand؛ Quote/CreateOrderResult | مالک پرونده، Approved، مدیر معتبر، نسخه و نگاشت معتبر؛ صاحب دیگر۴۰۴، تنظیم ناموجود۴۰۹ |
| `GET /api/v1/school-applications/{id}` و GET مدیریتی متناظر | detail/progress با Plan/Payment/refs/failure | owner یا permission جاری؛ ReviewerId فقط در projection مجاز Admin |

خواندن History، پرداخت/Verify/Receipt و Accept Invitation همان قراردادهای موجودند. مقادیر `ProvisioningCompleted` و `ProvisioningNeedsAttention` به OrderFulfillmentState اضافه شده‌اند. `Paid` با failure مالی یکی نیست و `ContactSupportForProvisioning` کاربر را به پرداخت دوباره هدایت نمی‌کند.

### ۸. مفاهیم فنی استفاده‌شده

Transactional Outbox، claim lease/fencing موجود، idempotency مبتنی بر operation/fingerprint، checkpoint، قفل SQL و unique filtered index، optimistic ExpectedVersion/rowversion، Options Pattern و audit سیستمی بدون session جعلی استفاده شدند. این پیاده‌سازی exactly-once بودن تحویل event را فرض نمی‌کند؛ اثر تجاری تکرارپذیر و محدود به یک ref است.

### ۹. تصمیم‌های طراحی

به‌جای Provisioning دوم یا transaction واحد برای تمام مراحل، همان درخواست و مرحله‌های commitشوندهٔ Platform reuse شدند؛ در عوض بازیابی باید checkpoint و failure را صریح مدیریت کند. به‌جای عضویت خام، Invitation موجود حفظ شد؛ متقاضی ممکن است مدیر نباشد و انتخاب مدیر جای رضایت خودش را نمی‌گیرد. مهلت دعوت در اولین ساخت از config محاسبه می‌شود و retry آن را تمدید نمی‌کند. قیمت/Quota واقعی از دادهٔ مصوب Catalog می‌آیند؛ fixture ساختگی تجاری Seed واقعی نیست.

Legacy Approved فاقد مدیر، بدون backfill باقی می‌ماند و فقط انتخاب خالی را می‌توان صریح تکمیل کرد؛ تغییر مدیر نهایی Deferred است. درخواست حذفِ هنوز فاقد اجرای قانونی، وضعیت FinalDeletion ساخته‌شده از حدس نیست. در بازیابی، Paid تغییر نمی‌کند؛ حتی اگر دورهٔ پرداخت‌شده پایان یافته باشد، قرارداد/تاریخ تازه حدس زده نمی‌شود و بررسی تجاری لازم است.

### ۱۰. تست‌ها

Unitهای `SchoolInitialManagerTests` الزام مدیر و Reviewer، نبود mutation ناقص، انتخاب متقاضی/جایگزین، تاریخچهٔ واقعی، immutability، عدم انتساب در NeedsChanges/Rejected، eligibility منفی، deadline ثابت، recovery ref و حفظ شکل fingerprint قبلی را می‌سنجند.

Integrationهای جدید/تکمیل‌شده HTTP و SQL واقعی دارند: مدیر گمشده/ناموجود/ناقص/معلق، تکمیل محدود Legacy، دو Reviewer مستقل با مدیرهای متفاوت و انتخاب متقابل، انتخاب B بدون عضویت خودکار A، پذیرش دعوت idempotent، اعتبار تجاری و سهمیه‌های واقعی، تعلیق مدیر پس از Paid، شکست SaveChanges بعد از سازمان/اشتراک، حفظ NeedsAttention در replay intent، repair audited، Receipt/list همسو، دو Worker و حفظ شناسه‌ها. آزمون زیبال برای هر حالت۱۰ callback تکراری و۱۰ Verify دارد؛ Payment/intent یکتا و paid محفوظ می‌مانند. adapter زیبال واقعی با HTTP stub کنترل‌شده استفاده شده و تماس مالی واقعی وجود ندارد.

Migration acceptance، Fresh و upgrade با Script idempotent تا Migration جدید و replay دوباره را می‌سنجد؛ Draft/Approved قدیمی، rowversion و انتخاب خالی حفظ می‌شوند و ظرفیت/قیمت/تاریخچه/مدیر حدسی ساخته نمی‌شود. Architectureهای موجود جهت Onion را کنترل می‌کنند؛ تست صرفاً برای افزایش شمار ساخته نشده است.

### ۱۱. Validation

Clean و Restore اولیه و Restore نهایی موفق شدند. Release Build نهایی موفق با `Warnings=0` و `Errors=0` بود. Final Full Regression: **۱۳۰۸ Unit +۱۸۹۷ Integration +۱۴ Architecture =۳۲۱۹ Passed**؛ Failed=0 و Skipped=0. source hash تمام۱۴۹۴ فایل C# از زمان Final Build/Test تا بازبینی Commit ثابت ماند.

۱۱۴ تست مرتبط با Zibal در مجموعهٔ نهایی پاس شدند. پیش از دور نهایی،۱۶۲ Integration هدفمند School/Zibal،۳۸ Unit مرتبط و۴ سناریوی نهایی recovery/مرز Worker پاس شدند؛ این‌ها subset پذیرش‌اند و به شمار۳۲۱۹ دوباره اضافه نمی‌شوند. دور اولیهٔ Integration نیز۱۸۹۲ تست و Unit/Architecture اولیه۱۳۰۷/۱۴ پاس داشت؛ اصلاحات نهایی روی دور کامل نهایی سنجیده شدند.

دو تست School Migration، Fresh و upgrade و replay Script idempotent تا `20261113103326_FinalizeSchoolManagerAndPaidProvisioning` را روی LocalDB GUID/disposable پاس کردند؛ `has-pending-model-changes` هیچ اختلافی نداشت. Migration جدید۱ مورد است؛ اجرای آن روی `MakanApp`، `MakanApp_Test` یا سرور **NOT_RUN** است. Source/EF9/SQL Server ثابت‌اند.

Snapshot نهایی:۴۳۶ path،۴۹۲ operation،۷۸۰ schema،۹۱ Controller،۱۴۹ جدول و۷۳ Migration؛ بدون SQL/Hosted Worker/SMS/AI استخراج شد. SHA256 OpenAPI: `ba5e08f0108fe8593e72b0b3c0874a18c7170a10166bd39a32eb656edcd57912`.

SQLهای تولیدشده در `.artifacts/school-finalization-20261113-d9f4938/` هستند: `school-finalization-idempotent.sql` برای۷۲→۷۳، SHA256=`008904663c240e866cf8381060fdbd6b717972069aa9e74dc03429190045be32`؛ و `product-v6-from-zibal-idempotent.sql` برای۶۵→۷۳، SHA256=`6b6cbde5578849e2320559d781fa8b7133a2e419bbcd97a3c1f467df94bb42a0`. هیچ Connection String یا Secret در SQL نیست.۱۳ تست ابزار اسناد و کنترل لینک هفت سند مرتبط نیز پاس شدند.

### ۱۲. Git

مبدأ `d9f493858b1ae3d1badceb6818d56aa1e34bb1cd`، شاخهٔ `feature/product-v6-backend-alignment` و remote `origin` با upstream=`origin/feature/product-v6-backend-alignment` است. پیام Runtime: `feat(schools): finalize initial manager and provisioning policy`. Commit Runtime: `1804f402be32e4d776216c17fcd76555fc5cf29f` شامل۳۱ فایل C# مرتبط است؛ مدل تولیدشدهٔ EF با اختلاف مدل قبلی نیز جدا بررسی شد. اسناد با پیام `docs(schools): finalize commercial and provisioning handoff` در Commit جدا نهایی می‌شوند؛ SHA نهایی و نتیجهٔ واقعی Push/fetch/remote verification در پاسخ پایان Task ثبت می‌شود تا hash خودِ Commit اسناد در همان فایل حدس زده نشود. Merge main و Deploy اجرا نمی‌شوند. سه فایل AI قبلی، private configها و adapter ملی‌پیامک از staging خارج و با backup/hash محافظت شده‌اند؛ هیچ secret واقعی در staged diff کد پیدا نشد.

### ۱۳. محدودیت‌های فعلی

قیمت/PlanVersion/Quota مصوب Production هنوز نیازمند ثبت Admin است و `BLOCKED_CONFIGURATION` باقی می‌ماند؛ صحت کد/E2E تست با تنظیم واقعی محیط یکسان نیست. تغییر مدیر نهایی پس از Approval، اصلاح/بازگشایی تصمیم نهایی، refund، Store billing، اجرای حذف حساب و تصمیم‌های دیگر v6 در این Task ساخته نشده‌اند. BE24 به دلیل سایر محدودیت‌های هدف کامل محصول PASS نمی‌شود. هیچ IIS/سرور/DB عادی، SMS واقعی یا AI runtime در این Task اجرا/فعال نشده است.

### ۱۴. نکات آموزشی

1. «مدیر پیشنهادی»، «مدیر نهایی»، «دعوت»، «Membership» و «RoleAssignment» پنج مفهوم متفاوت‌اند.
2. Paid حقیقت مالی است؛ شکست provision نباید آن را بازگرداند یا کاربر را دوباره شارژ کند.
3. exactly-once اثر با constraint و قفل/checkpoint حاصل می‌شود؛ به exactly-once event delivery تکیه نکنید.
4. payload کمینهٔ Outbox، دادهٔ authoritative را از SQL دوباره resolve می‌کند و اطلاعات خصوصی را تکثیر نمی‌کند.
5. nullable migration برای Legacy می‌تواند درست‌تر از backfill حدسی باشد؛ guard مسیر تازه و repair صریح لازم دارد.
6. افزودن فیلد اختیاری DTO می‌تواند fingerprint قبلی را بشکند؛ serialization داخلی هم بخشی از compatibility است.
7. واحد قیمت Create Version ریال و Update Draft تومان است؛ نام DTO و validation منبع حقیقت‌اند.

### ۱۵. مسیر پیشنهادی مطالعه

`SchoolApplicationsController` → `SchoolApplicationService` → `SchoolApplication` و `SchoolInitialManagerPolicy` → `EfSchoolApplicationStore`/Recovery/Workflow → `EfOrderPaymentStore.SchoolApplications` → `EfSubscriptionActivationHandler` → `ISchoolProvisioningWorkHandler` → `EfPlatformOperationsStore.SchoolProvisioning` و مرحله‌های فایل اصلی → Configuration/Migration → `SchoolInitialManagerTests` → `SchoolInitialManagerProvisioningTests` → آزمون Zibal/Migration.

### ۱۶. مرحلهٔ بعد

Admin طبق راهنمای تجاری، دو خدمت را با قیمت و شرایط مصوب همان محیط تنظیم کند؛ سپس Backup/Migration و پذیرش عملیاتی سرور در Task مجاز جدا انجام شود. تغییر مدیر نهایی یا گسترش مدارک/قواعد حقوقی تصمیم جدا می‌خواهد. فعال‌سازی AI یا PersonalClass academics و حذف واقعی حساب نباید با این کار مخلوط شوند.

## BE-19 — Personal AI و Personal Knowledge/RAG؛ 2026-11-13

این بخش نتیجهٔ مرحلهٔ مستقل بعد از School Finalization است؛ مبنای شروع `fc5b297d20cc1cb97fc3b75a1762427bf81e419e`. گزارش‌های پیشین با شواهد تاریخی خود حفظ می‌شوند. **BE19_AI_GATEWAY=PARTIAL**؛ مرز Generic Personal Chat به قرارداد خارجی وابسته است و با تست داخلی PASS اعلام نمی‌شود. [Handoff کامل](PERSONAL_AI_FRONTEND_HANDOFF_FA.md).

### ۱. خلاصهٔ مرحله

قبل از این مرحله، Assistant Context و Knowledge شخصی وجود داشتند، اما Executor عمومی به actor سازمانی وابسته بود و Chat شخصی صرفاً403 نقش می‌داد. اکنون Intent شخصیِ بدون Role، دامنهٔ Personal و انتخاب Payer در همان Executor/Commerce قابل اعتبارسنجی‌اند؛ API Chat برای حساب عادی، مانع واقعی upstream را با409 مشخص و بدون تماس/هزینه اعلام می‌کند. Knowledge/RAG شخصی موجود reuse و با پذیرش مستقل Context/Quota/Concurrency تقویت شد. Client خارجی جدید، نقش جعلی و موتور سهمیهٔ موازی ساخته نشد.

### ۲. معماری

Domain تغییر نکرد؛ مدل تجاری و SQL موجود حقیقت رزرو هستند. Application شامل `PersonalAiChatIntent`، قرارداد مشترک حداقلی `IAiInvocationRequest`، کنترل Scope/Payer و سنجهٔ اجرای Use Case است. Infrastructure/client/تنظیمات provider تغییر نکردند؛ adapter واقعی Knowledge در تست با transport کنترل‌شده اجرا می‌شود. Api فقط نگاشت خطا و OpenAPI را دقیق‌تر کرد؛ Controllerها thin و بدون منطق تازه ماندند. جهت وابستگی Domain ← Application ← Infrastructure و Api به Composition Root فعلی حفظ شد.

### ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Application/Ai/PersonalAiChatIntent.cs` | Intent داخلی حساب عادی با Scope تهی و مرز صریح قرارداد خارجی | مدل داخلی نباید برای تطبیق با DTO خارجی، Role آموزشی جعل کند. |
| `MakanApp.Application/Ai/AiExecutionTelemetry.cs` | شمار و latency اجرای Use Case با labelهای محدود | واحد اجرای Application با HTTP transport و واحد مصرف مالی یکی نیست. |
| `tests/MakanApp.UnitTests/Ai/PersonalAiInvocationTests.cs` | هویت/Scope/Payer/fingerprint و عدم نشت tag | آزمون pure rule نیازی به SQL یا Provider ندارد. |
| `tests/MakanApp.IntegrationTests/PersonalAiAcceptanceTests.cs` | حساب عادی و Executor با ledger واقعی SQL | delegate کنترل‌شده، پذیرش قرارداد wire واقعی نیست. |
| `tests/MakanApp.IntegrationTests/PersonalKnowledgeAcceptanceTests.cs` | HTTP/adapter/SQL برای ACL جاری و RAG شخصی | reauthorization و رقابت باید فراتر از Mock یک service بررسی شوند. |
| `docs/product-v6/PERSONAL_AI_FRONTEND_HANDOFF_FA.md` | قرارداد عملی، خطا، Retry و محدودیت خارجی | Handoff باید مرز target و implementation را آشکار کند. |

### ۴. فایل‌های مهم تغییرکرده

`AiServiceContracts`، request پایه را به شناسه/Correlation محدود و actor wire را در `IAiServiceRequest` نگه می‌دارد؛ DTOهای خارجی قبلی تغییر نکردند. `AiInvocationContracts` و `AiInvocationExecutor` Personal را فقط با هویت همین حساب/نشست، Scope تهی و Payer مجاز می‌پذیرند؛ branch سازمانی و الگوریتم fingerprint سازمانی حفظ‌اند. `AiChatService` نقش عمومی اختراع نمی‌کند و پیش از Reserve خطای Contract می‌دهد. `AssistantContextResolver` همان limitation را از constant مشترک می‌گیرد.

`KnowledgeIntegrationService` فقط سنجهٔ کمینهٔ Retrieve/RAG را اضافه می‌کند؛ hashing/pinning، رزرو و سازگاری عملیات قبلی عوض نمی‌شوند. `GlobalExceptionHandler` کد limitation را409 می‌کند. `AiInvocationOperationTransformer` پیش‌فرض Payer و محدودیت Chat را توضیح می‌دهد. تست‌های قبلی Personal/Revoked با انتخاب مستقل Assistant و ExpectedVersion واقعی اصلاح شدند؛ تست Telemetry حمل‌ونقل فقط instrumentهای `makan.ai.client.*` را رصد می‌کند تا metric موازیِ اجرای Application را اشتباه جزو transport حساب نکند. allowlistهای حساس تضعیف نشدند.

Audit تنظیم پایه یک اختلاف با سیاست مصوب نشان داد: `UseServiceAuthentication` در JSON وجود نداشت و fallback قدیمی Business AI برابرtrue بود. فقط `AiService:UseServiceAuthentication=false` و `AiKnowledge:UseServiceAuthentication=false` در `MakanApp.Api/appsettings.json` صریح شدند؛ Test/Production آن‌ها را از پایه می‌گیرند. هیچ مقدار خصوصی، Provider، timeout، Runtime Enabled یا default کد legacy عوض نشد؛ دو Unit Test مستقل Test/Production این Binding و خاموش‌ماندن Runtime را کنترل می‌کنند. فایل‌های خصوصی Development/Test/Production و credentialها byte-identical باقی می‌مانند. Backup پایه نیز حفظ شده و مقایسهٔ ساختاری JSON ثابت می‌کند اختلاف فقط همین دو فلگ است.

### ۵. جریان واقعی اجرا

```text
Chat HTTP → AiController → AiChatService
→ AiSourceSelectionService → AssistantContextResolver
→ PersonalAiChatIntent + AiInvocationContext
→ PersonalAiChatContract.EnsureSupported
→ GlobalExceptionHandler → 409؛ بدون Provider/Reserve

Knowledge HTTP → KnowledgeIntegrationController → KnowledgeIntegrationService
→ KnowledgeAuthorizationService → AssistantContextResolver + EfEducationalSourceStore
→ AiSourceSelectionOperation pin + CommercialSubscriptionService
→ EfCommercialStore / SQL Server UsageReservation
→ AiKnowledgeClient → Retrieve → بازبینی ACL → RAG → Consumed
→ بازبینی مجدد ACL → Result → HTTP Response
```

در پذیرش داخلی Executor، `PersonalAiChatIntent` به `AiInvocationExecutor` داده می‌شود و invoke delegate ساختگی است؛ این Endpoint تازه یا اتصال واقعی Personal Chat نیست. همان ReserveForAssistant و Finalize موجود استفاده می‌شوند.

### ۶. دیتابیس

Migration جدید ایجاد نشد و جدول/ستون/constraint تغییر نکرد. `AssistantContextSelection` به User/Session، pin منبع به User/Operation/نسخهٔ دقیق و UsageReservation به Owner/Consumer/Context/Payer/hash متصل می‌مانند. unique/transaction موجود رقابت دو رزرو و Retry را کنترل می‌کند؛ شبکه داخل transaction رزرو اجرا نمی‌شود. حالت‌های Reserved، Consumed، Released و Unknown عوض نشده‌اند. دیتابیس‌های اصلی `MakanApp` و `MakanApp_Test` و سرور تغییر نمی‌کنند.

### ۷. Endpointها

Endpoint تازه‌ای اضافه نشد. `PUT /api/v1/ai/assistant-context` با Kind/ClientOperationId و برای تغییر با ExpectedVersion، انتخاب همین Session را ذخیره می‌کند؛ Role/Membership فقط selector هستند. `POST /api/v1/ai/chat` با Message/ConversationId/OperationId و Headerهای موجود، برای Personal فعلاً409 Contract می‌دهد. مسیرهای نقش‌محور همچنان مجوز جاری سازمان/دانش‌آموز/والد/مدرس/مدیر می‌خواهند.

`POST sources/{id}/versions/{version}/knowledge/ingest` و `GET .../status` آماده‌بودن نسخهٔ دقیق را کنترل می‌کنند؛ `POST /api/v1/ai/knowledge/retrieve` evidence و `POST /api/v1/ai/knowledge/rag/answer` پاسخ grounded می‌دهند. Knowledge از Payer و ClientOperationId در body استفاده می‌کند. Backend authentication نشست، ACL current، حق تجاری و readiness اجباری‌اند.404 منبع نامجاز را conceal،409 نسخه/Context/intent/نتیجهٔ نامعلوم و422 Query نامعتبر را مشخص می‌کند. جزئیات کامل Request/Response/UX در Handoff آمده است.

### ۸. مفاهیم فنی استفاده‌شده

تفکیک identity از role، Actor سازمانی از intent شخصی، انتخاب per-session از Workspace، Payer از data authorization، fingerprint از requestId، و measurement اجرای Use Case از شمار HTTP. Retry امن به معنی حفظ OperationId و عدم تکرار اثر است؛ بازگرداندن دوبارهٔ متن خصوصی با idempotency یکی نیست.

### ۹. تصمیم‌های طراحی

قرارداد زندهٔ `/openapi.json` فقط Student/Parent/Teacher/Manager و Actor.role اجباری دارد؛ آدرس اعلام‌شدهٔ `/api/v1/openapi.json`404 بود. به‌جای تغییر سرویس خارجی، ارسال Student جعلی یا فرض enum تازه، مرز409 صریح شد. SHA256 سند دریافت‌شده `fc6bf405aefcc2232e0e35503fbee43fd82313d785fe41658bd474f8ad61277b` است. این مانع D11 خارجی است و تصمیم محصولی تازه نیست.

Knowledge شخصی قرارداد role-free موجود دارد و مستقل تست می‌شود؛ legacy selectedSourceIds در Business Chat هنوز RetrievalAvailable=false است. تغییر هش Knowledge یا Payer پیش‌فرض ذخیره‌شده برای عملیات قبلی انجام نشد تا replayهای قبلی نشکنند. Service Authentication هر دو سرویس خاموش باقی ماند؛ Authentication حساب Backend حذف نشد. درخواست حذف حساب request-only است و قانون اجرا/retention تازه حدس زده نشد.

### ۱۰. تست‌ها

Unitهای جدید، foreign actor/session/user و هر selector سازمانی در Personal، Scope تهی، نبود Role wire، Payer Individual/Trial/ممنوعیت Organization، fingerprint و label امن/failure سنجه را می‌سنجند. تست Chat حساب عادی باید limitation بگیرد و Provider را نخواند.

Integrationهای Personal AI با SQL واقعی، ledger Individual، Trial صریح، فقدان Subscription، سهمیهٔ صفر، replay completed، payload conflict، رقابت یک intent، Unknown بدون retry و Released پیش از invocation را ثابت می‌کنند؛ تغییر School Workspace یا Session دیگر Context شخصی را عوض نمی‌کند. Integrationهای Knowledge از adapter واقعی و transport کنترل‌شده استفاده می‌کنند: owner شخصی بدون نقش، scope ingest بدون org/class، empty BusinessContext، نبود Authorization سرویس، Individual/Trial، رقابت RAG، تغییر Context حین Retrieval، حساب suspended، منبع archived و منع منبع سازمانی در Personal.

پذیرش صریح هر۱۱ Endpoint آموزشی با حساب عادیِ دارای اشتراک Individual، منع Role-specific را بدون وابستگی به فقدان اشتراک می‌سنجد؛ هیچ Reserve یا تماس Provider نباید رخ دهد.

تست‌های قبلی Source Selection، cross-user/class/organization، Library، readiness، Role-specific AI، lifecycle و Lost ACK حفظ‌اند. Architectureهای موجود جهت Onion را بررسی می‌کنند؛ هیچ تستی غیرفعال یا با انتظار success جعلی جایگزین نشده است.

### ۱۱. Validation

Clean و Restore موفق شدند؛ Final Release Build با **۰ Warning و۰ Error** پاس شد. Final Full Regression: **۱۳۲۵ Unit +۱۹۲۲ Integration +۱۴ Architecture =۳۲۶۱ Passed**، Failed=0 و Skipped=0. نسبت به baseline۳۲۱۹، تعداد۴۲ تست مرتبط اضافه شد:۱۷ Unit و۲۵ Integration. همهٔ۱۱۴ تست Zibal نیز در همین مجموعه پاس شدند. هیچ تستی حذف یا غیرفعال نشد؛ فایل‌های۱۴۹۹گانهٔ C# از Manifest نهایی تا پایان Build/Test و بازبینی Commit ثابت ماندند.

Migration جدید صفر است و `has-pending-model-changes` هیچ اختلافی نشان نداد. SQL فقط دیتابیس خودکار و ایزولهٔ تست بود؛ `MakanApp`، `MakanApp_Test` و سرور تغییر نکردند. Snapshot نهایی۴۳۶ path،۴۹۲ operation،۷۸۰ schema،۹۱ Controller،۱۴۹ جدول و۷۳ Migration دارد؛ SHA256 OpenAPI: `98541362177563580f24c70be9bc98311df085db83dfe6448622156309c71b46`. Snapshot بدون DB/Hosted Worker/SMS/AI استخراج شد؛۱۳ تست ابزار مستندات و اعتبارسنجی لینک/Enum/مرجع API پاس شدند.

سه شکست اولیهٔ انتظار/آماده‌سازی تست با کد واقعی Subscription/ExpectedVersion/Assistant مستقل تطبیق یافتند؛۷۵ Integration متمرکز و۱۱ پذیرش Role پس از اصلاح پاس شدند. برخورد listener تست transport با metric جدید در دور کامل اولیه شناسایی شد؛ listener به instrumentهای متعلق به همان transport محدود و کل دور نهایی دوباره اجرا شد. اعداد subsetها به۳۲۶۱ دوباره اضافه نمی‌شوند.

### ۱۲. Git

مبدأ `fc5b297d20cc1cb97fc3b75a1762427bf81e419e`، شاخه `feature/product-v6-backend-alignment`، remote `origin` و upstream=`origin/feature/product-v6-backend-alignment` است. Commit کد **`bba4545b9c1b32db804a20953a410347b8439b4e`** با پیام `feat(ai): isolate personal execution and commerce boundaries` شامل۲۰ فایل مرتبط Application/Api/Test و دو فلگ عمومی است؛ پس از موفقیت نهایی Build/Test و بازبینی Cached Diff ثبت شد.

سه فایل untracked AI، سه appsettings خصوصی و سه فایل ملی‌پیامک عیناً با SHA256 حفظ شدند. Backup هر۱۰ فایل اولیه، شامل تنظیم پایه، سالم ماند؛ مقایسهٔ JSON پایه فقط دو فلگ مصوب را متفاوت نشان داد. Secret واقعی در افزوده‌های staged پیدا نشد. اسناد با پیام `docs(ai): hand off personal assistant and knowledge contracts` جدا ثبت می‌شوند؛ SHA نهایی اسناد و نتیجهٔ واقعی Push/fetch/live remote verification در پاسخ پایان Task درج می‌شود تا hash خودِ Commit در همان سند حدس زده نشود. Merge main و Deploy اجرا نمی‌شوند.

### ۱۳. محدودیت‌های فعلی

Generic Personal Chat برابر `BLOCKED_EXTERNAL_CONTRACT` و BE19 برابرPARTIAL است؛ Actor عمومی یا role-free upstream لازم است. تست موفق کنترل‌شدهٔ Knowledge، پذیرش زندهٔ Provider/Production نیست. BE24 کل v6 همچنانFAIL و۱۲ تصمیم باز Product/Legal برقرارند. Library AI، quota مشترک خانواده، حذف واقعی/retention، Store billing و PersonalClass AI/Academic gates خودکار فعال نشده‌اند. تنظیمات خصوصی، timeout، retry، JWT، SMS و Production تغییر نکردند.

### ۱۴. نکات آموزشی

1. داشتن Subscription مجوز دادهٔ آموزشی یا Role ایجاد نمی‌کند.
2. Context مستقل نشست را با تغییر Workspace هم‌زمان بازنویسی نکنید.
3. Contract خارجی را از OpenAPI واقعی اثبات کنید؛ enum داخلی یا سند هدف، شاهد پشتیبانی wire نیست.
4. Retry و concurrent duplicate را با reservation/transaction در SQL بسنجید، نه فقط شمار Mock.
5. شکست پس از آغاز Generation با شکست پیش از آن متفاوت است؛ Unknown را به Released حدسی تبدیل نکنید.
6. cardinality محدود و allowlist tag، مشاهده‌پذیری را بدون prompt/UserId ممکن می‌کند.
7. ExpectedVersion و source version بخشی از مجوز/هم‌زمانی‌اند، نه صرفاً جزئیات UI.

### ۱۵. مسیر پیشنهادی مطالعه

`AssistantContextResolver` → `AiChatService` → `PersonalAiChatIntent` → `AiInvocationContext`/`AiInvocationExecutor` → `CommercialSubscriptionService` و Store رزرو موجود → `KnowledgeAuthorizationService` → `KnowledgeIntegrationService` → `AiKnowledgeClient` → تست‌های Personal و تست‌های Source Selection → Handoff.

### ۱۶. مرحلهٔ بعد

مالک سرویس AI قرارداد Actor عمومی/role-free Personal را در upstream تصویب و منتشر کند؛ سپس adapter wire و پاسخ‌سنجی روی همان قرارداد واقعی و پذیرش زندهٔ مستقل انجام شود. Service Auth، سهمیهٔ خانواده، Library و آموزش PersonalClass نباید برای عبور از این blocker فعال یا بازطراحی شوند.

## Personal AI Activation — 2026-11-14

مبنای این مرحله `a5e24e43b2d458adf532648212caab5bdceee0b2` و شاخهٔ `feature/product-v6-backend-alignment` است. محدودیت Actor در بخش روز قبل تاریخی است؛ قرارداد جاری و جریان Client در [Handoff شخصی](PERSONAL_AI_FRONTEND_HANDOFF_FA.md) آمده‌اند.

### ۱. خلاصهٔ مرحله

پیش از این مرحله، Context/Intent/Commerce شخصی پیاده بودند ولی Chat عمداً پیش از رزرو409 می‌داد. upstream اکنون Actor رسمی Personal را پشتیبانی می‌کند. نگهبان قدیمی حذف و تبدیل Intent به درخواست v1 کامل شد؛ حساب عادی بدون Membership از همان HTTP Backend تا AI Production، پاسخ200 گرفت. رزرو و مصرف دقیقاً یک بار و Retry همان intent برابر409 بدون تماس AI دوم بود.

### ۲. معماری

تغییر Runtime در Application، ساخت actor/scope و انتخاب همان Executor است؛ Controller، Domain، EF/Infrastructure و Commerce تغییر نکردند. Api فقط Metadata OpenAPI و نگاشت خطای منقضی‌شده را به‌روز می‌کند. ابزار C# موجود سناریوی پذیرش واقعی را اجرا می‌کند؛ هیچ Debug Endpoint یا سیستم AI/Quota موازی ساخته نشد. وابستگی Domain ← Application ← Infrastructure و Composition Root قبلی حفظ است.

### ۳. فایل‌های مهم ایجادشده

`tools/ai/MakanApp.AiIntegrationProbe/LivePersonalAiScenario.cs` تنها فایل C# جدید است: ساخت حساب فاقد نقش در SQL GUID ایزوله، ایجاد قرارداد تجاری ساختگیِ معتبر، فراخوانی HTTP Backend، کنترل نتیجه و ledger و Retry. راز، OTP، JWT و متن پاسخ فقط در حافظه استفاده می‌شوند. نکتهٔ آموزشی: موفقیت مستقیم AI جای عبور از Authentication/Context/Commerce Backend را نمی‌گیرد.

### ۴. فایل‌های مهم تغییرکرده

- `MakanApp.Application/Ai/PersonalAiChatIntent.cs`: Intent داخلی حفظ و `PersonalAiChatContract.ToServiceRequest` با Role دقیق Personal افزوده شد؛ Scope/Context دوباره کنترل می‌شوند و ConversationId قرارداد قبلی حفظ می‌شود.
- `MakanApp.Application/Ai/AiChatService.cs`: شاخهٔ شخصی همان `IAiInvocationExecutor` را با callback ساخت wire و reauthorization حساب/نشست/Context فراخوانی می‌کند. شاخهٔ سازمانی همان Actor و Scope قبلی را می‌سازد؛ mapper پاسخ مشترک شد.
- `AssistantContextResolver`: Personal با UpstreamContextSupported=true و Limitation=null برمی‌گردد؛ وضعیت قرارداد با Runtime/اشتراک یکسان نیست.
- `AiServiceErrorCodes`، `GlobalExceptionHandler` و `AiExecutionTelemetry`: کد و مسیر blocked_upstream_contract که دیگر تولید نمی‌شوند حذف شدند؛ سایر mappingها و سنجه‌ها حفظ‌اند.
- `AiInvocationOperationTransformer`: شرح Chat فعال، Payer و مرزهای Source را دقیق می‌کند.
- تست‌های Personal/Assistant/OpenAPI و `ControlledAiV1Server`: انتظار409 قدیمی با Actor رسمی و تماس کنترل‌شده جایگزین شد؛ Student/Parent/Teacher/Manager محفوظ‌اند.
- `tools/ai/MakanApp.AiIntegrationProbe/Program.cs` و `LiveAiObservation.cs`: انتخاب صریح personal_chat، الزام Service Auth خاموش، Factory GUID موجود و guardِ Scope/Actor/Telemetry شخصی. عملیات قدیمی خودکار اجرا نمی‌شوند.
- template `tools/docs/sync_repository_docs.py`: متن جاریِ محدودیت رفع‌شده را دوباره تولید نمی‌کند؛ گزارش‌های تاریخی بازنویسی نمی‌شوند.

### ۵. جریان واقعی

```text
HTTP POST /api/v1/ai/chat → AiController → AiChatService
→ AiSourceSelectionService.GuardAsync → AssistantContextResolver
→ PersonalAiChatIntent.Create → AiInvocationExecutor
→ AiInvocationContext.CommerceContext → CommercialSubscriptionService.ReserveForAssistantAsync
→ SQL Server UsageReservation/ledger → Commit کوتاه رزرو
→ بازبینی حساب/نشست/Context → PersonalAiChatContract.ToServiceRequest
→ AiServiceClient.ChatAsync → HTTPS Production AI /api/v1/ai/chat
→ strict response/grounding/metadata validation → Consumed در SQL
→ AiChatResult → HTTP 200
```

Frontend فقط Backend را صدا می‌زند؛ Role، UserId و Scope upstream را Backend می‌سازد. هیچ تماس طولانی AI داخل transaction رزرو قرار نگرفت.

### ۶. دیتابیس

Migration، جدول، ستون، FK/index/constraint یا rowversion جدید وجود ندارد. همان UsageReservation و UsageLedgerEntry، Owner/Consumer/Personal Context/OperationId/hash را نگه می‌دارند. Outcomeهای Reserved/Consumed/Released/Unknown و قفل و یکتایی SQL تغییر نکردند. همان Intent با RequestId/Correlation تازه hash منطقی ثابتی دارد؛ Role wire به hash داخلی اضافه نشده تا identity intent بی‌دلیل عوض نشود.

پذیرش واقعی فقط در SQL LocalDB تازه با prefix موجود `MakanApp_KnowledgeProbe_` و پسوند GUID بود؛ reuse این helper صرفاً برای isolation است، نه RAG یا تولید دادهٔ Production. دیتابیس پس از اجرا طبق Factory حذف شد. `MakanApp`، `MakanApp_Test` و سرور Backend/AI تغییر نکردند.

### ۷. Endpointها

Endpoint جدیدی نیست. `PUT /api/v1/ai/assistant-context` همان Kind/ClientOperationId/ExpectedVersion را دارد و metadata پشتیبانی Personal اکنون true/null است. `POST /api/v1/ai/chat` همان Message/ConversationId/OperationId/SelectedSourceIds و Headerهای `X-AI-Operation-Id` و `X-AI-Payer` را دارد. Message غیرخالی تا4000، شناسه عملیات معتبر و header/body سازگار لازم‌اند. نشست/حساب فعال و Payer/سهمیه مجاز اجباری‌اند.

Personal: Individual پیش‌فرض، FreeTrial صریح، Organization403. نبود قرارداد/سهمیه همان خطای Commerce را می‌دهد و AI خوانده نمی‌شود. Completed/InProgress/Unknown/Failed همان409ها، ورودی نامعتبر400، RateLimit429، پاسخ نامعتبر502، unavailable503 و timeout504 حفظ‌اند. Actor=Personal هیچ مجوز Student/Parent/Teacher/Manager ایجاد نمی‌کند. selectedSourceIds legacy همچنان RetrievalUnavailable دارد؛ Knowledge/RAG عملی از API موجود خودش استفاده می‌کند.

### ۸. مفاهیم استفاده‌شده

مرز intent و wire DTO، authorization جاری، Scope کمینه، Payer مستقل، transaction کوتاه رزرو، response validation، idempotency منطقی و پذیرش واقعی از HTTP Backend. Contract support از Feature Enabled و دسترسی تجاری جداست.

### ۹. تصمیم‌های طراحی

تبدیل Role به Student، حدس User یا تغییر enum سازمانی کنار گذاشته شد؛ مقدار رسمی Personal در همان v1 استفاده می‌شود. پایهٔ Context/Commerce/Knowledge بازنویسی نشد. Backend client و timeout/retry/provider configuration تغییر نکردند. Service Auth هر دو سرویس false و هیچ JWT/CurrentKey requirement تازه ایجاد نشد.

OpenAPI/Health/Ready عمومی200 بودند؛ Role Personal زنده و SHA256 سند=`58559cd3903c051fa2b8e12e08c67523576f309dd6d791e39145eda5d61dbfdc`. Commit/release طبق handoff مالک AI به‌ترتیب `5735598ec9978bae7662164c3d3dc24b3445b8cd` و `personal-5735598ec997` است؛ HTTP hash انتشار را نشان نداد. تیم AI Business=23/23، Live Personal/Invalid Scope/Invalid Role/Student و NEW_FAILURES=0 گزارش کرده است؛ pytest=1303 Passed/31 Failed تاریخی، پس Full Pytest upstream پاک گزارش نمی‌شود. آن تست‌ها در این مخزن اجرا نشده‌اند.

### ۱۰. تست‌ها و شواهد

Unitهای موجود اکنون Mapper رسمی، Scope تهی، UserId سرور و رد scope آلوده را می‌سنجند. هشت Integration جدید، HTTP/SQL برای نبود قرارداد/سهمیه/Organization payer، Trial و Retry، duplicate هم‌زمان، Timeout/Unknown، تغییر Role/Organization با Assistant شخصی و Session مستقل و serializer/validator HTTP واقعی با AI کنترل‌شده را اثبات می‌کنند.۱۱ منع Endpoint آموزشی و چهار Role سازمانی قبلی محفوظ‌اند؛ Knowledge/source ACL از تست‌های موجود استفاده می‌کنند. Architectureهای موجود جهت Onion را می‌سنجند.

Live Probe با حساب عادی و قرارداد Individual فقط در SQL آزمایشی، Backend200/AI200 و metadata=`education-fast` گرفت. requestId=`efe44b0cfee14d849ce99147ba341345`، correlationId=`personal-live-4f348f38a2144c35a5d7638e7aa0e421`، زمان کل≈46780.9ms؛ timeout تنظیمی35s ثابت بود و orchestration/SQL در زمان کل حساب می‌شوند. Scope/Context خالی، Authorization سرویس غایب، aiCalls=1، reservation=1، consume=1 و replay409 بدون تماس دوم ثبت شدند. پاسخ خصوصی چاپ یا ذخیره نشد.

اولین اجرای ابزار در helper خواندن OTP ساختگی از میزبان پایه متوقف شد؛ helper به میزبان واقعی همان درخواست متصل شد و اجرای پذیرفته‌شده فقط یک تماس Production داشت. هیچ Authentication برنامه تغییر نکرد.

### ۱۱. Validation

Clean/Restore، Build اولیه و Final Release Build و Build نهایی ابزار با۰ Warning/Error موفق‌اند؛۲۸ Unit و۱۱۸ Integration متمرکز پاس شدند. Final Tests:۱۳۲۵ Unit +۱۹۳۰ Integration +۱۴ Architecture =۳۲۶۹ Passed، Failed=0 و Skipped=0، exit=0. هشت مورد Integration به baseline۳۲۶۱ افزوده شده‌اند.۱۱۴ تست Zibal، شامل۶۸ Unit و۴۶ Integration، در TRX نهایی Passed هستند؛ این پذیرش کنترل‌شدهٔ کد است، نه پرداخت واقعی Production. EF هیچ pending model change نشان نداد و Migration جدید۰ است. پذیرش واقعی Personal exit=0 بود.

اولین اجرای Full Regression پس از۱۴ timeout در دریافت SignalR برای تشخیص متوقف شد و یک اجرای کامل موفق شمرده نشد. همان۱۴ سناریو در فرایند تازه،۱۵ثانیه و بدون شکست پاس شدند؛ سپس اجرای کامل بدون Probe هم‌زمان، با همین سورس ثابت، در Integration حدود۳۲دقیقه‌و۱۰ثانیه و با۱۹۳۰/۱۹۳۰ موفق پایان یافت. هیچ timeout یا تستی تضعیف/حذف و هیچ کد Realtime تغییر نکرد. علت قطعی timeoutهای دور اول اثبات نشده و ادعای رفع یک باگ Realtime از این نتیجه نمی‌شود.

Snapshot نهایی از `31fe34c6dde4526badc43d43e62b8ba185ee6f36` بدون SQL/Worker/SMS/AI استخراج شد:۴۳۶ path،۴۹۲ operation،۹۱ Controller،۷۸۰ schema،۱۴۹ جدول و۷۳ Migration؛ OpenAPI SHA256=`5e317c540ce2a13dd1b26f94dd847b858e6242eb3d9fa56c8607feaee737a42c`. Source hash همهٔ۱۵۰۰ فایل C# اعتبارسنجی‌شده یکسان است. `BE19_AI_GATEWAY=PASS` و `BE24_FULL_ACCEPTANCE=FAIL`؛ رفع Actor شخصی سایر موانع Product/عملیاتی را نمی‌بندد.

همگام‌سازی اسناد و Validation آن موفق‌اند:۴۹۲ operation/۷۸۰ schema/۱۴۹ جدول/۷۳ Migration،۲۰۰ ورودی inventory، لینک شکسته و اختلاف Enum/OpenAPI صفر. HTML/PDF راهنما، مرجع API، دیتابیس و قرارداد مصرف Backend به‌ترتیب۷۹/۷۹۳/۲۳۵/۸۰۱صفحه دارند؛ hash منبع HTML و bookmark کنترل شد و صفحهٔ خالی یا متن خارج صفحه ندارند. نسخهٔ HTML/PDF دیتابیس طبق ignore قبلی محلی است.۱۳ تست ابزار مستندات نیز پاس‌اند و جدا از عدد۳۲۶۹ تست .NET شمرده می‌شوند.

### ۱۲. Git

Baseline=`a5e24e43b2d458adf532648212caab5bdceee0b2`، branch=`feature/product-v6-backend-alignment` و upstream=`origin/feature/product-v6-backend-alignment` پیش از تغییر برابر بودند. Runtime و تست‌های متناظر در `5329a2b4851661ec8eb9e9861af763d331ff54aa` با پیام `feat(ai): activate upstream personal chat contract` ثبت شدند؛۱۵ فایل. ابزار پذیرش در `31fe34c6dde4526badc43d43e62b8ba185ee6f36` با پیام `test(ai): complete personal backend-to-ai acceptance` ثبت شد؛۳ فایل. اسناد مرتبط با پیام `docs(ai): close personal chat integration blocker` ثبت می‌شوند؛ hash نهایی و نتیجهٔ واقعی Push/Remote verification در خروجی نهایی Task ارائه می‌شود.

Remote=`origin` با نشانی `https://github.com/RastinaxAI/MakanApp.git` و همان upstream feature است.۱۰ فایلِ تنظیمات/ملی‌پیامک/AI قبلی Backup و SHA256 دارند و تغییر نکرده‌اند. staged diff هر Commit فقط فایل‌های مرتبط را دارد و کنترل نشت مقدار خصوصی موفق است. سه فایل untracked قبلی `tools/ai/provider_diagnostic.py`، `run_exact_ai.py` و `test_provider_diagnostic.py` محفوظ و خارج Commit هستند. Merge main و Backend Deployment برابر NOT_RUN هستند.

### ۱۳. محدودیت‌های فعلی

Runtime پیش‌فرض/فایل خصوصی فعال نشده و Backend Deploy نشده است؛ این پذیرش از Backend محلی با وابستگی Production AI بود. Library، PersonalClass AI/Academic gates، سهمیهٔ خانواده، حذف اجرایی/retention، Store billing و محدودیت‌های Product قبلی تغییر نکرده‌اند. BE24 با رفع یک مانع BE19 خودکارPASS نمی‌شود. selectedSourceIds legacy با مسیر Knowledge یکی نیست. شمار۱۲ تصمیم Product/Legal حفظ است؛ D11 خارجی در آن شمار نبود و اکنون بسته است.

### ۱۴. نکات آموزشی

1. مقدار رسمی Actor خارجی را با نقش سازمانی Domain اشتباه نگیرید.
2. Context را از وضعیت جاریِ نشست resolve و پیش از network دوباره مجاز کنید.
3. رزرو باید قبل از Provider باشد؛ accounting بعد از پاسخ جای آن را نمی‌گیرد.
4. Retry همان intent می‌تواند409 بدهد و همچنان retry-safe باشد؛ نگه‌داری پاسخ خصوصی تصمیم جداست.
5. در E2E مشتق‌شده از WebApplicationFactory، کنترل OTP باید از همان Host درخواست خوانده شود.
6. PASS قرارداد/پذیرش Business با صفرشکست‌بودن کل pytest upstream یا Deploy Backend یکسان نیست.

### ۱۵. مسیر پیشنهادی مطالعه

`AiController` → `AiChatService` → `PersonalAiChatIntent/PersonalAiChatContract` → `AiInvocationExecutor` و `AiInvocationContext` → Commerce/Store رزرو موجود → `AiServiceClient` و response grounding → `PersonalAiAcceptanceTests` → `AiV1ControlledTransportTests` → `LivePersonalAiScenario` → Handoff.

### ۱۶. مرحلهٔ بعد

در Task انتشار جدا، Runtime و BaseUrl همان محیط با تنظیم خصوصی مصوب فعال و بستهٔ Backend پس از Backup/Validation مستقر شود. UI فقط Backend را صدا بزند و true/null بودن metadata را از وضعیت اشتراک/Feature Disabled جدا کند. Library، حذف واقعی، سهمیهٔ خانواده یا آموزش PersonalClass به این انتشار اضافه نشوند.
