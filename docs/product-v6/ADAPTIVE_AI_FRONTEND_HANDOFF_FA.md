# تحویل Adaptive Universal Assistant v1

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

> سیاست جاری `2026-11-26`: [Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md) با Commerce:EnforcementEnabled=false شرط تجاری استفادهٔ عادی و ذخیرهٔ Source را حذف کرده است؛ AI Unmetered و OperationId/ACL برقرارند. پرداخت/Push بیرونی و PersonalClass Academic مستقل Deferred هستند. [Media Review](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) اکنون تصمیم انسانی مصوب دارد؛ Pending/Rejected تحویل ندارد و Approval خودکار پیام نمی‌فرستد. شواهد و اعداد تاریخی این سند متعلق به زمان خود هستند.

مبنای Backend: `95cef1b72695e51a6c851c9958ac5a026adba387`، شاخهٔ
`feature/product-v6-backend-alignment`؛ قرارداد upstream با بررسی فایل‌های دقیق commit
`8bd0c8981dc49f339773ced6024e70e47c4ebdd2` از `RastinaxAI/MakanApp-AI` تطبیق داده شده است.
Business=`v1` و Knowledge=`knowledge-v1` هستند. وجود کد و تست محلی، اثبات Deploy نیست.
در این Task هیچ درخواست، SQL یا Deploy به سرور Backend مجاز نیست.

## شروع ساده و Profile اختیاری

کاربر واردشده با حساب/نشست فعال می‌تواند بدون فایل، Profile، Grade، Wizard یا نقش آموزشگاهی
چت کند. `AiUsage:Mode=Unmetered` حفظ است؛ Subscription، پرداخت، Trial، شمارندهٔ Remaining
و محدودیت محلی AI۲۰/دقیقه شرط این انتشار نیستند. ACL و محدودیت واقعی Upstream پابرجا هستند.

```http
POST /api/v1/ai/chat
Authorization: Bearer <session-token>
Content-Type: application/json
```

```json
{
  "message": "این مفهوم را ساده توضیح بده",
  "conversationId": null,
  "operationId": "11111111-1111-4111-8111-111111111111"
}
```

`conversationId` پاسخ canonical است؛ آن را برای پیام بعدی نگه دارید. Client GUID تازه نیز
می‌تواند اولین Conversation خودش را ایجاد کند. برای Retry، **همان body اولیه و OperationId**
را نگه دارید؛ پاسخ‌گرفتن و جایگزینی conversationId در body یک intent تازه است، نه Retry اولیه.

فیلد اختیاری `assistantProfile` فقط روش توضیح را تغییر می‌دهد:

```json
{
  "gradeLevel": 7,
  "proficiency": "Beginner",
  "goals": ["درک مفهوم", "حل مثال ساده"]
}
```

- `gradeLevel`: فقط integer۱..۱۲ یاnull؛ string، float وbool رد می‌شوند.
- `proficiency`: فقط `Beginner` / `Intermediate` / `Advanced` با همین حروف یاnull.
- `goals`: null یا حداکثر۸ متن غیرخالی؛ هر متن حداکثر۸۰ Unicode code point، نه تعداد byte
  یا واحد UTF-16. null در wire رسمی به `[]` تبدیل می‌شود.
- Profile هیچ Role، Scope، Source/File/Class/Child permission ایجاد نمی‌کند. مدیریت/ذخیرهٔ
  Profile یا Wizard جدید ساخته نشده است.

## History متعلق به Backend

History از جداول مستقل `intelligence.AiConversations` و `AiConversationTurns` خوانده می‌شود؛
Messenger، Contacts و Address Book وارد آن نمی‌شوند. Client نباید history را authority بداند؛
فیلد اضافهٔ history در ورودی Chat جایگزین SQL نمی‌شود.

Conversation به OwnerUserId و fingerprint بافت مجاز Assistant بسته است: Personal، سازمان،
Membership/Role، Class و Subject/Learner مؤثر. Session در fingerprint نیست تا مالک همان
بافت در نشست تازه ادامه دهد؛ نشست تازه همچنان باید مستقل احراز و مجاز شود. تغییر School
Workspace، Assistant ذخیره‌شده را عوض نمی‌کند. Context لغوشده fallback شخصی نمی‌سازد.

```http
GET /api/v1/ai/conversations/{conversationId}/history
```

پاسخ شامل `conversationId` و `turns:[{role,text}]` در **پنجرهٔ محدود جاری** است؛ این endpoint
آرشیو نامحدود یا API مدیریتی نیست. مالک/بافت نامنطبق404 می‌گیرد؛ لغو رابطه/نقش یا نشست،
خواندن و ادامهٔ آینده را می‌بندد. فقط User/Assistant کامل، به صورت زوج، مصرف می‌شوند:
پاسخ History دارای `Cache-Control: no-store` و `Vary: Authorization` است؛ حافظهٔ مرورگر
جای بازبینی جاری سرور را نمی‌گیرد.

| حد | مقدار |
|---|---|
| تعداد | حداکثر۲۰ turn؛۱۰ زوج |
| هر turn | حداکثر۴۰۰۰ Unicode code point |
| مجموع | حداکثر۱۲۰۰۰ Unicode code point |
| ترتیب | قدیمی به جدید، از جدیدترین زوج‌های قابل استفاده |

زوج دارای پاسخ طولانی‌تر از۴۰۰۰ از پنجرهٔ upstream حذف می‌شود؛ متن فعلی دوباره در History
قرار نمی‌گیرد. در SQL پاسخ کامل موفق تا۳۲۷۶۸ Unicode code point پذیرفته می‌شود؛ upstream
سقف کل answer نداشت، بنابراین این حد دفاعی Backend برای حافظه/ذخیره‌سازی انتخاب شده است.
prompt شکست‌خورده و متن partial در SQL ذخیره نمی‌شوند؛ فقط metadata عملیات pending می‌ماند.

History مبتنی بر Source، نسخه‌های همان منابع را به Conversation متصل می‌کند. ACL/File/Safety
آن نسخه‌ها پیش از خواندن و ارسال آینده دوباره بررسی می‌شود؛ حتی تغییر Mode به General این
بررسی را حذف نمی‌کند. برای تغییر مجموعه/نسخهٔ grounded، Conversation تازه شروع کنید.

## Mode و SourceIntent مصوب

| Mode | رفتار |
|---|---|
| `General` | Business Chat بدون نیاز Knowledge؛ Sourceهای انتخابی در این Mode استفاده نمی‌شوند؛ `sources=[]` و `citations=[]` |
| `Auto` | نسخهٔ دقیقِ منبع مجاز، Ready و evidence کافی → RAG؛ fallback عمومی فقط طبق SourceIntent |
| `SourcesOnly` | فقط RAG؛ NotReady/InsufficientEvidence خطاست و fallback عمومی ندارد |

Mode حذف‌شده بدون Source برابر General و با Source برابر Auto است.
`sourceIntent` اختیاری `General` یا `SourceFocused` است. با Source انتخاب‌شده، پیش‌فرض
SourceFocused است؛ Processing/NotReady پاسخ عمومی پنهان نمی‌سازد. General صریح در Auto
اجازهٔ fallback با provenance روشن می‌دهد. Unauthorized هیچ‌گاه دلیل fallback نیست.
SourcesOnly یا Auto/SourceFocused بدون منبع انتخاب‌شده، درخواست نامعتبر است؛ Client باید
منبع را صریح انتخاب کند یا برای سؤال عمومی General را برگزیند.

برای نسخهٔ صریح:

```json
{
  "message": "از همین منبع توضیح بده",
  "conversationId": null,
  "operationId": "22222222-2222-4222-8222-222222222222",
  "mode": "SourcesOnly",
  "selectedSources": [{"sourceId": "33333333-3333-4333-8333-333333333333", "version": 1}]
}
```

`selectedSourceIds` قدیمی نیز در اولین انتخاب، نسخهٔ منتشرشدهٔ مجاز را pin می‌کند. Retry همان
Operation نسخه را عوض نمی‌کند. هر دو selector اگر غیرخالی باشند باید همان SourceIdها را
معرفی کنند؛ exact version مثبت، SourceId یکتا و سقف منابع۳۲ است. Library باز نشده است.
پس از سیاست Open Access در۲۰۲۶-۱۱-۲۲، Enforcement=false کنترل مالی SourceCount/PersonalSources را نیز از استفادهٔ عادی خارج می‌کند؛ ACL، حدود فنی Storage و Safety همچنان لازم‌اند. هیچ Quota یا پلن فرضی ثبت نمی‌شود.

پاسخ Chat، فیلدهای قبلی را حفظ و `provenance:{kind,reason?}` و `citations` را اضافه می‌کند:
`General`، `GeneralFallback` یا `KnowledgeRag`. Citationهای RAG شامل SourceId/Version/Chunk
دقیق و فقط evidence مجاز هستند. آرایهٔ legacyِ `sources` در Generic Chat تهی می‌ماند؛ برای
RAG جدید از `citations` استفاده کنید. citation را از متن عمومی یا شمارهٔ صفحهٔ حدسی نسازید.
قرارداد Knowledge RAG فعلی history/profile مستقلی ندارد؛ Query و evidence دقیق دریافت می‌کند.

## Streaming واقعی

```http
POST /api/v1/ai/chat/stream
Accept: text/event-stream
Content-Type: application/json
```

body همان SendAiChatCommand است. از fetch همراه Bearer و خواندن incremental body استفاده
شود؛ EventSource معمولی برای POST/body/header مناسب این قرارداد نیست.

```text
event: start
data: {"requestId":"request-1","correlationId":"correlation-1","conversationId":"44444444444444448444444444444444","modelAlias":"education-fast","provenance":{"kind":"General"}}

event: delta
data: {"sequence":1,"text":"بخش اول "}

event: delta
data: {"sequence":2,"text":"پاسخ"}

event: done
data: {"requestId":"request-1","correlationId":"correlation-1","conversationId":"44444444444444448444444444444444","metadata":{"modelAlias":"education-fast","processingTimeMs":100}}
```

- start دقیقاً یک بار؛ delta از۱ و متوالی است. متن delta فقط append شود؛ done متن کامل
  را تکرار نمی‌کند. هر network read لزوماً یک event نیست؛ UTF-8 چندتکه و CRLF معتبرند.
- قبل از start، خطا HTTP/ProblemDetails است. پس از آغاز، `event:error` شکست نهایی است؛
  HTTP200 به‌تنهایی موفقیت نیست. EOF بدون done/error نیز شکست است.
- Backend done را تا EOF معتبر upstream نگه می‌دارد، سپس زوج کامل و نتیجهٔ عملیات را
  ثبت می‌کند. deltaهای قبلی بدون انتظار برای کل پاسخ forward/flush می‌شوند.
- بعد از error، done مجاز نیست. پاسخ partial را completed نمایش ندهید؛ Retry یا fallback
  خودکار نکنید. قطع Client، cancellation را به upstream منتقل می‌کند.
- این روال تضمین تراکنش exactly-once در شبکه نیست؛ SQL از بیش از یک invocation برای
  همان intent جلوگیری می‌کند و قطع مبهم Unknown می‌ماند.

خطای پس از start:

```text
event: error
data: {"error":{"code":"AI_SERVICE_TIMEOUT","message":"جریان پاسخ کامل نشد؛ این پاسخ موفق ثبت نشده است.","requestId":"request-1"}}
```

**`SOURCE_STREAMING=NOT_SUPPORTED_BY_UPSTREAM`**: knowledge-v1 endpoint رسمی RAG stream
ندارد. مسیر grounded/SourcesOnly با `409 AI_SOURCE_STREAMING_NOT_SUPPORTED` نیاز به
`POST /ai/chat` غیرجریانی را اعلام می‌کند؛ پاسخ کامل RAG به deltaهای ساختگی تبدیل نمی‌شود.
Auto اگر مجاز به GeneralFallback برسد، از stream واقعی Business استفاده می‌کند.
برای انتخاب مسیر غیرجریانی پس از این خطای قابلیت، عملیات تازه با OperationId تازه بسازید؛
تغییر endpoint برای همان intent ذخیره‌شده، Retry همان stream نیست.

## Retry، خطا و Privacy

| کد | اقدام Client |
|---|---|
| `AI_OPERATION_IN_PROGRESS` | همان intent در حال اجراست؛ درخواست Provider تازه ساخته نشود |
| `AI_OPERATION_ALREADY_COMPLETED` |409؛ پاسخ از این Retry بازپخش نمی‌شود؛ پنجرهٔ مجاز History را بخوانید |
| `AI_OPERATION_OUTCOME_UNKNOWN` | نتیجه مبهم؛ اجرای خودکار دوباره ممنوع |
| `AI_OPERATION_PREVIOUSLY_FAILED` | همان عملیات قبلاً بسته شده؛ اقدام تازه فقط با تصمیم صریح و OperationId تازه |
| `COMMERCE_USAGE_CONFLICT` / `AI_SOURCE_OPERATION_CONFLICT` | intent قبلی با payload متفاوت؛ همان کد سازگار قبلی حفظ شده است؛ body اصلی را بازبینی کنید |
| `AI_CONVERSATION_NOT_FOUND` |404؛ شناسه، مالک یا بافت جاری مجاز نیست؛ وجود حساب دیگر افشا نمی‌شود |
| `AI_CONVERSATION_SOURCE_BINDING_CONFLICT` | برای تغییر SourceVersion گفت‌وگوی تازه لازم است |
| `AI_SOURCE_NOT_READY` / `AI_SOURCE_PROCESSING` | وضعیت پردازش را نشان دهید؛ SourcesOnly را General نکنید |
| `AI_INSUFFICIENT_EVIDENCE` | شواهد کافی نیست؛ Citation حدسی تولید نشود |
| `AI_SERVICE_TIMEOUT` / `AI_RESPONSE_INVALID` | پاسخ موفق نیست؛ Retry خودکار و افزایش timeout مجاز نیست |

Sourceهای غیرمجاز پیش از هر fallback رد می‌شوند. Service Authentication هر دو سرویس false
می‌ماند؛ Service JWT جدید ساخته نمی‌شود. Primary/Fallback مدل فقط متعلق به upstream است؛
Backend provider fallback یا retry موازی ندارد. timeout فعلی Business برابر۳۵ ثانیه و
Circuit Breaker آن حفظ‌اند؛ budgetهای موجود Knowledge نیز تغییر نکرده‌اند.

متن سؤال/جواب، فایل، history، profile goals، OTP، token، header و secret وارد log/metric
نمی‌شوند. failure logging فقط code/correlation/trace امن دارد. API Admin برای مشاهدهٔ prompt
یا تاریخچه ساخته نشده است.

حذف صریح حساب از همان BE22 موجود، History و انتخاب Assistant حساب را purge می‌کند؛
حساب تازه با شمارهٔ بازیافتی تاریخچهٔ مالک قدیمی را به ارث نمی‌برد. سیاست حذف خودکار با زمان
نگه‌داری تعیین نشده است: **`PRODUCT_RETENTION_DECISION_REQUIRED`**. هیچ TTL/purge حدسی
اضافه نشده؛ فعال‌بودن اجرای BE22 در محیط واقعی تابع تنظیم موجود و مجوز عملیاتی است.

## انتشار و مرز پذیرش

Migration افزایشی `20261118164500_AddAdaptiveAiConversations` پیش‌نیاز باینری جدید است؛
فقط LocalDBهای ایزوله در این Task آن را اجرا می‌کنند. SQL امن و idempotent در هر Release
تحویل داده می‌شود. Test و Production بستهٔ جدا با Storage مستقل دارند؛ Secretهای موجود
فقط در artifact خصوصی حفظ می‌شوند. Build/Regression/SQL/Hash در گزارش نهایی ثبت می‌شوند.

`BACKEND_SERVER_TEST=NOT_RUN_BY_POLICY`، `BACKEND_SERVER_SQL=NOT_RUN_BY_POLICY` و
`BACKEND_SERVER_DEPLOYMENT=NOT_RUN_BY_POLICY`. هیچ ادعای Live/Deploy Backend یا
پذیرش عملیاتی Production از این سند استنتاج نشود.
