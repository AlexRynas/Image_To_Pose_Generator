using System.Text.Json;
using System.Text.Json.Serialization;
using ImageToPose.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Core.Services;

/// <summary>
/// Internal service for writing AI diagnostics logs to disk.
/// Logs are written to %LOCALAPPDATA%\ImageToPose\logs\ai_diagnostics\{yyyy-MM-dd}\{timestamp}.json
/// </summary>
internal sealed class DiagnosticsLogger
{
    private readonly ILogger _logger;
    private readonly string _baseLogPath;
    private const int MaxTextLength = 2000;
    private const int MaxImagePreviewLength = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public DiagnosticsLogger(ILogger logger)
    {
        _logger = logger;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _baseLogPath = Path.Combine(localAppData, "ImageToPose", "logs", "ai_diagnostics");
    }

    /// <summary>
    /// Writes a diagnostics log entry to disk.
    /// </summary>
    public async Task WriteAsync(
        AiDiagnostics diagnostics,
        object requestMetadata,
        object responseMetadata,
        OpenAIErrorInfo? error = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create date-based subdirectory
            var dateFolder = diagnostics.StartedAt.ToString("yyyy-MM-dd");
            var logDir = Path.Combine(_baseLogPath, dateFolder);
            Directory.CreateDirectory(logDir);

            // Generate filename with timestamp
            var timestamp = diagnostics.StartedAt.ToString("HHmmss-fff");
            var fileName = $"{timestamp}.json";
            var filePath = Path.Combine(logDir, fileName);

            // Build redacted log entry
            var logEntry = new
            {
                diagnostics.StartedAt,
                diagnostics.FinishedAt,
                Latency = diagnostics.Latency.TotalMilliseconds,
                diagnostics.Model,
                diagnostics.Temperature,
                diagnostics.Seed,
                Request = RedactRequestMetadata(requestMetadata),
                Response = RedactResponseMetadata(responseMetadata),
                diagnostics.Usage,
                diagnostics.ReasoningSummary,
                ReasoningItemCount = diagnostics.ReasoningItems.Count,
                ReasoningItems = diagnostics.ReasoningItems,
                diagnostics.LogProbs,
                ToolCalls = diagnostics.ToolTrace,
                Error = error
            };

            // Write asynchronously
            var json = JsonSerializer.Serialize(logEntry, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogDebug("Diagnostics log written to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            // Never throw from diagnostics logging - just log the error
            _logger.LogWarning(ex, "Failed to write diagnostics log");
        }
    }

    private static object RedactRequestMetadata(object metadata)
    {
        return metadata switch
        {
            Dictionary<string, object?> dict => RedactDictionary(dict),
            _ => RedactObject(metadata)
        };
    }

    private static object RedactResponseMetadata(object metadata)
    {
        return metadata switch
        {
            Dictionary<string, object?> dict => RedactDictionary(dict),
            _ => RedactObject(metadata)
        };
    }

    private static Dictionary<string, object?> RedactDictionary(Dictionary<string, object?> dict)
    {
        var redacted = new Dictionary<string, object?>();

        foreach (var (key, value) in dict)
        {
            var lowerKey = key.ToLowerInvariant();

            // Redact sensitive keys
            if (lowerKey.Contains("apikey") || lowerKey.Contains("api_key") ||
                lowerKey.Contains("key") || lowerKey.Contains("token") ||
                lowerKey.Contains("secret") || lowerKey.Contains("password"))
            {
                redacted[key] = "[REDACTED]";
                continue;
            }

            // Truncate long text values
            if (value is string str)
            {
                redacted[key] = TruncateText(str);
            }
            // Handle nested dictionaries
            else if (value is Dictionary<string, object?> nestedDict)
            {
                redacted[key] = RedactDictionary(nestedDict);
            }
            else
            {
                redacted[key] = value;
            }
        }

        return redacted;
    }

    private static object RedactObject(object obj)
    {
        // For complex objects, use reflection to create a redacted version
        var type = obj.GetType();
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var redacted = new Dictionary<string, object?>();

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                var propName = prop.Name;
                var lowerName = propName.ToLowerInvariant();

                // Redact sensitive properties
                if (lowerName.Contains("apikey") || lowerName.Contains("key") ||
                    lowerName.Contains("token") || lowerName.Contains("secret") ||
                    lowerName.Contains("password"))
                {
                    redacted[propName] = "[REDACTED]";
                    continue;
                }

                // Handle image data
                if (lowerName.Contains("image") && value is string imageStr)
                {
                    if (imageStr.StartsWith("data:image") || imageStr.Length > 1000)
                    {
                        redacted[propName] = $"[IMAGE_DATA:{imageStr.Length} chars]";
                        continue;
                    }
                }

                // Truncate long strings
                if (value is string str)
                {
                    redacted[propName] = TruncateText(str);
                }
                else
                {
                    redacted[propName] = value;
                }
            }
            catch
            {
                // Skip properties that can't be read
                continue;
            }
        }

        return redacted;
    }

    private static string TruncateText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Check if it's base64 image data
        if (text.StartsWith("data:image") || (text.Length > 100 && IsBase64Like(text)))
        {
            return $"[IMAGE_DATA:{text.Length} chars]";
        }

        if (text.Length <= MaxTextLength)
            return text;

        return text[..MaxTextLength] + $"... [truncated from {text.Length} chars]";
    }

    private static bool IsBase64Like(string text)
    {
        // Simple heuristic: check if it looks like base64
        return text.Length > 100 &&
               text.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
    }
}
