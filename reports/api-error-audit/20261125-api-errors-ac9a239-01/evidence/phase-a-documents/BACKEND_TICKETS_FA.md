# یافته‌های Backend در Phase A

دو Issue قابل بازتولید `ERR-001` و `ERR-002` در `evidence/issues-phase-a.json` ثبت شده‌اند. نخست پنج variant قرارداد HTTP فریم‌ورک و سپس چهار variant header شکست خوردند. نتیجهٔ نهایی Phase A: ۲۴ موفق، ۹ ناموفق، صفر Skipped از ۳۳ تست.

تست اولیهٔ RateLimiter با ظرفیت صفر خود fixture را نامعتبر می‌کرد؛ خطای 500 آن **Issue محصول نیست**. fixture اصلاح شد: ابتدا درخواست سالم واقعاً تنها permit را مصرف می‌کند، سپس 429 آزمایش می‌شود. هر سه Accept در تست معتبر RateLimiter موفق‌اند و هیچ Fix برای این بخش پیشنهاد نشده است. اجرای `phase-a-http-03` برای شفافیت حفظ شده است.

هیچ schema، Auth، Safety، timeout یا منطق AI/Assignment در Phase A تغییر نکرد. hashes تمام ۵۷ مسیر محافظت‌شده یکسان است. metadata زنده با Working Tree متفاوت است؛ این موضوع باگ ۴۰۹ تاریخی را ثابت نمی‌کند.
