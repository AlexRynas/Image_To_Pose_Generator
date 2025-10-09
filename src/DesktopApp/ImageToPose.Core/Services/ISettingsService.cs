using ImageToPose.Core.Models;

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
    private OpenAIOptions _options = new();

    public OpenAIOptions GetOpenAIOptions() => _options;

    public void SetOpenAIOptions(OpenAIOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
}
