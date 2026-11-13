using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformOperationsStore
{
    public const string SchoolProvisioningMessageType = "school.provisioning-requested.v1";

    public bool Handles(string messageType) => messageType == SchoolProvisioningMessageType;

    // این متد در تراکنش تأیید پرداخت/بازیابی فراخوانی می‌شود؛ کار مرحله‌ای بعد از Commit اجرا خواهد شد.
    public async Task<Guid> PreparePaidOrderAsync(Guid orderId, CancellationToken ct)
    {
        if (dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("ثبت درخواست و Outbox به تراکنش مشترک نیاز دارد.");
        var order = await dbContext.Orders.SingleAsync(o => o.Id == orderId, ct);
        var payment = await dbContext.Payments.SingleOrDefaultAsync(p => p.OrderId == orderId, ct);
        if (order.Status != OrderStatus.Paid || payment is null || !order.SchoolApplicationId.HasValue || !order.HasCommercialTerms)
            throw new InvalidOperationException("فقط پرداخت بانکی تأییدشدهٔ درخواست تأسیس قابل آماده‌سازی است.");
        var app = await dbContext.SchoolApplications.FromSqlInterpolated($"SELECT * FROM [organization].[SchoolApplications] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={order.SchoolApplicationId}").SingleAsync(ct);
        if (app.Status != SchoolApplicationStatus.Approved || app.ApplicantUserId != order.PurchaserUserId)
            throw new InvalidOperationException("مرجع تأییدشدهٔ پرداخت معتبر نیست.");
        var request = await dbContext.PlatformProvisioningRequests.FromSqlInterpolated($"SELECT * FROM [platform].[ProvisioningRequests] WITH (UPDLOCK,HOLDLOCK) WHERE [SchoolApplicationId]={app.Id}").SingleOrDefaultAsync(ct);
        if (request is not null && request.SourceOrderId != orderId) throw new InvalidOperationException("مرجع پرداخت آماده‌سازی قابل جایگزینی نیست.");
        if (!app.FinalInitialManagerUserId.HasValue || !app.InitialManagerConfirmedByUserId.HasValue)
        {
            if (request is null)
            {
                request = PlatformProvisioningRequest.AwaitSchoolManagerPolicy(app.Id, order.Id, app.ApplicantUserId, app.SchoolName!, order.PlanVersionId, SchoolNow());
                dbContext.PlatformProvisioningRequests.Add(request);
            }
            return request.Id;
        }
        if (request is null)
        {
            request = PlatformProvisioningRequest.ForPaidSchool(app.Id, order.Id, payment.Id, app.InitialManagerConfirmedByUserId.Value,
                app.FinalInitialManagerUserId.Value, app.SchoolName!, order.PlanVersionId, SchoolNow());
            dbContext.PlatformProvisioningRequests.Add(request);
            dbContext.AdminAuditLogs.Add(await CreateAuditAsync(null, "school_application.ProvisioningRequested", "PlatformProvisioningRequest",
                request.Id.ToString(), null, request.Reason, null, $"application={app.Id};order={order.Id};payment={payment.Id};planVersion={order.PlanVersionId};manager={request.InitialManagerUserId}", ct));
        }
        else if (!request.InitialManagerUserId.HasValue || !request.SourcePaymentId.HasValue)
            request.ConfirmSchoolManager(app.FinalInitialManagerUserId.Value, app.InitialManagerConfirmedByUserId.Value, payment.Id, SchoolNow());
        else if (request.InitialManagerUserId != app.FinalInitialManagerUserId || request.SourcePaymentId != payment.Id || request.PlanVersionId != order.PlanVersionId)
            throw new InvalidOperationException("مرجع منجمد مدیر، پلن یا پرداخت با درخواست تأییدشده سازگار نیست.");
        if (request.Status != PlatformProvisioningStatus.Completed) await EnqueueSchoolWork(request.Id, Guid.Empty, ct);
        return request.Id;
    }

    public async Task RequeueAsync(Guid requestId, Guid operationId, CancellationToken ct)
    {
        if (operationId == Guid.Empty || dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("بازیابی به عملیات و تراکنش معتبر نیاز دارد.");
        var request = await LockProvisioningAsync(requestId, ct);
        if (!request.SchoolApplicationId.HasValue || !request.SourceOrderId.HasValue)
            throw new InvalidOperationException("این مسیر فقط درخواست آماده‌سازی آموزشگاه را بازیابی می‌کند.");
        if (request.Status == PlatformProvisioningStatus.Completed) return;
        await ValidateSchoolDependenciesAsync(request, ct);
        request.Resume(SchoolNow());
        await EnqueueSchoolWork(request.Id, operationId, ct);
    }

    public async Task HandleAsync(string payload, CancellationToken ct)
    {
        var requestId = JsonSerializer.Deserialize<SchoolProvisioningWork>(payload)?.RequestId ?? Guid.Empty;
        if (requestId == Guid.Empty) throw new InvalidOperationException("شناسهٔ کار آماده‌سازی معتبر نیست.");
        if (!await dbContext.PlatformProvisioningRequests.AsNoTracking().AnyAsync(r => r.Id == requestId && r.SchoolApplicationId != null && r.SourceOrderId != null, ct))
            throw new InvalidOperationException("کار آموزشگاه نمی‌تواند درخواست عمومی Platform را اجرا کند.");
        try
        {
            // همان checkpointهای Platform؛ هر مرحله تراکنش کوتاه و قفل مستقل دارد.
            await EnsureOrganizationStageAsync(requestId, null, ct);
            await EnsureSubscriptionStageAsync(requestId, null, ct);
            await EnsureManagerInvitationStageAsync(requestId, null, ct);
            await EnsureProvisioningCompletedAsync(requestId, null, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await MarkProvisioningNeedsAttentionAsync(requestId, "SCHOOL_PROVISIONING_STAGE_FAILED", null, ct);
            throw;
        }
    }

    private async Task EnqueueSchoolWork(Guid requestId, Guid operationId, CancellationToken ct)
    {
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"school-provisioning:v1:{requestId:N}:{operationId:N}")).AsSpan(0, 16));
        if (dbContext.OutboxMessages.Local.Any(m => m.Id == id) || await dbContext.OutboxMessages.AnyAsync(m => m.Id == id, ct)) return;
        outbox.Enqueue(new OutboxEnvelope(id, SchoolProvisioningMessageType, JsonSerializer.Serialize(new SchoolProvisioningWork(requestId)), SchoolNow()));
    }

    private async Task ValidateSchoolDependenciesAsync(PlatformProvisioningRequest request, CancellationToken ct)
    {
        if (!request.SchoolApplicationId.HasValue) return;
        var app = await dbContext.SchoolApplications.AsNoTracking().SingleAsync(a => a.Id == request.SchoolApplicationId, ct);
        var order = await dbContext.Orders.AsNoTracking().SingleAsync(o => o.Id == request.SourceOrderId, ct);
        var manager = await dbContext.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={request.InitialManagerUserId}").AsNoTracking().SingleOrDefaultAsync(ct);
        if (app.Status != SchoolApplicationStatus.Approved || app.FinalInitialManagerUserId != request.InitialManagerUserId ||
            !request.InitialManagerUserId.HasValue || !SchoolInitialManagerPolicy.IsEligible(manager) ||
            order.Status != OrderStatus.Paid || order.SchoolApplicationId != app.Id || order.PlanVersionId != request.PlanVersionId ||
            !order.HasCommercialTerms || !await dbContext.Payments.AnyAsync(p => p.Id == request.SourcePaymentId && p.OrderId == order.Id, ct))
            throw new InvalidOperationException("وضعیت جاری مدیر، درخواست یا پرداخت اجازهٔ ادامهٔ آماده‌سازی نمی‌دهد.");
    }

    private DateTime SchoolNow() => timeProvider.GetUtcNow().UtcDateTime;
    private sealed record SchoolProvisioningWork(Guid RequestId);
}
