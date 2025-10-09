using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using ImageToPose.Desktop.Models;

namespace ImageToPose.Desktop.Services;

public sealed class ThemeService : IThemeService
{
    private const string SettingsFileName = "theme-settings.json";
    private const string ThemeKey = "theme";
    private readonly Application _app;
    private readonly string _settingsPath;

    public AppTheme Current { get; private set; } = AppTheme.Dark; // default

    public ThemeService(Application app)
    {
        _app = app;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImageToPose.Desktop", 
            SettingsFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        LoadPersistedOrDefault();
    }

    public void Apply(AppTheme theme, bool persist = true)
    {
        Current = theme;

        // Swap the ResourceInclude.Source of ActiveTheme
        if (_app.Resources.TryGetResource("ThemeBucket", null, out var bucket) && bucket is ResourceDictionary dict)
        {
            var include = (ResourceInclude)dict["ActiveTheme"]!;
            include.Source = new Uri(theme == AppTheme.Dark
                ? "avares://ImageToPose.Desktop/Styles/Themes/DarkTheme.axaml"
                : "avares://ImageToPose.Desktop/Styles/Themes/LightTheme.axaml");
        }

        // Also set RequestedThemeVariant to aid FluentTheme
        _app.RequestedThemeVariant = theme == AppTheme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        if (persist) Save();
    }

    public void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    private void LoadPersistedOrDefault()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = JsonDocument.Parse(File.ReadAllText(_settingsPath));
                if (json.RootElement.TryGetProperty(ThemeKey, out var v) &&
                    Enum.TryParse<AppTheme>(v.GetString(), true, out var parsed))
                {
                    Apply(parsed, persist: false);
                    return;
                }
            }
        }
        catch { /* ignore and fall back to default */ }

        // Default to Dark on first run
        Apply(AppTheme.Dark, persist: false);
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new { theme = Current.ToString().ToLowerInvariant() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { /* ignore save errors */ }
    }
}
