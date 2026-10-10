using System.Text;
using MakanApp.Infrastructure.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class IdentityCryptographyTests
{
    [Fact]
    public void PersistentKeyKeepsOtpHashVerifiableAcrossInstances()
    {
        var key = Encoding.UTF8.GetBytes(new string('K', 32));
        var salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        const string code = "opaque-provider-code-A7";
        var firstProcessSecurity = new IdentityCryptography(key);
        var persistedHash = firstProcessSecurity.HashOtp(code, salt);
        var restartedProcessSecurity = new IdentityCryptography(key);

        var candidateHash = restartedProcessSecurity.HashOtp(code, salt);

        Assert.Equal(persistedHash, candidateHash);
    }

    [Fact]
    public void DifferentKeyCannotVerifyPersistedOtpHash()
    {
        var firstKey = Encoding.UTF8.GetBytes(new string('A', 32));
        var restartedKey = Encoding.UTF8.GetBytes(new string('B', 32));
        var salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        const string code = "opaque-provider-code-A7";
        var persistedHash = new IdentityCryptography(firstKey).HashOtp(code, salt);

        var candidateHash = new IdentityCryptography(restartedKey).HashOtp(code, salt);

        Assert.NotEqual(persistedHash, candidateHash);
    }
}
