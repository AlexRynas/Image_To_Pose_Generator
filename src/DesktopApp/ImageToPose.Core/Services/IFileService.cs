namespace ImageToPose.Core.Services;

/// <summary>
/// Service abstraction for file operations (dialogs, save, etc.)
/// </summary>
public interface IFileService
{
    Task<string?> OpenImageFileAsync();
    Task SaveJsonFileAsync(string json, string defaultFileName);
}
