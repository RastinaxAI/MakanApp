# STEP 14A — ممیزی قرارداد Messenger PRD v2

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## 1. هدف، baseline و روش

این سند اختلاف میان کد موجود، Messenger PRD v2 و تصمیم‌های تثبیت‌شده محصول را ثبت می‌کند. STEP 14A فقط audit/documentation است: هیچ رفتار runtime، تست، migration یا schema در این مرحله تغییر نکرده است.

Baseline هنگام شروع ممیزی:

- Branch: `main`
- HEAD و `origin/main`: `030a1a45ab31752ee3d16efca544a0ebb83b142c`
- Worktree: تمیز
- معماری: Controller-based ASP.NET Core روی .NET 9، Onion Architecture در Modular Monolith، EF Core 9 و SQL Server
- Authentication: نشست opaque Bearer و `UserSession` سمت سرور؛ JWT در این audit پیشنهاد یا اضافه نشده است.

مجموع Requirementهای ماتریس: **127**.

| وضعیت | تعداد | معنی |
|---|---:|---|
| `IMPLEMENTED` | 35 | رفتار موردنیاز با API/Code/DB/Test موجود هم‌راستا است. |
| `PARTIAL` | 34 | بخشی از رفتار موجود است، اما قرارداد کامل یا یکی از مرزهای امنیتی ناقص است. |
| `MISSING` | 45 | API/مدل/persistence/test لازم وجود ندارد. |
| `CONFLICTING` | 8 | رفتار موجود صریحاً با تصمیم freeze‌شده تعارض دارد. |
| `UNRESOLVED_PRODUCT_POLICY` | 1 | Product عمداً تصمیم کافی نداده است. |
| `NOT_APPLICABLE` | 4 | قابلیت enhancement است و برای baseline فعلی الزام پیاده‌سازی ندارد. |

هر `IMPLEMENTED` در ماتریس به endpoint، کلاس/متد، migration و تست واقعی ارجاع دارد. وضعیت‌های `MISSING` بر اساس inventory کامل مسیرهای Messaging، Identity/Profile، Storage، Notification، Controllerها، migrationها و تست‌های Messaging ثبت شده‌اند؛ شباهت نام یک type به‌تنهایی پیاده‌سازی تلقی نشده است.

## 2. خلاصه وضعیت فعلی Messaging

پیاده‌سازی فعلی foundation قابل اتکایی برای Direct، Group، Channel، پیام پایدار، retry، media، revision، reaction، pin، search، block/report، Delta و SignalR دارد. نقاط قوی اصلی:

- زوج Direct canonical و unique index مانع گفت‌وگوی موازی در یک Scope می‌شود.
- `ClientMessageId` و sequence سرورمحور ارسال را retry-safe و مرتب می‌کنند.
- هر پیام revision اولیه دارد؛ Edit revision تازه می‌سازد و API عادی revision قدیمی را برنمی‌گرداند.
- Delete فعلی tombstone می‌سازد و متن جاری/SearchText را پاک می‌کند.
- دسترسی Search، Shared Media، Delta، SignalR و فایل bound در زمان درخواست دوباره بررسی می‌شود.
- Block فقط در مسیر Direct بررسی می‌شود و Group membership را حذف نمی‌کند.
- Report پیام‌محور، idempotent و دارای snapshot کمینه است؛ route عمومی reviewer وجود ندارد.

مهم‌ترین اختلاف‌ها با Messenger v2:

1. `CommunicationEligibilityPolicy.CanUsePersonal` دو حساب Adult و `PersonalCommunicationGrant` فعال می‌خواهد؛ تصمیم جدید Personal Direct را اصولاً میان حساب‌های معتبر مجاز می‌داند.
2. `Message.EditText` و `EfMessagingStore.DeleteMessageAsync` هیچ window یک‌ساعته ندارند.
3. `Delete For Me`، Contacts و Profile Privacy/Discovery وجود ندارند.
4. `Conversation.Archive` کل Conversation را برای همه Archive می‌کند؛ archive شخصی و unarchive وجود ندارد.
5. `ConversationParticipantRole.Admin` coarse است و permissionهای مستقل Group/Channel ندارد.
6. `JoinedAtUtc` ذخیره می‌شود، اما History/Search/Media بر اساس زمان یا sequence عضویت فیلتر نمی‌شوند و Group History setting وجود ندارد.
7. Public/Private discoverability، organization badge DTO، Saved Messages، Mute و conversation pin وجود ندارند.
8. Notification foundation دسته `Messages` دارد، ولی هیچ producer پیام‌رسانی، preview policy یا mention/reply notification ندارد.
9. Hook پیش از delivery، moderation provider port، `SafetyIncident`، queue reviewer و attachment safety scan وجود ندارند.

## 3. کاتالوگ شواهد مخزن

| کد شاهد | شواهد واقعی |
|---|---|
| `E-DIRECT` | `ConversationsController.StartDirect` → `MessagingService.StartOrGetDirectConversationAsync` → `EfMessagingStore.StartOrGetDirectConversationCoreAsync`؛ `DirectUserPair`؛ migration `AddDirectMessagingFoundation`؛ تست‌های `EligibleAdultsCreateOneCanonicalPersonalConversationWithoutPhoneDisclosure` و `ConcurrentStartsCreateExactlyOneConversation`. |
| `E-ELIG` | `CommunicationEligibilityPolicy.CanStartOrSend/CanRead` و `EfMessagingStore.LoadEligibilityFactsAsync`؛ تست‌های `PersonalConversationRequiresGrantAndAdultParticipantsUsingGenericFailure`، `TeacherAndStudentCanMessageOnlyWhileSharedClassRelationshipIsActive` و `PersonalHistoryRemainsReadableAfterEligibilityChanges`. |
| `E-MANAGE` | endpointهای Group/Channel/member/role/transfer/archive در `ConversationsController`؛ `ConversationManagementAuthorizationPolicy` و `ConversationManagementService`؛ فایل‌های `EfMessagingStore.Management.*`؛ migration `AddGroupAndChannelMessaging` و `MessagingGroupChannelEndpointsTests`. |
| `E-SEND` | `ConversationsController.SendMessage` → `MessagingService.SendMessageAsync` → `EfMessagingStore.CreateMessageAsync`؛ unique `(ConversationId, SenderUserId, ClientMessageId)` و `(ConversationId, Sequence)`؛ تست‌های `MessageUsesServerTimePreservesTextAndIsIdempotent` و `ConcurrentMessagesReceiveUniqueMonotonicSequences`. |
| `E-ADV` | Edit/Delete/Forward/Reaction/Pin/Media در `MessagingService` و `EfMessagingStore.Advanced.*`؛ migration `AddAdvancedMessagingFeatures`؛ `MessagingAdvancedMutationEndpointsTests` و `MessagingAdvancedInteractionEndpointsTests`. |
| `E-SYNC` | endpoints `changes/read/delivered`، `MessagingChangeEvent`، `RealtimeOutbox`، `MessagingHub` در `/hubs/messaging`؛ migration `AddMessagingRealtimeAndSync`؛ `MessagingSyncEndpointsTests` و `MessagingRealtimeEndpointsTests`. |
| `E-SAFETY` | `MessagesController.Search`، `MessagingSafetyController` و `MessageReportsController` → `MessagingSafetyService` → `EfMessagingStore.Safety`؛ migration `AddMessagingSafetyAndSearch`؛ `MessagingSafetyEndpointsTests`. |
| `E-FILE` | `FilesController` و `StorageService`؛ `FileUploadPolicy` با `Storage:MaxFileSizeBytes` و MIME allowlist؛ `EfFileAssetBoundAccessResolver` مجوز Participant/Scope جاری را بررسی می‌کند؛ تست‌های media و file authorization. |
| `E-PROFILE` | `ProfileController` فقط `/api/v1/me` و تکمیل profile خود کاربر را ارائه می‌کند؛ `User` فقط Username/AgeCategory و `Person` نام‌ها را دارد؛ Avatar/Bio/Privacy/Discovery/LastSeen وجود ندارد. |
| `E-NOTIFY` | `NotificationPreferenceCategory.Messages` وجود دارد، ولی `NotificationType` فقط Assignment/Exam است و Messaging producer/preview/mute integration یافت نشد. Realtime outbox مستقل از durable Notification outbox است. |
| `E-NONE` | در Domain/Application/Infrastructure/API/migration/testهای بررسی‌شده type یا endpoint سازگار با Contact، ProfilePrivacy، ConversationVisibility، DeleteForMe، ConversationMute، SavedMessages، InviteLink، SafetyIncident یا moderation queue وجود ندارد. |

## 4. رفتار Direct فعلی در برابر تصمیم جدید

`Personal Direct` فعلی فقط در Personal workspace، بدون OrganizationId، میان دو حساب profile-complete با `CommunicationAgeCategory.Adult` و `PersonalCommunicationGrant` فعال شروع/ارسال می‌شود. این رفتار با تصمیم جدید «Personal Direct generally allowed between Makan accounts subject to Privacy/Block/Safety» **متعارض** است. حذف ساده همه checkها مجاز نیست: account validity و Block باید بمانند، Privacy/Discovery و Child Safety باید پیش از بازکردن policy اضافه شوند.

`Organization Direct` همچنان به workspace، Membership، RoleAssignment و رابطه آموزشی/Guardian معتبر وابسته است و نباید با تغییر Personal Direct تضعیف شود. Group/Channel سازمانی نیز باید boundary سازمان را حفظ کند.

## 5. Edit، Delete و Revision

- `CreateMessageAsync` برای پیام اولیه `MessageRevision` شماره 1 می‌سازد.
- `EditMessageAsync` فقط sender، rowversion و عدم حذف را کنترل می‌کند و revision جدید می‌سازد؛ window یک‌ساعته وجود ندارد.
- `DeleteMessageAsync` فقط sender، rowversion و عدم حذف را کنترل می‌کند؛ window یک‌ساعته وجود ندارد.
- `Message.Delete`، `Text` و `SearchText` جاری را null می‌کند ولی revisionهای قبلی را نگه می‌دارد.
- `MessagingService.Map` برای tombstone، Kind/Text/Reply/Reaction/Mention/Attachment را پنهان می‌کند؛ Search و Media پیام حذف‌شده را حذف می‌کنند و revision endpoint عمومی وجود ندارد.
- `Delete For Me` و visibility row شخصی وجود ندارد.

نتیجه: حفظ داخلی و عدم افشای عادی فعلی مناسب است، اما windowهای یک‌ساعته و semantics مستقل `Delete For Me` در 14C لازم‌اند.

## 6. Group History، Block و scope

`ConversationParticipant.JoinedAtUtc` و `Message.Sequence` موجودند، اما `GetConversationMessagesAsync`، `SearchMessagesAsync` و `GetConversationMediaAsync` همه history را به Participant فعال برمی‌گردانند. setting تاریخچه وجود ندارد. برای schema آینده، مرز sequence هنگام join از time-only ایمن‌تر است، ولی نام و schema نهایی در 14A انتخاب نمی‌شود.

`HasEffectiveUserBlockAsync` فقط در ایجاد/ارسال Direct فراخوانی می‌شود. مسیر ارسال Group/Channel پیش از check Block بازمی‌گردد و Block عضویت را تغییر نمی‌دهد؛ بنابراین تصمیم «Block affects Direct, not shared Groups» اکنون رعایت می‌شود. Block دوطرفه مانع ارسال Direct جدید می‌شود، ولی رکورد Block جهت‌دار و فهرست فقط متعلق به blocker است.

## 7. Moderation & Report Gap Analysis

| Scope | چه کسی اکنون Report می‌کند؟ | reviewer فعلی | evidence فعلی | gap و مجوز آینده |
|---|---|---|---|---|
| Personal Direct | Participant فعال دارای دسترسی جاری | وجود ندارد | یک پیام، sender، kind، متن جاری، rowversion، reason و description؛ idempotent با `ClientReportId` | reviewer Personal Direct، retention و appeal حل‌نشده؛ School Manager نباید دسترسی بگیرد. |
| Personal Group | Participant فعال | وجود ندارد | همان snapshot کمینه | Owner/moderator فقط با permission صریح و evidence محدود؛ queue لازم است. |
| Personal Channel | Subscriber/participant فعال | وجود ندارد | همان snapshot | route و permission reviewer مستقل لازم است. |
| Organization Group | Participant فعال و Membership جاری | وجود ندارد | همان snapshot | Manager صرفاً به‌خاطر role مجاز نیست؛ permission منبع و scope سازمان لازم است. |
| Organization Channel | Participant فعال و Membership جاری | وجود ندارد | همان snapshot | publisher/admin title کافی نیست؛ permission review باید مستقل باشد. |

`AbuseReportStatus` حالت‌های آینده را مدل می‌کند، اما endpoint انتقال به `UnderReview/Resolved/Dismissed`، reviewer identity، moderation queue، appeal و retention job وجود ندارد. تست `ReportEvidenceIsNotAvailableToOutsiderManagerGroupOwnerOrPublicModerationRoute` عدم دسترسی blanket فعلی را اثبات می‌کند.

## 8. Child Safety Gap Analysis

- نمای minor فعلی فقط `CommunicationAgeCategory` روی `User` است و policy قدیمی Personal communication را به Adult محدود می‌کند.
- جریان متن و فایل مستقیماً پس از validation فنی و authorization commit می‌شود؛ pre-delivery safety hook وجود ندارد.
- port برای moderation provider، `SafetyIncident`، severity/priority، escalation SLA، reviewer queue و high-risk audit وجود ندارد.
- attachment فقط از نظر size/MIME/status/uploader/scope کنترل می‌شود؛ malware/content moderation انجام نمی‌شود.
- `AbuseReport` یک مسیر post-send دستی است و جای prevention پرخطر را نمی‌گیرد.
- Messaging realtime outbox فقط invalidation می‌فرستد و Notification foundation هنوز رخداد safety تولید نمی‌کند.
- وابستگی Backoffice/reviewer و provider/model/vendor عمداً حل‌نشده است؛ در 14D باید port و queue مستقل از vendor طراحی شوند.

## 9. ظرفیت، discoverability، badge و اولویت انتشار

### Product Limit در برابر Technical Safety Limit

- هیچ سقف member/subscriber در Domain، Application options یا persistence Group/Channel یافت نشد؛ در نتیجه business cap ثابت فعلی وجود ندارد.
- `Storage:MaxFileSizeBytes` به‌صورت پیش‌فرض `10 MiB` و قابل تنظیم است. `Program.cs` مقدار `MultipartBodyLengthLimit` را برابر این سقف به‌علاوه overhead قرار می‌دهد. این‌ها **Technical Safety Limit** هستند، نه تصمیم محصول درباره حداکثر اندازه فایل.
- MIME allowlist پیش‌فرض PDF/JPEG/PNG/Text نیز safeguard فنی است. Video/Voice فقط با تنظیم MIME محیط قابل upload هستند.
- `Messaging:MaximumAttachmentsPerMessage=10`، `MaximumMentionsPerMessage=50` و page limitها محدودیت شکل request/read هستند، نه سقف تجاری اندازه فایل یا عضو.
- هیچ malware scan یا content-safety scan وجود ندارد؛ validation فنی نباید به‌عنوان ایمنی محتوا گزارش شود.

### Public/Private و Organization badge

`ConversationScope.Personal` فقط ownership/scope را مشخص می‌کند و معادل `Public` نیست. مدل `Conversation` هیچ visibility/discoverability field ندارد؛ بنابراین همه Group/Channelهای فعلی عملاً فقط از طریق عضویت صریح قابل دسترسی‌اند. `ManagedConversationResult` و `ConversationSummaryResult` فقط `OrganizationId` را می‌دهند و نام/logo امن سازمان برای badge ندارند. 14C باید visibility را از Scope جدا و projection حداقلی badge را بدون افشای اطلاعات داخلی اضافه کند.

### stateهای قابل مشاهده پیام

- `Sent`: receipt پس از commit SQL Server؛ پیاده‌سازی‌شده.
- `Delivered/Available`: فقط با ACK صریح `delivered` و cursor monotonic؛ پیاده‌سازی‌شده و معادل read نیست.
- `Read`: فقط با ACK صریح `read`؛ پیاده‌سازی‌شده و Delivered را نیز جلو می‌برد.
- `Pending/Failed`: state پایدار server-side برای Message وجود ندارد؛ این‌ها فعلاً state کلاینت/upload هستند و قرارداد کامل نشده است.

### Contact Sync Security

sync پیاده‌سازی نشده است. طراحی 14B باید permission سیستم‌عامل و اقدام صریح کاربر، matching بدون enumeration، امکان خاموش‌کردن sync و حذف state واردشده بدون حذف Contact دستی یا Conversation را تضمین کند. upload خام و silent کل address book مجاز نیست.

### MVP / Full / Advanced

| سطح | وضعیت baseline |
|---|---|
| MVP | Direct/Group/Channel پایه، Block، chat list، text و Read موجودند؛ Contacts، Profile Privacy، Mute و granular permissions gap اصلی‌اند. |
| Full | file/media، Reply/Edit/Delete/Forward، Reaction، Search، Shared Media و multi-device تا حد زیادی موجودند؛ phone sync، Delete For Me، archive شخصی و Saved Messages ناقص‌اند. |
| Advanced | invite link پیشرفته، Poll، Topics، folders، Scheduled Message، Slow Mode و channel analytics پیاده‌سازی نشده‌اند و در 14B–14D نباید وارد شوند. |

## 10. API inventory فعلی

همه endpointهای زیر `[Authorize]` هستند، actor/session را از `AuthenticatedSession` می‌گیرند و Application service، `AccessContext` را سمت سرور resolve می‌کند.

| Method | Route | Controller → Application | authorization اصلی | Requirementها |
|---|---|---|---|---|
| POST | `/api/v1/conversations/direct` | `ConversationsController` → `MessagingService.StartOrGetDirectConversationAsync` | eligibility + Block + Scope | DM-01/02، AC-03 |
| GET | `/api/v1/conversations` | `ConversationsController` → `GetMyConversationsAsync` | Participant + workspace/Membership | LST-01/02/03، NTF-05 |
| POST | `/api/v1/conversations/groups` | `ConversationsController` → `ConversationManagementService.CreateGroupAsync` | create policy + target eligibility | GRP-01/03/15، LST-10 |
| POST | `/api/v1/conversations/channels` | `ConversationsController` → `CreateChannelAsync` | create policy + target eligibility | CHN-01/11، LST-10 |
| GET | `/api/v1/conversations/{id}` | `ConversationsController` → `GetDetailsAsync` | active Participant + Scope | GRP-09، CHN-07 |
| GET | `/api/v1/conversations/{id}/messages` | `ConversationsController` → `GetConversationMessagesAsync` | active Participant + Scope | DM-04/12، MSG-05/14 |
| POST | `/api/v1/conversations/{id}/messages` | `ConversationsController` → `SendMessageAsync` | Participant/publish policy + Direct eligibility/Block | DM-03/04، MSG-01/03/10 |
| PATCH | `/api/v1/conversations/{id}/messages/{messageId}` | `ConversationsController` → `EditMessageAsync` | sender + rowversion | DM-05، MSG-06 |
| DELETE | `/api/v1/conversations/{id}/messages/{messageId}` | `ConversationsController` → `DeleteMessageAsync` | sender + rowversion | DM-06، MSG-07 |
| POST | `/api/v1/conversations/{id}/messages/{messageId}/forward` | `ConversationsController` → `ForwardMessageAsync` | source/destination access + same Scope | DM-07، MSG-08 |
| POST/DELETE | `/api/v1/conversations/{id}/messages/{messageId}/reactions[...]` | `ConversationsController` → Add/RemoveReaction | active Participant | DM-08، MSG-09 |
| POST/DELETE | `/api/v1/conversations/{id}/pins/{messageId}` | `ConversationsController` → Pin/Unpin | Direct Participant یا Group/Channel Owner/Admin | DM-09، GRP-10، ORG-04 |
| GET | `/api/v1/conversations/{id}/media` | `ConversationsController` → `GetConversationMediaAsync` | current Participant + Scope | DM-12، MSG-12/13، AC-15 |
| GET | `/api/v1/conversations/{id}/changes` | `ConversationsController` → `GetConversationChangesAsync` | current Participant + Scope | LST-12، MSG-14 |
| POST | `/api/v1/conversations/{id}/read` | `ConversationsController` → `MarkConversationReadAsync` | current Participant + monotonic cursor | LST-08، DM-11، AC-10 |
| POST | `/api/v1/conversations/{id}/delivered` | `ConversationsController` → `AcknowledgeConversationDeliveryAsync` | current Participant + monotonic cursor | DM-11 |
| POST/DELETE | `/api/v1/conversations/{id}/members[...]` | `ConversationsController` → Add/RemoveMember | coarse Owner/Admin matrix | GRP-03/06/15، CHN-07 |
| POST | `/api/v1/conversations/{id}/leave` | `ConversationsController` → `LeaveAsync` | Owner ممنوع تا transfer | GRP-11، CHN-07 |
| PATCH | `/api/v1/conversations/{id}/members/{userId}/role` | `ConversationsController` → `ChangeMemberRoleAsync` | فقط Owner | GRP-06، CHN-03 |
| POST | `/api/v1/conversations/{id}/ownership-transfers` | `ConversationsController` → `StartOwnershipTransferAsync` | Owner | GRP-02/11 |
| POST | `/api/v1/conversations/{id}/ownership-transfers/{transferId}/accept` و `/decline` | `ConversationsController` → transfer completion | target Participant | GRP-02/11 |
| POST | `/api/v1/conversations/{id}/archive` | `ConversationsController` → `ArchiveAsync` | Owner؛ archive سراسری | LST-06، ORG-03، AC-11 |
| GET | `/api/v1/messages/search` | `MessagesController` → `MessagingSafetyService.SearchMessagesAsync` | current Participant/Scope در query | LST-05، DM-10، ORG-05/06/07 |
| POST/DELETE/GET | `/api/v1/messaging/blocks[...]` | `MessagingSafetyController` → Block/Unblock/List | حساب خود کاربر | CNT-19/20/22/23، AC-03 |
| POST | `/api/v1/conversations/{id}/messages/{messageId}/reports` | `MessageReportsController` → `ReportMessageAsync` | current Participant + message visible | CNT-24 |
| GET | `/api/v1/messaging/reports/{reportId}` | `MessagingSafetyController` → `GetMyReportAsync` | فقط reporter | CNT-24، P-10 |
| POST/GET/DELETE | `/api/v1/files[...]` | `FilesController` → `StorageService` | uploader یا bound-resource authorization | MSG-10/11/12 |

### SignalR

Hub در `/hubs/messaging` با `[Authorize]` قرار دارد. `MessagingHub.OnConnectedAsync` connection را به group نشست می‌افزاید؛ `SubscribeConversation` با `MessagingService.AuthorizeRealtimeSubscriptionAsync` مجوز جاری را دوباره کنترل می‌کند. event فقط `ConversationChanged` با metadata و cursor است. `MessagingRealtimeAudienceResolver` session فعال و Participant/Membership جاری را resolve می‌کند. payload متن خصوصی ندارد و recovery مرجع از Delta است. backplane چند-instance وجود ندارد و handler فعلی query-string `access_token` را پشتیبانی نمی‌کند.

## 11. Database inventory و migrationها

| Entity/Table | نقش و integrity اصلی | migration |
|---|---|---|
| `messaging.Conversations` | Type/Scope/Organization/direct pair، title/description، policy، message/change sequence و rowversion؛ unique direct pair و creation operation | `AddDirectMessagingFoundation` + `AddGroupAndChannelMessaging` + `AddMessagingRealtimeAndSync` |
| `messaging.ConversationParticipants` | User/Role/lifecycle، `JoinedAtUtc`، Delivered/Read cursors و rowversion؛ active membership/owner filtered unique | همان migrationهای بالا |
| `messaging.ConversationOwnershipTransfers` | transfer pending/accepted/declined با FKهای composite و pending unique | `AddGroupAndChannelMessaging` |
| `messaging.Messages` | sender، `ClientMessageId`، Sequence، Kind/Text/SearchText، Reply/Forward، edit/delete state و rowversion | `AddDirectMessagingFoundation` + `AddAdvancedMessagingFeatures` + `AddMessagingSafetyAndSearch` |
| `messaging.MessageRevisions` | revision اولیه و ویرایش‌های بعدی؛ unique Message/Revision | `AddAdvancedMessagingFeatures` |
| `messaging.MessageAttachments` | اتصال Message به `storage.FileAssets` و Kind | `AddAdvancedMessagingFeatures` |
| `messaging.MessageMentions` | منشن یکتای User در Message | `AddAdvancedMessagingFeatures` |
| `messaging.MessageReactions` | یک reaction فعال برای هر Message/User با lifecycle و rowversion | `AddAdvancedMessagingFeatures` |
| `messaging.ConversationPins` | pin پیام با lifecycle؛ pin گفتگو نیست | `AddAdvancedMessagingFeatures` |
| `messaging.ChangeEvents` | sequence پایدار mutation و audience اختیاری بدون متن پیام | `AddMessagingRealtimeAndSync` |
| `messaging.RealtimeOutbox` | dispatch at-least-once با claim/lease/retry | `AddMessagingRealtimeAndSync` |
| `messaging.PersonalCommunicationGrants` | grant زوج Personal که با تصمیم جدید conflict دارد | `AddDirectMessagingFoundation` |
| `messaging.UserBlocks` | Block جهت‌دار با active filtered unique و rowversion | `AddMessagingSafetyAndSearch` |
| `messaging.AbuseReports` | report idempotent، reason/status، snapshot و rowversion | `AddMessagingSafetyAndSearch` |

هیچ migration جدیدی در STEP 14A ساخته نشد. migrationهای احتمالی آینده فقط پس از طراحی هر مرحله:

- 14B: `UserContacts` و Privacy/Discovery settings و در صورت تصویب، state همگام‌سازی Contact؛
- 14C: Public/Private visibility، Group History policy و visibility boundary، granular permissions، per-user archive/mute/pin/delete state و organization badge projection؛
- 14D: `SafetyIncident`، moderation queue/review/evidence scope؛ retention تا تصمیم حقوقی نباید hard-code شود.

## 12. Security review

| ریسک | وضعیت فعلی | اقدام آینده |
|---|---|---|
| Global account enumeration | endpoint جست‌وجوی حساب/Phone وجود ندارد؛ Direct خطای عمومی می‌دهد | 14B باید response یکسان برای ناشناخته/پنهان و rate limit طراحی کند. |
| Phone leakage | DTO Messaging شماره تلفن ندارد | Privacy visibility و discoverability مستقل لازم است. |
| Cross-org leakage | Scope، Membership و OrganizationId دوباره بررسی می‌شوند؛ Forward cross-scope رد می‌شود | Public discovery سازمانی نباید bypass ایجاد کند. |
| Private chat/Manager overreach | فقط Participant فعال می‌خواند؛ reviewer عمومی وجود ندارد | reviewer صریح، نه `Manager` blanket. |
| Contact-book leakage | Contact book وجود ندارد | ownership فقط User و هیچ API مدیریتی سازمانی در 14B. |
| Deleted/revision leakage | DTO/Search/Media محتوای حذف‌شده و revision قدیمی را برنمی‌گردانند | همه read modelهای جدید نیز باید همین invariant را حفظ کنند. |
| File leakage | bound resolver Participant/Scope جاری و پیام غیرحذف‌شده را می‌سنجد | Group History و Delete For Me باید به resolver اضافه شوند. |
| Search/Shared Media leakage | Participant/Membership جاری لازم است | history visibility و per-user visibility لازم است. |
| Child safety | prevention/escalation معماری وجود ندارد | 14D؛ provider هنوز انتخاب نشده است. |
| AI privacy | هیچ integration از Messenger به AI وجود ندارد | Raw private Student chat خارج از Parent/Teacher/Manager AI context بماند. |

## 13. تحلیل تغییرات API

| API | نوع اثر آینده | دلیل |
|---|---|---|
| `POST /conversations/direct` | `BEHAVIOR CHANGE` | حذف الزام Grant/Adult-only برای Personal و افزودن Privacy/Discovery/Safety بدون تضعیف Organization Direct. |
| `PATCH .../messages/{messageId}` | `BEHAVIOR CHANGE` | enforce کردن window یک‌ساعته و error code پایدار. |
| `DELETE .../messages/{messageId}` | `POTENTIAL BREAKING` | نام/semantics فعلی فقط Delete For Everyone است؛ تفکیک For Me/Everyone ممکن است route/contract بخواهد. |
| Group/Channel creation | `CONTRACT CHANGE` | avatar، Public/Private، history setting و organization badge. |
| member/role management | `POTENTIAL BREAKING` | Role coarse باید به permissionهای granular مهاجرت کند. |
| Block | `NON-BREAKING` در semantics فعلی | Direct-only و حفظ Group اکنون رعایت می‌شود؛ integration با Profile/Contact اضافه می‌شود. |
| Report | `CONTRACT CHANGE` | reviewer routing، evidence scope و moderation lifecycle، بدون افشای private chat. |
| Search/Media/History | `BEHAVIOR CHANGE` | Group History و Delete For Me باید visibility جاری را اعمال کنند. |
| conversation list/archive | `POTENTIAL BREAKING` | archive سراسری فعلی باید از archive شخصی تفکیک شود؛ mute/pin/filter افزوده می‌شوند. |
| File download | `BEHAVIOR CHANGE` | resolver باید visibility شخصی/history policy آینده را نیز اعمال کند. |

## 14. برنامه مراحل بعد

- **14B:** `UserContact` با saved name و uniqueness، safe public profile، privacy مستقل phone visibility/discovery، Username/Phone discovery بدون enumeration، Direct عمومی subject to Privacy/Block/Safety، و foundation اختیاری sync با consent/revocation/removal. Organization Direct تغییر نمی‌کند.
- **14C:** window یک‌ساعته Edit/Delete Everyone، Delete For Me، visibility هماهنگ در History/Search/Media/File/Reply/Pin، Group History، Public/Private، organization badge، permissionهای granular، per-user Archive/Mute/Pin، Saved Messages و reconciliation محدودیت فنی.
- **14D:** report routing و reviewer authorization per-scope، evidence کمینه، `SafetyIncident` و queue priority، pre-delivery text/attachment hook و audit؛ provider و retention حل‌نشده جدا می‌مانند.
- **14E:** اجرای دوباره 127 requirement و acceptanceها، تست کامل SQL Server/authorization/concurrency/idempotency، و بستن gapهای باقی‌مانده بدون افزودن قابلیت خارج از PRD.

## 15. ماتریس جامع Traceability

ستون‌های فشرده `Mig/API/FE/Test` به‌ترتیب نیاز migration، اثر API، اثر قرارداد فرانت و تست آینده را نشان می‌دهند. `Y` یعنی لازم، `N` یعنی لازم نیست و `Maybe` یعنی وابسته به طراحی مرحله هدف. همه تست‌های آینده باید علاوه بر happy path، authorization/privacy و در صورت persistence، SQL Server integration را پوشش دهند.

### Contacts — CNT

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| CNT-01 | فهرست Contact ذخیره‌شده با profile امن | `MISSING` | `E-NONE`؛ هیچ Contact endpoint/entity/table/test وجود ندارد. | دفترچه شخصی و projection امن لازم است؛ نباید برای سازمان قابل مشاهده باشد. | 14B | Y | CONTRACT | Y | Unit+Int privacy |
| CNT-02 | جست‌وجوی Contact با saved/display/username/phone مجاز | `MISSING` | فقط message search در `MessagesController` وجود دارد؛ `E-PROFILE`. | index/query و privacy phone لازم؛ enumeration ریسک اصلی است. | 14B | Y | NEW | Y | Int leakage/filter |
| CNT-03 | مرتب‌سازی الفبایی و recent/frequent | `MISSING` | Contact read model/test وجود ندارد. | تعریف frequency بدون افشای دیگران و ordering پایدار لازم است. | 14B | Maybe | NEW | Y | Unit+Int ordering |
| CNT-04 | نمایش Makan member/invitable فقط با discovery مجاز | `MISSING` | API کشف حساب وجود ندارد؛ Direct فقط `TargetUserId` می‌گیرد. | پاسخ یکسان برای hidden/unknown لازم است. | 14B | Y | NEW | Y | Int enumeration |
| CNT-05 | profile مجاز و شروع Direct از Contact | `PARTIAL` | `POST /conversations/direct` و `E-DIRECT` موجود؛ Contact/profile دیگران وجود ندارد. | flow Privacy → Start/Get Direct ناقص است. | 14B | Y | CONTRACT | Y | Int end-to-end |
| CNT-06 | افزودن Contact با mobile یا Username | `MISSING` | Username unique است، phone در credential است؛ هیچ lookup/add API نداریم. | lookup باید privacy-safe و rate-limited باشد. | 14B | Y | NEW | Y | Int privacy/idempotency |
| CNT-07 | saved name مستقل از profile هدف | `MISSING` | `Person.DisplayName` فقط profile واقعی است؛ UserContact وجود ندارد. | تغییر نام محلی نباید profile را mutate کند. | 14B | Y | NEW | Y | Unit+Int |
| CNT-08 | جلوگیری از Contact تکراری | `MISSING` | unique Contact index وجود ندارد. | unique owner/target و race-safe upsert لازم است. | 14B | Y | NEW | Y | SQL concurrency |
| CNT-09 | ویرایش saved name مستقل | `MISSING` | API/state مربوط وجود ندارد. | rowversion یا قرارداد concurrency لازم است. | 14B | Y | NEW | Y | Int concurrency |
| CNT-10 | حذف Contact بدون حذف Direct history | `MISSING` | Contact lifecycle وجود ندارد؛ Direct مستقل موجود است. | FK/cascade آینده نباید Conversation را حذف کند. | 14B | Y | NEW | Y | SQL+Int |
| CNT-11 | اشتراک Contact card فقط با داده مجاز | `MISSING` | MessageKind برای ContactCard و privacy projection وجود ندارد. | phone/profile leakage محتمل است. | 14B/14C | Maybe | CONTRACT | Y | Int privacy |
| CNT-12 | ساخت Group از Contactهای انتخابی | `PARTIAL` | `CreateManagedConversationCommand.InitialParticipantUserIds` و `E-MANAGE` موجود؛ source Contact/privacy وجود ندارد. | selection فرانت و eligibility/privacy جدید باید هماهنگ شوند. | 14B | Y | BEHAVIOR | Y | Int member privacy |
| CNT-13 | دعوت unknown/non-discoverable بدون افشای وجود حساب | `MISSING` | invitation مربوط به Contacts وجود ندارد. | anti-enumeration و جداسازی SMS invite از account match لازم است. | 14B | Maybe | NEW | Y | Int indistinguishable responses |
| CNT-14 | sync address book فقط با consent | `MISSING` | backend sync endpoint/state وجود ندارد. | consent صریح و عدم upload خام silent لازم است. | 14B | Maybe | NEW | Y | Contract+Int |
| CNT-15 | خاموش‌کردن address-book sync | `MISSING` | setting/state وجود ندارد. | revoke باید sync را متوقف کند بدون شکستن chat. | 14B | Maybe | NEW | Y | Int revoke |
| CNT-16 | حذف داده sync مستقل از Contact دستی | `MISSING` | source/provenance برای Contact وجود ندارد. | provenance و deletion مستقل لازم است. | 14B | Y | NEW | Y | SQL+Int |
| CNT-17 | resolve شماره تکراری/تغییرکرده با account identity | `MISSING` | credential phone به User وصل است، ولی Contact matching وجود ندارد. | phone string نباید identity پایدار باشد. | 14B | Y | NEW | Y | Int remap/privacy |
| CNT-18 | failure sync Messenger را از کار نیندازد | `MISSING` | sync subsystem وجود ندارد. | failure isolation و status لازم است. | 14B | Maybe | NEW | Y | Fault integration |
| CNT-19 | Block از profile/contact/chat | `PARTIAL` | `POST /api/v1/messaging/blocks/{userId}` و `MessagingSafetyService.BlockUserAsync` موجود؛ profile/contact entry contract نداریم. | endpoint reusable است، ولی discovery/profile contract و UX source ناقص است. | 14B | N | NON-BREAKING | Y | Int entry flows |
| CNT-20 | مشاهده/مدیریت Block list | `IMPLEMENTED` | GET/POST/DELETE block؛ `UserBlock` + `UX_UserBlocks_Active_DirectionalPair`؛ تست `BlockIsDirectionalIdempotentAndReauthorizesOfflineDirectSend`. | pagination سقف 500 در آینده بررسی شود؛ داده فقط blocker است. | 14B | N | NON-BREAKING | Y | Preserve Int |
| CNT-21 | Unblock، Contact حذف‌شده را بازنگرداند | `MISSING` | Unblock lifecycle موجود است، ولی Contact state وجود ندارد. | استقلال دو aggregate باید پس از افزودن Contact اثبات شود. | 14B | Y | BEHAVIOR | Y | Int invariant |
| CNT-22 | Block ارسال Direct جدید را منع و history را حفظ کند | `IMPLEMENTED` | `HasEffectiveUserBlockAsync` در Start/Send Direct؛ read به Block وابسته نیست؛ تست‌های Block و history eligibility. | error عمومی باید privacy-safe بماند. | 14B | N | NON-BREAKING | Y | Preserve Int |
| CNT-23 | Block عضویت Group را حذف نکند | `IMPLEMENTED` | مسیر Group/Channel در `EnsureCanSendAdvancedAsync` پیش از Block return می‌کند؛ mutation عضویت ندارد؛ تست `BlockDoesNotChangeEnrollmentOrSharedConversationMembership`. | Group visibility نباید در read model جدید با Block فیلتر شود. | 14B/14E | N | NON-BREAKING | Y | Regression Int |
| CNT-24 | Report حساب/پیام با evidence انتخابی و description | `PARTIAL` | Message report API، `AbuseReport` snapshot/reason/description و idempotency موجود؛ account-only report/reviewer workflow نیست؛ تست report evidence. | evidence کمینه خوب است؛ routing/retention/appeal حل‌نشده. | 14D | Y | CONTRACT | Y | Auth+evidence Int |

### Profile / Discovery — PRF

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| PRF-01 | profile عمومی امن | `PARTIAL` | `SafeMessagingIdentityResult` فقط UserId/Username/DisplayName؛ `/api/v1/me` فقط self؛ Avatar/Bio/privacy نیست. | endpoint دیگران و field-level privacy لازم است. | 14B | Y | NEW | Y | Int projection |
| PRF-02 | Who can see phone | `MISSING` | phone فقط در `UserCredential`/self DTO؛ privacy setting وجود ندارد. | phone leakage پرریسک؛ visibility مستقل لازم است. | 14B | Y | NEW | Y | Matrix Int |
| PRF-03 | Who can find by phone | `MISSING` | lookup phone عمومی وجود ندارد. | از PRF-02 مستقل و anti-enumeration لازم است. | 14B | Y | NEW | Y | Matrix+rate tests |
| PRF-04 | Who can see online/last-seen | `MISSING` | presence/last-seen state یا API وجود ندارد. | اگر افزوده شد best-effort و privacy-aware باشد. | 14B/14E | Maybe | NEW | Y | Privacy tests |
| PRF-05 | Who can see profile image | `MISSING` | Avatar/FileAsset profile وجود ندارد. | فایل avatar هم authorization لازم دارد. | 14B | Y | NEW | Y | File/privacy Int |
| PRF-06 | Who may Direct/add to Group | `MISSING` | Personal eligibility فعلی Grant/Adult است، نه user privacy؛ group target همان policy را reuse می‌کند. | permissionهای مستقل و child policy لازم است. | 14B | Y | BEHAVIOR | Y | Policy+Int |
| PRF-07 | privacy یکسان در Contact/Search/Invite/Card | `MISSING` | هیچ privacy resolver مشترک یا این read modelها وجود ندارد. | پیاده‌سازی پراکنده باعث leakage می‌شود؛ یک Application policy مشترک لازم است. | 14B | Y | CONTRACT | Y | Cross-surface Int |

### Chat List / Inbox — LST

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| LST-01 | list متحد Direct/Group/Channel | `IMPLEMENTED` | `GET /conversations`، `GetMyConversationsAsync` روی همه Typeها؛ active Participant/Scope؛ تست‌های Direct و Group/Channel. | list در workspace جاری است، نه تجمیع هم‌زمان Personal و همه سازمان‌ها. | 14E | N | NON-BREAKING | Y | Regression Int |
| LST-02 | recent sort و pinned conversation | `PARTIAL` | store بر اساس LastMessage/CreatedAt sort می‌کند؛ `ConversationPin` فقط message pin است. | per-user conversation pin/state لازم است. | 14C | Y | CONTRACT | Y | Ordering+pin Int |
| LST-03 | unread count و unread mentions | `PARTIAL` | `UnreadCount` از monotonic read cursor؛ MessageMention ذخیره می‌شود، mention unread projection نیست. | mention count/query بدون leakage لازم است. | 14C | Maybe | CONTRACT | Y | Int multi-device |
| LST-04 | filter All/Direct/Group/Channel/Unread | `MISSING` | GET list query parameter ندارد. | server-side filter/page contract لازم است. | 14C | N | CONTRACT | Y | Query Int |
| LST-05 | جست‌وجوی نام گفتگو و محتوای مجاز | `PARTIAL` | `/messages/search` متن/filename مجاز را می‌جوید؛ title/name search ندارد. | search نام و visibility آینده لازم است. | 14C | Maybe | CONTRACT | Y | Int auth/filter |
| LST-06 | archive/unarchive بدون حذف history | `CONFLICTING` | `ArchiveAsync`، `Conversation.Archive` و `Status=Archived` کل Conversation را سراسری می‌کنند؛ unarchive نیست؛ تست preservation history. | تصمیم محصول archive شخصیِ presentation است؛ endpoint فعلی semantics متفاوت دارد. | 14C | Y | POTENTIAL BREAKING | Y | Migration+Int |
| LST-07 | mute/unmute Conversation | `MISSING` | `E-NONE`؛ Message preference کلی هست ولی state Conversation نیست. | mute نباید persistence پیام را متوقف کند. | 14C | Y | NEW | Y | Unit+Int |
| LST-08 | mark read/unread | `PARTIAL` | `POST .../read` و monotonic cursor موجود؛ mark unread وجود ندارد. | mark unread state باید receipt واقعی را عقب نبرد. | 14C | Maybe | CONTRACT | Y | Cursor semantics |
| LST-09 | حذف از list شخصی با اثر روشن | `MISSING` | visibility/list removal شخصی وجود ندارد. | نباید copy طرف مقابل یا history رسمی را حذف کند. | 14C | Y | NEW | Y | Int isolation |
| LST-10 | New Direct/Group/Channel | `IMPLEMENTED` | سه POST creation در `ConversationsController`؛ migrationهای 7A/7B و endpoint tests. | policy و contract آن‌ها در 14B/14C تغییر می‌کند. | 14B/14C | Maybe | BEHAVIOR | Y | Regression Int |
| LST-11 | custom folders enhancement | `NOT_APPLICABLE` | folder model/API وجود ندارد و PRD آن را future می‌داند. | در 14B–14D ساخته نشود. | 14E | N | NONE | Y | N |
| LST-12 | state قابل فهم/همگام چنددستگاهی | `PARTIAL` | Delta، SignalR، read/delivery participant cursor و tests موجود؛ archive/mute/list pin شخصی وجود ندارد. | stateهای جدید باید ChangeEvent/audience مناسب داشته باشند. | 14C/14E | Y | CONTRACT | Y | Multi-device Int |

### Direct Message — DM

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| DM-01 | شروع از Contact/Profile/Search/link | `PARTIAL` | `POST /conversations/direct` با TargetUserId موجود؛ Contact/Profile/Search-account/link نیست. | Privacy/Discovery gate لازم است؛ شناسه خام مجوز نیست. | 14B | Y | BEHAVIOR | Y | Flow+enumeration Int |
| DM-02 | Direct موازی تکراری نباشد | `IMPLEMENTED` | canonical `DirectUserPair`، filtered unique index و Serializable lock؛ تست concurrent start و distinct Scope. | uniqueness باید بعد از policy جدید حفظ شود. | 14B/14E | N | NON-BREAKING | Y | Regression concurrency |
| DM-03 | text/emoji/sticker/GIF/image/video/file/voice | `PARTIAL` | `MessageKind` Text/Image/Video/Voice/File؛ emoji متن است؛ Sticker/GIF first-class نیست؛ media tests موجود. | MIME/UX contract و safety لازم؛ Product file cap نداریم. | 14C/14E | Maybe | CONTRACT | Y | Media Int |
| DM-04 | Reply | `IMPLEMENTED` | ReplyToMessageId FK همان Conversation، safe deleted placeholder؛ تست `ReplyWithinConversationWorksAndDeletedSourceUsesSafePlaceholder`. | history visibility آینده باید reply source را محدود کند. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| DM-05 | Edit با window یک‌ساعته | `CONFLICTING` | Edit sender+rowversion+revision موجود، ولی هیچ time-window در `Message.EditText/EfMessagingStore.EditMessageAsync` نیست. | پس از یک ساعت باید deny شود؛ server time و boundary دقیق لازم است. | 14C | N | BEHAVIOR | Y | Time boundary Unit+Int |
| DM-06 | Delete For Me و Everyone مستقل؛ Everyone یک ساعت | `CONFLICTING` | DELETE فعلی فقط sender/all و بدون window؛ DeleteForMe entity/API ندارد. | route/contract و per-user visibility لازم؛ audit evidence بماند. | 14C | Y | POTENTIAL BREAKING | Y | Time+visibility Int |
| DM-07 | Forward امن | `IMPLEMENTED` | access هر دو Conversation، source non-deleted، same Scope/Organization و server-copied payload؛ تست unauthorized/concealment و cross-scope rejection. | privacy/history policy جدید نیز باید source را کنترل کند. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| DM-08 | Reaction | `IMPLEMENTED` | reaction add/change/remove، active unique index و test deterministic lifecycle. | visibility جدید و deleted state حفظ شود. | 14E | N | NON-BREAKING | Y | Regression Int |
| DM-09 | Pin مجاز | `IMPLEMENTED` | Direct Participant می‌تواند message pin کند؛ unique active pin و tests. | pin message از conversation pin جدا بماند. | 14C/14E | N | NON-BREAKING | Y | Regression Int |
| DM-10 | search با content type/date | `PARTIAL` | query متن، kind، date، conversation و cursor دارد؛ sender filter ندارد. | DeleteForMe/history/privacy باید به query اضافه شوند. | 14C | Maybe | CONTRACT | Y | Filter+auth Int |
| DM-11 | Sending/Delivered/Read semantics | `PARTIAL` | receipt فقط `Sent`؛ explicit monotonic Delivered/Read و tests؛ Pending/Failed state server-side ندارد. | UI/client state contract و عدم یکی‌گرفتن delivered/read باید روشن بماند. | 14C/14E | Maybe | CONTRACT | Y | Multi-device Int |
| DM-12 | Shared Media | `PARTIAL` | `/conversations/{id}/media` برای attachmentهای non-deleted و authorized؛ link discovery نیست. | history/DeleteForMe و link extraction gap دارند. | 14C | Maybe | CONTRACT | Y | Auth/visibility Int |
| DM-13 | clear history شخصی بدون حذف copy طرف مقابل | `MISSING` | per-user message/conversation visibility وجود ندارد. | عملیات نباید row Message مشترک را پاک کند. | 14C | Y | NEW | Y | Isolation Int |
| DM-14 | mute/background؛ auto-delete/scheduled future | `PARTIAL` | realtime+delta recovery موجود؛ mute نیست؛ auto-delete/scheduled عمداً future. | Conversation mute و notification integration لازم است. | 14C/14E | Y | CONTRACT | Y | Notification+sync Int |

### Group — GRP

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| GRP-01 | ساخت Group با title/avatar/description و حداقل عضو | `PARTIAL` | create group با title/description/initial IDs و idempotency موجود؛ avatar/minimum participant rule نیست. | avatar authorization و تصمیم حداقل لازم است؛ گروه صفر عضو اولیه اکنون مجاز است. | 14C | Maybe | CONTRACT | Y | Validation+Int |
| GRP-02 | ownership و transfer تأییدشده | `IMPLEMENTED` | start/accept/decline، pending unique، owner unique و transaction؛ تست‌های accepted/declined/idempotent. | permission granular آینده نباید invariant یک Owner را بشکند. | 14E | N | NON-BREAKING | Y | Regression concurrency |
| GRP-03 | add/remove member با membership/privacy | `PARTIAL` | Owner/Admin matrix و target eligibility موجود؛ Personal target به Grant/Adult قدیمی وابسته و add-to-group privacy نیست. | PRF-06 و child policy باید اعمال شوند. | 14B/14C | Maybe | BEHAVIOR | Y | Privacy/auth Int |
| GRP-04 | invite link قابل revoke | `MISSING` | InviteLink entity/API/table/test وجود ندارد. | token hash، revoke و anti-bypass لازم؛ expiry/cap می‌تواند بعدی باشد. | 14C | Y | NEW | Y | Security Int |
| GRP-05 | Public/Private Group | `MISSING` | Scope فقط Personal/Organization است؛ discoverability field/API وجود ندارد. | Personal با Public یکی نیست؛ Organization boundary باید بر Public مقدم باشد. | 14C | Y | CONTRACT | Y | Discovery/auth Int |
| GRP-06 | permission granular Admin | `CONFLICTING` | فقط Owner/Admin/Member و `ConversationManagementAuthorizationPolicy` coarse؛ Admin مجموعه ثابت اختیارات دارد. | هر action باید permission مستقل داشته باشد؛ title Admin کافی نیست. | 14C | Y | POTENTIAL BREAKING | Y | Permission matrix Int |
| GRP-07 | capability مستقل member برای text/media/link/poll/invite | `MISSING` | capability model/settings وجود ندارد؛ Group member همواره publish می‌کند. | deny باید server-side و per-capability باشد. | 14C | Y | CONTRACT | Y | Policy+Int |
| GRP-08 | mute/restrict/ban عضو | `MISSING` | فقط remove/leave lifecycle؛ restriction/mute/ban entity/API نیست. | moderation action و expiry/audit نیاز دارد. | 14C/14D | Y | NEW | Y | Auth+lifecycle Int |
| GRP-09 | member/admin list با search | `PARTIAL` | GET details همه Participantهای فعال را برمی‌گرداند؛ search/page/filter جدا ندارد. | list بزرگ و privacy-safe pagination لازم است. | 14C | Maybe | CONTRACT | Y | Query Int |
| GRP-10 | Reply/Reaction/Mention/Pin/Search | `IMPLEMENTED` | endpointهای E-ADV/E-SAFETY برای Participant گروه؛ mention عضو فعال؛ pin Owner/Admin؛ تست‌های interaction/search. | history و granular permission باید اضافه شوند. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| GRP-11 | leave؛ Owner اول ownership را حل کند | `IMPLEMENTED` | `CanLeave` Owner را رد می‌کند و transfer flow موجود؛ تست `OwnerCannotLeave...`. | رفتار Public/invite آینده invariant را حفظ کند. | 14E | N | NON-BREAKING | Y | Regression Int |
| GRP-12 | history عضو جدید تابع setting | `MISSING` | `JoinedAtUtc` موجود ولی history/search/media بدون join boundary؛ setting نیست. | عضو جدید اکنون همه history را می‌بیند؛ leakage جدی. | 14C | Y | BEHAVIOR | Y | SQL visibility Int |
| GRP-13 | Poll/Topics/Slow Mode enhancement | `NOT_APPLICABLE` | model/API ندارند و PRD آن‌ها را advanced می‌داند. | در 14B–14D ساخته نشود. | 14E | N | NONE | Y | N |
| GRP-14 | audit اقدام moderator/admin | `PARTIAL` | Participant lifecycle، transfer، rowversion و `MessagingChangeEvent` actor/resource موجود؛ permission-specific audit/reviewer action نیست. | audit immutable و action type granular لازم است. | 14C/14D | Maybe | CONTRACT | Y | Audit Int |
| GRP-15 | Organization Group بدون bypass خارجی | `IMPLEMENTED` | `CanAccessScopeAsync` و target eligibility Membership جاری را می‌سنجند؛ nonparticipant direct ID concealed؛ تست organization creation/system-managed. | Public discovery آینده نباید این مرز را دور بزند. | 14C/14E | N | NON-BREAKING | Y | Cross-org regression |

### Channel — CHN

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| CHN-01 | ساخت Channel title/avatar/description، Public/Private | `PARTIAL` | create channel title/description و Personal/Organization موجود؛ avatar و visibility نیست. | Scope و discoverability باید جدا مدل شوند. | 14C | Y | CONTRACT | Y | Validation/discovery Int |
| CHN-02 | Owner/publisher publish؛ subscriber read | `IMPLEMENTED` | `CanPublish` برای Channel فقط Owner/Admin؛ Member history را می‌خواند؛ تست `ChannelMemberCannotPublishButAdminCan`. | Admin granular آینده جای coarse Admin را می‌گیرد. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| CHN-03 | permission granular publish/edit/delete/invite/manage admins | `CONFLICTING` | فقط Role coarse؛ Admin publish و member-management محدود دارد، permission flags ندارد. | اصل least privilege رعایت کامل نمی‌شود. | 14C | Y | POTENTIAL BREAKING | Y | Permission matrix |
| CHN-04 | publish text/image/video/file/album | `PARTIAL` | انواع Text/Image/Video/File/Voice موجود؛ album/grouped media نیست. | upload safety و album contract لازم است. | 14C/14E | Maybe | CONTRACT | Y | Media Int |
| CHN-05 | edit/delete/pin post طبق permission | `PARTIAL` | edit/delete فقط sender؛ pin Owner/Admin؛ حذف پیام دیگران یا permission مستقل نیست. | moderator/publisher permission و one-hour sender rule باید تفکیک شوند. | 14C | Y | BEHAVIOR | Y | Auth/time Int |
| CHN-06 | view-count semantics | `UNRESOLVED_PRODUCT_POLICY` | view-count state/API/test وجود ندارد و PRD تصمیم را باز گذاشته است. | تعریف unique viewer/device، privacy و consistency لازم است؛ فعلاً پیاده‌سازی نشود. | 14E | Maybe | TBD | Y | بعد از freeze |
| CHN-07 | subscribe/unsubscribe/invite | `PARTIAL` | add/remove/leave participant موجود؛ public subscribe و invite management نیست. | Public/Private و organization gate لازم است. | 14C | Y | CONTRACT | Y | Auth/lifecycle Int |
| CHN-08 | reaction در صورت enabled | `IMPLEMENTED` | Reaction برای Participant فعال Channel موجود و deterministic است؛ setting disable وجود ندارد، پس قابلیت همواره enabled است. | اگر toggle افزوده شد server-side enforce شود. | 14E | Maybe | NON-BREAKING | Y | Regression Int |
| CHN-09 | linked discussion group enhancement | `NOT_APPLICABLE` | entity/API وجود ندارد و future است. | خارج از 14B–14D. | 14E | N | NONE | Y | N |
| CHN-10 | scheduled publishing/statistics enhancement | `NOT_APPLICABLE` | entity/API وجود ندارد و future است. | خارج از 14B–14D. | 14E | N | NONE | Y | N |
| CHN-11 | Organization Channel با boundary سازمان | `IMPLEMENTED` | Scope/OrganizationId، Membership جاری در read/manage/realtime/file؛ تست cross-org creation/access. | discoverability آینده boundary را حفظ کند. | 14C/14E | N | NON-BREAKING | Y | Cross-org regression |

### Message / Media — MSG

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| MSG-01 | sender/conversation/content type/creation time | `IMPLEMENTED` | `Message` دارای SenderUserId/ParticipantId، ConversationId، Kind و server `SentAtUtc`؛ migration و receipt tests. | client time authoritative نیست و باید همین‌طور بماند. | 14E | N | NON-BREAKING | Y | Regression |
| MSG-02 | متن و formatting پشتیبانی‌شده | `PARTIAL` | Text تا 4000 و exact preservation؛ formatting schema/sanitization contract وجود ندارد. | formatting آینده باید XSS-safe و versioned باشد. | 14C/14E | Maybe | CONTRACT | Y | Validation/security |
| MSG-03 | retry/double send بدون duplicate | `IMPLEMENTED` | `ClientMessageId` lock+unique+payload match؛ تست idempotent و offline retry one effect. | retry بعد از policy change دوباره authorize می‌شود و باید حفظ شود. | 14E | N | NON-BREAKING | Y | Regression concurrency |
| MSG-04 | ordering پایدار قابل recovery | `IMPLEMENTED` | `NextMessageSequence`، unique index، stable history cursor؛ Delta sequence مستقل؛ concurrency/pagination tests. | Group History باید sequence boundary را هماهنگ کند. | 14C/14E | N | BEHAVIOR | Y | Regression |
| MSG-05 | Reply به deleted/unavailable placeholder امن | `IMPLEMENTED` | `MapReply` Kind/Text را برای deleted null می‌کند؛ integration test صریح موجود. | unavailable-by-history نیز باید همین رفتار را داشته باشد. | 14C/14E | N | BEHAVIOR | Y | Visibility regression |
| MSG-06 | edited marker + یک ساعت + history داخلی | `CONFLICTING` | IsEdited/EditedAt/revision داخلی و عدم exposure موجود؛ window یک‌ساعته نیست. | پس از deadline edit رد شود؛ revision public نشود. | 14C | N | BEHAVIOR | Y | Clock boundary Int |
| MSG-07 | Delete For Me/Everyone مستقل | `CONFLICTING` | tombstone Everyone موجود و امن ولی بدون window؛ For Me وجود ندارد. | per-user visibility + one-hour Everyone لازم است. | 14C | Y | POTENTIAL BREAKING | Y | Multi-user Int |
| MSG-08 | Forward مجوز source را دور نزند | `IMPLEMENTED` | source/destination active access، nondeleted و same scope/org؛ unauthorized source concealed؛ tests موجود. | history/DeleteForMe visibility به source check افزوده شود. | 14C/14E | N | BEHAVIOR | Y | Regression |
| MSG-09 | Reaction deterministic per-user | `IMPLEMENTED` | filtered unique active Message/User، replacement/removal lifecycle؛ unit/integration tests. | concurrent toggle regression حفظ شود. | 14E | N | NON-BREAKING | Y | Concurrency Int |
| MSG-10 | فایل: size/type/access/safety؛ بدون product cap | `PARTIAL` | technical `MaxFileSizeBytes` 10MiB قابل تنظیم، MIME allowlist و bound access؛ safety scan/hook نیست. | 10MiB technical است نه Product rule؛ content safety در 14D. | 14C/14D | Maybe | BEHAVIOR | Y | Upload+safety fault |
| MSG-11 | retry امن upload؛ incomplete ارسال‌شده نباشد | `PARTIAL` | Pending→Ready/Rejected و Send فقط FileAsset Ready؛ cleanup/retry idempotency کامل upload نیست. | operation id/reconciliation و status UX لازم است. | 14C | Maybe | CONTRACT | Y | Fault/retry Int |
| MSG-12 | preview/download فقط مجاز فعلی | `IMPLEMENTED` | `StorageService.GetAuthorizedAsync` + `EfFileAssetBoundAccessResolver`، پیام nondeleted و Participant/Scope جاری؛ file authorization tests. | Group History/DeleteForMe باید resolver را گسترش دهند. | 14C/14E | N | BEHAVIOR | Y | Auth regression |
| MSG-13 | file/link در Shared Media | `PARTIAL` | Media endpoint attachmentها را می‌دهد؛ link extraction/index وجود ندارد. | visibility یکسان و safe link metadata لازم است. | 14C | Maybe | CONTRACT | Y | Search/media Int |
| MSG-14 | consistency متن/attachment/display بین دستگاه‌ها | `PARTIAL` | ChangeEvent/Delta/SignalR و tombstone metadata موجود؛ client Pending/Failed و stateهای per-user آینده نیستند. | stateهای جدید باید delta/version contract داشته باشند. | 14C/14E | Y | CONTRACT | Y | Multi-device Int |

### Messaging Notifications — NTF

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| NTF-01 | notification Direct/Group/Channel با preference | `MISSING` | `E-NOTIFY`؛ Messages preference هست، ولی Message NotificationType/producer نیست. | recipient resolution، mute و privacy لازم است؛ realtime جای notification نیست. | 14C/14E | Maybe | NEW | Y | Outbox+preference Int |
| NTF-02 | notification Mention/Reply | `MISSING` | Mention/Reply ذخیره می‌شوند ولی notification intent تولید نمی‌کنند. | dedupe و privacy preview لازم است. | 14C/14E | Maybe | NEW | Y | Outbox Int |
| NTF-03 | mute موقت/دائم | `MISSING` | Conversation mute entity/API نیست. | mute فقط delivery channel را خاموش کند نه message persistence. | 14C | Y | NEW | Y | Clock+Int |
| NTF-04 | privacy preview | `MISSING` | Messaging notification payload وجود ندارد. | default امن و setting مستقل لازم است. | 14C/14D | Maybe | CONTRACT | Y | Privacy tests |
| NTF-05 | unread count بین دستگاه‌ها | `IMPLEMENTED` | participant-level monotonic read cursor، unread query، audience-specific change و tests concurrent/multi-device. | cursor per-device نیست؛ max user state آگاهانه است. | 14E | N | NON-BREAKING | Y | Regression Int |
| NTF-06 | Online/LastSeen/Typing best-effort | `MISSING` | presence/typing API/event/state وجود ندارد. | اگر افزوده شد نباید حقیقت قطعی یا durable privacy leak تلقی شود. | 14E | Maybe | NEW | Y | Ephemeral/privacy |
| NTF-07 | جلوگیری عملی از notification duplicate | `MISSING` | realtime outbox at-least-once است و client باید dedupe کند؛ durable Message notification اصلاً تولید نمی‌شود. | SourceEvent idempotency و multi-device fanout لازم است. | 14C/14E | Maybe | NEW | Y | Retry Int |
| NTF-08 | خاموشی notification مانع persistence نشود | `MISSING` | چون Messaging به Notification متصل نیست، رفتار preference end-to-end وجود ندارد. | integration آینده باید پس از commit و خارج transaction باشد. | 14C/14E | Maybe | NEW | Y | Failure isolation Int |

### Saved / Archive / Search — ORG

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| ORG-01 | Saved Messages شخصی | `MISSING` | Saved conversation/item entity/API/test وجود ندارد. | باید فقط owner ببیند و text/file/forward پشتیبانی شود. | 14C | Y | NEW | Y | Auth+Int |
| ORG-02 | حذف Saved item منبع را حذف نکند | `MISSING` | Saved state وجود ندارد. | copy/reference semantics باید source را mutate نکند. | 14C | Y | NEW | Y | Isolation Int |
| ORG-03 | Archive فقط presentation | `CONFLICTING` | archive فعلی `Conversation.Status` را سراسری تغییر می‌دهد و list/send همه را متاثر می‌کند. | per-user archive و unarchive migration لازم است. | 14C | Y | POTENTIAL BREAKING | Y | Multi-user Int |
| ORG-04 | pin conversation و pin message جدا | `PARTIAL` | `ConversationPin` در واقع pin پیام است؛ pin list/conversation وجود ندارد. | naming/contract آینده باید دو مفهوم را جدا کند. | 14C | Y | CONTRACT | Y | Int distinction |
| ORG-05 | Search فقط مجاز فعلی | `IMPLEMENTED` | `EfMessagingStore.SearchMessagesAsync` active Participant/Scope/Membership و nondeleted را در query enforce می‌کند؛ search authorization tests. | Group History/DeleteForMe به predicate افزوده شود. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| ORG-06 | filter type/date/sender/conversation | `PARTIAL` | type/date/conversation/query موجود؛ sender filter نیست. | pagination/filter allowlist و visibility لازم است. | 14C | N | CONTRACT | Y | Filter Int |
| ORG-07 | revocation فوراً Search/Media را محدود کند | `IMPLEMENTED` | active Participant و Membership در Search/Media/File لازم؛ تست `RevokedParticipantCannotResearchOrOpenCachedSearchResult`. | policy history پس از leave تصمیم visibility را تکمیل می‌کند. | 14C/14E | N | BEHAVIOR | Y | Regression Int |

### Acceptance Criteria — AC

| ID | Requirement | Status | شواهد فعلی API/Code/DB/Test | Gap و اثر امنیتی | Step | Mig | API | FE | Test |
|---|---|---|---|---|---|---|---|---|---|
| AC-01 | ذخیره Contact تکراری row دوم نسازد | `MISSING` | Contact table/API/index ندارد. | unique + concurrent upsert در 14B. | 14B | Y | NEW | Y | SQL concurrency |
| AC-02 | حذف Contact history را حذف نکند | `MISSING` | Contact lifecycle ندارد. | FK/cascade isolation لازم است. | 14B | Y | NEW | Y | SQL+Int |
| AC-03 | blocked user Direct جدید تحویل ندهد | `IMPLEMENTED` | symmetric effective Block در Start/Send، generic deny؛ integration test offline reauthorization. | notification future نیز نباید قبل از deny ساخته شود. | 14B/14E | N | NON-BREAKING | Y | Regression Int |
| AC-04 | phone محدود از search/card نشت نکند | `MISSING` | phone search/card/privacy وجود ندارد؛ Messaging DTO فعلی phone ندارد. | معیار پس از افزودن surfaceها باید end-to-end اثبات شود. | 14B | Y | NEW | Y | Leakage Int |
| AC-05 | unauthorized user private Group را مستقیم/search نبیند | `PARTIAL` | nonparticipant در details/history/search concealed؛ Public/Private concept وجود ندارد. | visibility و discovery private هنوز مدل نشده است. | 14C | Y | BEHAVIOR | Y | Discovery auth Int |
| AC-06 | Channel subscriber publish نکند | `IMPLEMENTED` | `CanPublish` فقط Owner/Admin؛ integration test صریح. | granular permission آینده regression ندهد. | 14C/14E | N | BEHAVIOR | Y | Regression Int |
| AC-07 | delete permission به‌تنهایی appoint-admin ندهد | `PARTIAL` | Admin فعلی نمی‌تواند Admin appoint کند؛ فقط Owner role change می‌دهد، اما delete-others permission مستقل وجود ندارد. | معیار کامل پس از granular permissions قابل اثبات است. | 14C | Y | CONTRACT | Y | Permission matrix |
| AC-08 | Send identity واحد حداکثر یک Message | `IMPLEMENTED` | unique index + retry payload check؛ idempotency integration tests. | حفظ زیر concurrency. | 14E | N | NON-BREAKING | Y | Regression concurrency |
| AC-09 | retry/reconnect duplicate نسازد | `IMPLEMENTED` | offline retry یک Message/Change effect؛ Delta pagination و SignalR recovery tests. | realtime at-least-once است و client event را dedupe می‌کند. | 14E | N | NON-BREAKING | Y | Regression Int |
| AC-10 | Read state بین دستگاه‌ها سازگار | `IMPLEMENTED` | monotonic participant cursor، audience user change، concurrent max test. | per-device telemetry عمداً نیست. | 14E | N | NON-BREAKING | Y | Regression Int |
| AC-11 | Archive history را نابود نکند | `IMPLEMENTED` | archive row/history را حذف نمی‌کند؛ تست `ArchiveRejectsNewMessagesButPreservesCommittedHistory`. | با وجود تحقق این معیار محدود، semantics presentation در LST-06/ORG-03 conflict دارد. | 14C/14E | N | BEHAVIOR | Y | Preserve history |
| AC-12 | Organization A به Group B با ID دسترسی نگیرد | `IMPLEMENTED` | workspace/Membership/Organization checks در read/manage/realtime/file؛ cross-org tests. | Public discovery آینده نباید bypass کند. | 14C/14E | N | NON-BREAKING | Y | Cross-org regression |
| AC-13 | Parent/Manager بدون authority Contacts/chat Student را نبیند | `PARTIAL` | private chat فقط active Participant؛ test report evidence برای outsider Manager؛ Contacts وجود ندارد. | Contacts ownership و reviewer permissions باید در 14B/14D اثبات شوند. | 14B/14D | Y | BEHAVIOR | Y | Cross-role privacy |
| AC-14 | revoke contact sync conversation را نشکند | `MISSING` | contact sync/state وجود ندارد. | sync source باید از Conversation مستقل باشد. | 14B | Maybe | NEW | Y | Revoke Int |
| AC-15 | Search/File/Media فقط مجاز فعلی | `IMPLEMENTED` | active authorization در Search/Media/`EfFileAssetBoundAccessResolver` و revoke/cache tests. | visibilityهای 14C باید به هر سه مسیر تزریق شوند. | 14C/14E | N | BEHAVIOR | Y | Regression Int |

## 16. Product Policyهای حل‌نشده

موارد زیر با وضعیت `UNRESOLVED_PRODUCT_POLICY` یا مرز صریح تصمیم ثبت می‌شوند و پیاده‌سازی آینده حق حدس‌زدن ندارد:

- semantics و privacy دقیق Channel view count؛
- مدت retention برای AbuseReport evidence، revision، tombstone، ChangeEvent و Outbox؛
- حذف/نگه‌داری قانونی داده حساب؛
- reviewer دقیق همه Personal Direct reportها و appeal lifecycle؛
- moderation provider/model/vendor و SLA escalation؛
- policy حساب کودک برای ساخت Personal Group/Channel و افزودن عضو؛
- semantics/مهلت `Delete For Me`؛
- نام/schema نهایی Group History و سقف‌های فنی محیطی.

## 17. Validation STEP 14A

Validation پس از تکمیل اسناد روی `MakanApp.sln` اجرا شد:

| عملیات | نتیجه واقعی |
|---|---|
| `dotnet restore MakanApp.sln` | موفق؛ همه projectها up-to-date بودند. |
| `dotnet build MakanApp.sln --configuration Debug --nologo` | موفق؛ `0` error و `0` warning. |
| `dotnet test MakanApp.sln --configuration Debug --nologo --no-build` | موفق؛ Unit: `278/278`، Integration: `462/462`، Architecture: `14/14`، مجموع: `754`، failed: `0`، skipped: `0`. |
| Migration | هیچ migration ساخته یا اجرا نشد و model/schema تغییر نکرد. |

در STEP 14A هیچ فایل product code یا test تغییر نکرد. این نتیجه baseline موجود را تأیید می‌کند و ادعای پیاده‌سازی gapهای Messenger v2 نیست.

## 18. Addendum پس از STEP 14B

این بخش تاریخچه ممیزی STEP 14A را بازنویسی نمی‌کند؛ فقط requirementهایی را که با
پیاده‌سازی `Contacts + Profile Privacy + Discovery + Global Personal Direct` تغییر
وضعیت داده‌اند، به‌صورت versioned ثبت می‌کند. جزئیات قرارداد در
`STEP_14B_CONTACTS_PRIVACY_DISCOVERY.md` آمده است.

| ID | وضعیت پس از 14B | شواهد و مرز باقیمانده |
|---|---|---|
| CNT-01 | `IMPLEMENTED` | `UserContact`، unique index مالک/مخاطب و save idempotent |
| CNT-02 | `IMPLEMENTED` | جست‌وجوی SavedName، Username و DisplayName؛ Phone فقط exact resolve امن و مستقل است |
| CNT-03 | `PARTIAL` | Recent از فعالیت Personal Direct پیاده شد؛ Frequent ranking عمداً deferred است |
| CNT-04 | `IMPLEMENTED` | loading/empty/pagination contract و پاسخ امن not-discoverable مستند و تست شدند |
| CNT-05 | `IMPLEMENTED` | ذخیره با UserId مقصد و SavedName خصوصی، بدون نیاز به شماره |
| CNT-06 | `IMPLEMENTED` | نتیجه discovery قابل ذخیره با ContactUserId است |
| CNT-07 تا CNT-10 | `IMPLEMENTED` | SavedName خصوصی، Favorite/unfavorite، edit و delete مستقل از history |
| CNT-11 | `PARTIAL` | safe profile projection وجود دارد؛ Contact card message خارج از 14B است |
| CNT-12 | `PARTIAL` | Contacts و GroupInvites privacy backend موجود است؛ UX انتخاب عضو و 14C باقی است |
| CNT-13 | `PARTIAL` | مقصد غیرمجاز/ناموجود امن است؛ SMS invite پیاده نشده است |
| CNT-14 تا CNT-16 | `MISSING` | Contact sync و revoke عمداً deferred هستند |
| CNT-17 | `IMPLEMENTED` | هویت Contact برابر UserId است و Phone فقط credential کشف است |
| CNT-18 | `MISSING` | merge/dedup مربوط به sync هنوز مدل نشده است |
| CNT-19 | `IMPLEMENTED` | profile/discovery/contact entry به Direct با Privacy و Block متصل شد |
| CNT-20 | `IMPLEMENTED` | Block مستقل باقی مانده و Contact آن را دور نمی‌زند |
| CNT-21 تا CNT-23 | `IMPLEMENTED` | مالکیت خصوصی، one-way Contacts semantics و pagination/limits enforce شدند |
| PRF-01 | `PARTIAL` | پروفایل امن Username/DisplayName موجود است؛ Avatar و Bio پیاده نشده‌اند |
| PRF-02، PRF-03، PRF-06، PRF-07 | `IMPLEMENTED` | Privacyهای Direct/Phone/Group Invite، defaults و enforcement فعلی موجودند؛ child safety همچنان release gate است |
| PRF-04، PRF-05 | `MISSING` | Avatar/Bio و Presence خارج از 14B هستند |
| DM-01 | `IMPLEMENTED` | Personal Direct سراسری، بدون Organization/Adult/grant و با Privacy/Block برقرار است |
| DM-02 | `IMPLEMENTED` | Organization Direct با authorization قبلی بدون تغییر باقی مانده است |
| GRP-03 | `PARTIAL` | Privacy دعوت Personal Group اعمال شد؛ child/group policyهای حل‌نشده باقی‌اند |
| AC-01 تا AC-04 | `IMPLEMENTED` | uniqueness همزمان، استقلال delete/history، Block و جلوگیری از phone leakage تست شدند |
| AC-13 | `PARTIAL` | Contact privacy/ownership بسته شد؛ reviewer/moderation authority در 14D باقی است |
| AC-14 | `MISSING` | Contact sync پیاده نشده و بنابراین revoke آن نیز deferred است |

Migration افزوده‌شده `20261012203821_AddMessengerContactsPrivacyDiscovery` فقط schema
دو جدول `messaging.UserContacts` و `messaging.MessagingPrivacySettings` را تعریف می‌کند؛
در STEP 14B روی دیتابیس production یا developer اعمال نشده است.

## 19. Addendum پس از STEP 14C

این addendum نتیجه ممیزی تاریخی STEP 14A را بازنویسی نمی‌کند؛ وضعیت requirementهای سخت‌سازی‌شده را
پس از پیاده‌سازی `Messaging Behavior Hardening & Conversation State` ثبت می‌کند. قرارداد کامل در
`STEP_14C_MESSAGING_BEHAVIOR_HARDENING.md` است.

| حوزه | وضعیت پس از 14C | شاهد و مرز باقی‌مانده |
|---|---|---|
| Edit/Delete window | `IMPLEMENTED` | مرز دقیق یک ساعت با TimeProvider؛ moderator edit وجود ندارد |
| Delete For Me | `IMPLEMENTED` | visibility یکتای Message/User در History/Search/Media/File/Reply/Forward/Delta |
| Group History | `IMPLEMENTED` | FullHistory/FromJoin و boundary sequence پایدار؛ Channel policy مستقل deferred |
| Public/Private | `IMPLEMENTED` | discovery/join bounded و organization-scoped؛ system-managed غیرقابل join |
| Admin permissions | `IMPLEMENTED` | flags صریح، جلوگیری از privilege escalation و rowversion |
| Member capabilities | `IMPLEMENTED` | SendMessages/SendMedia/InviteMembers؛ Poll/Link اختراع نشده‌اند |
| Conversation state | `IMPLEMENTED` | archive/mute/pin/manual-unread per-user و sync با audience خصوصی |
| Chat list | `IMPLEMENTED` | فیلتر، pagination، pinned-first/activity order و badge امن سازمان |
| Saved Messages | `DEFERRED` | نیازمند ConversationType/uniqueness و audit سراسری switchها؛ منتقل به 14E |

Migration این مرحله `20261012212650_HardenMessengerConversationBehavior` است. archiveهای global
قدیمی به state شخصی حدس‌زده و تبدیل نشده‌اند؛ `Conversation.Status.Archived` برای lifecycle
legacy/internal باقی مانده، ولی endpoint عمومی archive از `ConversationUserState` استفاده می‌کند.

## 20. Addendum پس از STEP 14D

این addendum ممیزی تاریخی 14A را بازنویسی نمی‌کند. پس از پیاده‌سازی
`Moderation + Child Safety Foundation` وضعیت gapهای مربوط چنین است:

| حوزه | وضعیت پس از 14D | شاهد و مرز باقی‌مانده |
|---|---|---|
| report routing | `IMPLEMENTED` | ConversationModerators برای Group/Channel کاربرمدیریت‌شده؛ PlatformSafety برای Direct/SystemManaged/ChildSafetyConcern |
| reviewer authorization | `IMPLEMENTED` | Owner یا Admin دارای `ReviewReports`؛ Manager سازمان به‌تنهایی مجاز نیست |
| minimum evidence | `IMPLEMENTED` | snapshot همان پیام و پیوست‌ها؛ بدون history همسایه یا داده هویتی حساس |
| moderation lifecycle/audit | `IMPLEMENTED` | case دارای rowversion و actionهای append-only؛ status reporter همگام است |
| child-safety release gate | `IMPLEMENTED` | Minor/Unknown با SQL EXISTS؛ Allowed commit و Block/Review بدون Message |
| provider integration | `PARTIAL` | port، Unavailable و development/test adapter موجود؛ vendor واقعی انتخاب نشده است |
| Platform Safety operations | `DEFERRED` | داده و queue آماده است؛ role/API/Backoffice در STEP 15 |
| retention/appeal/SLA | `UNRESOLVED_PRODUCT_POLICY` | هیچ مدت یا workflow حدسی hard-code نشده است |

Migration این مرحله `20261012222340_AddMessagingModerationAndChildSafety` است و قرارداد کامل در
`STEP_14D_MODERATION_CHILD_SAFETY.md` ثبت شده است.

## 21. Addendum پس از STEP 14E

این addendum ماتریس تاریخی STEP 14A را بازنویسی نمی‌کند. ممیزی نهایی هر ۱۲۷
requirement، وضعیت نهایی، شواهد API/Code/DB/Test و release verdict در
[`STEP_14E_MESSENGER_FINAL_ACCEPTANCE.md`](STEP_14E_MESSENGER_FINAL_ACCEPTANCE.md)
ثبت شده است.

خلاصه تغییر وضعیت پس از STEP 14E:

| حوزه | وضعیت | شاهد اصلی |
|---|---|---|
| Saved Messages | `IMPLEMENTED` | Conversation شخصی یکتا و lazy، owner-only، text/file/forward |
| Clear History | `IMPLEMENTED` | watermark خصوصی و monotonic در تمام read surfaceهای پیام |
| Messaging Notification | `IMPLEMENTED` | Outbox durable، recipient فعلی، dedupe، preview امن و mute خارجی |
| Unread Mention | `IMPLEMENTED` | projection server-side و هماهنگ با read/clear-history |
| Search completion | `IMPLEMENTED` | sender filter و جست‌وجوی نام Conversation با authorization جاری |
| Contact Card | `IMPLEMENTED` | snapshot typed بدون Phone و بدون side effect روی Contact/Direct |
| Contact Sync | `DEFERRED_SECURE_DESIGN` | consent/upload/provenance/revoke هنوز قرارداد مصوب ندارد |
| Avatar و Presence | `DEFERRED_SECURE_DESIGN` | lifecycle فایل و semantics چند-instance/privacy هنوز freeze نشده است |

Migration این مرحله `20261013042337_CompleteMessengerV2` است. این migration ستون‌ها
و constraintهای Saved، Clear History، Contact Card و enumهای Notification را ثبت
می‌کند؛ Contact Sync، Avatar و Presence هیچ schema ساختگی دریافت نکرده‌اند.
