using ImageToPose.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    OpenAIOptions GetOpenAIOptions();
    void SetOpenAIOptions(OpenAIOptions options);
}

/// <summary>
/// In-memory implementation of settings service
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private OpenAIOptions _options = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        SettingsServiceLogs.ServiceInitialized(_logger);
    }

    public OpenAIOptions GetOpenAIOptions()
    {
        SettingsServiceLogs.OptionsRetrieved(_logger);
        return _options;
    }

    public void SetOpenAIOptions(OpenAIOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        SettingsServiceLogs.OptionsUpdated(_logger);
    }
}

internal static partial class SettingsServiceLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "SettingsService initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenAI options retrieved")]
    public static partial void OptionsRetrieved(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenAI options updated")]
    public static partial void OptionsUpdated(ILogger logger);
}
