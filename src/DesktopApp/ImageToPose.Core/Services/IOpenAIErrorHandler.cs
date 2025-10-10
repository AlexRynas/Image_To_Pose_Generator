using System.Net;

namespace ImageToPose.Core.Services;

public record OpenAIErrorInfo(string Code, string Message);

public interface IOpenAIErrorHandler
{
    OpenAIErrorInfo Translate(Exception ex);
}

public class OpenAIErrorHandler : IOpenAIErrorHandler
{
    public OpenAIErrorInfo Translate(Exception ex)
    {
        try
        {
            // Operation canceled / timeout
            if (ex is OperationCanceledException)
            {
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
                        return new OpenAIErrorInfo("429_insufficient_quota", "Insufficient OpenAI quota (HTTP 429). Add billing credits or lower usage.");
                    return new OpenAIErrorInfo("429_rate_limited", "Rate limited (HTTP 429). Please wait and try again.");
                }
                if (status == HttpStatusCode.Unauthorized)
                    return new OpenAIErrorInfo("401_invalid_api_key", "Invalid API key (HTTP 401). Check the key and try again.");
                if (status == HttpStatusCode.Forbidden)
                    return new OpenAIErrorInfo("403_forbidden", "Access forbidden (HTTP 403). Check account permissions.");
                if (status == HttpStatusCode.NotFound)
                    return new OpenAIErrorInfo("404_not_found", "Requested model or resource not found (HTTP 404).");
                if (status == HttpStatusCode.ServiceUnavailable)
                    return new OpenAIErrorInfo("503_unavailable", "Service temporarily unavailable (HTTP 503). Try again later.");

                return new OpenAIErrorInfo(((int?)status)?.ToString() ?? "http_error", $"HTTP error{(status is not null ? $" {(int)status}" : string.Empty)}. {Trim(httpEx.Message)}");
            }

            // OpenAI SDK often wraps into client exceptions. Fall back to message inspection.
            var msg = Trim(ex.Message);
            if (msg.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase))
                return new OpenAIErrorInfo("429_insufficient_quota", "Insufficient OpenAI quota (HTTP 429). Add billing credits or lower usage.");
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                return new OpenAIErrorInfo("429_rate_limited", "Rate limited (HTTP 429). Please wait and try again.");
            if (msg.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || msg.Contains("401"))
                return new OpenAIErrorInfo("401_invalid_api_key", "Invalid API key (HTTP 401). Check the key and try again.");
            if (msg.Contains("model", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return new OpenAIErrorInfo("model_not_found", "Requested model is not available for this account.");
            if (msg.Contains("content policy", StringComparison.OrdinalIgnoreCase) || msg.Contains("safety", StringComparison.OrdinalIgnoreCase))
                return new OpenAIErrorInfo("policy_block", "Request blocked by safety policy. Try adjusting the input.");

            return new OpenAIErrorInfo("unknown_error", $"Unexpected error: {msg}");
        }
        catch
        {
            return new OpenAIErrorInfo("unknown_error", "Unexpected error.");
        }
    }

    private static bool Contains(Exception ex, string needle)
        => (ex.Message?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

    private static string Trim(string? s)
        => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();
}
