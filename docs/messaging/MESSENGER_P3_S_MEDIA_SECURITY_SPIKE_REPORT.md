# گزارش P3-S — توقف در S1 به‌علت نیاز زیرساخت

تاریخ بررسی `2026-10-17`؛ starting commit: `3cfe196dc0de1bbb0c36653834fde544f1d1ed4e`؛ worktree مستقل Messenger، شاخهٔ `messenger/p0` و upstream موجود `origin/main`. در شروع `git status --short` خالی بود و پس از fetch، HEAD و origin/main برابر starting commit بودند. مرجع محصول همان [P3 Media Policy v1](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) است؛ policy و تصمیم‌های تاریخی P3-0 تغییر نکرده‌اند.

**نتیجهٔ preflight: P3_S_INFRA_REQUIRED.** S0 با regression موفق اجرا شد و S1 فقط بررسی read-only محیط بود؛ آزمایش رسانه و فازهای S2–S23 شروع نشده‌اند. این گزارش نتیجهٔ موفق/ناموفق LiveKit نیست و هیچ stale-token window را اندازه‌گیری نکرده است. طبق شرط صریح S1، تا تأمین محیط مجاز، harness، Docker deployment، token issuer و endpoint محصول ایجاد نمی‌شوند. P3-A نیز آغاز نشود.

## ۱. شواهد واقعی محیط موجود

| بررسی read-only | مشاهدهٔ واقعی | معنی محدود مشاهده |
|---|---|---|
| OS میزبان | Microsoft Windows، نسخهٔ گزارش‌شده `10.0.22000` | Linux media host در این بررسی وجودِ احرازشده ندارد |
| `Get-Command docker,wsl,ssh` | هر سه CLI موجودند | وجود CLI اثبات daemon، VM یا سرور مجاز نیست |
| `docker version --format '{{json .}}'` | Client=`27.4.0`؛ context=`desktop-linux`؛ Server=null؛ Linux Engine named pipe یافت نشد | Docker daemon قابل‌استفاده برای spike در این context در دسترس نیست؛ هیچ image pull یا container اجرا نشد |
| `docker context ls` | default و desktop-linux؛ desktop-linux جاری | context تغییر نکرد؛ هیچ remote Docker endpoint آزمایش نشد |
| `wsl --list --quiet` | صفر خط نام توزیع پس از حذف NUL/whitespace | هیچ distro ثبت‌شده‌ای در خروجی این فرمان نیست؛ distro نصب/راه‌اندازی نشد |
| `wsl --status` | default version2؛ پیام `WSL 2 kernel file is not found` | kernel آماده نیست؛ update، install یا تغییر تنظیمات Windows انجام نشد |
| متغیرهای صریح media | نامی با الگوی LIVEKIT/MEDIA_SPIKE/P3_SPIKE در environment جاری یافت نشد | مقادیر هیچ secretی چاپ نشد؛ نبود نام متغیر، اثبات نبود تمام secret storeهای ممکن نیست |
| مسیرهای tooling | tools/media-spike و deploy/media-spike موجود نیستند | tooling آیندهٔ plan هنوز ساخته نشده؛ ابزار موجود فرض نشود |
| محیط خارجی | Linux lab مجاز، آدرس/روش دسترسی آماده، IP/DNS/TLS و کنترل firewall معرفی/احراز نشده‌اند | هیچ سرور قدیمی Makan، IIS، SSH host یا endpoint ناشناخته‌ای probe/deploy نشد |
| پاسخ صریح مالک | «فعلاً چنین محیطی آماده نیست» | نبود محیط آماده تأیید شد؛ اجرای S2 و مراحل رسانه مجاز به فرض‌کردن محیط نیست |

این نتیجه محدود به context فعلی و اطلاعات ارائه‌شده است؛ ادعای نبود هر Linux host دیگری در اختیار مالک نیست. صرف رفع Docker/WSL محلی هم IP عمومی، TLS، TURN و دو مسیر شبکهٔ موردنیاز آزمایش کامل را ثابت نمی‌کند.

## ۲. بررسی مستندات رسمی؛ نه شاهد آزمایش

در [مرجع رسمی token lifecycle](https://docs.livekit.io/frontends/reference/tokens-grants/) دوباره تأیید شد که در self-hosted، RemoveParticipant یا تغییر permission توکن قبلی را invalid نمی‌کند؛ TTL کوتاه و توقف صدور توکن تازه توصیه شده است. token refresh داخلی نیز باید مستقل آزمایش شود. این اطلاعات **reference evidence** است، نه اجرای REUSE_SAME_TOKEN_AFTER_REMOVAL و نه رأی FAIL برای نسخه‌ای که مستقر نشده است.

راهنمای [deployment](https://docs.livekit.io/transport/self-hosting/deployment/) و [ports/firewall](https://docs.livekit.io/transport/self-hosting/ports-firewall/) برای فهرست تأمین محیط بررسی شدند. تنظیمات زیر هنوز اجرا، pin یا آزمایش نشده‌اند؛ defaultهای vendor جای policy ماکان را نمی‌گیرند. هیچ binary/image/SDK یا نسخهٔ latest دانلود نشد؛ انتخاب نسخهٔ دقیق stable و image digest در S2، پس از عبور S1 انجام شود.

## ۳. فهرست دقیق تأمین زیرساخت برای ادامه

1. **Linux lab اختصاصی و غیرProduction:** host/VM مجاز با نام مشخص، OS/architecture پشتیبانی‌شدهٔ نسخهٔ انتخابی، Docker Engine قابل‌دسترسی و دسترسی خروجی برای image/SDK/CA. resource و bandwidth بودجهٔ pilot دو client مشخص باشد؛ هیچ عدد ظرفیت تجاری از این preflight نتیجه نمی‌شود.
2. **دسترسی عملیاتی آماده:** روش SSH یا console تأییدشده، credential فقط در محیط امن اجرا، امکان محدود و مجاز تغییر firewall/NAT و lifecycle سرویس آزمایش؛ نه رمز/private key در چت یا Git. سرور فعلی Makan نباید به‌طور ضمنی lab فرض شود.
3. **IP عمومی و mapping درست:** IP ثابت یا mapping قابل‌دسترسی، advertised candidate درست و مسیر رفت/برگشت UDP/TCP. IP خصوصیِ Docker یا موفقیت HTTP به‌تنهایی کافی نیست.
4. **DNS/TLS:** نام‌های signaling و TURN، CA معتبر، certificate chain/renewal و private key خارج repository؛ طرح endpoint/IP یا L4 routing برای هم‌زیستی signaling443 و TURN/TLS443. هنوز دامنه‌ای انتخاب/تست نشده است.
5. **Firewall محدود:** طرح پورت‌های جدول بعد، ورودی/خروجی و مسیرهای proxy/management مشخص؛ امکان قطع کنترل‌شدهٔ UDP و signaling/media برای fault test، فقط در lab. بازکردن range عمومی بدون توجیه ممنوع است.
6. **TURN authenticated:** embedded TURN گزینهٔ pilot مصوب است؛ endpoint عمومی، UDP و TCP/TLS fallback، credential موقت و بدون admin secret در client؛ امکان forced relay و مشاهدهٔ selected candidate pair لازم است.
7. **دو client آزمایشی کنترل‌شده:** actorهای synthetic A/B، دو اتصال مستقل و identity جدا برای B-device-1/2، synthetic audio بدون ذخیرهٔ raw media؛ ترجیحاً دو شبکه، و امکان UDP-block و network interruption. frontend محصول clone/تغییر نشود.
8. **کنترل و fault injection ایزوله:** محل اجرای harness .NET9، state Allowed/Revoked آزمایشی، مسیر callback/gateway آزمایش، امکان delay/unavailable/duplicate webhook و remove failure؛ clock/timeline معتبر و مشاهدهٔ قطع publish/subscribe. دیتابیس معمولی Makan استفاده نشود.
9. **Secret/evidence custody:** API key/secret، TURN credential و TLS key در secret store محیط؛ لاگ‌ها بدون JWT/SDP/media/PII، evidence ignored با ACL و حد نگه‌داری مشخص، cleanup فقط resourceهای دارای RunId و ownership تأییدشده.

| مسیر موردنیاز آینده | گزینهٔ مستنداتی برای طرح محدود | دسترسی هدف، نه پورت تست‌شده |
|---|---|---|
| signaling/API با TLS | public TCP443 → proxy → LiveKit7880 | 443 عمومیِ کنترل‌شده؛ 7880 فقط proxy/internal و admin credential فقط server |
| ICE/UDP | mux روی UDP7882، یا range محدود موجه مطابق config نسخهٔ pinشده | public media؛ mux و range بی‌دلیل هم‌زمان باز نشوند |
| ICE/TCP | TCP7881 در صورت انتخاب topology | public media؛ موفقیت این مسیر معادل TURN نیست |
| TURN/UDP / STUN | UDP3478 یا UDP443 طبق topology مصوب lab | public و authenticated برای relay |
| TURN/TLS | public TCP443 روی endpoint مناسب؛ 5349 فقط اگر پشت L4 LB متناظر انتخاب شد | TLS معتبر؛ listener/published port باید با topology نسخهٔ انتخابی سازگار باشد |
| admin، harness authority، metrics و secret store | پورت‌ها پس از طراحی مشخص شوند | فقط مدیریت/private؛ نه SQL/admin interface عمومی |

**هیچ پورت firewall باز یا تست نشده است.** این جدول provisioning checklist است و جای «exact tested ports» را جعل نمی‌کند. استفاده از mux، نیاز به range وسیع vendor را به‌صورت پیش‌فرض تحمیل نمی‌کند؛ topology نهایی در S2/S3 ثبت و آزمایش شود.

## ۴. وضعیت مراحل و نتایج درخواست‌شده

| مرحله/موضوع | نتیجهٔ این اجرا |
|---|---|
| S0 / Backend baseline | restore/build موفق، صفر warning/error؛ Unit558 + Integration1023 + Architecture14 = 1595 executed/passed؛ failed/skipped=0؛ Integration حدود11:22 |
| S1 / محیط | INFRA_REQUIRED؛ شواهد بخش۱ و فهرست بخش۳ |
| S2 / exact LiveKit version، image، digest، config | NOT_SELECTED / NOT_DEPLOYED؛ به‌علت توقف قبل S2 |
| S3 / topology، Linux runtime، domain/TLS، ports tested | NOT_RUN؛ هیچ deployment یا اتصال به media host خارجی انجام نشد |
| S4/S19 / TURN implementation و UDP/TCP/TLS | NOT_RUN؛ candidate/transport/setup latency اندازه‌گیری نشده |
| S5/S6/S12 / issuer، TTL5/15/30، invalid token matrix | NOT_RUN؛ valid/expired/wrong-room/wrong-identity/wrong-permission/tampered/wrong-signature هیچ‌کدام تست نشدند |
| S7 / A/B join و synthetic audio | NOT_RUN؛ هیچ participant/track ساخته نشد |
| S8 / RemoveParticipant و زمان قطع | NOT_RUN؛ درخواست remove ارسال نشد |
| S9/S10 / REUSE_SAME_TOKEN_AFTER_REMOVAL | NOT_TESTED؛ پذیرش signaling، active media، publish/subscribe و مدت window نامعلوم‌اند؛ هیچ window تأیید نشده |
| S11 / webhook و ejection / failure / delay / duplicate | NOT_RUN؛ race/window و time-to-eject نامعلوم |
| S13 / permission-change stale token | NOT_RUN؛ بازگشت مجوز قدیمی بررسی نشده |
| S14/S15/S16 / session، Block و Child Safety revoke | NOT_PROVEN؛ فقط policy مصوب موجود است، نه شاهد SFU |
| S17 / چندdevice و identity strategy | NOT_RUN؛ راهبرد نهایی participant identity با آزمایش freeze نشده |
| S18 / Viewer و Speaker | NOT_RUN؛ publish/subscribe enforcement اثبات نشده |
| S20 / network failure و reconnect | NOT_RUN؛ رفتار provider و grace نهایی ماکان پیاده/آزموده نشده |
| S21 / metrics | هیچ join/removal/TURN/media latency مشاهده نشده؛ unavailable را صفر موفقیت/صفر خطا گزارش نمی‌کنیم |
| S22 / PS00–PS10 | نتیجهٔ تک‌تک caseها در بخش۵؛ بدون تغییر شناسه‌ها |
| S23 / verdict | هیچ PASS یا FAIL امنیتی صادر نمی‌شود؛ توقف مقدماتی S1 = P3_S_INFRA_REQUIRED |
| S24 / regression پس از تغییر | final restore/build/full test موفق؛1595 executed/passed، failed/skipped=0، build صفر warning/error؛ Integration حدود6:08؛ فقط regression Backend، نه آزمایش رسانه |

## ۵. ماتریس دقیق PS00–PS10

| شناسهٔ plan مصوب | وضعیت | علت / شاهد موجود |
|---|---|---|
| PS00 — Preflight | PENDING_ENVIRONMENT | بررسی محلی انجام شد؛ Linux/SFU/TLS/ownership lab و دسترسی لازم احراز نشد؛ preflight کامل PASS نیست |
| PS01 — Token scope | NOT_RUN | issuer و SFU مستقر نیست |
| PS02 — Media grant | NOT_RUN | publish/subscribe واقعی اجرا نشد |
| PS03 — Baseline واقعی | NOT_RUN | دو media client به room واقعی وصل نشدند |
| PS04 — Old-token rejoin | NOT_RUN | token معتبر/room/remove وجود ندارد |
| PS05 — Refresh/replay | NOT_RUN | refresh واقعی provider مشاهده نشد |
| PS06 — Control failure | NOT_RUN | control/admission و callback آزمایشی مستقر نیست |
| PS07 — TURN واقعی | NOT_RUN | relay و transport تست نشده‌اند |
| PS08 — Grace و امنیت | NOT_RUN | fault شبکه و revoke رسانه اجرا نشد |
| PS09 — Isolation | NOT_RUN | room/device/grant واقعی برای negative test وجود ندارد |
| PS10 — Evidence/recovery | NOT_RUN | runtime media evidence/RunId resource برای verify/cleanup تولید نشد؛ TRXهای Backend شاهد رسانه نیستند |

## ۶. flags صادقانه و مرز نتیجه

```text
P3_S_LIVEKIT_DEPLOYMENT = NOT_RUN
P3_S_TURN_UDP = NOT_RUN
P3_S_TURN_FALLBACK = NOT_RUN
P3_S_REMOVE_PARTICIPANT = NOT_RUN
P3_S_STALE_TOKEN_REJOIN = NOT_TESTED
P3_S_IMMEDIATE_REVOCATION = NOT_PROVEN
P3_S_SESSION_REVOKE = NOT_PROVEN
P3_S_BLOCK_REVOKE = NOT_PROVEN
P3_S_CHILD_SAFETY_REVOKE = NOT_PROVEN
P3_S_BACKEND_REGRESSION = PASS
P3_PRODUCTION_MEDIA_APPROVAL = NOT_GRANTED
P1_PUSH_RUNTIME_PENDING
```

برای REMOVE_PARTICIPANT، prompt فقط PASS/FAIL فهرست کرده بود؛ چون همان prompt در نبود زیرساخت دستور توقف و منع evidence ساختگی می‌دهد، این گزارش آگاهانه NOT_RUN را ثبت می‌کند. خطای محیط را به FAIL آزمونی که اجرا نشده تبدیل نمی‌کنیم. `SELF_HOSTED_TOKEN_REJOIN_WINDOW_CONFIRMED` نیز صادر نشده است. انتخاب Cloud/Janus یا پذیرش bounded stale-token risk تصمیم تازه نمی‌گیرد؛ مقایسهٔ fallback پس از شکست واقعی gate، نه به‌جای محیط گمشده، انجام شود.

## ۷. scope، Git و ادامهٔ کار

فقط همین گزارش و appendهای مرتبط در roadmap، acceptance matrix و gap register تغییر می‌کنند؛ Source/Test/Domain/Application/Infrastructure/Api، sln/csproj، config، migration، MeliPayamak، frontend و AI/RAG دست‌نخورده‌اند. هیچ فایل dirty workspace اصلی AI بررسی/تغییر/stage/revert/stash نشد. ابزار media، package جدید، certificate یا .env ایجاد نشد. شواهد regression موجود فقط در `.artifacts/media-spike/p3-s-preflight` ignored هستند؛ هیچ runtime media dump ساخته نشده است.

نتیجهٔ commit/push و remote verification فقط پس از validation و اجرای واقعی در گزارش پایانی اعلام می‌شود. اگر validation موفق شد، commit فقط ثبت نیازهای محیط است، نه «validated spike» یا «failed security gate». گام بعد معرفی/تأمین اقلام بخش۳ و تکرار S1؛ سپس S2 pin نسخه و اجرای مراحل با همان شناسه‌های PS00–PS10. تا آن زمان P3-A/B/D/E آغاز نشوند.

نکات آموزشی این بررسی، نه ادعای کد تازه: CLI≠runtime؛ مستندات vendor≠اثبات رفتار نسخهٔ مستقر؛ NOT_RUN≠FAIL؛ TTL≠revocation. مسیر مطالعه: این گزارش → policy مصوب → spike plan → test matrix. مرز معماری و policy تغییر نکرده و هیچ flow اجرایی محصول، SQL یا endpoint جدید برای آموزش وجود ندارد.

## ۸. اعتبارسنجی واقعی Backend و اسناد

S0: `dotnet restore MakanApp.sln` و `dotnet build MakanApp.sln --configuration Debug --nologo` موفق، صفر warning/error. suite کامل با `dotnet test MakanApp.sln --configuration Debug --nologo --no-build --logger trx --results-directory .artifacts/media-spike/p3-s-preflight/baseline` اجرا شد: **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، failed=0، skipped=0. Integration حدود11:22 طول کشید. fixtureهای SQL موجود فقط محیط تست ایزوله را مصرف کردند؛ migration محصول جدید یا apply سرور نداریم.

final restore/build/full test برای همین تحویل مستنداتی موفق شد: باز هم **558 Unit + 1023 Integration + 14 Architecture = 1595 executed/passed**، failed=0، skipped=0، build صفر warning/error؛ TRX در `.artifacts/media-spike/p3-s-preflight/final` و Integration حدود6:08. این دو اجرا همان1595 تست موجودند، نه3190 تست متفاوت و نه تست رسانه. هیچ تستی اضافه/حذف/غیرفعال نشد و EF model تغییر نکرد؛ ادعای اجرای جداگانهٔ EF pending-model check هم نداریم. بررسی allowlist چهار سند،38 لینک نسبی، حفظ تمام متن قبلی سه سند، fenceها و یکتایی PS00–PS10 موفق بود؛ بازبینی نهایی diff/staged content نیز پیش از commit لازم است. advisoryهای Git دربارهٔ LF→CRLF خطای build یا warning کامپایل نیستند.
