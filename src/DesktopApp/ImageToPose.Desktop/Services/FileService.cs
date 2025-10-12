using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ImageToPose.Desktop.Services;

/// <summary>
/// Service abstraction for file operations (dialogs, save, etc.)
/// </summary>
public interface IFileService
{
    Task<string?> OpenImageFileAsync();
    Task SaveJsonFileAsync(string json, string defaultFileName);
}

/// <summary>
/// Avalonia implementation of file service
/// </summary>
public class FileService : IFileService
{
    public async Task<string?> OpenImageFileAsync()
    {
        var window = GetMainWindow();
        if (window == null)
            return null;

        var storageProvider = window.StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg" }
                }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task SaveJsonFileAsync(string json, string defaultFileName)
    {
        var window = GetMainWindow();
        if (window == null)
            return;

        var storageProvider = window.StorageProvider;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save JSON file",
            DefaultExtension = "json",
            SuggestedFileName = defaultFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
        }
    }

    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
