# گزارش و راهنمای آمادگی انتشار Backend

تاریخ: `2026-10-22`؛ مخزن اصلی `D:\Project\MakanApp\MakanApp-Backend`.
این گزارش اعتبارسنجی محلی کد و بسته است، نه اثبات استقرار IIS یا پذیرش زیرساخت Production.

## ۱. خلاصه

یکپارچه‌سازی قبلی در `20f6034` موجود بود؛ این مرحله آن را دوباره اجرا نمی‌کند.
نقص باقی‌مانده، مقدار Mock در الگوی Production و نبود regression روی جفت فایل خصوصی موجود بود.
اکنون Production به OTP واقعی اختصاص دارد و Test به Mock مجاز؛ schema دوطرفه آزموده می‌شود.
هیچ تغییر رفتار Authentication، مدل، migration، AI یا adapter ملی‌پیامک ایجاد نشده است.
شکست واقعی regression هم‌زمانی Block با اصلاح محدود query قفل بر index موجود برطرف شد.

## ۲. معماری

در Domain/Application تغییری نداریم. تنظیمات و profile در Api و شاهد رفتار در پروژه‌های تست‌اند.
تنها تغییر Infrastructure محدودکردن query قفل Block به seek بر index موجود است؛ سیاست محصول ثابت ماند.
Options و composition root موجود دوباره استفاده می‌شوند؛ وابستگی Onion و نسخهٔ .NET/EF/SQL Server ثابت‌اند.
Test Double فقط مرز `IOtpDeliveryProvider` را در میزبان تست جایگزین می‌کند، نه use case یا اعتبارسنجی challenge.

## ۳. فایل‌های مهم جدید

| فایل | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| `tests/MakanApp.IntegrationTests/ReleaseEnvironmentOtpTests.cs` | هشت مورد واقعی Test/Production با SQL ایزوله | نام محیط را از DI واقعی بخوانید، نه از نام تست |
| `MakanApp.Api/Properties/PublishProfiles/ProductionIis.pubxml` | انتخاب Production و حذف فایل Test/Development از Publish | جداسازی بسته‌ها مکمل محافظ startup است، نه جایگزین آن |
| همین سند | قرارداد تنظیمات، اجرای مجدد و محدودیت پذیرش | smoke محلی با عملیات سرور یکسان نیست |

## ۴. فایل‌های تغییرکرده و تصمیم Commit

| فایل | هدف | اعتبارسنجی | Commit |
|---|---|---|---|
| `MakanApp.Api/appsettings.Production.json.example` | OTP واقعی `true/false`، بدون secret | parse، schema، binding و OTP | بله |
| `tests/MakanApp.UnitTests/Identity/TestConfigurationSchemaTests.cs` | مقایسه دوطرفهٔ actual/template، nested arrays، null و duplicate key | Unit معمولی | بله |
| `tests/MakanApp.UnitTests/Identity/ServerMockOtpConfigurationTests.cs` | تفاوت عمدی OTP دو محیط و قرارداد profile | Unit معمولی | بله |
| `tests/MakanApp.IntegrationTests/TestEnvironmentConfigurationTests.cs` | binding واقعی Options هر دو محیط | Integration | بله |
| `tests/MakanApp.IntegrationTests/OtpConfigurationPrecedenceTests.cs` | Production امن پیش‌فرض و رد override ناامن | Integration | بله |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.Safety.cs` | قفل زوج فعال روی index یکتا، نه scan کلید اصلی | concurrency SQL واقعی | بله |
| `tests/MakanApp.IntegrationTests/MessagingSafetyEndpointsTests.cs` | همان تست idempotency با ۴/۸/۱۲ درخواست موازی | سه سناریوی Integration | بله |
| `README.md` و دو راهنمای `docs/dev/CONFIGURABLE_OTP_FA.md`، `docs/dev/SERVER_MOCK_OTP_FA.md` | حذف توصیهٔ قدیمی false/true برای Production | بازبینی | بله |
| `docs/CURRENT_PROJECT_STATUS.md` | لینک شاهد جدید بدون حذف تاریخچه | بازبینی | بله |
| فایل خصوصی `MakanApp.Api/appsettings.Production.json` | فقط دو boolean مربوط به OTP | schema و حفظ تمام متن غیر OTP | خیر؛ ignored و دارای secret |
| فایل خصوصی `MakanApp.Api/appsettings.Test.json` | بدون تغییر؛ Mock حفظ شد | schema و SHA-256 | خیر؛ ignored |
| `tools/ai/provider_diagnostic.py` | Source/debug ناتمام AI، کار مستقل قبلی | SHA-256 بدون تغییر؛ scan بدون secret واقعی | خیر |
| `tools/ai/run_exact_ai.py` | Source/debug ناتمام AI، کار مستقل قبلی | SHA-256 بدون تغییر؛ اجرا نشد | خیر |
| `tools/ai/test_provider_diagnostic.py` | تست ابزار debug قبلی، خارج از این تحویل | SHA-256 بدون تغییر؛ اجرا نشد | خیر |
| `.artifacts/release-validation` | خروجی Publish، schema SQL، دادهٔ smoke | ignored و ACL خصوصی بسته‌ها | خیر |

## ۵. جریان واقعی OTP

`AuthController` → `IdentityService` → `IOtpDeliveryProvider` در حالت واقعی →
`OtpChallenge` با HMAC/salt، انقضا و محدودیت تلاش → `EfIdentityStore`/`MakanDbContext` →
SQL Server → `UserSession` و refresh token محافظت‌شده → پاسخ Bearer.

در Mock، تنها بررسی مقدار challenge حذف می‌شود؛ ورودی همچنان باید فرمت مجاز داشته باشد.
پیامک و challenge واقعی ساخته نمی‌شوند و صدور نشست از همان مسیر Application انجام می‌شود.
توکن کاربر فعلی opaque است، نه JWT. JWT سرویس AI قرارداد مستقل دارد و تغییر نکرده است.

## ۶. دیتابیس

Schema جدیدی نداریم؛ migration جدید ساخته نشد و auto-migration startup اضافه نشد.
شاهد deadlock در LocalDB روی `messaging.UserBlocks / PK_UserBlocks / RangeS-U` بود.
query `LockActiveBlockAsync` با `UPDLOCK/HOLDLOCK` اکنون `INDEX(UX_UserBlocks_Active_DirectionalPair), FORCESEEK`
دارد تا بازهٔ همان زوج روی index فعال قفل شود. index از قبل موجود بود؛ retry جدید یا کاهش isolation اضافه نشد.
trade-off این است که نام index وابستگی صریح Infrastructure است؛ تغییر نام آینده باید query و تست SQL واقعی را هماهنگ کند.
تست‌ها روی LocalDB اختصاصی suite اجرا می‌شوند، نه دیتابیس عادی MakanApp.
برای اجرای خروجی Publish یک دیتابیس یکتای `MakanApp_ReleaseSmoke_*` با ۴۸ migration موجود ساخته می‌شود.
اسکریپت EF غیر idempotent برای دیتابیس تازه و `sqlcmd -I -b` استفاده می‌شود؛
`QUOTED_IDENTIFIER` برای filtered indexهای SQL Server لازم است.
این اجرا وضعیت migrationهای سرور واقعی را اثبات نمی‌کند.

## ۷. Endpointهای آزموده‌شده

هیچ API/DTO جدیدی ایجاد نشد.

| Method/Route | ورودی/خروجی و کنترل |
|---|---|
| `POST /api/v1/auth/otp/challenges` | شمارهٔ معتبر؛ پاسخ 202 با شناسه و زمان، نه کد؛ در Mock بدون SMS/SQL challenge |
| `POST /api/v1/auth/otp/verify` | شناسه، شماره، کد؛ مسیر واقعی challenge و مسیر Mock مجاز؛ کد بد 400، منقضی 410، تلاش زیاد 429، حساب معلق 403 |
| `GET /api/v1/me` | نشست معتبر؛ اثبات مصرف عادی Bearer |
| `POST /api/v1/auth/refresh` | refresh فعلی؛ صدور عادی پاسخ بدون bypass |
| `GET /api/admin/v1/me` | کاربر بدون نقش/MFA مجاز نیست؛ 403 |
| `GET /api/v1/organization/people` | ورود به‌تنهایی workspace مدیر سازمان نمی‌سازد؛ 403 |
| `GET /health/live`، `GET /health/ready` | smoke خروجی Publish روی loopback و وابستگی‌های محلی |

تست جدید عمداً حساب موجود را می‌سازد و همان UserId را در پاسخ کنترل می‌کند.
ثبت‌نام خودکار موجود در `IdentityService` بازطراحی یا حذف نشده است.

## ۸. مفاهیم فنی

binding با DI واقعی؛ تفکیک authentication و authorization؛ schema در برابر value؛
override محدود process؛ fail-closed Production؛ تست SQL واقعی و استفاده از test double فقط در مرز خارجی.

## ۹. تصمیم‌ها و trade-off

قرارداد دائمی: `Production schema == Test schema`، نه فقط شمول یک‌طرفه.
نوع Boolean مستقل از true/false است؛ null فقط با null هم‌ساختار است.
کلید اضافه/کم، duplicate حتی با تفاوت casing، nested object/array و نوع ناسازگار خطا می‌شوند.
مقایسهٔ array به‌صورت محافظه‌کارانه همهٔ indexها و طول را می‌سنجد.
الگوهای عمومی همیشه در CI آزموده می‌شوند؛ فایل خصوصی موجود نیز آزموده می‌شود و در نبود آن الگو مرجع است.
CI برای ساخت فایل secretدار یا کپی credentialهای Production مجاز نمی‌شود.

OTP تنها دو property واقعی دارد: `Enabled` و `AllowMockAuthentication`.
Test=`false/true`؛ Production=`true/false`؛ Development بدون تغییر ماند.
تنظیم‌های قدیمی فعال‌کنندهٔ DevelopmentBypass همچنان رد می‌شوند.
حفاظت Production را برای عبور smoke تضعیف نکردیم؛ استثنای گواهی فقط برای LocalDB تست process-scoped است.

## ۱۰. تست‌ها

Unit: ساختار actual و template، کلید اضافه/کم، duplicate، null، nested arrays و profile انتشار.
Integration جدید: حساب موجود در دو محیط، حساب معلق در دو محیط، challenge ناموجود/کد غلط/
سقف تلاش در Production، انقضا و دو ترکیب ناامن startup.
در Test هر فراخوانی delivery باعث شکست تست می‌شود؛ شمارش باید صفر بماند.
در Production ثبت adapter واقعی ملی‌پیامک پیش از جایگزینی مرز خارجی بررسی می‌شود.
سپس provider کنترل‌شده در حافظه کد می‌دهد؛ HMAC/challenge/SQL/نشست واقعی‌اند و SMS ارسال نمی‌شود.
binding شامل OTP، Session، Messaging/Ephemeral، PushDevice، MFA/DataProtection،
Storage، NotificationDelivery، Payment، Reports، AI/Auth، OperationalMetrics، Cleanup و HostOptions است.
transportهای Push در تنظیم Unavailable عمداً ثبت نمی‌شوند؛ این نبود، خطای DI نیست.
Architecture suite بدون تغییر قواعد Onion را کنترل می‌کند.
تست Block/Report حذف یا سریالی نشد؛ با چهار، هشت و دوازده درخواست واقعاً موازی اجرا می‌شود.

## ۱۱. Validation

Restore و Debug Build موفق؛ warning/error کامپایل صفر.
تست متمرکز: ۱۴۸ Unit و ۱۶۲ Integration موفق؛ زیرمجموعهٔ Configuration/Options برابر ۱۲۸ است.
تست تقویت‌شدهٔ Block/Report: هر سه سناریوی ۴/۸/۱۲ درخواست موازی موفق.
Full Regression اولیه یک deadlock داشت؛ پس از اصلاح، Debug suite با
۸۷۳ Unit، ۱۳۵۲ Integration و ۱۴ Architecture، جمع ۲۲۳۹، بدون failure/skip موفق شد.
Release Build، Publish هر دو profile و بررسی assemblies/JSON/web.config/حذف کانفیگ محیط دیگر موفق شدند.
هر دو برنامهٔ منتشرشده روی loopback شروع شدند؛ liveness/readiness=200 و درخواست بدون نشست=401.
لاگ startup انتخاب Mock در Test و Real OTP در Production را تأیید کرد.
هر دو با Ctrl+C shutdown شدند؛ listener باقی نماند و دیتابیس موقت smoke پس از تأیید خالی‌بودن Users حذف شد.

هشدارهای runtime پنهان نشدند: دو `EF20601` درباره sentinel برای
`OnlineAudience/LastSeenAudience` از مدل قبلی وجود دارند؛ enum معتبر ۱/۲/۳ است، نه صفر،
و این هشدار به معنی پذیرفتن Nobody به‌جای مقدار دیگری نیست.
رخداد امن `EF_CORE_DIAGNOSTIC/10103` نیز ثبت شد؛ exception یا دادهٔ خصوصی ندارد.
هشدار روشن‌بودن Mock در Test و استثنای گواهی فقط LocalDB در smoke Production انتظار می‌رفتند.
Final Release Build نیز با صفر warning/error و Final Release Tests با
۸۷۳ Unit، ۱۳۵۲ Integration و ۱۴ Architecture، مجموع ۲۲۳۹، صفر failure و صفر skip موفق شدند.
زمان Integration اجرای Debug اصلاح‌شده ۱۰دقیقه‌و۲۶ثانیه و اجرای نهایی Release ۱۰دقیقه‌و۴۳ثانیه بود.
هر شش فایل TRX دو اجرای موفق بررسی می‌شوند تا total/executed/passed برابر و failed/notExecuted صفر باشند.

## ۱۲. Git و کارهای محفوظ

Baseline: `main@20f6034` همگام با `origin/main`؛ تنها سه ابزار Python untracked بودند.
Commit/Push فقط پس از موفقیت تمام gateهای درخواست انجام می‌شود؛ hash و تأیید remote در گزارش نهایی است.
دو تغییر مستقل با پیام‌های `fix(messaging): prevent active block retry deadlocks` و
`test(release): validate environment OTP and publish readiness` جدا ثبت می‌شوند.
backup اولیهٔ ۲۷ فایل و backup بعدی ۲۵ فایل و SHAهای آن‌ها حفظ شدند.
۱۱۴ فایل دو worktree مستندسازی و پنج فایل draft توسعه دست‌نخورده‌اند.
Worktreeهای OTP و STEP10G commit گم‌شده نسبت به main ندارند.
draftهای dev seed که scope دیتابیس را گسترش می‌دهند دوباره کپی نشدند.
هیچ worktree، فایل خصوصی یا artifact قبلی حذف نشد.
فهرست پنج فایل محفوظ worktree توسعه: `docs/dev/DEVELOPMENT_TEST_USERS_FA.md`،
`docs/dev/cleanup-development-test-users.sql`، `docs/dev/create-development-test-users.sql`،
`docs/dev/check-development-test-users-database.sql` و `tests/MakanApp.IntegrationTests/DevelopmentTestUsersTests.cs`.
جزئیات audit قبلی worktreeها در [گزارش consolidation](../release/BACKEND_FINAL_CONSOLIDATION_FA.md) است؛
در این مرحله hash فایل‌های محفوظ و نبود commit گم‌شده دوباره بررسی شد.

## ۱۳. محدودیت‌های فعلی

AI Backend implementation از regression محلی سنجیده می‌شود؛ Service Auth قبلاً واقعی PASS شده است.
Full-12 واقعی Deferred و Parent Child Summary دارای AI_PROVIDER_TIMEOUT خارجی است.
هیچ تماس AI تازه، تغییر Auth، افزایش timeout ۳۵ثانیه‌ای یا retry اضافه نشده است.
Push دستگاه واقعی، رسانه P3، gateway پرداخت و gateهای عملیات هدف، با این smoke بسته نمی‌شوند.
پیش از استقرار باید secret store، مسیر پایدار Data Protection، ACL هویت IIS، Storage و TLS هدف تنظیم شوند.
محیط Mock Test نباید به داده، credential یا key ring محیط واقعی Production متصل شود؛
smoke این مرحله با overrideهای process و منابع محلی جدا اجرا شد.
بسته‌های محلی دارای config خصوصی‌اند: در Git، چت یا فضای عمومی توزیع نشوند.

## ۱۴. نکته‌های آموزشی

۱. داشتن نام Production در تست کافی نیست؛ Environment و Options مؤثر را از host بخوانید.
۲. SQL واقعی باقی بماند و فقط شبکهٔ خارجی را با test double قطع کنید.
۳. Schema برابر به معنی یکسان‌کردن فلگ امنیتی دو محیط نیست.
۴. assert روی مسیر ناموجود 404 اثبات authorization نیست؛ route واقعی را تست کنید.
۵. opaque session را برای نوشتن گزارش JWT ننامید.
۶. Publish و startup محلی، مدرک deploy یا custody کلیدهای سرور نیستند.

## ۱۵. ترتیب مطالعه

`OtpAuthenticationOptions` → `DependencyInjection.AddOtpDeliveryProvider` → `AuthController` →
`IdentityService` → `OtpChallenge` → `EfIdentityStore` → تست‌های schema/binding →
`ReleaseEnvironmentOtpTests` → profileهای TestIis/ProductionIis.

## ۱۶. اجرای مجدد و مرحلهٔ بعد

از مخزن اصلی پس از restore/build/test موفق:

```powershell
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/release-validation/test
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=ProductionIis --output .artifacts/release-validation/production
```

در checkout فاقد فایل خصوصی، ابتدا از الگوی بدون secret همان محیط با تنظیمات امن خودش provision کنید.
web.config خروجی باید محیط همان profile را داشته باشد؛ DOTNET_ENVIRONMENT متعارض در IIS باقی نماند.
استقرار واقعی کار بعدیِ جداگانه است؛ هیچ IIS یا دیتابیس سرور در این مرحله تغییر نمی‌کند.
