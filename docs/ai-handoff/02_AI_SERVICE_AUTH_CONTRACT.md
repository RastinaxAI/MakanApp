# قرارداد Service-to-Service Authentication

نسخه: `v1`

## credential

Backend برای هر درخواست AI یک JWT کوتاه‌عمر می‌سازد:

- Scheme: `Bearer`
- Algorithm: `HS256`
- Header: `alg=HS256`, `typ=JWT`, `kid=<current-key-id>`
- Default lifetime: 120 seconds
- Backend accepted lifetime configuration: 30 تا 300 ثانیه
- Shared key: حداقل 32 بایت UTF-8

Claims دقیق:

```json
{
  "iss": "makan-backend",
  "aud": "makan-ai",
  "sub": "makan-backend",
  "scope": "makan.ai.invoke",
  "iat": 1791849600,
  "nbf": 1791849600,
  "exp": 1791849720,
  "jti": "f7c2319911d94cab84225b92e4d39ce3"
}
```

Token فقط identity سرویس است و نباید `UserId`، `PersonId`، `StudentId`، `OrganizationId`، متن پیام یا داده آموزشی داشته باشد.

## headerهای هر درخواست

```text
Authorization: Bearer <service-jwt>
X-AI-Contract-Version: v1
X-Correlation-ID: <correlationId>   # فقط وقتی correlationId موجود است
Content-Type: application/json
```

UserSession access token هرگز به AI forward نمی‌شود.

## تنظیمات Backend

```text
AiService:Authentication:Issuer
AiService:Authentication:Audience
AiService:Authentication:ServiceName
AiService:Authentication:Scope
AiService:Authentication:CurrentKeyId
AiService:Authentication:CurrentKey
AiService:Authentication:TokenLifetimeSeconds
```

مقادیر غیرحساس پیش‌فرض: `makan-backend`، `makan-ai`، `makan-backend`، `makan.ai.invoke` و 120 ثانیه. `CurrentKeyId` و `CurrentKey` باید از secret store یا environment وارد شوند.

## تنظیمات AI

تنظیمات موجود در commit ممیزی‌شده:

```text
MAKAN_AI_AUTH_ISSUER=makan-backend
MAKAN_AI_AUTH_AUDIENCE=makan-ai
MAKAN_AI_AUTH_REQUIRED_SCOPE=makan.ai.invoke
MAKAN_AI_AUTH_CURRENT_KEY_ID=<secret-metadata>
MAKAN_AI_AUTH_CURRENT_KEY=<secret>
MAKAN_AI_AUTH_PREVIOUS_KEY_ID=<optional-secret-metadata>
MAKAN_AI_AUTH_PREVIOUS_KEY=<optional-secret>
MAKAN_AI_AUTH_CLOCK_SKEW_SECONDS=30
```

تیم AI باید یک setting صریح برای expected subject نیز اضافه کند یا subject ثابت قرارداد را enforce کند:

```text
MAKAN_AI_AUTH_EXPECTED_SUBJECT=makan-backend
```

هیچ مقدار secret واقعی در این سند وجود ندارد.

## verification الزامی سمت AI

برای تمام business routeها:

1. وجود Bearer token و دقیقاً سه segment.
2. `alg=HS256` و `typ=JWT`؛ algorithm substitution مردود است.
3. resolution کلید فقط با `kid` برابر current یا previous key id.
4. constant-time signature comparison.
5. equality دقیق `iss`، `aud` و `sub` با تنظیمات مورد انتظار.
6. وجود scope مستقل `makan.ai.invoke` در claim space-delimited.
7. integer بودن `iat/nbf/exp`، رعایت clock skew و `exp > iat`.
8. طول عمر token حداکثر 300 ثانیه.
9. non-empty بودن `jti`.
10. عدم ثبت token یا key در log/metric/error.

Health/readiness می‌توانند public بمانند؛ business routeهای AI باید protected باشند.

## rotation بدون downtime

1. secret جدید با key id جدید بیرون از Git ساخته شود.
2. AI ابتدا key فعلی را در slot قبلی نگه دارد و key جدید را current کند؛ هر دو را موقتاً بپذیرد.
3. Backend سپس `CurrentKeyId/CurrentKey` را به کلید جدید تغییر دهد.
4. پس از rollout کامل و گذشت بیش از حداکثر lifetime + clock skew، previous key از AI حذف شود.
5. key id نباید secret فرض شود، ولی خود key محرمانه است.

## failure

- credential غایب، malformed، expired، unknown `kid`، signature/issuer/audience/subject نامعتبر: HTTP `401`.
- signature معتبر ولی scope لازم غایب: HTTP `403`.
- Backend هر دو را به public `503` تبدیل می‌کند تا جزئیات اعتماد داخلی به client نشت نکند.
