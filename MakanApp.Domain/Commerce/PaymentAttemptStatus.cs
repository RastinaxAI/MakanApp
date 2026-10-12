namespace MakanApp.Domain.Commerce;

public enum PaymentAttemptStatus
{
    Created = 1,
    Initiating = 2,
    PendingVerification = 3,
    Succeeded = 4,
    Failed = 5,
    Unknown = 6
}
