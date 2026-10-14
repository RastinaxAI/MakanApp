using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed class SchoolManagementAccessResolver(IAccessContextResolver resolver)
{
    public async Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var access = await resolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (access.WorkspaceType != WorkspaceType.Organization || access.OrganizationId is null ||
            access.MembershipId is null || access.ActiveRole != OrganizationRole.Manager)
            throw new OrganizationException(OrganizationErrorCodes.WorkspaceNotAllowed, "فضای سازمانی با نقش فعال مدیر لازم است.");
        return access;
    }
}

public static class SchoolOnboardingValidation
{
    public static OrganizationException Invalid(string message) => new(SchoolOnboardingErrorCodes.Invalid, message);
    public static OrganizationException NotFound() => new(SchoolOnboardingErrorCodes.PersonNotFound, "پرونده مجاز پیدا نشد.");
    public static string Name(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
            throw Invalid("نام الزامی است و حداکثر ۱۰۰ نویسه دارد.");
        return normalized;
    }
    public static string? DisplayName(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 200) throw Invalid("نام نمایشی حداکثر ۲۰۰ نویسه دارد.");
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
    public static string Reason(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500)
            throw Invalid("دلیل معتبر با حداکثر ۵۰۰ نویسه لازم است.");
        return normalized;
    }
    public static void Version(byte[] current, string expected)
    {
        byte[] decoded;
        try { decoded = Convert.FromBase64String(expected ?? string.Empty); }
        catch (FormatException) { throw Invalid("نسخه رکورد معتبر نیست."); }
        if (decoded.Length != 8) throw Invalid("نسخه رکورد معتبر نیست.");
        if (!current.SequenceEqual(decoded))
            throw new OrganizationException(OrganizationErrorCodes.ConcurrencyConflict, "رکورد هم‌زمان تغییر کرده است.");
    }
    public static void Page(int page, int pageSize)
    {
        if (page < 1 || page > 100_000 || pageSize is < 1 or > 100)
            throw Invalid("شماره صفحه و اندازه ۱ تا ۱۰۰ معتبر لازم است.");
    }
}
