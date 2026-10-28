# پیاده‌سازی Profile/DOB و پذیرش Personal Messaging مستقل از نقش

تاریخ: `2026-10-28` — مخزن اصلی: `D:\Project\MakanApp\MakanApp-Backend` — شاخه: `main`.

HEAD مبنا: `b87fbd89cef33f26315fddd4a1e57116b44cc781`.

مبنای محصول: [مدل محصول نسخهٔ ۲](MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md). این مرحله فقط self-declared DOB و ممیزی/تست Personal Messaging را پوشش می‌دهد. Badge، Commerce، Sources، AI، Push، OTP، MeliPayamak، MFA و CORS توسعه یا بازطراحی نشده‌اند.

## 1. Product decision — تصمیم محصول و خلاصهٔ مرحله

`User Identity ≠ Organization Role`. حساب، Profile و گفتگوهای شخصی متعلق به User هستند؛ نقش سازمانی مجوز رسمی اضافه می‌کند. گرفتن نقش Teacher یا پایان Membership نباید User، Profile، Personal Direct، Group یا Channel را حذف کند.

قبل از مرحله، Profile فقط نام‌ها و Username داشت و DOB خوداظهاری نداشت. بعد از مرحله، `Person.DateOfBirth` و فیلد nullable `dateOfBirth` در قراردادهای self وجود دارند؛ فرمت date-only و بدون timezone است. DOB شرط جدید تکمیل Profile نشده است.

Personal Messaging بدون School Role از قبل پیاده‌سازی شده بود. این مرحله با fixtureهای فاقد Membership و OrganizationPerson، Direct/Send/Inbox/Group/Channel، Privacy، Block، role lifecycle و SignalR را بررسی می‌کند. تنها اصلاح production code در Messaging، رفع ناسازگاریِ راهنمای قابلیت خواندن برای Profile ناقص است؛ policy مقصد/ارسال/فضای رسمی تغییر نکرده است.

## 2. Current bug — نتیجهٔ Audit پیش از تغییر

```text
SELF_DECLARED_DATE_OF_BIRTH_IN_DOMAIN=NO
SELF_DECLARED_DATE_OF_BIRTH_IN_DATABASE=NO
SELF_DECLARED_DATE_OF_BIRTH_IN_GET_ME=NO
SELF_DECLARED_DATE_OF_BIRTH_IN_PROFILE_UPDATE=NO
VERIFIED_BIRTH_DATE_EXISTS=YES
```

مقصود database، مدل EF/Migration مخزن است؛ دیتابیس عملیاتی برای این ممیزی خوانده نشد. `VerifiedBirthDate` قبلی به evidence مستقل Communication Age مربوط است.

گزارش کلی «User بدون School Role به Personal Messaging دسترسی ندارد» با Context شخصی و شرایط معتبر سن/Privacy بازتولید نشد:

```text
FRONTEND_REPORTED_BUG_NOT_REPRODUCED
```

درخواست/پاسخ و state همان حساب Frontend در اختیار این مرحله نبود؛ تست‌ها روی Backend محلی با TestServer/SignalR و SQL Server LocalDB ایزوله اجرا می‌شوند. این عبارت با رفع یک ناسازگاری مستقل Backend در بخش بعد تناقض ندارد؛ علت حادثهٔ نامشخص Frontend به آن نسبت داده نشده است.

## 3. Root cause واقعی — مورد بازتولیدشده

```text
ROOT_CAUSE_1
File: MakanApp.Application/Messaging/MessagingStatus.cs
Class: MessagingStatusPolicy
Method: Evaluate
Old behavior: ProfileComplete=false -> allowedActions=[]
Why ordinary user was blocked: پاسخ capability هیچ امکان خواندنی اعلام نمی‌کرد،
  در حالی که GET Inbox و تنظیمات شخصی همان حساب مجاز بودند.
  این ناسازگاری Backend بازتولید شد؛ استفادهٔ UI گزارش‌شده از این پاسخ بررسی نشده است.
New behavior: فقط ReadExistingConversations برای Profile ناقص اعلام می‌شود؛
  NextAction همچنان ProfileCompletionRequired است.
Security preserved: مجوز هر گفتگو/نشست/حساب در endpoint و Hub بازبینی می‌شود؛
  ایجاد Direct/Group/Channel یا Discovery تازه از این تغییر مجوز نمی‌گیرد.
```

شاهد قبل از اصلاح: تست `IncompleteBasicProfileDoesNotBlanketDenyPrivateMessagingSettingsAndInbox` موفقیت GETهای شخصی را دید، سپس روی `Assert.Single(status.AllowedActions)` با collection خالی fail شد. پس از تغییر محدود policy، همان تست در رگرسیون متمرکز پاس شد. تست موجود Unit دربارهٔ Profile gate مطابق همین تصمیم محصول به «read-only access» تغییر کرد؛ assertion تفکیک age verification باقی ماند و چهار حالت محافظت‌شدهٔ دیگر افزوده شد. هیچ تستی Skip/Disable نشد.

### جدول Gateهای ممیزی‌شده

| Operation | Current Gate / کلاس و متد واقعی | Product-valid? | Change Needed? |
|---|---|---|---|
| Personal Workspace | `OrganizationService.GetMyWorkspacesAsync` / `ResolveBaseContextAsync`؛ Personal بدون Membership، یا Context سازمانی انتخاب‌شدهٔ معتبر | بله؛ roleless به مدرسه نیاز ندارد | خیر |
| بازگشت پس از Role loss | `OrganizationService.SelectWorkspaceAsync`؛ انتخاب Personal فقط نشست معتبر می‌خواهد | بله؛ همان endpoint موجود مسیر بازگشت است | خیر؛ fallback پنهان ساخته نشد |
| Contacts | `MessagingDirectoryService.GetContactsAsync/SaveContactAsync` و `EfMessagingDirectoryStore`؛ مالک User، حساب/Privacy/Block/رابطه | بله؛ OrganizationPerson شرط عمومی نیست | خیر |
| People | `SearchPeopleAsync/ResolvePersonAsync` و `MessengerRelationshipQuery.DiscoverableAccounts/RelatedUsers` | بله؛ Adult تأییدشده یا رابطهٔ ایمن، نه School Role عمومی | خیر |
| Personal Direct | `MessagingService.StartOrGetDirectConversationAsync`، `CommunicationEligibilityPolicy.CanStartOrSend/CanUsePersonal` | بله؛ Personal Context، هویت نمایشی، سن/رابطه/Privacy/Block | خیر |
| Send | `EfMessagingStore` در `Advanced.Send` و `ChildAuthorization`؛ account/session/participant، Context، restriction، Privacy/Block و Safety جاری | بله | خیر |
| List/History | `EfMessagingStore.GetMyConversationsAsync/GetConversationMessagesAsync`؛ active participant و Scope/History مجاز | بله؛ منتخب Workspace فعلی | خیر |
| Personal Group/Channel | `ConversationManagementAuthorizationPolicy.CanCreate` و `EnsurePersonalCreationSafetyAsync` | بله؛ بدون School Role، با محدودیت Age/Visibility/دعوت | خیر |
| Messaging status | `MessagingStatusPolicy.Evaluate`؛ Profile و Age/Restriction/Workspace؛ hint است، نه authorization | Profile ناقص نباید قابلیت خواندن موجود را پنهان کند | اصلاح محدود ReadExistingConversations |
| Privacy | `MessagingDirectoryService.GetPrivacySettingsAsync/UpdatePrivacySettingsAsync`؛ self و قرارداد audience/version | بله؛ Everyone/Contacts/Nobody حفظ | خیر |
| Presence | `MessagingEphemeralService.PresenceCoreAsync` و access store؛ session/context و visibility/relation | بله؛ مخفی‌بودن، مجوز آنلاین‌دیدن نمی‌دهد | خیر |
| Block/Report | `MessagingSafetyService` و store؛ حساب، scope، participant/resource مجاز | بله؛ Role یا plan راه عبور نیست | خیر |
| SignalR | `MessagingHub.SubscribeConversation` / سرویس authorize؛ session و resource جاری | بله؛ دانستن ConversationId مجوز نیست | خیر |
| رسمی کلاس | `OfficialClassConversationService.ResolveAsync` و `EfMessagingStore.ResolveClassAsync/CanAccessScopeAsync`؛ ClassMembers جاری | بله؛ enrollment/assignment و membership/role همان سازمان | خیر |

پروفایل پایه برای هویت نمایشیِ عملیات public-facing لازم است: snapshot و public identity به `Person`، Username و completion فعلی متکی‌اند. DOB، LearningProfile و اطلاعات آموزشی تازه به این prerequisite افزوده نشده‌اند. کاربر ناقص همچنان Inbox خالی/تنظیمات خصوصی را می‌خواند؛ status اکنون خواندنِ مجاز را منعکس می‌کند.

## 4. Files changed — فایل‌ها و مسئولیت در Onion

| فایل مرتبط این مرحله | نوع | مسئولیت و نکتهٔ آموزشی |
|---|---|---|
| [Person.cs](../../MakanApp.Domain/Identity/Person.cs) | تغییر Domain | nullable DOB و `SetDateOfBirth`؛ جلوگیری از تاریخ آینده بدون وابستگی HTTP/EF و بدون حداقل سن اختراعی |
| [IdentityContracts.cs](../../MakanApp.Application/Identity/IdentityContracts.cs) | تغییر Application | پارامتر optional انتهایی در request/response؛ constructor callهای قدیمی معتبر می‌مانند |
| [IdentityService.cs](../../MakanApp.Application/Identity/IdentityService.cs) | تغییر Application | validation با `TimeProvider`، persistence از port موجود و mapping پاسخ self؛ منطق OTP تغییر نکرد |
| [MessagingStatus.cs](../../MakanApp.Application/Messaging/MessagingStatus.cs) | تغییر Application | capability hint خواندن؛ resource authorization مستقل می‌ماند |
| [PersonConfiguration.cs](../../MakanApp.Infrastructure/Persistence/Configurations/Identity/PersonConfiguration.cs) | تغییر Infrastructure | نگاشت DateOnly nullable به SQL `date`؛ persistence attribute وارد Domain نشد |
| [Migration](../../MakanApp.Infrastructure/Persistence/Migrations/20261028102653_AddSelfDeclaredProfileDateOfBirth.cs) | جدید Infrastructure | فقط AddColumn nullable؛ بدون backfill/default |
| [Migration Designer](../../MakanApp.Infrastructure/Persistence/Migrations/20261028102653_AddSelfDeclaredProfileDateOfBirth.Designer.cs) | جدید، generated | مدل نسخهٔ همین Migration برای بازتولید schema |
| [ModelSnapshot](../../MakanApp.Infrastructure/Persistence/Migrations/MakanDbContextModelSnapshot.cs) | تغییر، generated | هماهنگی EF model؛ تنها امتداد DOB در snapshot |
| [PersonDateOfBirthTests.cs](../../tests/MakanApp.UnitTests/Identity/PersonDateOfBirthTests.cs) | جدید Unit | مرز امروز/آینده، null اولیه، تاریخ قدیمی و حفظ DOB در تغییر نام |
| [PersonalMessagingStatusTests.cs](../../tests/MakanApp.UnitTests/Messaging/PersonalMessagingStatusTests.cs) | تغییر Unit | حفظ هدف safety تست قبلی و قرارداد read-only جدید برای Profile ناقص |
| [PersonalProfileDateOfBirthTests.cs](../../tests/MakanApp.IntegrationTests/PersonalProfileDateOfBirthTests.cs) | جدید Integration | API/JSON/OpenAPI، جدایی verification، سازگاری و عدم نشت |
| [PersonalMessagingRoleLifecycleTests.cs](../../tests/MakanApp.IntegrationTests/PersonalMessagingRoleLifecycleTests.cs) | جدید Integration | roleless، دریافت/پایان نقش و عضویت، امنیت و Hub واقعی محلی |
| [PersonalProfileDateOfBirthMigrationTests.cs](../../tests/MakanApp.IntegrationTests/PersonalProfileDateOfBirthMigrationTests.cs) | جدید Integration | ارتقای baseline قدیمی و اجرای دوبارهٔ script idempotent روی DB موقت |
| همین سند | جدید Documentation | قرارداد اجرایی، نتایج واقعی و handoff؛ سند Product قبلی بازنویسی نشد |

Controller، DI، Program، Hub، policy سن، authentication handler، SMS، AI و config تغییر نکردند. OpenAPI از همان DTOها تولید می‌شود و extension آن به تغییر Controller یا route نیاز ندارد.

## 5. Domain changes — مدل و جریان اجرا

`Person.DateOfBirth: DateOnly?` شخصی و جهانی است. جدول Profile موازی، Role سراسری روی User یا وابستگی DOB به Membership ایجاد نشده است.

```text
PATCH /api/v1/me/profile
-> ProfileController.CompleteProfile
-> IdentityService.CompleteProfileAsync
-> validation نام/Username و تاریخ با TimeProvider UTC
-> Person.Create/Update + Person.SetDateOfBirth
-> IIdentityStore / EfIdentityStore.SaveChangesAsync
-> MakanDbContext / SQL Server identity.Persons
-> IdentityService.BuildCurrentUserAsync
-> CurrentUserResult
-> HTTP 200
```

ساختار Onion حفظ شد: قاعدهٔ تاریخ در Domain، validation/قرارداد و orchestration در Application، نگاشت/Migration در Infrastructure و HTTP موجود در Api. متد Domain همگام است چون I/O ندارد؛ I/Oهای موجود همچنان async و دارای CancellationToken هستند.

`MessagingStatusPolicy.Evaluate` صرفاً خروجی `CommunicationAgeService.GetSelfStatusAsync` را می‌سازد. اضافه‌کردن ReadExistingConversations به hint، هیچ participant، membership یا permission جدیدی در SQL ایجاد نمی‌کند.

## 6. DB changes — ستون، قیود و concurrency

| مورد | تغییر واقعی |
|---|---|
| Table | `identity.Persons`، همان جدول قبلی |
| Column | `DateOfBirth date NULL` |
| Default / Backfill | ندارد؛ تاریخ از سن، verification یا زمان ایجاد حدس زده نمی‌شود |
| PK/FK | تغییر نکرده‌اند |
| Index | ایندکس DOB اضافه نشده؛ فیلد Searchable عمومی نیست |
| Constraint | قاعدهٔ آینده در Domain/Application؛ CHECK سن یا حداقل سن جدید ساخته نشد |
| rowversion | RowVersion موجود Person حفظ؛ قرارداد جدید ExpectedVersion به Profile اضافه نشده |
| Transactions | الگوی SaveChanges موجود استفاده می‌شود؛ transaction شبکه‌ای جدید ساخته نشد |

FKهای Conversation/Participant به User و Organization/Class با رفتارهای موجود حفظ شدند؛ برای دادهٔ شخصی FK به RoleAssignment/Membership اضافه نشد. پایان Role/Membership وضعیت را تغییر می‌دهد و در تست‌ها User، Profile، Owner participant و ConversationIdهای شخصی حفظ شدند. EF cascade یا عملیات حذف دادهٔ شخصی تغییر نکرد.

## 7. API changes — قرارداد موجود

### FRONTEND_HANDOFF — DOB

| مورد | قرارداد واقعی |
|---|---|
| Endpoint | `/api/v1/me/profile` |
| HTTP Method | `PATCH` |
| Request field | `dateOfBirth` روی CompleteProfileCommand |
| Response field | `dateOfBirth` روی CurrentUserResult؛ همچنین `GET /api/v1/me` |
| Nullable? | بله؛ optional و nullable |
| Format | `YYYY-MM-DD`، OpenAPI `type: string, format: date, nullable: true` |
| Validation behavior | تاریخ calendar معتبر و غیرآینده با روز UTC سرور؛ بدون حداقل سن جدید |
| Error behavior | تاریخ آینده: HTTP 400 با `PROFILE_VALIDATION_FAILED`؛ فرمت/تاریخ نامعتبر: 400 و model-binding ProblemDetails موجود |
| Authentication | همان نشست احراز‌شدهٔ فعلی؛ فقط self |
| Compatibility | DOB null باعث incomplete شدن Profile کامل قبلی نمی‌شود؛ route/enum قبلی rename نشده |

نمونهٔ درخواست غیرمحرمانه:

```json
{
  "firstName": "نام",
  "lastName": "نام خانوادگی",
  "displayName": "نام نمایشی",
  "username": "sample.user",
  "dateOfBirth": "2005-08-21"
}
```

PATCH همچنان نام‌ها و Username مورد نیاز قرارداد قبلی را دریافت می‌کند؛ این مرحله PATCH صرفاً یک‌فیلدی جدید ایجاد نکرده است. اگر `dateOfBirth` ارسال نشود یا null باشد، DOB قبلی حفظ می‌شود؛ null برای حسابی که هنوز DOB ندارد همان null باقی می‌ماند. برای حفظ client قدیمی، عملیات پاک‌کردن DOB از طریق null تعریف نشده است؛ تصمیم clear در مرحلهٔ مستقل لازم است.

تاریخ API میلادی و ISO است؛ نمایش/انتخاب شمسی در کلاینت نباید مقدار API را به timestamp یا سال شمسیِ تفسیرشده به‌عنوان میلادی تبدیل کند.

خطاهای نام/Username و uniqueness قبلی باقی‌اند. `PhoneNumber`، Credential، verified phone، role و age evidence با این request قابل تغییر نیستند.

## 8. DateOfBirth vs VerifiedBirthDate

| موضوع | DateOfBirth | VerifiedBirthDate |
|---|---|---|
| محل | `Person` / `identity.Persons` | `CommunicationAgeVerification` / evidence table موجود |
| authority | خوداظهاری self-profile | workflow مستقل و مجاز verification |
| optional | بله | طبق contract ثبت evidence موجود |
| کاربرد | اطلاعات شخصی | طبقه‌بندی و اعتبار سن ارتباطی |
| اثر روی Safety | خودکار مجوز بیشتر یا verification موفق ایجاد نمی‌کند | همان policy فعلی authority است |
| نمایش | self؛ نه directory/public identity | سن دقیق در پاسخ عمومی نمایش داده نمی‌شود |

تست دو جهت اجرا می‌شود: تغییر DOB شخصی evidence و verified category را عوض نمی‌کند؛ ثبت evidence پس از DOB شخصی، DOB را backfill/overwrite نمی‌کند. self-DOB بزرگسال، Unknown/Minor را به Adult مجاز تبدیل نمی‌کند. مدل Conflict خوداظهاری با evidence در این مرحله اختراع نشده است.

## 9. Personal vs Official Messaging

`ConversationScope.Personal/Organization`، `WorkspaceType.Personal/Organization` و Typeهای Direct/Group/Channel/Saved همان مقادیر قبلی‌اند.

- Personal به User/participant تعلق دارد؛ School Role و OrganizationPerson prerequisite عمومی نیستند.
- انتخاب Workspace موجود حفظ است. وقتی session روی Organization قرار دارد، عملیات scopeدار از آن Context استفاده می‌کند؛ برای Personal باید Workspace شخصی انتخاب شود.
- پس از پایان Role انتخاب‌شده، client می‌تواند با `POST /api/v1/workspaces/select` و Personal به فضای شخصی برگردد. fallback خودکار یا تبدیل درخواست رسمی به شخصی ساخته نشده است.
- Official Class به Enrollment/TeacherAssignment و membership/role فعال وابسته می‌ماند. Manager با provision کردن participant خودکار نمی‌شود.
- پاسخ denial رسمیِ مورد تست، `404 CONVERSATION_NOT_FOUND` برای concealment است؛ به 403 عمومی تبدیل نشده است.

نمونهٔ انتخاب Context شخصیِ موجود:

```json
{
  "workspaceType": "Personal",
  "membershipId": null,
  "role": null
}
```

Scope و Role selector هستند؛ policy فعلی Backend همچنان مجوز را می‌سنجد. هیچ Organization جعلی ساخته نشده است.

## 10. Roleless User behavior

### FRONTEND_HANDOFF — Messaging

| مورد | رفتار فعلیِ تأییدشده با تست |
|---|---|
| Roleless user expected state | حساب/نشست فعال و Personal Context، بدون Membership/OrganizationPerson؛ DOB و LearningProfile شرط جدید نیستند |
| Personal messaging capability | Direct/Send/List/Group/Channel برای Verified Adult واجد Privacy/Safety؛ ساخت Channel و Group عمومی برای Minor/Unknown ممنوع |
| Official messaging capability | نداشتن رابطهٔ معتبر رسمی، مجوز کلاس/سازمان نمی‌دهد؛ شناسهٔ Resource کافی نیست |
| Messaging status behavior | Profile ناقص: فقط ReadExistingConversations و ProfileCompletionRequired؛ کامل: actions مطابق verification/age/restriction/workspace موجود |
| Privacy errors | Start غیرمجاز با `DIRECT_RECIPIENT_NOT_AVAILABLE` و concealment؛ Send مطابق policy جاری رد می‌شود |
| Block errors | ارسال Direct تازه پس از Block در هر جهت: `MESSAGE_NOT_ALLOWED`؛ history پایدار پاک نمی‌شود |
| Age/safety state | Unverified/Conflict به Unknown مؤثر می‌رسد؛ رابطهٔ مجاز برای مسیر محافظت‌شده لازم است؛ DOB خوداظهاری bypass نیست |

Teacher/Parent/Student/Manager بودن طرف مقابل به‌تنهایی Personal Direct را منع یا مجاز نمی‌کند. تست role lifecycle، دریافت Teacher و حفظ Parent پس از پایان Teacher را نیز بررسی می‌کند. Badge system جدید، privilege تازه یا entitlement مالی در این مرحله وجود ندارد.

## 11. Age/Safety behavior

GAP-05 حفظ شده است: Unknown مانند Minor در عملیات حساس محافظت می‌شود؛ stranger direct منع است؛ گروه خصوصی تحت policy ممکن است، اما public group و personal channel برای protected account مجاز نیست. `CreateDirectWithAuthorizedRelationship` به معنی مجوز ارتباط با هر مقصد نیست.

`MessagingNextAction` و enumهای موجود تغییر نکرده‌اند. hint جدید خواندن، نه verification است و نه اجازهٔ ورود به conversation ناشناس. policy فعلی Internal Makan Content Safety، fail-closed، restriction و suspension تغییر نکرده است. Full suite شامل تست‌های فعلی این حوزه‌هاست؛ تست زندهٔ SMS/AI اجرا نشده است.

## 12. Privacy/Block behavior

Everyone/Contacts/Nobody و جهت Contact حفظ شده‌اند. برای Contacts، ذخیره‌کردن Receiver توسط Sender کافی نیست؛ رابطهٔ لازم از دفترچهٔ مقصد بررسی می‌شود. حتی پس از ذخیرهٔ متقابل، Nobody مجوز Direct نمی‌دهد. Block در هر جهت جلوی Send تازه را می‌گیرد؛ Role یا Membership جدید آن را دور نمی‌زند.

DOB به People Search، Contacts، SafeMessagingIdentityResult، participant public identity، Message sender و public profile اضافه نشده است. تست wire و schema این مرز را بررسی می‌کند؛ مقدار تاریخ آزمایشی در captured log و Audit همان actor نیز جست‌وجو شده است. snapshot خروجی Log یا Credential واقعی در این سند درج نمی‌شود.

## 13. SignalR behavior

Hub و مسیرهای authorization آن تغییر نکردند. تست جدید با HubConnection واقعیِ TestServer، Personal Direct مجاز را subscribe می‌کند و برای Conversation رسمی کلاسِ موجود، با وجود داشتن شناسه، HubException با `REALTIME_SUBSCRIPTION_NOT_ALLOWED` می‌گیرد. REST رسمی هم برای همان کاربر رد می‌شود.

تست‌های قبلی Personal delivery/reconnect و realtime current-state در focused regression باقی‌اند. Presence HTTP مجاز نیز بررسی شده است؛ پاسخ HTTP موفق به معنی افشای Online/LastSeen برخلاف Privacy نیست. عبور از School Role با حذف resource guard اشتباه گرفته نشده است.

## 14. Frontend contract changes — اثر اجرایی

Frontend در این Task تغییر نکرده است. برای مصرف Backend جدید:

1. DOB را date-only ارسال/نمایش دهد؛ تبدیل به ساعت محلی/UTC timestamp نکند.
2. null self-DOB را نقص تحصیلی یا الزام Role نداند.
3. در Profile ناقص، از `allowedActions` استفاده کند: خواندن مجاز می‌ماند و NextAction تکمیل Profile برای ایجاد هویت نمایشی پابرجاست.
4. نداشتن School Role را با UnknownAge، Privacy مقصد یا Workspace نامعتبر یک خطا نداند.
5. برای رفتن به Personal، endpoint انتخاب Workspace موجود را به‌کار ببرد؛ request رسمی نامعتبر را خودکار Personal فرض نکند.
6. DOB را در public profile/contact card/Presence یا logging کلاینت منتشر نکند؛ فیلد self جدید مجوز نمایش عمومی نیست.

OpenAPI زندهٔ TestServer در تست خوانده شده: فیلد request optional است، نوع string/date/nullable صحیح است و schemaهای public Messaging DOB ندارند. مسیر جدید یا breaking rename ایجاد نشده است.

## 15. Migration — تولید، SQL و اعتبارسنجی ایزوله

نام واقعی: `20261028102653_AddSelfDeclaredProfileDateOfBirth`.

Up فقط این تغییر را دارد:

```sql
ALTER TABLE [identity].[Persons] ADD [DateOfBirth] date NULL;
```

SQL idempotent محلی: `.artifacts/profile-messaging-epic-20261028-83050d3b/profile-dob-idempotent.sql`، از baseline `20261024184613_RemovePlatformMfa` تا Migration جدید. این فایل فقط ارتقای DOB بعد از آن baseline است؛ full-history deployment script نیست. اسکریپت در transaction و با بررسی `__EFMigrationsHistory` اجرا می‌شود؛ هیچ default/backfill/UPDATE روی دادهٔ کاربر ندارد.

تست Migration، دیتابیس تصادفی `MakanApp_ProfileDob_Migration_<guid>` را فقط روی LocalDB می‌سازد، baseline را اعمال و Person/User و evidence قدیمی درج می‌کند؛ script جدید را دوبار اجرا و حفظ داده، null اولیه، قابلیت ثبت DOB و نبود pending model changes را assert می‌کند. DB موقت در finally حذف می‌شود؛ نام و server آن قبل از عملیات کنترل می‌شوند.

برای تولید از `--project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure` و DesignTime factory موجود استفاده شد. تلاش اولیه با startup Api خطای نبود reference ابزار EF Design داد؛ بدون نصب package یا تغییر پروژه، فرمان صحیح Infrastructure استفاده شد. `migrations list --no-connect` ثبت Migration را تأیید کرد؛ applied بودن دیتابیس‌های عادی را اعلام نمی‌کند. `has-pending-model-changes` موفق و نتیجه NONE بود.

هیچ Migration روی `MakanApp`، `MakanApp_Test`، Production یا Test server اعمال نشده است. Down تولیدشده ستون را حذف می‌کند؛ rollback روی دادهٔ DOB نیازمند تصمیم و حفاظت داده در عملیات مستقل است و اینجا اجرا نشده است. migration حذف MFA و migrationهای قبلی ویرایش نشده‌اند.

## 16. Test results — شواهد و Validation

وضعیت نهایی: **PASS؛ دو اجرای کامل موفق، با ورودی کد یکسان و بدون Commit/Push/Publish/Deploy.**

| مرحله | نتیجهٔ واقعی ثبت‌شده |
|---|---|
| Restore | PASS؛ packageها از قبل up-to-date بودند |
| Release Build پس از اصلاح‌ها | PASS؛ ۰ Warning و ۰ Error |
| Focused Unit نهایی | ۲۰ اجرا / ۲۰ Pass / ۰ Failed / ۰ Skipped |
| Focused Integration نهایی | ۱۲۷ اجرا / ۱۲۷ Pass / ۰ Failed / ۰ Skipped |
| Full regression اول | PASS؛ Unit=877، Integration=1381، Architecture=14؛ مجموع 2272، Failed=0، Skipped=0 |
| Final Build | PASS؛ ۰ Warning و ۰ Error |
| Final Test | PASS؛ Unit=877، Integration=1381، Architecture=14؛ مجموع 2272، Failed=0، Skipped=0 |
| Migration / model | isolated upgrade و idempotent دو اجرا PASS؛ pending model changes=NONE |

فرمان‌های validation:

```powershell
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Release --no-restore --nologo
dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo --logger trx --results-directory .artifacts/profile-messaging-epic-20261028-83050d3b/full-regression
dotnet build MakanApp.sln --configuration Release --no-restore --nologo
dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo --logger trx --results-directory .artifacts/profile-messaging-epic-20261028-83050d3b/final-regression
```

Focused Unit از `PersonDateOfBirthTests` و `PersonalMessagingStatusTests` است. Focused Integration شامل Identity، PlatformAuthorization، Organization، ChildSafety، lifecycle، realtime authorization، OpenAPI و Migration است؛ همهٔ ۳۲ case تازهٔ API/Messaging و تست Migration در آن پوشش دارند. تکرار focusedها تعداد تست جدید محسوب نمی‌شود.

افزایش واقعی نسبت به baseline اعلام‌شده: ۱۱ Unit و ۳۳ Integration، مجموع ۴۴ case جدید؛ تعداد Architecture همان ۱۴ است. زمان Integration اجرای کامل اول حدود ۲۲ دقیقه و ۴۲ ثانیه و اجرای نهایی حدود ۱۵ دقیقه و ۸ ثانیه بود. این دو مجموعهٔ ۲۲۷۲تایی دو بار اجرا شده‌اند؛ تعداد یکتای تست‌ها ۲۲۷۲ است، نه جمع دو اجرا.

نتیجه از Counters فایل‌های TRX زیر خوانده شد؛ فقط suite/count/status در summary محلی ذخیره شده‌اند، نه payload/credential:

```text
.artifacts/profile-messaging-epic-20261028-83050d3b/focused-unit-final
.artifacts/profile-messaging-epic-20261028-83050d3b/focused-regression-final
.artifacts/profile-messaging-epic-20261028-83050d3b/full-regression
.artifacts/profile-messaging-epic-20261028-83050d3b/final-regression
```

### چه تست‌هایی چه چیزی را اثبات می‌کنند؟

| دسته | شاهد واقعی |
|---|---|
| Unit Domain | null اولیه، امروز، آینده، leap day، تاریخ قدیمی بدون حداقل سن جدید، حفظ DOB با تغییر نام |
| Unit Application | Profile ناقص فقط hint خواندن می‌گیرد؛ verified/minor/conflict/unknown و restriction مجوز ساخت/کشف ایجاد نمی‌کنند |
| Integration Profile | JSON date-only، DB round-trip، null و کلاینت قدیمی، تاریخ آینده/نامعتبر، جدایی دو DOB، خوداظهاری بدون verification |
| Integration Privacy | wireهای People/Contacts/Conversation/Sender و schemaهای عمومی، log capture و audit بدون مقدار DOB |
| Integration Messaging | دو حساب بدون مدرسه، چهار Role مقصد، Group/Channel، Role addition/removal، Membership end و Parent باقی‌مانده |
| Integration Authorization | Privacy جهت‌دار Contacts/Nobody، Block دو جهت، suspension/session revoke، protected age، رسمی کلاس و Hub |
| Integration Migration | raw seed مطابق schema قبل، script دو بار، حفظ Person/evidence، نوشتن DOB بعد از upgrade و cleanup DB موقت |
| Architecture | suite موجود dependencyهای Onion؛ production dependency جدیدی اضافه نشد |

هیچ تست skipped/disabled نشده است. یک failure قبل از اصلاح برای بازتولید Profile gate ثبت شد؛ با failure باقی‌ماندهٔ validation نهایی یکسان نیست. هشدار xUnit2012 اولیه نیز با assertion امنِ مبتنی بر projection بولی رفع شد؛ raw log در failure assertion چاپ نمی‌شود.

## 17. Known limitations — مرزهای باقی‌مانده

- self-DOB optional است؛ سیاست required شدن یا clear آن هنوز مشخص نشده. مقدار null/omitted، DOB قبلی را پاک نمی‌کند.
- self-DOB نه evidence رسمی است و نه policy Conflict جدید؛ اختلاف با VerifiedBirthDate به‌طور خودکار طبقه‌بندی را تغییر نمی‌دهد.
- Profile پایهٔ فاقد Person/Username برای ساخت هویت نمایشی/شروع ارتباط همچنان prerequisite دارد؛ Inbox و settings خصوصی blanket deny نمی‌شوند. DOB و LearningProfile prerequisite جدید نیستند.
- انتخاب Workspace موجود حفظ است. session انتخاب‌شده روی Role پایان‌یافته باید با endpoint موجود به Personal برگردد؛ تبدیل پنهان Context درخواست سازمانی اجرا نشده است.
- حادثهٔ خاص Frontend بدون همان request/state بازتولید نشد؛ هیچ root cause حدسی برای آن ثبت نشده است.
- عمومیت audience همهٔ گروه/کانال‌های رسمی، Badge و revoke مخصوص Role در آن‌ها خارج از این Epic هستند. Class binding موجود و محافظ‌های رسمی دست‌نخورده‌اند.
- TestServer/LocalDB و Hub محلی اثبات تست سرور واقعی IIS، مرورگر خارجی، SMS یا AI مستقر نیستند.
- Migration و patch در Working Directory باقی می‌مانند تا مالک بررسی کند؛ Commit/Push/Publish/Deploy در این مرحله مجاز نیستند.

## 18. Remaining Product decisions — تصمیم‌های بعدی و نکات آموزشی

تصمیم‌های باز مرتبط با این مرحله: الزام احتمالی DOB برای قابلیت‌های آینده، پاک‌کردن DOB، سیاست تعارض self/evidence، فیلدهای Profile تکمیلی و UX انتخاب Workspace. هیچ minimum age تازه، خوداظهاری Verified Adult، Badge یا رفتار مالی از این مرحله نتیجه نمی‌شود.

نکات عملی برای Backend Developer:

1. «nullable در request» و «پاک‌کردن مقدار قبلی در PATCH» یک معنا ندارند؛ semantics حذف/عدم ارسال باید روشن و تست شوند.
2. `DateOnly` با SQL `date` از تبدیل timezone ناخواسته جلوگیری می‌کند؛ round-trip واقعی JSON و SQL از تست صرف mapping مهم‌تر است.
3. `AllowedActions` hint محصول است؛ مجوز نهایی روی resource/session/relationship اعمال می‌شود. Read hint نباید مجوز Send ساخته باشد.
4. Role، Membership، participant role و age verification چهار منبع متفاوت‌اند؛ ادغام آن‌ها به دسترسی ناخواسته می‌انجامد.
5. برای تست migration قبل از ستون جدید، نباید با EF model جدید همان Entity را روی schema قدیمی INSERT کرد؛ seed باید schema baseline را رعایت کند.
6. Test double برای SMS/AI، همراه SQL Server واقعی ایزوله، امنیت و رفتار persistence را می‌سنجد؛ این ترکیب ادعای موفقیت provider واقعی ایجاد نمی‌کند.
7. در مخزن dirty، hash baseline و diff مرتبط امکان حفاظت از کارهای دیگر را فراهم می‌کنند؛ stage کردن کل مخزن روش تفکیک تغییرات نیست.

مسیر پیشنهادی مطالعه:

```text
ProfileController
-> CompleteProfileCommand / CurrentUserResult
-> IdentityService.CompleteProfileAsync / BuildCurrentUserAsync
-> Person.SetDateOfBirth
-> EfIdentityStore / PersonConfiguration
-> AddSelfDeclaredProfileDateOfBirth
-> PersonalProfileDateOfBirthTests / PersonalProfileDateOfBirthMigrationTests
-> MessagingStatusPolicy.Evaluate
-> PersonalMessagingStatusTests / PersonalMessagingRoleLifecycleTests
-> guardهای موجود MessagingStore و MessagingHub
```

مرحلهٔ بعد تنها پس از بازبینی مالک: تصمیم دربارهٔ Commit/Push و آماده‌سازی انتشار/اجرای Migration در Task جداگانه. AI، Sources، Commerce و Badge نباید ضمن این کار وارد scope شوند.

### وضعیت Git و حفاظت از کار قبلی

شاخه `main`؛ HEAD قبل و پس از تغییرات فایل‌ها `b87fbd89cef33f26315fddd4a1e57116b44cc781` است. index خالی مانده است. **Commit=NO، Push=NO، Publish=NO، Deploy=NO** مطابق درخواست صریح این Epic؛ commit message/hash تازه یا remote verification وجود ندارد.

قبل از شروع، ۱۰ فایل modified و ۶ فایل untracked بودند:

```text
 M MakanApp.Api/Configuration/ProductionConfigurationValidator.cs
 M MakanApp.Api/Program.cs
 M MakanApp.Api/appsettings.Production.json.example
 M MakanApp.Api/appsettings.Test.json.example
 M MakanApp.Api/appsettings.json
 M README.md
 M docs/production/DEPLOYMENT_IIS.md
 M docs/production/PRODUCTION_READINESS.md
 M tests/MakanApp.IntegrationTests/ProductionConfigurationValidationTests.cs
 M tests/MakanApp.IntegrationTests/ProductionHostingTests.cs
?? docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md
?? docs/production/CORS_CONFIGURATION_FA.md
?? tests/MakanApp.IntegrationTests/CorsConfigurationSchemaTests.cs
?? tools/ai/provider_diagnostic.py
?? tools/ai/run_exact_ai.py
?? tools/ai/test_provider_diagnostic.py
```

manifest SHA-256 اولیه، ۱۲۴۱ فایل موجود از جمله configهای خصوصی را پوشش می‌دهد. فقط هفت فایل baseline مرتبط با همین Epic تغییر کرده‌اند؛ فایل‌های قبلی CORS/AI/Product و configهای خصوصی همان hash قبلی را دارند. فایل‌های OTP/provider، AI، حذف MFA و csprojها تغییر نکرده‌اند. سیزده فایل source/test/migration این Epic نیز برای تطبیق ورودی Final Test hash شده‌اند.

`git diff --stat` فایل‌های tracked در این بازبینی:

```text
17 files changed, 291 insertions(+), 34 deletions(-)
```

این عدد شامل تغییرات **قبلی** CORS هم هست؛ diff tracked این Epic هفت فایل با ۶۳ insertion و ۷ deletion است. هفت فایل تازهٔ Epic شامل Migration/Designer، چهار فایل تست و همین سند، untracked هستند و در diff stat معمولی شمرده نمی‌شوند. هیچ `git add` اجرا نشده است.

### Acceptance Matrix — شواهد واقعیِ تست محلی

این ماتریس نتیجهٔ تست‌های خودکار محلی است؛ تست دستی روی سرور Production/Test یا مرورگر خارجی انجام نشده است. «مجاز» در سطرهای Personal، با Personal Context و پیش‌نیازهای معتبر همان عملیات، از جمله سن/Privacy، معنا دارد.

```text
ACTIVE_ROLELESS_USER_CAN_USE_PERSONAL_MESSAGING=true
NO_ORGANIZATION_DOES_NOT_BLOCK_PERSONAL_MESSAGING=true
SCHOOL_ROLE_NOT_REQUIRED_FOR_PERSONAL_DM=true
PERSONAL_GROUP_ROLE_INDEPENDENT=true
PERSONAL_CHANNEL_ROLE_INDEPENDENT=true
OFFICIAL_CLASS_ACCESS_STILL_PROTECTED=true
PRIVACY_STILL_ENFORCED=true
BLOCK_STILL_ENFORCED=true
AGE_SAFETY_STILL_ENFORCED=true
SUSPENSION_STILL_ENFORCED=true
PERSONAL_DATA_SURVIVES_ROLE_LOSS=true
SIGNALR_PERSONAL_AUTHORIZATION_PASS=true
SELF_DECLARED_DOB_IMPLEMENTED=true
DOB_GET_ME_PASS=true
DOB_PROFILE_UPDATE_PASS=true
DOB_NULL_BACKWARD_COMPATIBLE=true
DOB_FUTURE_DATE_REJECTED=true
VERIFIED_BIRTH_DATE_UNCHANGED=true
DOB_NOT_PUBLICLY_EXPOSED=true
```

Criteria نهایی مرحله:

```text
REPOSITORY_AUDITED=true
PROFILE_DOB_IMPLEMENTED=true
DOB_BACKWARD_COMPATIBLE=true
DOB_SEPARATE_FROM_VERIFIED_AGE=true
PERSONAL_MESSAGING_ROLE_INDEPENDENT=true
PERSONAL_MESSAGING_ORGANIZATION_INDEPENDENT=true
OFFICIAL_MESSAGING_AUTHORIZATION_PRESERVED=true
PRIVACY_PRESERVED=true
BLOCK_PRESERVED=true
AGE_SAFETY_PRESERVED=true
SUSPENSION_PRESERVED=true
SIGNALR_AUTHORIZATION_PRESERVED=true
NO_UNRELATED_AI_CHANGE=true
NO_OTP_CHANGE=true
MFA_REMOVAL_PRESERVED=true
CORS_CHANGES_PRESERVED=true
NO_SECRET_LEAK=true
MIGRATION_SAFE=true
BUILD_RELEASE_PASS=true
FOCUSED_TESTS_PASS=true
FULL_TEST_SUITE_PASS=true
NO_COMMIT=true
NO_PUSH=true
NO_PUBLISH=true
NO_DEPLOY=true
```

### گزارش پایانی ۲۹ مورد درخواستی

| مورد | نتیجهٔ نهایی |
|---|---|
| 1. Branch | `main` |
| 2. HEAD قبل/بعد | هر دو `b87fbd89cef33f26315fddd4a1e57116b44cc781` |
| 3. Git status اولیه | ۱۰ modified + ۶ untracked؛ فهرست کامل بالاتر ثبت شده است |
| 4. فایل‌های قبلی محفوظ | همهٔ ۱۶ dirty file قبلی و configهای خصوصی بدون تغییر hash؛ ۱۲۳۴ فایل baseline خارج از تغییرات مجاز دست‌نخورده‌اند |
| 5. Root Cause پیام‌رسان | ناسازگاری hint خواندن Profile ناقص در `MessagingStatusPolicy.Evaluate` بازتولید و رفع شد؛ گزارش کلی Frontend بدون Role بازتولید نشد |
| 6. Age Policy | VerifiedBirthDate و verification authority مستقل؛ Minor/Unknown/Privacy/Safety جاری حفظ |
| 7. Profile gate | DOB شرط جدید نیست؛ ناقص فقط hint خواندن، ایجاد ارتباط نیازمند هویت نمایشی/قواعد قبلی |
| 8. فایل‌های DOB | Person، IdentityContracts، IdentityService، PersonConfiguration، Migration/Designer/Snapshot و تست‌های بخش 4 |
| 9. فایل‌های Messaging | فقط MessagingStatus و تست‌های Status/RoleLifecycle؛ Store/Hub/resource policies تغییر نکرده‌اند |
| 10. Entity/Domain | Person.DateOfBirth nullable و SetDateOfBirth؛ entity تکراری ساخته نشد |
| 11. EF | date nullable روی identity.Persons؛ PK/FK/index/rowversion موجود حفظ |
| 12. Migration | `20261028102653_AddSelfDeclaredProfileDateOfBirth`؛ فقط روی DB ایزوله تست شد |
| 13. API | فیلد additive در PATCH profile و self result؛ route جدید ندارد |
| 14. OpenAPI | date nullable/optional واقعی تست شد؛ schemaهای عمومی Messaging بدون DOB |
| 15. SignalR | کد Hub بدون تغییر؛ Personal subscription موفق و official subscription غیرمجاز رد |
| 16. Authorization | مجوز رسمی/session/account/participant و رابطهٔ جاری حفظ؛ hint مجوز resource نمی‌سازد |
| 17. Privacy/Block | Contacts جهت‌دار، Nobody، Block دو جهت و history retention با تست تأیید |
| 18. تست‌های جدید | ۱۱ Unit و ۳۳ Integration، مجموع ۴۴ case؛ تست‌های قبلی Skip/Disable نشدند |
| 19. Focused | Unit=20، Integration=127؛ همگی Passed، Failed=0، Skipped=0 |
| 20. Unit | 877/877 در هر دو اجرای کامل |
| 21. Integration | 1381/1381 در هر دو اجرای کامل |
| 22. Architecture | 14/14 در هر دو اجرای کامل |
| 23. Failed | 0 در full و final regression؛ failure پیش از fix فقط شاهد بازتولید است |
| 24. Skipped | 0 در هر دو اجرای کامل |
| 25. Build | Final Release: موفق، Warning=0، Error=0؛ Restore نیز موفق |
| 26. git diff --stat | ۱۷ فایل tracked، ۲۹۱ insertion و ۳۴ deletion؛ تغییرات قبلی نیز در این عددند |
| 27. git status | فهرست کامل پایان کار در ادامه؛ index خالی، همهٔ تغییرات uncommitted |
| 28. Acceptance Matrix | هر ۱۹ شاخص بخش قبل true بر اساس تست محلی؛ سرور دستی تست نشده است |
| 29. تصمیم‌های باز | required/clear DOB، تعارض self/evidence و UX انتخاب Workspace؛ سایر Epicها خارج از scope |

خروجی کامل `git diff --stat` در پایان کار، شامل تغییرات قبلیِ محفوظ:

```text
 .../ProductionConfigurationValidator.cs            |  13 +-
 MakanApp.Api/Program.cs                            |  26 ++--
 MakanApp.Api/appsettings.Production.json.example   |   5 +-
 MakanApp.Api/appsettings.Test.json.example         |   5 +-
 MakanApp.Api/appsettings.json                      |   1 +
 MakanApp.Application/Identity/IdentityContracts.cs |   6 +-
 MakanApp.Application/Identity/IdentityService.cs   |  14 ++-
 MakanApp.Application/Messaging/MessagingStatus.cs  |   7 +-
 MakanApp.Domain/Identity/Person.cs                 |  12 ++
 .../Configurations/Identity/PersonConfiguration.cs |   1 +
 .../Migrations/MakanDbContextModelSnapshot.cs      |   3 +
 README.md                                          |   2 +-
 docs/production/DEPLOYMENT_IIS.md                  |   3 +-
 docs/production/PRODUCTION_READINESS.md            |   4 +-
 .../ProductionConfigurationValidationTests.cs      |  64 +++++++++-
 .../ProductionHostingTests.cs                      | 132 +++++++++++++++++++++
 .../Messaging/PersonalMessagingStatusTests.cs      |  27 ++++-
 17 files changed, 291 insertions(+), 34 deletions(-)
```

خروجی `git status --short --untracked-files=all` در پایان کار:

```text
 M MakanApp.Api/Configuration/ProductionConfigurationValidator.cs
 M MakanApp.Api/Program.cs
 M MakanApp.Api/appsettings.Production.json.example
 M MakanApp.Api/appsettings.Test.json.example
 M MakanApp.Api/appsettings.json
 M MakanApp.Application/Identity/IdentityContracts.cs
 M MakanApp.Application/Identity/IdentityService.cs
 M MakanApp.Application/Messaging/MessagingStatus.cs
 M MakanApp.Domain/Identity/Person.cs
 M MakanApp.Infrastructure/Persistence/Configurations/Identity/PersonConfiguration.cs
 M MakanApp.Infrastructure/Persistence/Migrations/MakanDbContextModelSnapshot.cs
 M README.md
 M docs/production/DEPLOYMENT_IIS.md
 M docs/production/PRODUCTION_READINESS.md
 M tests/MakanApp.IntegrationTests/ProductionConfigurationValidationTests.cs
 M tests/MakanApp.IntegrationTests/ProductionHostingTests.cs
 M tests/MakanApp.UnitTests/Messaging/PersonalMessagingStatusTests.cs
?? MakanApp.Infrastructure/Persistence/Migrations/20261028102653_AddSelfDeclaredProfileDateOfBirth.Designer.cs
?? MakanApp.Infrastructure/Persistence/Migrations/20261028102653_AddSelfDeclaredProfileDateOfBirth.cs
?? docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md
?? docs/product/PROFILE_AND_PERSONAL_MESSAGING_IMPLEMENTATION_FA.md
?? docs/production/CORS_CONFIGURATION_FA.md
?? tests/MakanApp.IntegrationTests/CorsConfigurationSchemaTests.cs
?? tests/MakanApp.IntegrationTests/PersonalMessagingRoleLifecycleTests.cs
?? tests/MakanApp.IntegrationTests/PersonalProfileDateOfBirthMigrationTests.cs
?? tests/MakanApp.IntegrationTests/PersonalProfileDateOfBirthTests.cs
?? tests/MakanApp.UnitTests/Identity/PersonDateOfBirthTests.cs
?? tools/ai/provider_diagnostic.py
?? tools/ai/run_exact_ai.py
?? tools/ai/test_provider_diagnostic.py
```
