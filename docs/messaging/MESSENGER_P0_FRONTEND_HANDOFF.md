# قرارداد مصرف Messenger P0

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

بازبینی ۲۰۲۶-۱۰-۱۶؛ فقط Backend. این سند مجوز ساخت یا تغییر frontend نیست. مرجع اجرایی، Controller/DTO و OpenAPI همین نسخه است؛ PRD هدف محصول است، نه فهرست قابلیت‌های تحویل‌شده. پذیرش این تحویل طبق دستور مالک **فقط محلی** است؛ استقرار یا REAL SERVER PASS ادعا نمی‌شود. [ممیزی و شواهد](MESSENGER_P0_DELIVERY_AUDIT.md).

## هویت، بافت و eligibility

- تمام مسیرهای زیر جز login عمومی، Bearer معتبر در `Authorization` می‌خواهند. cookie session و query-token برای Hub نداریم. 401 یعنی ورود/refresh طبق قرارداد Identity؛ شناسهٔ شناخته‌شده جای مجوز نیست.
- حساب Personal سراسری است و School membership، Role مدرسه یا `PersonalCommunicationGrant` شرط ارتباط دو Verified Adult مجاز نیست. در session دارای بافت سازمانی، با `POST /api/v1/workspaces/select` و `workspaceType:Personal` و `membershipId/role` خالی بافت Personal را انتخاب کنید؛ Role مدرسه مجوز مدرسهٔ دیگر یا چت خصوصی دیگران نیست.
- OTP مالکیت شماره را ثابت می‌کند، نه Adult بودن. `PATCH /api/v1/me/profile` پروفایل را کامل می‌کند، اما DOB خوداظهاری تأیید سن نیست.
- `GET /api/v1/me/messaging-status` فقط دربارهٔ caller است و `verificationStatus`، `effectiveAgeCategory`، `allowedActions`، `nextAction` و `version` می‌دهد. action راهنمای UI است؛ هر مقصد مجوز جاری را دوباره بررسی می‌کند. این endpoint selector کاربر دیگر ندارد.
- نام actionهای جاری: `ReadExistingConversations`، `DiscoverUsers`، `CreateDirect`، `CreatePersonalGroup`، `CreatePersonalChannel`، `DiscoverRelatedUsers`، `CreateDirectWithAuthorizedRelationship`، `CreatePrivatePersonalGroup`. مجموعهٔ واقعی تابع سن مؤثر، کامل‌بودن profile، بافت انتخاب‌شده و محدودیت ارسال است.
- `nextAction` می‌تواند `None`، `VerificationRequired`، `ProfileCompletionRequired`، `VerificationConflictResolutionRequired`، `MessagingRestrictionActive` یا `SelectPersonalWorkspace` باشد. وضعیت را پس از تغییر profile/verification/workspace و reconnect تازه بخوانید؛ cache، مجوز دائمی نیست.

### تأیید اولیهٔ حساب شخصی — مخصوص Platform

`GET /api/admin/v1/users/{userId}/communication-age` و `POST /api/admin/v1/users/{userId}/communication-age/verifications` با `platform.users.manage`، MFA و Step-up تازهٔ GAP-04 اجرا می‌شوند. ورودی POST، `verifiedBirthDate`، `expectedVersion` و `reason` است؛ پاسخ `CommunicationAgeResult` بدون DOB، شامل classification/status/version است. این API خوداظهاری کاربر نیست و شاهد معتبر را اپراتور مجاز باید بررسی کند.

فقط Unverified به تأیید اولیه می‌رود؛ Verified درجا overwrite نمی‌شود؛ Conflict از گردش موجود `POST /api/admin/v1/users/{userId}/communication-age/resolve` با شرایط Owner استفاده می‌کند. stale version برابر 412 و تغییر وضعیت ناسازگار برابر 409 است. هیچ User، Credential، OrganizationPerson یا Membership جعلی ساخته نمی‌شود.

## کشف، دفترچه مخاطبین و Privacy

| Method و مسیر | ورودی | خروجی/رفتار |
|---|---|---|
| GET `/api/v1/people/search` | `q`, `page`, `pageSize` | `PeopleSearchPageResult`؛ Username/display، نه جست‌وجوی جزئی شماره؛ phoneNumber همواره null |
| POST `/api/v1/people/resolve` | دقیقاً یکی از `username` یا `phoneNumber` | `PeopleResolveResult`؛ hidden و nonexistent هر دو `isDiscoverable:false, profile:null`؛ مسیر Username شماره برنمی‌گرداند |
| GET `/api/v1/people/{userId}` | شناسهٔ مقصد | `MessagingPublicProfileResult` فقط با discovery مجاز؛ نامجاز/ناموجود 404 عمومی |
| GET `/api/v1/contacts` | `q`, `favoritesOnly`, `page`, `pageSize` | `UserContactPageResult`، فقط دفترچه caller |
| GET `/api/v1/contacts/recent` | `page`, `pageSize` | `RecentContactPageResult` بر پایهٔ فعالیت Personal Direct؛ نه Presence/Last Seen |
| POST `/api/v1/contacts` | `contactUserId`, `savedName` | `UserContactResult`؛ 201 یا 200/AlreadyExisted در retry، بدون overwrite نام قبلی |
| PATCH `/api/v1/contacts/{contactUserId}` | `savedName`, `isFavorite`, `expectedVersion` | همان Contact با version تازه؛ stale برابر 412 |
| DELETE `/api/v1/contacts/{contactUserId}` | — | 204، تکرار امن؛ نه حذف User/history و نه Block |
| GET `/api/v1/messaging/privacy` | — | `MessagingPrivacySettingsResult`؛ defaults هم معتبرند |
| PUT `/api/v1/messaging/privacy` | چهار audience و `expectedVersion` | ایجاد با version خالی در حالت default؛ تغییر setting موجود با version جاری |

چهار audience ورودی Privacy: `directMessagesAudience`، `phoneNumberVisibilityAudience`، `phoneNumberDiscoveryAudience`، `groupInvitesAudience`. `Everyone/Contacts/Nobody` متعلق به **مقصد** است؛ Contacts یعنی مقصد، درخواست‌کننده را ذخیره کرده باشد، نه برعکس. visibility شماره با discoverability شماره مستقل است. resolve شماره فقط exact و تابع privacy مقصد است؛ Username به permission دفترچه تلفن دستگاه وابسته نیست.

Verified Adultهای مجاز دوطرفه قابل کشف‌اند؛ Minor/Unknown عمومی کشف نمی‌شوند و caller محافظت‌شده نیز Adult نامرتبط را عمومی کشف نمی‌کند. رابطه/رضایت معتبر موجود مسیر محدود مستقل دارد. profile عمومی، verification/age/child/block reason فرد دیگر را افشا نمی‌کند. `canStartDirect:false` دلیل خصوصی را توضیح نمی‌دهد و true نیز جای بررسی سرور نیست.

Contact با UserId پایدار نگه‌داری می‌شود؛ شماره فقط ورودی discovery است. SavedName و Favorite محلیِ owner هستند؛ ذخیرهٔ B توسط A دفترچهٔ B را تغییر نمی‌دهد. address-book sync خودکار در این P0 وجود ندارد.

## Direct، ثبت پایدار و وضعیت پیام

`POST /api/v1/conversations/direct` با `{ "targetUserId":"<guid>", "scope":"Personal" }` یک `DirectConversationResult` با 201 یا 200/AlreadyExisted می‌دهد. A→B و B→A هم‌زمان یک canonical pair دارند؛ DB/transaction invariant از تکرار جلوگیری می‌کند. Contact اجباری نیست؛ مجوز حساب، سن، privacy، block و رابطهٔ لازم دوباره بررسی می‌شوند. پاسخ نامجاز create، علت خصوصی مقصد را لو نمی‌دهد.

`GET /api/v1/conversations` با `filter/page/pageSize/q` آرایهٔ `ConversationSummaryResult` می‌دهد؛ wrapper و hasMore خیالی اضافه نکنید. ordering شامل pin، فعالیت و tie-breaker شناسه است. paging فعلی offset-based است؛ پس از invalidation فعالیت، صفحه‌ها را refresh و با ConversationId deduplicate کنید. خواندن چند صفحه در زمان mutation، snapshot اتمیک نیست.

`POST /api/v1/conversations/{conversationId}/messages`، `SendMessageCommand` با `clientMessageId` یکتا و `kind/text/attachmentIds/replyToMessageId/mentionedUserIds` متناسب با نوع می‌گیرد؛ 200 و `MessageReceiptResult` شامل `messageId/conversationId/clientMessageId/sequence/kind/sentAtUtc/status/version`، تأیید ثبت سرور است. `Sent`، تأیید peer delivered/read نیست. sequence فقط داخل همان گفتگو معنا دارد.

حالت‌های پیشنهادی مصرف، نه enum تازهٔ Backend:

1. پیش از receipt، پیام pending است؛ ClientMessageId را **پیش از POST** نگه دارید.
2. receipt موفق، accepted/Sent را ثابت می‌کند؛ پیام، sequence، change و outbox در transaction ثبت شده‌اند.
3. timeout/قطع پاسخ نتیجه را نامعلوم می‌کند؛ **همان شناسه و همان payload** را retry کنید. retry هم‌زمان نیز یک اثر دارد؛ payload متفاوت با همان شناسه 409 است.
4. validation/403/422 موفقیت نیست؛ draft محلی را نگه دارید و علت مجاز را رسیدگی کنید. ReviewRequired/Blocked را Sent نمایش ندهید. 503 Safety برای مسیر محافظت‌شده fail-closed است.

`GET /api/v1/conversations/{conversationId}/messages?beforeSequence=...&limit=...`، `ConversationMessagePageResult` با `messages/nextBeforeSequence` می‌دهد؛ page number برای تاریخچه ابداع نکنید. PATCH همان message با `text/expectedVersion` و DELETE آن با query `expectedVersion`، و مسیرهای `/for-everyone` و `/for-me` موجودند. edit و حذف عادی برای همه مهلت سروری یک ساعت دارند؛ ForMe خصوصی است. مجوز جاری و version دوباره بررسی می‌شوند؛ revision داخلی متن قبلی را به UI عادی نمی‌دهد.

## SignalR، reconnect و cursor

- Hub: `/hubs/messaging`؛ متدها: `SubscribeConversation(Guid)` و `UnsubscribeConversation(Guid)`؛ event: `ConversationChanged` با `MessagingRealtimeNotification`، نه متن کامل پیام.
- فقط Bearer header پشتیبانی می‌شود؛ در مرورگر transport مبتنی بر header مانند LongPolling با `accessTokenFactory` و `withCredentials:false` را مطابق محدودیت محیط انتخاب کنید. پشتیبانی query-token مرورگر یا WebSocket مبتنی بر آن ادعا نشده است.
- اتصال در session-group ثبت می‌شود؛ اولین گفتگوی مجاز بدون Subscribe ازپیش‌موجود نیز invalidation می‌دهد. UnsubscribeConversation خروج از حساب یا خاموش‌کردن session invalidation نیست.
- گیرندگان از حساب/نشست/بافت/عضویت جاری و **visibility همان event در Delta** تعیین می‌شوند؛ FromJoin، Clear، DeleteForMe و رویداد خصوصی نیز اعمال می‌شوند. شناسه یا subscription قدیمی bypass نیست.
- `GET /api/v1/conversations/{conversationId}/changes?afterCursor=...&limit=...`، `ConversationChangePageResult` با `changes/nextCursor/hasMore` می‌دهد. cursor opaque و conversation-bound است؛ آن را decode یا برای گفتگوی دیگر reuse نکنید.
- رویداد SignalR ممکن است تکرار/دیرکرد/فقدان داشته باشد. پس از reconnect، list و Delta/history را بازیابی کنید. دریافت notification.cursor به‌تنهایی دلیل جلو بردن checkpoint نیست؛ فقط **پس از پردازش موفق صفحهٔ Delta** nextCursor را نگه دارید و hasMore را تا پایان دنبال کنید. ChangeId/MessageId را deduplicate کنید.
- `POST /api/v1/conversations/{conversationId}/read` و `/delivered`، `upToMessageSequence` و cursorهای monotonic **خود caller** را نگه می‌دارند. دریافت private read-state طرف دیگر تضمین نشده است: **T10 = DEFERRED_TO_P1_B04**.
- لغو session، authorization REST/Hub و دریافت آینده را می‌بندد؛ محتوا یا رویداد قبلاً تحویل‌شده را نمی‌توان از دستگاه پس گرفت. backplane/scale-out چند instance در P0 پیاده نشده است.

## Group و Channel پایه

`POST /api/v1/conversations/groups` یا `/channels`، `CreateManagedConversationCommand` شامل `clientOperationId/scope/title/description/initialParticipantUserIds/visibility/groupHistoryPolicy/memberCapabilities` می‌گیرد و `ManagedConversationResult` با 201 یا 200/AlreadyExisted می‌دهد. پیش‌فرض private است؛ retry همان operation/payload را نگه دارید. capabilityهای Group با permissionهای Admin یکی نیستند.

| Method و مسیر | مسئولیت و ورودی مهم |
|---|---|
| GET `/api/v1/conversations/{conversationId}` | جزئیات فعلی و versionها |
| GET/POST `/api/v1/conversations/{conversationId}/members` | فهرست صفحه‌بندی‌شده / add با `userId` و eligibility/privacy جاری |
| DELETE `/api/v1/conversations/{conversationId}/members/{targetUserId}` | remove مجاز؛ GUID قدیمی حق send/read/download نمی‌دهد |
| POST `/api/v1/conversations/{conversationId}/leave` | خروج؛ Owner آخر باید ابتدا مالکیت را منتقل کند |
| PATCH `/api/v1/conversations/{conversationId}/members/{targetUserId}/role` | `role/adminPermissions`؛ کنترل server-side و transaction موجود |
| PATCH `/api/v1/conversations/{conversationId}/members/{targetUserId}/permissions` | `permissions/expectedVersion` برای Admin؛ stale برابر 412 |
| PATCH `/api/v1/conversations/{conversationId}/settings` | `visibility/groupHistoryPolicy/memberCapabilities/expectedVersion` |
| GET `/api/v1/conversations/discover`، POST `/api/v1/conversations/{conversationId}/join` | کشف/عضویت Public مجاز؛ Private با GUID قابل join نیست |
| POST `/api/v1/conversations/{conversationId}/ownership-transfers` | شروع با `targetUserId`؛ accept/decline روی `/{transferId}/accept` یا `/decline` |

Owner/Admin/Member از Backend می‌آیند. Admin کانال بدون `PublishMessages` و subscriber معمولی حق انتشار ندارند. Group بدون `SendMedia` با پنهان‌کردن دکمه ایمن نمی‌شود؛ سرور آن را رد می‌کند. قرارداد فعلی پیام `SenderUserId/Sender` دارد؛ posting ناشناس با هویت تازهٔ Channel در این P0 ساخته نشده است.

Minor/Unknown تنها Group شخصی private محدود دارند؛ ساخت Public/Channel و انتشار Channel مجاز نیست. عضو محافظت‌شده نیازمند رابطه و دعوت با پذیرش صریح است: POST `/api/v1/conversations/{conversationId}/invitations`، GET `/api/v1/messaging/invitations` و POST `/api/v1/messaging/invitations/{invitationId}/accept`، `/decline` یا `/revoke`. گروه رسمی کلاس از Enrollment/TeacherAssignment می‌آید، نه add دلخواه participant.

## فایل، Block و ایمنی

`POST /api/v1/files` با multipart field `File` یک FileAsset می‌سازد؛ فقط دارایی آماده و مجاز خود فرستنده در attachmentIds پذیرفته می‌شود. `GET /api/v1/files/{fileId}/content` و `GET /api/v1/conversations/{conversationId}/media` مجوز جاری، عضویت و history visibility را می‌سنجند. URL فایل، path عمومی دائمی یا storage key نیست؛ حذف عضو دسترسی بعدی را می‌بندد. محدودیت فنی Storage قابل تنظیم است، نه سقف محصولی جدید. MIME/signature اثبات ایمنی معنایی نیست؛ رسانه unsupported در مسیر محافظت‌شده ReviewRequired است.

GET `/api/v1/messaging/blocks` و POST/DELETE `/api/v1/messaging/blocks/{userId}` موجودند: GET روی collection، POST و DELETE روی userId. Block، Direct جدید/ارسال آینده را می‌بندد؛ history، Shared Group یا دادهٔ تحصیلی را حذف نمی‌کند. Unblock، Contact حذف‌شده را خودکار برنمی‌گرداند.

GuardianRelation دسترسی به private chat کودک نیست؛ School Manager نیز private Direct browsing ندارد. Unknown مانند Minor محافظت می‌شود؛ تغییر سن، پایان رابطه، block و permission در عملیات آینده دوباره ارزیابی می‌شوند. Internal Makan Content Safety موجود حفظ شده؛ AI/RAG و MeliPayamak در این تحویل تغییر نکردند.

## خطا، Version و Enum

| وضعیت | نمونهٔ code و اقدام |
|---|---|
| 400 | `PEOPLE_RESOLVE_INVALID`، `CHANGE_CURSOR_INVALID`؛ ورودی را اصلاح کنید؛ نام Enum ناشناخته نیز binding را رد می‌کند |
| 401 | نشست نامعتبر/منقضی/لغوشده؛ ورود/refresh موجود، نه retry بی‌نهایت |
| 403 | `MESSAGE_NOT_ALLOWED`، `MESSAGE_PUBLISH_NOT_ALLOWED`، `CONVERSATION_PERMISSION_REQUIRED`؛ permission معتبر لازم است؛ bit ناشناخته مجوز نمی‌گیرد |
| 404 | `DIRECT_RECIPIENT_NOT_AVAILABLE`، `PERSON_NOT_DISCOVERABLE`، `CONVERSATION_NOT_FOUND`؛ علت خصوصی را حدس نزنید |
| 409 | `MESSAGE_IDEMPOTENCY_CONFLICT`، `CONVERSATION_CREATION_CONFLICT`، `AGE_VERIFICATION_CONFLICT`؛ payload/state را بررسی کنید |
| 412 | `CONTACT_CONFLICT`، `MESSAGING_PRIVACY_CONFLICT`، `CONVERSATION_STATE_CONFLICT`، `MESSAGE_EDIT_CONFLICT`؛ state تازه بخوانید و تصمیم کاربر را دوباره اعمال کنید |
| 422 | `MESSAGE_SAFETY_BLOCKED`، `MESSAGE_SAFETY_REVIEW_REQUIRED`؛ پیام پذیرفته نشده است |
| 429 | `MESSAGING_DISCOVERY_RATE_LIMITED`؛ Retry-After برحسب ثانیه را رعایت کنید؛ حلقهٔ تند retry ممنوع |
| 503 | `MESSAGE_SAFETY_UNAVAILABLE`؛ fail-closed؛ پیام Sent نیست |

خطاهای دامنه ProblemDetails/code دارند؛ denial عمومی middleware ممکن است body خالی داشته باشد، پس status را نیز هندل کنید. `X-Correlation-ID` و `Retry-After` در CORS قابل خواندن‌اند؛ allowlist محیط حفظ شده است. secret/OTP/token را در telemetry قرار ندهید.

Version، Base64 از SQL rowversion و opaque است؛ expectedVersion را از همان resource جاری بفرستید. همهٔ عملیات یک قرارداد version ندارند؛ برای contact/privacy/settings/permissions/edit طبق DTO بالا عمل کنید، نه با header خیالی If-Match یا Idempotency-Key.

OpenAPI اکنون Enumهای عادی و nullable را با **type:string** و literal واقعی MVC توصیف می‌کند؛ SignalR همان نمایش را دارد. Flags رشتهٔ نام‌دار یا ترکیب با `, ` است، مثلاً `ManageMembers, PublishMessages`؛ aliasهایی مثل `LegacyMemberDefault` نیز ممکن‌اند. Flags را به enum تک‌مقداری محدود یا به JSON array تبدیل نکنید. parsing عدد معتبر قدیمی حفظ شده، ولی ورودی frontend رشتهٔ مصوب باشد؛ مقدار/bit ناشناخته هرگز مجوز نمی‌دهد.

## موارد معوق

Peer Read Receipt، Push واقعی، Typing، Presence/Last Seen، backplane، Topics/Poll/Scheduled Messages/Folders پیشرفته، تماس/ویدئو/Stories/Bots/Mini Apps/Premium/Monetization و Secret Chat/RAG خارج این P0 هستند. [ثبت رسمی موارد معوق](../release/DEFERRED_DECISIONS_AND_REMAINING_GAPS.md). این سند API خیالی برای آن‌ها معرفی نمی‌کند.
