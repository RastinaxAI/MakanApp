using System.Net;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Commerce;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class ZibalPaymentGatewayTests
{
    [Fact]
    public async Task RequestUsesFrozenIntegerRialAndAttemptCorrelationWithoutOptionalPersonalData()
    {
        using var handler = new Handler((_, _) => """{"result":100,"trackId":90000000001} """);
        using var client = new HttpClient(handler);
        var gateway = new ZibalPaymentGateway(client, Options.Create(Config()));
        var attempt = Guid.NewGuid();
        var result = await gateway.CreatePaymentAsync(new(attempt, Guid.NewGuid(), 4000000000m, CurrencyCode.Irr, "ignored"), default);
        Assert.Equal(PaymentGatewayCreationOutcome.PendingVerification, result.Outcome);
        Assert.Equal("90000000001", result.ProviderReference);
        Assert.Equal("https://gateway.zibal.ir/start/90000000001", result.PaymentUrl);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://gateway.zibal.ir/v1/request", request.Url);
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal(4000000000L, json.RootElement.GetProperty("amount").GetInt64());
        Assert.Equal(attempt.ToString("N"), json.RootElement.GetProperty("orderId").GetString());
        Assert.Equal(5, json.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain("merchant", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(102)] [InlineData(103)] [InlineData(104)] [InlineData(105)]
    [InlineData(106)] [InlineData(113)] [InlineData(115)] [InlineData(140)]
    public async Task KnownRequestErrorsNeverProducePaymentUrl(int code)
    {
        var result = await CreateAsync($"{{\"result\":{code},\"message\":\"private-provider-detail\"}}");
        Assert.Equal(PaymentGatewayCreationOutcome.Failed, result.Outcome);
        Assert.Equal(code, result.ProviderResult); Assert.Null(result.PaymentUrl); Assert.Null(result.ProviderReference);
        Assert.DoesNotContain("private-provider-detail", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(0)] [InlineData(999)] [InlineData(1000.5)] [InlineData(4000000001)]
    public async Task InvalidAmountsNeverCallProvider(double amount)
    {
        using var handler = new Handler((_, _) => throw new InvalidOperationException());
        using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config()))
            .CreatePaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), (decimal)amount, CurrencyCode.Irr, ""), default);
        Assert.Equal(PaymentGatewayCreationOutcome.Failed, result.Outcome); Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UnsupportedCurrencyDoesNotCallProvider()
    {
        using var handler = new Handler((_, _) => "{}"); using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config()))
            .CreatePaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1000, (CurrencyCode)999, ""), default);
        Assert.Equal(CommerceErrorCodes.PaymentAmountMismatch, result.FailureCode); Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")] [InlineData("not-json")] [InlineData("{}")]
    [InlineData("{\"result\":100}")]
    [InlineData("{\"result\":100,\"trackId\":0}")]
    [InlineData("{\"result\":100,\"trackId\":-1}")]
    [InlineData("{\"result\":100,\"trackId\":9223372036854775808}")]
    [InlineData("{\"result\":999}")]
    public async Task UncertainRequestIsUnknownWithoutBlindRetry(string reply)
    {
        var result = await CreateAsync(reply);
        Assert.Equal(PaymentGatewayCreationOutcome.Unknown, result.Outcome); Assert.Null(result.PaymentUrl);
    }

    [Theory]
    [InlineData("500")] [InlineData("timeout")] [InlineData("network")]
    public async Task TransportFailureDuringInitiationIsUnknownAndPostsExactlyOnce(string failure)
    {
        using var handler = new Handler((_, _) => failure == "timeout" ? throw new TaskCanceledException() :
            failure == "network" ? throw new HttpRequestException("sensitive-path") : "{}");
        handler.Status = failure == "500" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).CreatePaymentAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, ""), default);
        Assert.Equal(PaymentGatewayCreationOutcome.Unknown, result.Outcome); Assert.Single(handler.Requests);
        Assert.DoesNotContain("sensitive-path", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SuccessAndAlreadyVerifiedInquiryRequireAuthoritativeFullData(bool alreadyVerified)
    {
        var attempt = Guid.NewGuid();
        using var handler = new Handler((path, _) => alreadyVerified && path.EndsWith("/verify", StringComparison.Ordinal)
            ? "{\"result\":201}" : Paid(attempt));
        using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).VerifyPaymentAsync(
            new(attempt, Guid.NewGuid(), 1000, CurrencyCode.Irr, "92001"), default);
        Assert.Equal(PaymentGatewayVerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal("92001", result.ProviderReference); Assert.Equal("123456789", result.SettlementReference);
        Assert.Equal(1000m, result.VerifiedAmount); Assert.Null(result.ProviderPaidAtUtc);
        Assert.Equal(alreadyVerified ? 2 : 1, handler.Requests.Count);
        Assert.Equal(alreadyVerified, result.Observations.Any(o => o.IsInquiry));
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(-2, false)] [InlineData(2, false)] [InlineData(3, true)]
    [InlineData(4, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(7, true)]
    [InlineData(8, true)] [InlineData(9, true)] [InlineData(10, true)] [InlineData(11, true)]
    [InlineData(12, true)] [InlineData(15, true)] [InlineData(16, true)] [InlineData(18, true)]
    [InlineData(21, true)] [InlineData(99, false)]
    public async Task InquiryStatusNeverTurnsPendingFailedOrRefundIntoSuccess(int status, bool failed)
    {
        var attempt = Guid.NewGuid();
        using var handler = new Handler((path, _) => path.EndsWith("/verify", StringComparison.Ordinal) ? "{\"result\":202}" :
            JsonSerializer.Serialize(new { result = 100, status, amount = 1000, orderId = attempt.ToString("N") }));
        using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).VerifyPaymentAsync(
            new(attempt, Guid.NewGuid(), 1000, CurrencyCode.Irr, "92001"), default);
        Assert.Equal(failed ? PaymentGatewayVerificationOutcome.Failed : PaymentGatewayVerificationOutcome.Unknown, result.Outcome);
        Assert.Null(result.SettlementReference); Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("amount")] [InlineData("orderId")] [InlineData("trackId")] [InlineData("refNumber")]
    public async Task MismatchedOrIncompleteProofCannotSettle(string field)
    {
        var attempt = Guid.NewGuid();
        using var handler = new Handler((_, _) => Paid(attempt, field)); using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).VerifyPaymentAsync(
            new(attempt, Guid.NewGuid(), 1000, CurrencyCode.Irr, "92001"), default);
        Assert.Equal(PaymentGatewayVerificationOutcome.Unknown, result.Outcome);
        Assert.Null(result.ProviderReference); Assert.Null(result.SettlementReference);
    }

    [Theory]
    [InlineData("{}")] [InlineData("{\"result\":201}")] [InlineData("{\"result\":203}")]
    [InlineData("{\"result\":102}")] [InlineData("not-json")]
    public async Task AlreadyVerifiedWithoutValidInquiryIsNotSuccess(string inquiry)
    {
        using var handler = new Handler((path, _) => path.EndsWith("/verify", StringComparison.Ordinal) ? "{\"result\":201}" : inquiry);
        using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).VerifyPaymentAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, "92001"), default);
        Assert.Equal(PaymentGatewayVerificationOutcome.Unknown, result.Outcome); Assert.Null(result.ProviderReference);
    }

    [Fact]
    public async Task InvalidTrackIdFailsBeforeNetworkAnd203IsNotPaid()
    {
        using var handler = new Handler((_, _) => "{\"result\":203}"); using var client = new HttpClient(handler);
        var gateway = new ZibalPaymentGateway(client, Options.Create(Config()));
        foreach (var reference in new[] { "-1", "0", "01", "https://internal/", "9223372036854775808" })
            Assert.Equal(PaymentGatewayVerificationOutcome.Unknown, (await gateway.VerifyPaymentAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, reference), default)).Outcome);
        Assert.Empty(handler.Requests);
        var invalid = await gateway.VerifyPaymentAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, "92001"), default);
        Assert.Equal(CommerceErrorCodes.PaymentReferenceInvalid, invalid.FailureCode); Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CallerCancellationIsNotSwallowed()
    {
        using var handler = new Handler((_, _) => throw new OperationCanceledException()); using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ZibalPaymentGateway(client, Options.Create(Config())).CreatePaymentAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, ""), cancellation.Token));
    }

    [Theory]
    [InlineData("Production")] [InlineData("Staging")] [InlineData("Development")]
    public void OfficialTestMerchantIsRejectedOutsideDedicatedTestEnvironment(string environment) =>
        Assert.Throws<OptionsValidationException>(() => Config().Validate(environment, true));

    [Fact]
    public void TestConfigurationIsValidAndUnconfiguredUnavailableRemainsSupported()
    {
        Config().Validate("Test", true); Config().Validate("Testing", true);
        new ZibalOptions().Validate("Production", false);
        Assert.Throws<OptionsValidationException>(() => new ZibalOptions().Validate("Production", true));
    }

    [Fact]
    public void ProductionRejectsCaseVariantsOfTestMerchantAndHttpEvenWithRealMerchantShape()
    {
        foreach (var merchant in new[] { "zibal", "ZIBAL", "ZiBaL" })
        {
            var options = new ZibalOptions { Merchant = merchant, CallbackUrl = "https://backend.example.test" + ZibalOptions.CallbackPath,
                ClientReturnUrl = "https://client.example.test/return", AllowedClientReturnOrigins = ["https://client.example.test"] };
            Assert.Throws<OptionsValidationException>(() => options.Validate("Production", true));
        }
        var http = new ZibalOptions { Merchant = Guid.NewGuid().ToString("N"), CallbackUrl = "http://backend.example.test" + ZibalOptions.CallbackPath,
            ClientReturnUrl = "https://client.example.test/return", AllowedClientReturnOrigins = ["https://client.example.test"] };
        Assert.Throws<OptionsValidationException>(() => http.Validate("Production", true));
    }

    [Theory]
    [InlineData("https://internal.example", "https://client.example.test", 15)]
    [InlineData("http://gateway.zibal.ir", "https://client.example.test", 15)]
    [InlineData("https://gateway.zibal.ir@evil.example", "https://client.example.test", 15)]
    [InlineData("https://gateway.zibal.ir", "https://evil.example", 15)]
    [InlineData("https://gateway.zibal.ir", "https://client.example.test?url=https://evil.example", 15)]
    [InlineData("https://gateway.zibal.ir", "https://client.example.test", 0)]
    [InlineData("https://gateway.zibal.ir", "https://client.example.test", 31)]
    public void UnsafeOriginRedirectAndTimeoutAreRejected(string origin, string returnUrl, int timeout)
    {
        var config = new ZibalOptions { BaseUrl = origin, Merchant = "zibal", TimeoutSeconds = timeout,
            CallbackUrl = "https://backend.example.test" + ZibalOptions.CallbackPath, ClientReturnUrl = returnUrl,
            AllowedClientReturnOrigins = ["https://client.example.test"] };
        Assert.Throws<OptionsValidationException>(() => config.Validate("Test", true));
    }

    internal static ZibalOptions Config() => new()
    {
        Merchant = "zibal", CallbackUrl = "https://backend.example.test" + ZibalOptions.CallbackPath,
        ClientReturnUrl = "https://client.example.test/payment-return", AllowedClientReturnOrigins = ["https://client.example.test"]
    };
    private static string Paid(Guid attempt, string? mismatch = null) => JsonSerializer.Serialize(new
    {
        result = 100, status = 1, amount = mismatch == "amount" ? 999 : 1000,
        orderId = mismatch == "orderId" ? Guid.NewGuid().ToString("N") : attempt.ToString("N"),
        refNumber = mismatch == "refNumber" ? (long?)null : 123456789,
        trackId = mismatch == "trackId" ? (long?)92002 : null,
        paidAt = "2018-03-25T23:43:01.053000", cardNumber = "must-not-persist", hashedCardNumber = "must-not-persist"
    });
    private static async Task<PaymentGatewayCreationResult> CreateAsync(string reply)
    {
        using var handler = new Handler((_, _) => reply); using var client = new HttpClient(handler);
        var result = await new ZibalPaymentGateway(client, Options.Create(Config())).CreatePaymentAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), 1000, CurrencyCode.Irr, ""), default);
        Assert.Single(handler.Requests); return result;
    }
    private sealed class Handler(Func<string, string, string> reply) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var url = request.RequestUri!.AbsoluteUri; Requests.Add((url, body));
            return new(Status) { Content = new StringContent(reply(url, body), Encoding.UTF8, "application/json") };
        }
    }
}
