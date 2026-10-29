using System.Globalization;
using System.Numerics;

namespace MakanApp.Domain.Commerce;

public static class CommercialPeriods
{
    // دادهٔ IANA از ۲۰۲۳ برای تهران DST ندارد؛ registry قدیمی Windows نباید مرز مالی را جابه‌جا کند.
    // https://lists.iana.org/hyperkitty/list/tz%40iana.org/message/KU35AF3J6H6NOGGWBS7FOFPIULA7VDHD/
    public static TimeZoneInfo Tehran { get; } = TimeZoneInfo.CreateCustomTimeZone("Asia/Tehran", TimeSpan.FromMinutes(210), "Asia/Tehran", "Asia/Tehran");

    public static (DateTime Start, DateTime End) TrialDay(DateTime utcNow)
    {
        RequireUtc(utcNow);
        var day = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Tehran).Date;
        return (TimeZoneInfo.ConvertTimeToUtc(day, Tehran), TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1), Tehran));
    }

    public static DateTime MonthlyBoundary(DateTime anchorUtc, int monthOffset, BillingCalendar calendar)
    {
        RequireUtc(anchorUtc);
        if (monthOffset < 0 || !Enum.IsDefined(calendar)) throw new ArgumentOutOfRangeException(nameof(monthOffset));
        var local = TimeZoneInfo.ConvertTimeFromUtc(anchorUtc, Tehran);
        Calendar calculator = calendar == BillingCalendar.Persian ? new PersianCalendar() : new GregorianCalendar();
        var first = calculator.ToDateTime(calculator.GetYear(local), calculator.GetMonth(local), 1, 0, 0, 0, 0);
        var next = calculator.AddMonths(first, monthOffset);
        var day = Math.Min(calculator.GetDayOfMonth(local), calculator.GetDaysInMonth(calculator.GetYear(next), calculator.GetMonth(next)));
        var result = calculator.AddDays(next, day - 1).Add(local.TimeOfDay);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(result, DateTimeKind.Unspecified), Tehran);
    }

    public static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان معتبر UTC لازم است.");
    }
}

public readonly record struct ProratedQuota(long Delta, long RemainingTicks, long DurationTicks);

public static class CommercialProration
{
    public static long EffectiveLimit(long initial, IEnumerable<ProratedQuota> changes)
    {
        BigInteger numerator = initial;
        BigInteger denominator = 1;
        foreach (var change in changes)
        {
            if (change.Delta < 0 || change.DurationTicks <= 0 || change.RemainingTicks < 0 || change.RemainingTicks > change.DurationTicks)
                throw new ArgumentException("نسبت ارتقا معتبر نیست.");
            numerator = numerator * change.DurationTicks + (BigInteger)change.Delta * change.RemainingTicks * denominator;
            denominator *= change.DurationTicks;
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            numerator /= gcd;
            denominator /= gcd;
        }
        return checked((long)(numerator / denominator));
    }

    public static decimal CumulativePriceToman(IEnumerable<(decimal DeltaToman, long RemainingTicks, long DurationTicks)> changes)
    {
        BigInteger numerator = 0;
        BigInteger denominator = 1;
        foreach (var (delta, remaining, duration) in changes)
        {
            if (delta < 0 || duration <= 0 || remaining < 0 || remaining > duration) throw new ArgumentException("نسبت قیمت معتبر نیست.");
            var bits = decimal.GetBits(delta);
            var scale = (bits[3] >> 16) & 0xff;
            var integer = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
            var partDenominator = BigInteger.Pow(10, scale) * duration;
            numerator = numerator * partDenominator + integer * remaining * denominator;
            denominator *= partDenominator;
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            numerator /= gcd; denominator /= gcd;
        }
        return checked((decimal)((2 * numerator + denominator) / (2 * denominator)));
    }
}
