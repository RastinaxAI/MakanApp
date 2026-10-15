# تصمیم‌های مصوب و موارد باز برای بازبینی نهایی

تاریخ ثبت GAP-04: ۲۰۲۶-۱۰-۱۵. این register مجوز پیاده‌سازی موارد آینده نیست.

**تصمیم جاری:** TOTP Authenticator روش اصلی Backoffice MFA است؛ پیاده‌سازی واقعی، مستقل از provider بیرونی و پشت portهای قابل جایگزینی است. TOTP انتخاب مصوب است، نه placeholder ناامن یا موقت. جزئیات در [قرارداد GAP-04](GAP_04_BACKOFFICE_TOTP_MFA.md) است.

| مورد | پیاده‌سازی جاری | دلیل defer | گزینه آینده | اثر Production | اولویت |
|---|---|---|---|---|---|
| WebAuthn / Passkeys | TOTP + recovery | خارج release مصوب و نیازمند credential/device lifecycle | عامل مقاوم‌تر به phishing در همان مرز MFA | TOTP فعلی معتبر است؛ upgrade با rollout مستقل | P2 |
| SMS fallback | SMS فقط برای login OTP موجود؛ MFA پیامکی نداریم | delivery/privacy/cost و قواعد fallback تصویب نشده | provider/consent مصوب، بدون downgrade پنهان | نبود SMS مانع اجرای TOTP نیست | P2 |
| enterprise identity / SSO | Identity موجود + TOTP؛ IAM پلتفرم ثابت | نیاز سازمانی و trust/link/recovery contract مشخص نیست | OIDC/SSO پس از تصویب | مهاجرت نباید roleهای سازمان و پلتفرم را یکی کند | P2 |
| last-owner offline recovery | recovery code یک‌بار؛ Owner دیگر می‌تواند reset کند | فقدان TOTP و همه codeها نیازمند authority/عملیات اضطراری صریح است | رویه offline audited با احراز هویت مالک، نه API backdoor | `DEFERRED_OPERATIONAL_DECISION`؛ ریسک قفل‌شدن آخرین Owner | P1 پیش از rollout |
| key custody / migration میان سرورها | key ring پایدار + DPAPI Windows یا certificate | destination/backup/rotation/مسئول عملیاتی به محیط وابسته است | secret/certificate custody و rehearsal انتقال مستقل | پیش‌نیاز استفاده عملیاتی MFA؛ از دست‌رفتن کلید می‌تواند re-enrollment بخواهد | P1 پیش از rollout |
| سن و Child Safety / GAP-05 | سیاست/adapter قبلی بدون تغییر؛ blocked | تصمیم معتبر سن، گروه/کانال و provider مصوب لازم است | تصمیم محصول و adapter ایمن مستقل | closure GAP-04 آن gap را باز یا بسته نمی‌کند | P0 دامنه کودک |

دیگر gapهای پرداخت، Push/SMS اعلان، worker recovery، observability، CI/CD و AI در [ممیزی غیر AI](NON_AI_BACKEND_FINAL_GAP_AUDIT.md) یا track مستقل خود باقی‌اند. این register برای هیچ‌کدام implementation یا approval تازه ادعا نمی‌کند.
