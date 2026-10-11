# قرارداد فرانت‌اند و بک‌اند ماکان

این تحویل برای تیم‌های **React** و **React Native** تهیه شده و رفتار نسخه واقعی بک‌اند را از قابلیت‌های آینده محصول جدا می‌کند.

- [سند اصلی HTML](MakanApp-Frontend-Backend-Contract.html): مستقل، آفلاین، فارسی RTL، دارای فهرست و لینک داخلی، جدول و نمونه HTTP/JSON؛ برای مطالعه و جست‌وجوی روزانه پیشنهاد می‌شود.
- [نسخه PDF](MakanApp-Frontend-Backend-Contract.pdf): خروجی چاپی همان قرارداد در ۵۹۷ صفحه با شماره صفحه و ۴۳۸ نشانک بخش‌ها، APIها و DTOها؛ برای مرور و اشتراک‌گذاری.

HTML به اینترنت، CDN، فونت آنلاین، JavaScript یا فایل جانبی وابسته نیست. آن را پس از دریافت فایل با مرورگر باز کنید؛ نمایش متن خام HTML در مرورگر مخزن، نمایش نهایی سند نیست. برای چاپ دوباره از گزینه Print مرورگر استفاده کنید. PDF تحویلی از رندر واقعی مرورگر ساخته و از نظر فارسی، لینک‌ها، صفحات خالی و خروج متن از صفحه کنترل شده است.

## مبنای بررسی

- شاخه: `main`
- commit مبنای سورس پیش از این تحویل: `f0b78dd`
- تاریخ بررسی: `2026-10-12`
- معماری: Onion Architecture در Modular Monolith
- پشته تأییدشده: .NET 9، ASP.NET Core Controllers، EF Core 9.0.5، SQL Server و xUnit
- حوزه‌های بررسی‌شده: Identity، Organization، Guardian، Academic، Assessment، Messaging، Storage و Reports؛ همچنین authentication، ProblemDetails، SignalR، CORS، DI و persistence میزبان.

سورس جاری مرجع اصلی است. OpenAPI از اجرای همین میزبان خوانده و مسیرها و schemaها با Controller و DTO تطبیق داده شده‌اند. اسناد معماری و PDFهای UX فقط برای مقایسه انتظار محصول استفاده شده‌اند. MySQL موجود در سند معماری قدیمی، دیتابیس فعلی معرفی نشده است.

## پوشش

| مورد | تعداد |
|---|---:|
| بخش‌های اصلی | ۶۳ |
| Controller | ۲۲ |
| عملیات HTTP کاربردی | ۱۴۱ در ۱۲۸ مسیر متمایز |
| SignalR Hub | ۱؛ دو invoke و یک server event |
| مدل‌های مرتبط HTTP | ۱۸۷؛ شامل ۱۸۲ مدل JSON، چهار مدل query و یک مدل multipart |
| DTO رویداد SignalR | ۱ |
| Enum مرتبط | ۴۵ |
| literal کد خطا | ۲۰۶؛ شامل کدهای تعریف‌شده و `UNEXPECTED_ERROR` |
| پیکربندی entity/table | ۵۳ |
| Migration | ۲۰ |
| نمونه JSON کنترل‌شده | ۲۰۰ |
| صفحه UX | ۹۹ صفحه اصلی و یک پیشنهاد اختیاری `OPT-01` |
| سناریوی سرتاسری | ۱۷ |
| ردیف آمادگی قابلیت | ۷۹ |
| شکاف و مورد باز | ۳۲ |

مسیرهای `/health`، `/swagger`، `/openapi/v1.json` و `/hubs/messaging` جدا مستند شده‌اند و در شمارش ۱۴۱ عملیات Controller نیستند. DTOهای داخلی store و query به‌عنوان API عمومی معرفی نشده‌اند؛ چهار مدل query جدول بالا واقعاً به `[FromQuery]` متصل‌اند. نام کد تعریف‌شده‌ای که محل استفاده عمومی آن تأیید نشده، در جدول خطا صریح مشخص شده است. مدل‌های بسته generic مانند `ReportPage<StudentExamReportItem>` قرارداد جداگانه و فیلدهای دقیق خود را دارند.

## تغییرات این بازبینی

- چهار داشبورد Student/Parent/Teacher/Manager و شانزده گزارش تفصیلی با query، pagination، sort، محدوده زمان و visibility نمره اضافه شدند.
- نمونه HTTP/JSON، کارت API، DTO و enum، سناریوها، نگاشت صد صفحه UX، cache/refetch و ماتریس آمادگی هم‌زمان اصلاح شدند.
- OTP مدیریت‌شده ملی‌پیامک، ارقام فارسی/عربی، طول قابل تنظیم code و خطاهای provider مستند شدند؛ secret سرویس فقط در backend است.
- Swagger/OpenAPI در Production عمومی‌اند؛ CORS برای همه originها و بدون cookie فعال است. LocalFileStorage با مسیر مطلق پایدار در Production قابل استفاده است.
- [راهنمای استقرار](../deployment-windows-linux-docker.md) برای secret، storage، migration و smoke test به‌روز شده است.

## مسیر مطالعه

۱. بخش‌های ۰ تا ۱۳: معماری، هویت، نشست، workspace و child context.

۲. بخش‌های ۱۴ تا ۴۳: داشبورد و گزارش‌ها در بخش۱۴، سپس جریان‌های آموزش، فایل، پیام‌رسانی، retry، concurrency و آزمون.

۳. بخش‌های ۴۴ تا ۵۳: خطا، کاتالوگ صفحه، خلاصه API، کارت تفصیلی هر endpoint، DTO، enum و JSON.

۴. بخش‌های ۵۴ تا ۶۲: سناریوها، تفاوت Web/Mobile، cache، امنیت، AI، آمادگی، شکاف‌ها و برنامه اجرای فرانت‌اند.

مسیرهای `/app/...` و `/ops/...` پیشنهاد مسیریابی فرانت‌اند هستند؛ endpoint بک‌اند محسوب نمی‌شوند. نمونه UUID، نام و زمان مصنوعی‌اند؛ هنگام اتصال، شناسه و نسخه از response واقعی گرفته شود. token و OTP مثال صرفاً placeholder هستند و credential قابل استفاده در اختیار نمی‌گذارند.

## محدودیت‌های مهم تحویل محصول

- API عمومی provisioning سازمان، مدیریت/جست‌وجوی افراد و عضویت، ارسال دعوت و مدیریت رابطه سرپرست وجود ندارد؛ جریان‌های مدیریتی کامل به این قراردادها نیاز دارند.
- Parent summary نتیجه را از داشبورد و گزارش می‌گیرد؛ class-list مستقل و کشف شناسه attempt برای drill-down هنوز کامل نیست. Dashboard بدون انتخاب child پاسخ200 خالی دارد؛ Reports در این حالت404 می‌دهد.
- SignalR فقط Bearer header را می‌خواند؛ `access_token` در query مرورگر پشتیبانی نمی‌شود. با CORS فعلی، LongPolling دارای header و `withCredentials:false` در محیط واقعی باید آزمایش شود.
- SMS واقعی به ServiceToken و کلید پایدار و storage به RootPath مطلق، permission و volume نیاز دارند. تأیید کد به معنی تأیید تنظیمات یک سرور مستقر نیست.
- آزمون تا انتشار نتیجه پیاده‌سازی شده، اما auto-finalize، grace period و پایش زنده وجود ندارند. StudentPreview در window می‌تواند سؤال‌ها را پیش از StartAttempt نشان دهد.
- Delivered/Read فعلی cursor خود کاربر است؛ receipt خواندن مخاطبان از آن قابل استنتاج نیست.
- Copilot، Knowledge/AI ingestion، Notification، Commerce و عملیات پلتفرم API کاربردی فعلی ندارند؛ این موارد با وضعیت روشن در سند آمده‌اند.

وجود API به معنی آماده‌بودن تمام جریان UX یا deployment production نیست. نقش، organizationId و childId به‌تنهایی مجوز نمی‌سازند؛ تصمیم نهایی از رابطه جاری و authorization سرور می‌آید.

## اعتبارسنجی و نگهداری

نتیجه واقعی restore، build، دو دور test و build نهایی در انتهای بخش ۶۲ سند ثبت شده است. ۱۴۱ method/route با OpenAPI یکسان‌اند؛ تمام فیلدهای ۱۸۲ schema JSON با DTOها تطبیق داده شده‌اند. چهار مدل query از کلاس پایه و propertyهای اختصاصی استخراج و با پارامترهای OpenAPI کنترل شده‌اند؛ `UploadFileRequest` قرارداد multipart و `MessagingRealtimeNotification` قرارداد Hub است.

restore و هر دو build موفق بودند؛ build نهایی صفر warning و صفر error داشت. در هر دو دور test، ۶۶۵ تست اجرا و موفق شدند: ۲۳۴ Unit، ۴۱۷ Integration و ۱۴ Architecture؛ شکست و skipped هر دو صفر بودند. Integration از دیتابیس ایزوله fixture استفاده کرد؛ migration جدیدی ساخته یا روی دیتابیس عادی/Production اعمال نشد. این مرحله مستندسازی است و تست محصول جدیدی اضافه نکرده است.

نمونه‌ها از نظر نام فیلد، nesting، نوع، enum، nullable، UUID، تاریخ و نسخه Base64 کنترل شده‌اند. این کنترل ساختاری جای اجرای درخواست با داده معتبر، رابطه مجاز، deadline و نسخه جاری را نمی‌گیرد. فایل‌های محصول، frontend، schema و migration در این تحویل تغییر نکرده‌اند.

HTML منبع مستقل قابل نگهداری این تحویل است. پس از تغییر Controller، DTO، policy، Domain یا تنظیمات، بخش‌های مرتبط، نمونه‌ها و وضعیت شکاف‌ها باید با سورس جدید تطبیق داده شوند و PDF دوباره ساخته شود. خروجی یک OpenAPI تازه به‌تنهایی جای توضیح مجوز، retry، state و محدودیت محصول را نمی‌گیرد.
