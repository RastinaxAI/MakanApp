# تغییرات OpenAPI کلاس مستقل v2

تاریخ: ۲۰۲۶-۱۱-۰۳؛ مبنا: `8ed3fad6ea464ad02c90fecb295eda2606abca64`.

این سند رفتار پیاده‌سازی‌شده را توضیح می‌دهد. قرارداد قابل دریافت از `/openapi/v1.json` تولید می‌شود؛ نمونهٔ تنظیمات یا وجود DTO به معنی فعال‌بودن یک سرویس خارجی نیست.

## نتیجهٔ بررسی سند واقعی

| معیار | مبنا | پس از تغییر |
|---|---:|---:|
| تعداد operation | 426 | 433 |
| operationId مفقود | 426 | 0 |
| operationId تکراری | 0 | 0 |
| پاسخ موفق 200/201 بدون content | 7 | 0 |
| مرجع داخلی بررسی‌شده | — | 4751 |
| عملیات PersonalClass/Academic دارای summary و description | — | 136 |

این اعداد از سند واقعی Kestrel در محیط محلی ایزوله استخراج شدند. تست `PersonalClassContractOpenApiHasResolvedReferencesUniqueIdsAndActualSecurity` سند تولیدی را برای فرهنگ‌های `fa-IR` و `en-US` نیز parse می‌کند.

## Authentication و مجوز

- scheme برابر `type=http`، `scheme=bearer` و **`bearerFormat=Opaque`** است. توکن نشست فعلی JWT نیست؛ آن را decode نکنید.
- `/api/v1/auth/otp/challenges`، `/otp/verify` و `/refresh` طبق مدل credential فعلی anonymous هستند. سند برای anonymous از `security: [{}]` استفاده می‌کند؛ این requirement خالی در OpenAPI به معنی عدم الزام Bearer است.
- endpointهای protected، `security: [{"Bearer": []}]` دارند. `x-authentication` نیز anonymous یا bearer-session را مشخص می‌کند.
- `x-platform-permissions` از metadata واقعی `[Authorize(Policy=...)]` استخراج می‌شود. نشست معتبر، permission پلتفرم را ایجاد نمی‌کند. MFA و step-up موجود حفظ شده‌اند.
- این تغییرات مجوز تجاری جدید، نقش جدید یا استثنای Authentication ایجاد نمی‌کنند.

## operationId

شناسه از HTTP method و route ساخته می‌شود؛ پارامتر مسیر با `by_` مشخص می‌شود. نمونه:

```text
GET /api/v1/personal-classes/{classId}/history
get_api_v1_personal_classes_by_classId_history
```

تغییر نام متد C# به‌تنهایی این شناسه را تغییر نمی‌دهد. تغییر خود route یک تغییر قرارداد است. تمام شناسه‌ها برنامه‌ای از نظر تکرار و فقدان بررسی می‌شوند.

## خطا و اعتبارسنجی

schema مشترک `MakanProblemDetails` شامل `status`, `code`, `traceId`, `correlationId` و فیلدهای اختیاری RFC، از جمله `title`, `detail`, `errors` است. خطاهای اختصاصی Safety اطلاعات typed قبلی خود را نیز حفظ می‌کنند.

| وضعیت | معنای قابل مصرف |
|---|---|
| 400 | ورودی یا JSON نامعتبر؛ model validation دارای `REQUEST_INVALID` |
| 401 | نشست لازم/نامعتبر؛ `AUTH_REQUIRED` |
| 403 | permission ناکافی؛ challenge پلتفرم از قرارداد واقعی MFA پیروی می‌کند؛ منع عمومی `AUTH_FORBIDDEN` |
| 404 | نبودن منبع یا پنهان‌سازی دسترسی نامجاز |
| 409 | state، ظرفیت، idempotency و نسخهٔ کهنهٔ **Core** |
| 412 | precondition/version در مسیرهای Academic که واقعاً از این وضعیت استفاده می‌کنند |
| 422 | فقط policyهایی که نگاشت واقعی این وضعیت دارند |
| 429 | محدودیت نرخ در مسیرهای دارای limiter |
| 503 | وابستگی/زیرساخت موقتاً در دسترس نیست |

`x-error-codes` کاتالوگ خانوادهٔ خطاهای مرتبط حوزه است؛ تضمین نمی‌کند تمام کدهای آن خانواده از تک‌تک endpointها تولید شوند. نگاشت از `GlobalExceptionHandler` فعلی استخراج می‌شود، نه جدول وضعیت حدسی. کدهای واقعی Core/Academic در راهنمای یکپارچه آمده‌اند.

پاسخ 401/403 حتی در درخواست با `Accept: image/png`، بدنهٔ امن ProblemDetails و correlation را حفظ می‌کند. وضعیت واقعی عوض نمی‌شود. هیچ exception خام، توکن یا payload خصوصی وارد این بدنه نشده است.

## ورودی

- title تا 200، purpose تا 2000، description تشکیل تا 4000 و توضیح آموزشی تا 10000 کاراکتر مطابق Domain فعلی مستند شده‌اند.
- بسیاری از متن‌ها پس از `Trim` محدود می‌شوند؛ `maxLength` خام برای آن‌ها نادرست بود. `pattern`، `x-normalization=trim` و `x-normalized-max-length` همین رفتار را بیان می‌کنند. کاربر SDK همچنان باید خطای سرور را مدیریت کند.
- `MaxScore` مثبت، با حداکثر دو رقم اعشار است؛ سقف نمرهٔ ارزیابی از نسخهٔ فعالیت/سؤال گرفته می‌شود. مقدار جهانی 20 وجود ندارد.
- تعداد شناسه‌های پیوست تکلیف حداکثر 20 است. رد تعداد بیشتر، رفتار واقعی فعلی `403 SUBMISSION_FILE_NOT_ALLOWED` را حفظ می‌کند.
- سؤال ObjectiveSingleChoice حداقل دو گزینه و دقیقاً یک پاسخ صحیح لازم دارد؛ Descriptive گزینه ندارد. این شرط در description آمده و حداقل دو گزینه به‌اشتباه روی همهٔ انواع سؤال اعمال نشده است. انتشار آزمون حداقل یک سؤال و برابری مجموع نمره‌ها با MaxScore را می‌خواهد.
- deadline معتبر به زمان UTC سرور وابسته است؛ schema نمی‌تواند شرط پویای «بعد از زمان جاری» را به حد ثابت تبدیل کند.
- rowversion رشتهٔ Base64 استاندارد است؛ `ClientOperationId` باید UUID غیرخالی و متعلق به همان قصد عملیات باشد.
- upload از `multipart/form-data` با بخش اجباری **`File`** استفاده می‌کند. `x-max-file-size-bytes` و `x-allowed-content-types` از policy جاری Storage استخراج می‌شوند؛ مسیر خصوصی ذخیره‌سازی وارد سند نمی‌شود. برای فایل کلاس مستقل، selector واقعی `PersonalClassId` است.

serializer فعلی OpenAPI مقدار decimal را تحت `fa-IR` نادرست تولید می‌کرد. `OpenApiDocumentCultureMiddleware` فقط هنگام تولید مسیر `/openapi` فرهنگ عددی invariant را اعمال و سپس مقدار قبلی را برمی‌گرداند؛ فرهنگ APIهای دیگر تغییر نمی‌کند.

## پاسخ موفق و سازگاری

- دانلود source و moderation evidence بایت خام با media type مستندشده دارند؛ Range در مسیرهای پشتیبانی‌شده با 206، `Content-Range` و 416 مستند است.
- دانلود report export پاسخ binary دارد؛ برای آن Range جدید ادعا نشده است.
- پایان عضویت و availability بدون بدنه با 204 مستند می‌شوند. درخواست repair فضاهای کلاس 202 بدون بدنه است؛ readiness را با GET spaces بخوانید.
- `AcademicPage<T>` نام قدیمی `total` را نگه می‌دارد و `totalCount` را به‌صورت additive اضافه می‌کند. canonical UI از `items/page/pageSize/totalCount` استفاده کند.
- پنج route صفحه‌بندی جدید و دو history route اضافه شده‌اند. raw-arrayهای قبلی شکسته نشده‌اند؛ مسیر مهاجرت در راهنمای Frontend آمده است.

Generator فعالی برای SDK در این مخزن یافت نشد؛ ابزار جدیدی نصب نشده و ادعای generated-SDK build نمی‌شود. parse سند، resolution تمام `$ref`ها، typed HTTP consumption و اجرای واقعی HTTP بررسی شده‌اند.
