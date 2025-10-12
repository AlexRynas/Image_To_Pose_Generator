using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using ImageToPose.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private const string SettingsFileName = "theme-settings.json";
    private const string ThemeKey = "theme";
    private readonly Application _app;
    private readonly string _settingsPath;

    public AppTheme Current { get; private set; } = AppTheme.Dark; // default

    public ThemeService(Application app, ILogger<ThemeService> logger)
    {
        _app = app;
        _logger = logger;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImageToPose.Desktop", 
            SettingsFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        ThemeServiceLogs.ServiceInitialized(_logger, _settingsPath);
        LoadPersistedOrDefault();
    }

    public void Apply(AppTheme theme, bool persist = true)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> 
        { 
            ["Theme"] = theme.ToString(),
            ["Persist"] = persist
        });
        
        ThemeServiceLogs.ApplyingTheme(_logger, theme.ToString(), persist);
        Current = theme;

        var sourceUri = new Uri(theme == AppTheme.Dark
            ? "avares://ImageToPose.Desktop/Styles/Themes/DarkTheme.axaml"
            : "avares://ImageToPose.Desktop/Styles/Themes/LightTheme.axaml");

        // Replace theme ResourceInclude in Application.Resources.MergedDictionaries by removing the old
        // include and adding a fresh one. This reliably triggers resource change notifications.
        if (_app.Resources is ResourceDictionary appDict)
        {
            var includesToRemove = appDict.MergedDictionaries
                .OfType<ResourceInclude>()
                .Where(ri => ri.Source != null && ri.Source.OriginalString.Contains("/Styles/Themes/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var inc in includesToRemove)
                appDict.MergedDictionaries.Remove(inc);

            var include = new ResourceInclude(new Uri("avares://ImageToPose.Desktop"))
            {
                Source = sourceUri
            };
            appDict.MergedDictionaries.Add(include);
        }

        // Also set RequestedThemeVariant to aid FluentTheme
        _app.RequestedThemeVariant = theme == AppTheme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        if (persist) Save();
        
        ThemeServiceLogs.ThemeApplied(_logger, theme.ToString());
    }

    public void Toggle()
    {
        var newTheme = Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        ThemeServiceLogs.TogglingTheme(_logger, Current.ToString(), newTheme.ToString());
        Apply(newTheme);
    }

    private void LoadPersistedOrDefault()
    {
        ThemeServiceLogs.LoadingPersistedTheme(_logger, _settingsPath);
        
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = JsonDocument.Parse(File.ReadAllText(_settingsPath));
                if (json.RootElement.TryGetProperty(ThemeKey, out var v) &&
                    Enum.TryParse<AppTheme>(v.GetString(), true, out var parsed))
                {
                    ThemeServiceLogs.PersistedThemeLoaded(_logger, parsed.ToString());
                    Apply(parsed, persist: false);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            ThemeServiceLogs.LoadThemeFailed(_logger, ex);
        }

        // Default to Dark on first run
        ThemeServiceLogs.UsingDefaultTheme(_logger, "Dark");
        Apply(AppTheme.Dark, persist: false);
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new { theme = Current.ToString().ToLowerInvariant() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            ThemeServiceLogs.ThemeSaved(_logger, _settingsPath);
        }
        catch (Exception ex)
        {
            ThemeServiceLogs.SaveThemeFailed(_logger, ex);
        }
    }
}

internal static partial class ThemeServiceLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ThemeService initialized, settings path: {SettingsPath}")]
    public static partial void ServiceInitialized(ILogger logger, string settingsPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying theme: {Theme}, persist: {Persist}")]
    public static partial void ApplyingTheme(ILogger logger, string theme, bool persist);

    [LoggerMessage(Level = LogLevel.Information, Message = "Theme applied: {Theme}")]
    public static partial void ThemeApplied(ILogger logger, string theme);

    [LoggerMessage(Level = LogLevel.Information, Message = "Toggling theme from {CurrentTheme} to {NewTheme}")]
    public static partial void TogglingTheme(ILogger logger, string currentTheme, string newTheme);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading persisted theme from {Path}")]
    public static partial void LoadingPersistedTheme(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Persisted theme loaded: {Theme}")]
    public static partial void PersistedThemeLoaded(ILogger logger, string theme);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load persisted theme, using default")]
    public static partial void LoadThemeFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using default theme: {Theme}")]
    public static partial void UsingDefaultTheme(ILogger logger, string theme);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme saved to {Path}")]
    public static partial void ThemeSaved(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save theme")]
    public static partial void SaveThemeFailed(ILogger logger, Exception exception);
}
