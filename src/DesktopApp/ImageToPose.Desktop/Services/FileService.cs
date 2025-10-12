using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<FileService> _logger;

    public FileService(ILogger<FileService> logger)
    {
        _logger = logger;
        FileServiceLogs.ServiceInitialized(_logger);
    }

    public async Task<string?> OpenImageFileAsync()
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["Operation"] = "OpenImageFile" });
        FileServiceLogs.OpeningImageDialog(_logger);
        
        var window = GetMainWindow();
        if (window == null)
        {
            FileServiceLogs.MainWindowNotAvailable(_logger);
            return null;
        }

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

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            FileServiceLogs.ImageFileSelected(_logger, path);
            return path;
        }
        
        FileServiceLogs.ImageDialogCanceled(_logger);
        return null;
    }

    public async Task SaveJsonFileAsync(string json, string defaultFileName)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "SaveJsonFile",
            ["FileName"] = defaultFileName
        });
        
        FileServiceLogs.OpeningSaveDialog(_logger, defaultFileName);
        
        var window = GetMainWindow();
        if (window == null)
        {
            FileServiceLogs.MainWindowNotAvailable(_logger);
            return;
        }

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
            var savePath = file.Path.LocalPath;
            FileServiceLogs.SavingJsonFile(_logger, savePath, json.Length);
            
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            
            FileServiceLogs.JsonFileSaved(_logger, savePath);
        }
        else
        {
            FileServiceLogs.SaveDialogCanceled(_logger);
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

internal static partial class FileServiceLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "FileService initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opening image file dialog")]
    public static partial void OpeningImageDialog(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Main window not available")]
    public static partial void MainWindowNotAvailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Image file selected: {Path}")]
    public static partial void ImageFileSelected(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Image dialog canceled")]
    public static partial void ImageDialogCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opening save dialog for file: {FileName}")]
    public static partial void OpeningSaveDialog(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saving JSON file to {Path}, size: {Size} bytes")]
    public static partial void SavingJsonFile(ILogger logger, string path, int size);

    [LoggerMessage(Level = LogLevel.Information, Message = "JSON file saved successfully: {Path}")]
    public static partial void JsonFileSaved(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Save dialog canceled")]
    public static partial void SaveDialogCanceled(ILogger logger);
}
