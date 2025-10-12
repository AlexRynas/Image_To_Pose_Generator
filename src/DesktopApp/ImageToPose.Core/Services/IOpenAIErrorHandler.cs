using System.Net;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Core.Services;

public record OpenAIErrorInfo(string Code, string Message);

public interface IOpenAIErrorHandler
{
    OpenAIErrorInfo Translate(Exception ex);
}

public class OpenAIErrorHandler : IOpenAIErrorHandler
{
    private readonly ILogger<OpenAIErrorHandler> _logger;

    public OpenAIErrorHandler(ILogger<OpenAIErrorHandler> logger)
    {
        _logger = logger;
    }

    public OpenAIErrorInfo Translate(Exception ex)
    {
        try
        {
            // Operation canceled / timeout
            if (ex is OperationCanceledException)
            {
                OpenAIErrorHandlerLogs.RequestCanceled(_logger);
                return new OpenAIErrorInfo("request_canceled", "Request canceled or timed out. Try again.");
            }

            // HTTP layer
            if (ex is HttpRequestException httpEx)
            {
                var status = httpEx.StatusCode;
                if (status == HttpStatusCode.TooManyRequests)
                {
                    // Distinguish quota vs. rate limit if message hints it
                    if (Contains(httpEx, "insufficient_quota"))
                    {
                        OpenAIErrorHandlerLogs.InsufficientQuota(_logger);
                        return new OpenAIErrorInfo("429_insufficient_quota", "Insufficient OpenAI quota (HTTP 429). Add billing credits or lower usage.");
                    }
                    OpenAIErrorHandlerLogs.RateLimited(_logger);
                    return new OpenAIErrorInfo("429_rate_limited", "Rate limited (HTTP 429). Please wait and try again.");
                }
                if (status == HttpStatusCode.Unauthorized)
                {
                    OpenAIErrorHandlerLogs.InvalidApiKey(_logger);
                    return new OpenAIErrorInfo("401_invalid_api_key", "Invalid API key (HTTP 401). Check the key and try again.");
                }
                if (status == HttpStatusCode.Forbidden)
                {
                    OpenAIErrorHandlerLogs.AccessForbidden(_logger);
                    return new OpenAIErrorInfo("403_forbidden", "Access forbidden (HTTP 403). Check account permissions.");
                }
                if (status == HttpStatusCode.NotFound)
                {
                    OpenAIErrorHandlerLogs.ResourceNotFound(_logger);
                    return new OpenAIErrorInfo("404_not_found", "Requested model or resource not found (HTTP 404).");
                }
                if (status == HttpStatusCode.ServiceUnavailable)
                {
                    OpenAIErrorHandlerLogs.ServiceUnavailable(_logger);
                    return new OpenAIErrorInfo("503_unavailable", "Service temporarily unavailable (HTTP 503). Try again later.");
                }

                var errorMsg = $"HTTP error{(status is not null ? $" {(int)status}" : string.Empty)}. {Trim(httpEx.Message)}";
                OpenAIErrorHandlerLogs.HttpError(_logger, status?.ToString() ?? "unknown", errorMsg);
                return new OpenAIErrorInfo(((int?)status)?.ToString() ?? "http_error", errorMsg);
            }

            // OpenAI SDK often wraps into client exceptions. Fall back to message inspection.
            var msg = Trim(ex.Message);
            if (msg.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
            {
                OpenAIErrorHandlerLogs.InsufficientQuota(_logger);
                return new OpenAIErrorInfo("429_insufficient_quota", "Insufficient OpenAI quota (HTTP 429). Add billing credits or lower usage.");
            }
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                OpenAIErrorHandlerLogs.RateLimited(_logger);
                return new OpenAIErrorInfo("429_rate_limited", "Rate limited (HTTP 429). Please wait and try again.");
            }
            if (msg.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || msg.Contains("401"))
            {
                OpenAIErrorHandlerLogs.InvalidApiKey(_logger);
                return new OpenAIErrorInfo("401_invalid_api_key", "Invalid API key (HTTP 401). Check the key and try again.");
            }
            if (msg.Contains("model", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                OpenAIErrorHandlerLogs.ModelNotFound(_logger, msg);
                return new OpenAIErrorInfo("model_not_found", "Requested model is not available for this account.");
            }
            if (msg.Contains("content policy", StringComparison.OrdinalIgnoreCase) || msg.Contains("safety", StringComparison.OrdinalIgnoreCase))
            {
                OpenAIErrorHandlerLogs.PolicyBlock(_logger);
                return new OpenAIErrorInfo("policy_block", "Request blocked by safety policy. Try adjusting the input.");
            }

            OpenAIErrorHandlerLogs.UnknownError(_logger, ex, msg);
            return new OpenAIErrorInfo("unknown_error", $"Unexpected error: {msg}");
        }
        catch (Exception translationEx)
        {
            OpenAIErrorHandlerLogs.TranslationFailed(_logger, translationEx);
            return new OpenAIErrorInfo("unknown_error", "Unexpected error.");
        }
    }

    private static bool Contains(Exception ex, string needle)
        => (ex.Message?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

    private static string Trim(string? s)
        => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();
}

internal static partial class OpenAIErrorHandlerLogs
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAI request was canceled or timed out")]
    public static partial void RequestCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenAI quota insufficient (429)")]
    public static partial void InsufficientQuota(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAI rate limited (429)")]
    public static partial void RateLimited(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid OpenAI API key (401)")]
    public static partial void InvalidApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenAI access forbidden (403)")]
    public static partial void AccessForbidden(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenAI resource not found (404)")]
    public static partial void ResourceNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAI service unavailable (503)")]
    public static partial void ServiceUnavailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP error {StatusCode}: {Message}")]
    public static partial void HttpError(ILogger logger, string statusCode, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenAI model not found: {Message}")]
    public static partial void ModelNotFound(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request blocked by OpenAI content policy")]
    public static partial void PolicyBlock(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unknown OpenAI error: {Message}")]
    public static partial void UnknownError(ILogger logger, Exception exception, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error translating OpenAI exception")]
    public static partial void TranslationFailed(ILogger logger, Exception exception);
}
