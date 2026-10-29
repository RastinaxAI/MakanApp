namespace MakanApp.Domain.Reports;

public sealed partial class ReportExportJob
{
    public Guid? ClientOperationId { get; private set; }
    public byte[]? RequestPayloadHash { get; private set; }
    public Guid? CommerceReservationId { get; private set; }

    public void SetIdempotency(Guid operationId, byte[] hash)
    {
        if (ClientOperationId.HasValue || operationId == Guid.Empty || hash.Length != 32 || Status != ReportExportJobStatus.Queued)
            throw new InvalidOperationException("شناسهٔ عملیات خروجی گزارش معتبر نیست.");
        ClientOperationId = operationId; RequestPayloadHash = hash.ToArray();
    }
    public void BindCommercialReservation(Guid reservationId)
    {
        if (CommerceReservationId.HasValue || reservationId == Guid.Empty || Status != ReportExportJobStatus.Queued)
            throw new InvalidOperationException("رزرو خروجی گزارش معتبر نیست.");
        CommerceReservationId = reservationId;
    }
}
