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
| سن و Child Safety / GAP-05 | سیاست v1 مصوب، age verification audited، رابطه/رضایت و Internal Makan اجرا شده‌اند | تصمیم P0 قبلی رفع شده؛ محدودیت‌های ارتقای زیر مستقل‌اند | توسعه پشت همان مرزها | closure کد، اثبات deployment یا دقت معنایی کامل نیست | رفتار جاری الزامی |

## افزوده GAP-05 — رفتار جاری و گزینه‌های ارتقا

Internal Makan Content Safety تصمیم مصوب جاری است؛ موتور قواعد متن با `PolicyVersion`، نتیجه صریح و Fail-closed وجود دارد. جای خالی AI با موفقیت ساختگی پر نشده است. رسانه فاقد پشتیبانی معنایی، ReviewRequired و غیرقابل انتشار معمولی است. منبع جاری: [قرارداد GAP-05](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md).

| مورد | پیاده‌سازی جاری | دلیل defer | گزینه آینده | اثر Production | اولویت |
|---|---|---|---|---|---|
| بازبینی حقوقی آستانه ۱۸ | مرز محصول مصوب ۱۸، مستقل از نقش؛ Unknown محافظت‌شده | تأیید قوانین همه حوزه‌ها در scope کدنویسی نیست | بازبینی حقوقی/محصولی بازار هدف و policy نسخه‌دار | ادعای compliance حقوقی نمی‌شود؛ تغییر نیازمند rollout/test است | P1 پیش از عرضه هر بازار |
| richer age assurance/provider | شاهد مدیر مجاز، تعارض auditable و حل Owner با MFA تازه | provider هویتی جدید تصویب نشده | مدرک/consent/provider قوی‌تر پشت گردش موجود | شماره تلفن یا DOB خوداظهاری مدرک سن نیست | P1 |
| درخواست پیام عمومی برای Minor | Stranger Direct ممنوع؛ هیچ message-request عمومی وجود ندارد | خارج قرارداد v1 | فقط با consent/abuse/notification مصوب | رفتار پیش‌فرض باید همچنان deny باشد | P2 |
| گروه عمومی یا کانال Minor | ساخت عمومی/کانال و publish کانال ممنوع؛ گروه خصوصی و رضایت محدود موجود | محصول v1 صریحاً منع کرده است | تصمیم محصول جدید با guard و moderation متناسب | تا تصویب، ممنوعیت server-side حفظ شود | P2 |
| classifier داخلی قوی‌تر | قواعد قطعی محدود متن؛ Known pattern و لینک/رسانه unsupported به review می‌روند | مدل/ارزیابی معنایی/بودجه/زبان‌های هدف تصویب نشده | classifier یا ML بازبینی‌شده داخلی پشت IContentSafetyProvider | false positive/negative واقعی‌اند؛ queue انسانی و ظرفیت آن لازم است | P1 |
| provider بیرونی Safety | هیچ vendor یا انتقال محتوا به بیرون وجود ندارد | privacy/قرارداد/region/هزینه/دقت مستقل لازم است | adapter مصوب پشت همان port | بدون approval، raw private content خارج نمی‌رود | P2 |
| اسکن معنایی تصویر/ویدئو/صدا/فایل | ReviewRequired، evidence محدود و بدون انتشار عادی در مسیر محافظت‌شده | موتور متن bytes را تحلیل نمی‌کند | pipeline مصوب با quarantine، دسترسی و failure policy | صرف MIME/signature اثبات ایمنی محتوا یا AV نیست؛ GAP-11 مستقل است | P1 |
| appeal، SLA و retention پیشرفته | پرونده، claim، escalation، action و audit موجودند؛ حذف خودکار نداریم | مهلت/نگه‌داری حقوقی و عملیات تیم نیازمند تصمیم‌اند | workflow مصوب و job/guard قابل تست | GAP-20 و نگه‌داری storage همچنان بازند | P1 |

دیگر gapهای پرداخت، Push/SMS اعلان، worker recovery، observability، CI/CD و AI در [ممیزی غیر AI](NON_AI_BACKEND_FINAL_GAP_AUDIT.md) یا track مستقل خود باقی‌اند. این register برای هیچ‌کدام implementation یا approval تازه ادعا نمی‌کند.
