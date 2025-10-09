using System;
using System.IO;
using System.Linq;
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
