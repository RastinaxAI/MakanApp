# گزارش پیاده‌سازی و یادگیری Backend v6

> به‌روزرسانی آموزشگاه `2026-11-13` در بخش [تکمیل درخواست آموزشگاه](#تکمیل-درخواست-آموزشگاه--2026-11-13) همین سند است. بخش اولیهٔ تاریخ‌دار زیر گزارش مرحلهٔ قبلی و Commit خودش است؛ محدودیت قدیمی D01 با این Task بسته شده است. قرارداد جاری: [Frontend آموزشگاه](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) و [Admin آموزشگاه](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md).

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
