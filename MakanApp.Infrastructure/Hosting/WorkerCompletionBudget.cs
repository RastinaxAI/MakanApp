namespace MakanApp.Infrastructure.Hosting;

public static class WorkerCompletionBudget
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // ثبت نتیجه پس از لغو درخواست مستقل است، اما نباید shutdown را نامحدود نگه دارد.
    public static CancellationTokenSource Create() => new(Timeout);
}
