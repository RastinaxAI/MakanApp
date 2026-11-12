# گزارش پیاده‌سازی و یادگیری Backend v6

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
