# یافته‌های Frontend و AI

صفحهٔ عمومی به `/enter` می‌رود؛ ورود لازم است و هیچ کارت AI قابل مشاهده نیست. هیچ شماره، OTP یا نشست تولید/مصرف نشد. fixture امنِ نقش‌ها ارائه نشده است. رفتار کارت، spinner، toast، AbortController و ترتیب نمایش پاسخ‌ها BLOCKED است؛ Backend API، مشاهدهٔ UI عمومی و تست محلی جدا هستند.

درخواست ناشناس BFF بدون Origin، `403 ORIGIN_REJECTED` برگرداند. این درخواست مرورگر معمولی نیست و **دلیل اثبات خرابی CORS نیست**؛ Origin واقعی جدا آزمایش می‌شود. CORS preflight مستقیم API پاسخ 204 داد؛ این هم به‌تنهایی Mixed Content یا موفقیت مرورگر را ثابت نمی‌کند.

مقدار `template=null` در action catalog به معنی نبود کارت محصول نیست؛ قرارداد Backend template کارت‌ها را تعریف نمی‌کند. متن خطای مخصوص Deadline باید از business code انتخاب شود و زیر پیام Generic Conflict پنهان نشود. هیچ ادعای UI Fix یا AI production PASS وجود ندارد.
