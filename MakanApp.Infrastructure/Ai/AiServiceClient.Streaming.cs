using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Domain.Intelligence;

namespace MakanApp.Infrastructure.Ai;

public sealed partial class AiServiceClient
{
    public async IAsyncEnumerable<AiChatStreamEvent> StreamChatAsync(AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request = ValidateAdaptiveRequest(request); ValidateOptions(); ValidateRequest(request, request.Actor.Role);
        var started = Stopwatch.GetTimestamp(); const string operation = "chat_stream";
        using var activity = AiServiceTelemetry.Start(operation, request.RequestId, request.CorrelationId);
        using var logScope = logger?.BeginScope(new Dictionary<string, object?> {
            ["AiOperation"] = operation, ["RequestId"] = request.RequestId, ["CorrelationId"] = request.CorrelationId });
        try { circuitBreaker.ThrowIfOpen(); }
        catch (AiServiceException error) { AiServiceTelemetry.RecordCircuitRejection(operation); RecordFailure(operation, error.Code, started); throw; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl), "/api/v1/ai/chat/stream")) {
            Content = JsonContent.Create(request, options: JsonOptions) };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Headers.TryAddWithoutValidation("X-AI-Contract-Version", options.ContractVersion);
        if (request.CorrelationId is not null) message.Headers.TryAddWithoutValidation("X-Correlation-ID", request.CorrelationId);
        if (options.UseServiceAuthentication)
        {
            try
            {
                var credential = await credentialProvider.GetCredentialAsync(timeout.Token);
                message.Headers.Authorization = new AuthenticationHeaderValue(credential.Scheme, credential.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { throw StreamFailure(new AiServiceException(AiServiceErrorCodes.ServiceUnavailable, "دسترسی سرویس AI آماده نیست."), started, cancellationToken); }
        }
        HttpResponseMessage response;
        try { response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token); }
        catch (Exception error) when (error is OperationCanceledException or HttpRequestException)
        { throw StreamFailure(error, started, cancellationToken); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                await ThrowMappedFailureAsync(operation, response, started, timeout.Token, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "text/event-stream")
                throw StreamFailure(new JsonException(), started, cancellationToken);
            Stream content;
            try { content = await response.Content.ReadAsStreamAsync(timeout.Token); }
            catch (Exception error) when (error is IOException or HttpRequestException or OperationCanceledException)
            { throw StreamFailure(error, started, cancellationToken); }
            await using var ownedContent = content;
            await using var events = AiSseReader.ReadAsync(content, request, timeout.Token).GetAsyncEnumerator(timeout.Token);
            while (true)
            {
                bool next; AiChatStreamEvent? value;
                try { next = await events.MoveNextAsync(); value = next ? events.Current : null; }
                catch (Exception error) when (error is OperationCanceledException or HttpRequestException or IOException or JsonException or ArgumentException or InvalidOperationException or AiServiceException)
                { throw StreamFailure(error, started, cancellationToken); }
                if (!next) break;
                if (value is AiChatStreamDone)
                {
                    circuitBreaker.RecordSuccess();
                    AiServiceTelemetry.Record(operation, "success", "none", Stopwatch.GetElapsedTime(started));
                }
                yield return value!;
            }
        }
    }

    private AiServiceException StreamFailure(Exception error, long started, CancellationToken caller)
    {
        if (error is OperationCanceledException && caller.IsCancellationRequested) throw new OperationCanceledException(caller);
        var mapped = error as AiServiceException ?? new AiServiceException(error switch {
            OperationCanceledException => AiServiceErrorCodes.ServiceTimeout,
            HttpRequestException or IOException => AiServiceErrorCodes.ServiceUnavailable,
            _ => AiServiceErrorCodes.ResponseInvalid }, "جریان پاسخ AI تکمیل نشد.");
        if (mapped.Code != AiServiceErrorCodes.RateLimited) circuitBreaker.RecordTransientFailure();
        RecordFailure("chat_stream", mapped.Code, started, mapped.UpstreamStatusCode);
        return mapped;
    }

    private static AiChatRequest ValidateAdaptiveRequest(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message?.Text) || string.IsNullOrWhiteSpace(request.Message.Language) ||
            request.Message.Language.Length > 6 || request.Context is null || request.Scope is null || request.Actor is null) throw InvalidRequest();
        if (request.Actor.Role is not ("Personal" or "Student" or "Parent" or "Teacher" or "Manager") ||
            request.Actor.Role == "Personal" && (request.Scope.OrganizationId is not null || request.Scope.SchoolId is not null ||
                request.Scope.ClassId is not null || request.Scope.StudentId is not null || request.Context.Subjects.Count != 0 ||
                request.Context.Grades.Count != 0 || request.Context.Homeworks.Count != 0 || request.Context.Exams.Count != 0 || request.Context.Activity.Count != 0)) throw InvalidRequest();
        var profile = request.Context.AssistantProfile?.Validate();
        try
        {
            if (AiConversationTurn.CharacterCount(request.Message.Text) > AiConversationTurn.MaximumUserCharacters) throw InvalidRequest();
            if (request.History is { } history)
            {
                if (history.Count > AiHistoryWindow.MaximumTurns) throw InvalidRequest();
                var total = 0;
                foreach (var turn in history)
                {
                    if (turn is null || turn.Role is not ("User" or "Assistant") || string.IsNullOrWhiteSpace(turn.Text)) throw InvalidRequest();
                    var count = AiConversationTurn.CharacterCount(turn.Text);
                    if (count > AiHistoryWindow.MaximumTurnCharacters) throw InvalidRequest();
                    total += count;
                }
                if (total > AiHistoryWindow.MaximumTotalCharacters) throw InvalidRequest();
            }
        }
        catch (ArgumentException) { throw InvalidRequest(); }
        return request with { Context = request.Context with { AssistantProfile = profile } };
    }
}
