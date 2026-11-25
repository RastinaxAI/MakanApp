namespace MakanApp.Domain.Assessment;

public static class SubmissionDeadlinePolicy
{
    // مرز Deadline شامل برابری است؛ نتیجه فقط دیرهنگام‌بودن همین زمان پذیرش را مشخص می‌کند.
    public static bool EnsureAllowed(DateTime acceptedAtUtc, DateTime dueAtUtc, bool allowLateSubmission)
    {
        acceptedAtUtc = NormalizeUtc(acceptedAtUtc, nameof(acceptedAtUtc));
        dueAtUtc = NormalizeUtc(dueAtUtc, nameof(dueAtUtc));
        var isLate = acceptedAtUtc > dueAtUtc;
        if (isLate && !allowLateSubmission) throw new SubmissionDeadlinePassedException();
        return isLate;
    }

    private static DateTime NormalizeUtc(DateTime value, string parameterName) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        // datetime2 از SQL Server بدون Kind برمی‌گردد؛ قرارداد ذخیره‌سازی پروژه UTC است.
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => throw new ArgumentException("زمان Deadline باید UTC باشد.", parameterName)
    };
}
