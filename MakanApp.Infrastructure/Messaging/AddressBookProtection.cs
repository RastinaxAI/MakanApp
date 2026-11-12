using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using PhoneNumbers;

namespace MakanApp.Infrastructure.Messaging;

public sealed class AddressBookProtection(IDataProtectionProvider provider, byte[] indexKey)
{
    private readonly byte[] _key = HMACSHA256.HashData(indexKey, Encoding.UTF8.GetBytes("MakanApp.AddressBook.Index.v1"));
    private IDataProtector Protector(Guid owner, string purpose) => provider.CreateProtector("MakanApp.AddressBook.v1", owner.ToString("N"), purpose);
    public string Protect(Guid owner, string purpose, string value) => Protector(owner, purpose).Protect(value);
    public string Unprotect(Guid owner, string purpose, string value) => Protector(owner, purpose).Unprotect(value);
    public string Fingerprint(Guid owner, string purpose, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{owner:N}:{purpose}:{value}")));
}

public sealed class ContactPhoneCanonicalizer
{
    private readonly PhoneNumberUtil _phones = PhoneNumberUtil.GetInstance();
    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80) throw new ArgumentException("شماره معتبر نیست.");
        var digits = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (c is >= '۰' and <= '۹') digits.Append((char)('0' + c - '۰'));
            else if (c is >= '٠' and <= '٩') digits.Append((char)('0' + c - '٠'));
            else if (c is >= '0' and <= '9' || c == '+') digits.Append(c);
            else if (!char.IsWhiteSpace(c) && c is not '(' and not ')' and not '-') throw new ArgumentException("شماره معتبر نیست.");
        }
        var raw = digits.ToString();
        if (raw.StartsWith("00", StringComparison.Ordinal)) raw = "+" + raw[2..];
        if (raw.Length == 12 && raw.StartsWith("98", StringComparison.Ordinal)) raw = "+" + raw;
        // شمارهٔ ملی تنها با پیشوند ۰ ایران تفسیر می‌شود؛ شمارهٔ ناقص یا منطقهٔ مبهم حدس زده نمی‌شود.
        if (!raw.StartsWith('+') && !raw.StartsWith('0')) throw new ArgumentException("پیش‌شمارهٔ کشور لازم است.");
        try
        {
            var phone = _phones.Parse(raw, raw.StartsWith('+') ? null : "IR");
            if (!_phones.IsValidNumber(phone) || phone.HasExtension) throw new ArgumentException("شماره معتبر نیست.");
            return _phones.Format(phone, PhoneNumberFormat.E164);
        }
        catch (NumberParseException) { throw new ArgumentException("شماره معتبر نیست."); }
    }
}
