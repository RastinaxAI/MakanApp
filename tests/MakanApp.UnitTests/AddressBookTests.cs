using System.Security.Cryptography;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class AddressBookTests
{
    [Theory]
    [InlineData("۰۹۱۲ ۱۲۳ ۴۵۶۷", "+989121234567")]
    [InlineData("٠٩١٢١٢٣٤٥٦٧", "+989121234567")]
    [InlineData("0098 (912) 123-4567", "+989121234567")]
    [InlineData("+98 912 1234567", "+989121234567")]
    [InlineData("۹۸۹۱۲۱۲۳۴۵۶۷", "+989121234567")]
    [InlineData("02112345678", "+982112345678")]
    public void ValidNumbersHaveOneCanonicalForm(string input, string expected) => Assert.Equal(expected, new ContactPhoneCanonicalizer().Normalize(input));

    [Theory]
    [InlineData("9121234567")]
    [InlineData("+98912")]
    [InlineData("1234")]
    [InlineData("+98<script>")]
    public void IncompleteOrAmbiguousPhonesAreNotGuessed(string input) => Assert.Throws<ArgumentException>(() => new ContactPhoneCanonicalizer().Normalize(input));

    [Fact]
    public void ProtectionIsBoundToOwnerAndEntryAndBlindIndexesAreOwnerScoped()
    {
        var protector = new AddressBookProtection(new EphemeralDataProtectionProvider(), RandomNumberGenerator.GetBytes(32));
        var owner = Guid.NewGuid(); var other = Guid.NewGuid();
        var encrypted = protector.Protect(owner, "entry", "private-contact-phone");
        Assert.DoesNotContain("private-contact-phone", encrypted);
        Assert.Equal("private-contact-phone", protector.Unprotect(owner, "entry", encrypted));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(other, "entry", encrypted));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(owner, "other-entry", encrypted));
        Assert.NotEqual(protector.Fingerprint(owner, "phone", "+989121234567"), protector.Fingerprint(other, "phone", "+989121234567"));
    }

    [Fact]
    public void TombstoneClearsPrivateContentAndCannotBeResurrectedByAnOldDevice()
    {
        var now = DateTime.UtcNow;
        var entry = AddressBookEntry.Create(Guid.NewGuid(), Guid.NewGuid(), "encrypted", true, 1, now, Guid.NewGuid());
        entry.Delete(2, now);
        Assert.Empty(entry.ProtectedContent); Assert.Null(entry.LinkedAccountUserId);
        Assert.Throws<InvalidOperationException>(() => entry.Update("encrypted", true, null, 3, now));
    }
}
