# ممیزی قرارداد Backend و AI — STEP 10A

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## نتیجه Gate

وضعیت: **GO**

هر دو مخزن در شاخه `main` و هم‌تراز با `origin/main` ممیزی شدند. Backend با .NET 9 و AI Service با FastAPI کار می‌کند. baseline واقعی پیش از تغییر:

- Backend: build بدون warning؛ 319 تست Unit، 14 تست Architecture و 550 تست Integration موفق.
- AI: 287 تست و 536 subtest موفق.

## مرز مالکیت

Backend تنها مرجع معتبر برای identity، session، عضویت سازمانی، نقش، رابطه ولی/فرزند، enrollment، teacher assignment، publication/release و داده‌های SQL Server است. AI Service حق اتصال مستقیم به SQL Server، تفسیر bearer token کاربر یا تصمیم‌گیری مجوز را ندارد.

جریان مجاز:

```text
Client -> MakanApp.Api -> Application authorization/context builder
       -> IAiServiceClient -> AI Service -> Provider/RAG
       <- structured response <-
```

جریان‌های ممنوع:

- Client یا Frontend به AI Service
- AI Service به SQL Server
- ارسال entityهای EF/Domain یا private message content به AI
- استنتاج نقش یا tenant از payload بدون تأیید Backend

## دارایی‌های موجود

### Backend

- `AccessContext` شامل actor/user/session/workspace/organization/membership/role/subject است.
- نقش‌های سازمانی مجاز: `Student`، `Parent`، `Teacher` و `Manager`.
- `GuardianAccessContextResolver` رابطه فعال ولی/فرزند را دوباره اعتبارسنجی می‌کند.
- dashboardها و detailed reportها read modelهای قابل استفاده برای context کمینه دارند.
- `AssignmentVersion.MaxScore` و `ExamVersion.MaxScore` سقف نمره version-specific هستند.
- فقط gradeهای دارای release باید در context دانش‌آموز و والد دیده شوند.
- `FileAsset` lifecycle فایل را مدیریت می‌کند، اما source lifecycle مربوط به knowledge/RAG نیست.

### AI Service

- endpointهای FastAPI برای chat و سناریوهای Student/Parent/Teacher/Manager موجود است.
- schemaها camelCase و `extra=forbid` هستند.
- orchestration، prompt، provider port، error taxonomy و observability کم‌کاردینال موجود است.
- RAG داخلی lifecycle و checksum دارد، اما persistence/embedding/vector production و قرارداد مالکیت منبع با Backend کامل نیست.
- پیش از STEP 10B، endpointهای business فاقد service authentication هستند.

## شکاف‌های تأییدشده

1. service-to-service authentication وجود ندارد.
2. Backend port/adapter برای AI Service وجود ندارد.
3. endpoint عمومی Backend برای AI وجود ندارد.
4. context builder مجاز و کمینه برای نقش‌ها وجود ندارد.
5. قرارداد منبع knowledge میان `FileAsset` و RAG حل نشده است.
6. contract/E2E/resilience/rollout production هنوز کامل نیست.

## ریسک‌های اصلی و کنترل آن‌ها

| ریسک | کنترل اجباری |
|---|---|
| جعل role/organization/student | context فقط از `AccessContext` و queryهای سرور ساخته شود |
| نشت داده بین سازمان‌ها | تمام queryها با `OrganizationId` معتبر scope شوند |
| نشت داده فرزند | رابطه فعال `GuardianRelation` و selected child اجباری باشد |
| نشت نمره | فقط نتیجه release‌شده و `MaxScore` همان version ارسال شود |
| افشای private chat | محتوای پیام خصوصی هرگز context AI نشود |
| retry دوگانه | برای POST retry کور انجام نشود؛ فقط circuit/timeout کنترل‌شده |
| credential leak | secret فقط از configuration/secret store؛ بدون log و repository |

## تصمیم 10F

اتصال production RAG در این برنامه مشروط است. `FileAssetStatus.Ready` صرفاً آماده بودن blob را ثابت می‌کند و publication، audience، supersession، deletion propagation و re-index ownership را ثابت نمی‌کند. اگر این lifecycle تا 10F تعریف نشود، نتیجه رسمی `RAG_INTEGRATION_DEFERRED_BY_SOURCE_LIFECYCLE` خواهد بود.

## خارج از محدوده

- هرگونه تغییر frontend
- child-safety یا payment behavior
- featureهای STEP 17/18
- ذخیره conversation در AI Service
- ارسال domain entity کامل
