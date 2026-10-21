using System.Security.Cryptography;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class MockOtpIdentityServiceTests
{
    [Theory]
    [InlineData("09123456789", "+989123456789")]
    [InlineData("09999999999", "+989999999999")]
    [InlineData("۰۹۱۱۱۱۱۱۱۱۱", "+989111111111")]
    public async Task MockRequest_AcceptsUnregisteredPhonesWithoutAnyOtpSideEffects(string phone, string normalized)
    {
        var context = new Context(enabled: false);
        var result = await context.Service.RequestOtpAsync(new RequestOtpCommand(phone), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, result.ChallengeId);
        Assert.Empty(context.Store.Challenges);
        Assert.Empty(context.Store.Users);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal(0, context.Security.OtpHashes);
        Assert.Equal(0, context.Store.Saves);
        Assert.True(PhoneNumberNormalizer.TryNormalize(phone, out var actual));
        Assert.Equal(normalized, actual);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("999999")]
    [InlineData("000000")]
    [InlineData("456789")]
    [InlineData("۱۲۳۴۵۶")]
    [InlineData("not-a-real-code")]
    public async Task MockVerify_RegistersNewFakePhoneWithoutPriorChallengeOrComparingTheCode(string code)
    {
        var context = new Context(enabled: false);
        var result = await context.Service.VerifyOtpAsync(new VerifyOtpCommand(Guid.Empty, "09999999999", code), CancellationToken.None);
        Assert.Equal("+989999999999", result.User.PhoneNumber);
        Assert.Equal(Assert.Single(context.Store.Users).Id, result.User.Id);
        Assert.False(result.User.IsProfileComplete);
        Assert.Equal(AuthenticationAssuranceLevel.Primary, Assert.Single(context.Store.Sessions).AssuranceLevel);
        Assert.Single(context.Store.Credentials);
        Assert.Single(context.Store.RefreshTokens);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Empty(context.Store.Challenges);
        Assert.Equal(0, context.Store.ChallengeReads);
        Assert.Equal(0, context.Security.OtpHashes);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal(1, context.Store.Commits);
    }

    [Fact]
    public async Task MockVerify_ExistingPhoneReusesUserWithoutGrantingAdditionalIdentity()
    {
        var context = new Context(enabled: false);
        var first = await context.Service.VerifyOtpAsync(new VerifyOtpCommand(Guid.Empty, "09123456789", "111111"), CancellationToken.None);
        var second = await context.Service.VerifyOtpAsync(new VerifyOtpCommand(Guid.NewGuid(), "+989123456789", "000000"), CancellationToken.None);
        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Single(context.Store.Users);
        Assert.Single(context.Store.Credentials);
        Assert.Equal(2, context.Store.Sessions.Count);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
    }

    [Theory]
    [InlineData("invalid-phone")]
    [InlineData("09123")]
    [InlineData("")]
    public async Task MockRejectsInvalidPhoneBeforeRequestOrVerifySideEffects(string phone)
    {
        var context = new Context(enabled: false);
        var request = await Assert.ThrowsAsync<IdentityException>(() => context.Service.RequestOtpAsync(new RequestOtpCommand(phone), CancellationToken.None));
        var verify = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(new VerifyOtpCommand(Guid.Empty, phone, "111111"), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.PhoneInvalid, request.Code);
        Assert.Equal(IdentityErrorCodes.PhoneInvalid, verify.Code);
        Assert.Empty(context.Store.Users);
        Assert.Equal(0, context.Provider.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234\n5678")]
    public async Task MockRejectsInvalidCodeFormatWithoutDoingRealOtpValidation(string code)
    {
        var context = new Context(enabled: false);
        var error = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
            new VerifyOtpCommand(Guid.Empty, "09123456789", code), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.OtpInvalid, error.Code);
        Assert.Empty(context.Store.Users);
        Assert.Equal(0, context.Security.OtpHashes);
        Assert.Equal(0, context.Store.ChallengeReads);
    }

    [Fact]
    public Task MockRejectsOversizedCode() => MockRejectsInvalidCodeFormatWithoutDoingRealOtpValidation(new string('0', 65));

    [Fact]
    public async Task MockPreservesAccountSuspension()
    {
        var context = new Context(enabled: false);
        var result = await context.Service.VerifyOtpAsync(new VerifyOtpCommand(Guid.Empty, "09123456789", "111111"), CancellationToken.None);
        context.Store.Users.Single().Suspend(context.Clock.GetUtcNow().UtcDateTime);
        var error = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
            new VerifyOtpCommand(Guid.Empty, result.User.PhoneNumber, "999999"), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.AccountSuspended, error.Code);
        Assert.Single(context.Store.Sessions);
    }

    [Fact]
    public async Task RealOtp_UsesProviderHashAndChallenge_RejectsWrongCodeAndAcceptsCorrectCode()
    {
        var context = new Context(enabled: true);
        var requested = await context.Service.RequestOtpAsync(new RequestOtpCommand("09123456789"), CancellationToken.None);
        Assert.Equal(1, context.Provider.Calls);
        Assert.Single(context.Store.Challenges);
        var error = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
            new VerifyOtpCommand(requested.ChallengeId, "09123456789", "different-code"), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.OtpInvalid, error.Code);
        Assert.Empty(context.Store.Sessions);
        var result = await context.Service.VerifyOtpAsync(new VerifyOtpCommand(requested.ChallengeId, "09123456789", context.Provider.Code), CancellationToken.None);
        Assert.NotEmpty(result.AccessToken);
        Assert.True(context.Security.OtpHashes >= 3);
    }

    [Fact]
    public async Task RealOtp_StillEnforcesExpiry()
    {
        var context = new Context(enabled: true);
        var requested = await context.Service.RequestOtpAsync(new RequestOtpCommand("09123456789"), CancellationToken.None);
        context.Clock.Now = context.Clock.Now.AddMinutes(6);
        var error = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
            new VerifyOtpCommand(requested.ChallengeId, "09123456789", context.Provider.Code), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.OtpExpired, error.Code);
        Assert.Empty(context.Store.Sessions);
    }

    [Fact]
    public async Task RealOtp_StillEnforcesFailedAttemptLimit()
    {
        var context = new Context(enabled: true);
        var requested = await context.Service.RequestOtpAsync(new RequestOtpCommand("09123456789"), CancellationToken.None);
        for (var attempt = 0; attempt < 2; attempt++)
            await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
                new VerifyOtpCommand(requested.ChallengeId, "09123456789", "wrong-code"), CancellationToken.None));
        var error = await Assert.ThrowsAsync<IdentityException>(() => context.Service.VerifyOtpAsync(
            new VerifyOtpCommand(requested.ChallengeId, "09123456789", context.Provider.Code), CancellationToken.None));
        Assert.Equal(IdentityErrorCodes.OtpTooManyAttempts, error.Code);
        Assert.Empty(context.Store.Sessions);
    }

    private sealed class Context
    {
        public RecordingStore Store { get; } = new();
        public RecordingSecurity Security { get; } = new();
        public RecordingProvider Provider { get; } = new();
        public MutableClock Clock { get; } = new();
        public IdentityService Service { get; }

        public Context(bool enabled) => Service = new IdentityService(Store, Security, Provider, new SecurityEvents(), Clock,
            new OtpOptions { Enabled = enabled, MaxFailedAttempts = 2 }, new SessionOptions());
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingProvider : IOtpDeliveryProvider
    {
        public int Calls { get; private set; }
        public string Code { get; } = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        public Task<OtpDeliveryResult> RequestOtpAsync(string normalizedPhoneNumber, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new OtpDeliveryResult(Code));
        }
    }

    private sealed class RecordingSecurity : IIdentitySecurity
    {
        private readonly IdentityCryptography _inner = new(RandomNumberGenerator.GetBytes(32));
        public int OtpHashes { get; private set; }
        public byte[] GenerateSalt() => _inner.GenerateSalt();
        public byte[] HashOtp(string code, byte[] salt) { OtpHashes++; return _inner.HashOtp(code, salt); }
        public string GenerateSessionToken() => _inner.GenerateSessionToken();
        public byte[] HashSessionToken(string rawToken) => _inner.HashSessionToken(rawToken);
        public string GenerateRefreshToken() => _inner.GenerateRefreshToken();
        public byte[] HashRefreshToken(string rawToken) => _inner.HashRefreshToken(rawToken);
    }

    private sealed class SecurityEvents : IIdentitySecurityEventRecorder
    {
        public Task RecordRefreshTokenReuseAsync(Guid userId, Guid sessionId, DateTime occurredAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // این double فقط مرز I/O را جایگزین می‌کند؛ IdentityService، قواعد Domain و رمزنگاری واقعی اجرا می‌شوند.
    private sealed class RecordingStore : IIdentityStore
    {
        public List<User> Users { get; } = [];
        public List<UserCredential> Credentials { get; } = [];
        public List<UserSession> Sessions { get; } = [];
        public List<RefreshToken> RefreshTokens { get; } = [];
        public List<OtpChallenge> Challenges { get; } = [];
        public int Saves { get; private set; }
        public int Commits { get; private set; }
        public int ChallengeReads { get; private set; }
        public Task<IIdentityTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken) => Task.FromResult<IIdentityTransaction>(new Transaction(this));
        public Task<OtpChallenge?> GetLatestOtpChallengeAsync(string normalizedPhoneNumber, CancellationToken cancellationToken) => Task.FromResult(Challenges.LastOrDefault(item => item.NormalizedPhoneNumber == normalizedPhoneNumber));
        public Task<OtpChallenge?> GetOtpChallengeAsync(Guid challengeId, string normalizedPhoneNumber, CancellationToken cancellationToken)
        {
            ChallengeReads++;
            return Task.FromResult(Challenges.SingleOrDefault(item => item.Id == challengeId && item.NormalizedPhoneNumber == normalizedPhoneNumber));
        }
        public Task<int> CountOtpChallengesAsync(string normalizedPhoneNumber, DateTime createdAfterUtc, CancellationToken cancellationToken) => Task.FromResult(Challenges.Count(item => item.NormalizedPhoneNumber == normalizedPhoneNumber && item.CreatedAtUtc >= createdAfterUtc));
        public Task<UserCredential?> GetMobileCredentialAsync(string normalizedPhoneNumber, CancellationToken cancellationToken) => Task.FromResult(Credentials.SingleOrDefault(item => item.NormalizedIdentifier == normalizedPhoneNumber));
        public Task<UserCredential?> GetMobileCredentialByUserIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Credentials.SingleOrDefault(item => item.UserId == userId));
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Users.SingleOrDefault(item => item.Id == userId));
        public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserSession?> GetSessionByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserSession?> GetSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefreshToken?> GetRefreshTokenForUpdateAsync(byte[] tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<RefreshToken>> GetRefreshTokensAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UsernameExistsAsync(string normalizedUsername, Guid exceptUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(OtpChallenge challenge) => Challenges.Add(challenge);
        public void Add(User user) => Users.Add(user);
        public void Add(Person person) => throw new NotSupportedException();
        public void Add(UserCredential credential) => Credentials.Add(credential);
        public void Add(UserSession session) => Sessions.Add(session);
        public void Add(RefreshToken refreshToken) => RefreshTokens.Add(refreshToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.CompletedTask; }

        private sealed class Transaction(RecordingStore owner) : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken) { owner.Commits++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
