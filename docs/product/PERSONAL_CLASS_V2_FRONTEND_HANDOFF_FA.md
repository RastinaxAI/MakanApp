# قرارداد Frontend کلاس مستقل نسخهٔ ۲

همهٔ مسیرها نیازمند نشست فعال هستند. نقش سازمانی شرط تشکیل یا عضویت کلاس مستقل نیست. Controllerها و OpenAPI منبع دقیق schema هستند؛ متن زیر مسیرهای واقعی همین مرحله را توضیح می‌دهد.

## درخواست تشکیل

| Method | مسیر با پیشوند `/api/v1/personal-class-requests` | ورودی و نتیجه |
|---|---|---|
| POST | ریشه | `clientOperationId`, `details`؛ Draft و نسخه؛ retry همان payload همان Id |
| GET | ریشه | `status?`, `page=1`, `pageSize=30`؛ فقط درخواست‌های خود |
| GET | `/{id}` | جزئیات، capabilities، تصمیم و Progress کلاس تأییدشده |
| PUT | `/{id}` | `expectedVersion`, `details`؛ فقط Draft/NeedsChanges |
| POST | `/{id}/submit` | `expectedVersion`؛ ارسال صریح بدون ساخت کلاس |
| POST | `/{id}/cancel` | `expectedVersion`؛ طبق وضعیت مجاز |
| GET | `/{id}/history` | تاریخچهٔ صفحه‌بندی‌شدهٔ تصمیم‌ها |

`details`: عنوان لازم حداکثر ۲۰۰، هدف لازم حداکثر ۲۰۰۰، توضیح اختیاری حداکثر ۴۰۰۰، `suggestedCapacity>0` و پیشنهاد اختیاری شناسهٔ مدیر/مدرس. پیشنهاد، انتصاب نیست. نمایش دکمه‌ها از `capabilities.canEdit/canSubmit/canCancel/canRespondToChanges` باشد. Approved به معنی Active نیست؛ Progress شامل وضعیت کلاس، پذیرش مدیر و وضعیت هر مقصد است.

مسیر قدیمی ایجاد مستقیم کلاس `409 PERSONAL_CLASS_REVIEW_REQUIRED` می‌دهد؛ کلاینت باید کاربر را به درخواست تشکیل هدایت کند.

## کلاس و اعضا

پیشوند جدول `/api/v1/personal-classes` است. Mutationهای نسخه‌دار، `expectedVersion` Base64 هشت‌بایتیِ پاسخ جاری را می‌گیرند؛ دلیل لازم ۱ تا ۵۰۰ کاراکتر است.

| Method | مسیر | قرارداد و اختیار |
|---|---|---|
| GET | `/page` | صفحهٔ کلاس‌های دارای عضویت جاری؛ `items/page/pageSize/totalCount` |
| GET | `/{id}` | جزئیات و effective capabilities؛ عضو جاری |
| PUT | `/{id}/settings` | نسخه، `capacity>0`, `acceptingJoinRequests`, دلیل؛ مدیر |
| POST | `/{id}/activate`, `/{id}/suspend`, `/{id}/archive` | نسخه و دلیل؛ مدیر؛ Activate نیازمند ظرفیت/مدیر/هر دو مقصد آماده |
| GET | `/{id}/memberships` | مدیر همهٔ دوره‌های عضویت، دیگران فقط عضویت خود؛ صفحه‌بندی |
| PUT | `/{id}/memberships/{memberId}/roles` | نسخهٔ عضویت، `roles`, `delegatedCapabilities`, دلیل؛ مدیر؛ Manager فقط انتقال |
| DELETE | `/{id}/memberships/{memberId}` | body شامل نسخهٔ عضویت و دلیل؛ مدیر یا خود عضو؛ `204` |
| POST | `/{id}/invitations` | `destinationUserId`, `clientOperationId`, نقش/تفویض اختیاری؛ مدیر |
| GET | `/invitations/page` | دعوت‌های مقصد یا کلاس‌های مدیریت‌شده؛ صفحه‌بندی |
| POST | `/invitations/{id}/accept` | بدون body نسخه؛ مقصد دعوت؛ idempotent |
| POST | `/invitations/{id}/decline`, `/revoke` | نسخهٔ دعوت و دلیل؛ به‌ترتیب مقصد و مدیر |
| POST | `/{id}/join-requests` | `clientOperationId`؛ حساب فعال، فقط پذیرش صریح مدیر؛ `201` |
| GET | `/join-requests` | بدون classId درخواست‌های خود؛ با classId فقط مدیر آن کلاس |
| POST | `/join-requests/{id}/decision` | نسخه، `Approved/Rejected/Cancelled`, دلیل؛ تأیید/رد مدیر، لغو صاحب درخواست |
| POST | `/{id}/manager-transfers` | نسخهٔ کلاس، `targetUserId`, `clientOperationId`, دلیل؛ مدیر فعلی؛ `201` |
| GET | `/manager-transfers` | انتقال‌های خود؛ با classId فقط مدیر آن کلاس |
| POST | `/manager-transfers/{id}/decision` | نسخه، `Accepted/Declined/Cancelled`؛ پذیرش/رد مقصد، لغو ایجادکننده |
| GET | `/{id}/spaces` | شناسه/وضعیت Group و Channel، attemptCount، failureCategory و capabilities |
| GET | `/{id}/spaces/backfill-plan` | فقط مدیر؛ طرح read-only و SHA-256 |
| POST | `/{id}/spaces/repair` | نسخه، دلیل و برای Legacy `approvedPlanSha256`؛ `202`، اجرای پس‌زمینه |

فهرست‌های قدیمی ریشه، `/invitations` و `/{id}/participants` برای سازگاری شکل آرایه را نگه داشته‌اند و حداکثر اولین ۱۰۰ ردیف را می‌دهند. برای فهرست کامل از endpointهای صفحه‌بندی استفاده شود. ترتیب تاریخ نزولی و Id پایدار است؛ pageSize حداکثر ۱۰۰ است.

## حالت‌ها و Retry

- Invitation: Pending/Accepted/Declined/Revoked/Expired؛ اعتبار فعلی تنظیم `PersonalClasses:InvitationLifetimeHours` پیش‌فرض ۱۶۸ ساعت است. این policy مستقل از Messaging invitation است. Retry زمان قبلی را تمدید نمی‌کند.
- JoinRequest: Pending/Approved/Rejected/Cancelled؛ Pending هیچ مجوز خواندن نمی‌دهد. پذیرفته‌شدن از یکی از مسیرها intentهای موازی را با علت مشخص می‌بندد.
- LocalRoles یک flags enum است: Learner=1، Teacher=2، Manager=4. تنها Learner صندلی مصرف می‌کند؛ Teacher پیش‌فرض قدرت مدیریتی ندارد.
- Class: Provisioning/ReadyForSetup/Active/ProvisioningFailed/Suspended/Archived. کلاینت وضعیت را به مجوز حدسی تبدیل نکند.
- تغییر Role/Manager/عضویت با نسخهٔ قدیمی `409` می‌دهد؛ وضعیت تازه را GET کنید. انتقال Pending مسئولیت را منتقل نمی‌کند.
- ClassId به‌تنهایی فقط برای JoinRequest فعال‌شده قابل استفاده است؛ API خصوصی برای غیرعضو `404` می‌دهد.

## خطاها و Realtime

ProblemDetails دارای `code` پایدار و correlation موجود است. کدهای جدید: `PERSONAL_CLASS_REQUEST_INVALID` (۴۰۰)، `PERSONAL_CLASS_NOT_FOUND` (۴۰۴)، `PERSONAL_CLASS_REQUEST_CONFLICT`، `PERSONAL_CLASS_REVIEW_REQUIRED`، `PERSONAL_CLASS_NOT_READY`، `PERSONAL_CLASS_CAPACITY_REACHED`، `PERSONAL_CLASS_MEMBERSHIP_INACTIVE`، `PERSONAL_CLASS_LAST_MANAGER_REQUIRED`، `PERSONAL_CLASS_PROVISIONING_FAILED` و `PERSONAL_CLASS_VERSION_CONFLICT` (۴۰۹). خطای مجوز پلتفرم از قرارداد Platform موجود می‌آید.

Hub، replay/cursor و `ClientInvalidated` موجود reuse شده‌اند. invalidationهای `access` و `commerce` علامت بازخوانی API هستند؛ payload شامل محتوای کلاس نیست. ConversationChanged و account-inbox موجود برای چت استفاده می‌شوند. رویداد تکراری نباید ردیف اضافه بسازد. پس از revoke، Read/Send/Download/Replay و تحویل آینده دوباره مجوز جاری را کنترل می‌کنند.

Guardians، نقش مدرسه و سن هیچ اختیار ضمنی برای فضاهای این کلاس نمی‌سازند. Capabilityهای AuthorAssignments/AuthorExams/Grade/ViewOwnResults در این Foundation فعال نیستند. هیچ Frontend در این مرحله تغییر نکرده است.
