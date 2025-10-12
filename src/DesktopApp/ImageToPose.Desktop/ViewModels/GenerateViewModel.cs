using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;
using ImageToPose.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.ViewModels;

public partial class GenerateViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;
    private readonly IOpenAIErrorHandler _errorHandler;
    private readonly ILogger<GenerateViewModel>? _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGeneratePoseRig))]
    private string _extendedPoseDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCopyToClipboard))]
    [NotifyPropertyChangedFor(nameof(CanSaveJson))]
    private string _generatedJson = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGeneratePoseRig))]
    private bool _isGenerating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCopyToClipboard))]
    [NotifyPropertyChangedFor(nameof(CanSaveJson))]
    private bool _hasGenerated;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public GenerateViewModel(
        WizardViewModel wizard,
        IOpenAIService openAIService,
        IFileService fileService,
        IOpenAIErrorHandler errorHandler,
        ILogger<GenerateViewModel>? logger = null)
    {
        _wizard = wizard;
        _openAIService = openAIService;
        _fileService = fileService;
        _errorHandler = errorHandler;
        _logger = logger;
        
        GenerateViewModelLogs.ViewModelInitialized(_logger);
    }

    [RelayCommand]
    private async Task GeneratePoseRig()
    {
        using var _ = _logger?.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "GeneratePoseRig",
            ["DescriptionLength"] = ExtendedPoseDescription?.Length ?? 0
        });
        
        GenerateViewModelLogs.GeneratingPoseRig(_logger, ExtendedPoseDescription?.Length ?? 0);
        
        if (string.IsNullOrWhiteSpace(ExtendedPoseDescription))
        {
            ErrorMessage = "No pose description available";
            GenerateViewModelLogs.NoPoseDescription(_logger);
            return;
        }

        IsGenerating = true;
        ErrorMessage = string.Empty;
        GeneratedJson = string.Empty;
        HasGenerated = false;

        try
        {
            var result = await _openAIService.GenerateRigAsync(ExtendedPoseDescription);
            GenerateViewModelLogs.RigGenerated(_logger, result.Bones.Count);
            
            // Convert to pretty JSON
            var jsonObject = new Dictionary<string, double[]>();
            foreach (var bone in result.Bones)
            {
                jsonObject[bone.BoneName] = new[] { bone.X, bone.Y, bone.Z };
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            GeneratedJson = JsonSerializer.Serialize(new { POSE_DEGREES = jsonObject }, options);
            HasGenerated = true;
            
            GenerateViewModelLogs.JsonSerialized(_logger, GeneratedJson.Length);
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
            GenerateViewModelLogs.GenerationFailed(_logger, ex);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task CopyToClipboard()
    {
        GenerateViewModelLogs.CopyingToClipboard(_logger);
        
        if (string.IsNullOrWhiteSpace(GeneratedJson))
        {
            GenerateViewModelLogs.NoJsonToCopy(_logger);
            return;
        }

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(GeneratedJson);
                    GenerateViewModelLogs.CopiedToClipboard(_logger, GeneratedJson.Length);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to copy to clipboard: {ex.Message}";
            GenerateViewModelLogs.CopyToClipboardFailed(_logger, ex);
        }
    }

    [RelayCommand]
    private async Task SaveJson()
    {
        GenerateViewModelLogs.SavingJson(_logger);
        
        if (string.IsNullOrWhiteSpace(GeneratedJson))
        {
            GenerateViewModelLogs.NoJsonToSave(_logger);
            return;
        }

        try
        {
            await _fileService.SaveJsonFileAsync(GeneratedJson, "pose_rotations.json");
            GenerateViewModelLogs.JsonSaved(_logger);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save file: {ex.Message}";
            GenerateViewModelLogs.SaveJsonFailed(_logger, ex);
        }
    }

    [RelayCommand]
    private void OpenBlenderWorkflowDocs()
    {
        GenerateViewModelLogs.OpeningWorkflowDocs(_logger);
        
        try
        {
            // Start from the base directory and search upward for the repository root
            var docsPath = FindDocsFile();
            
            if (!string.IsNullOrEmpty(docsPath) && File.Exists(docsPath))
            {
                var fullPath = Path.GetFullPath(docsPath);
                GenerateViewModelLogs.OpeningDocsFile(_logger, fullPath);
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }
            else
            {
                GenerateViewModelLogs.DocsFileNotFound(_logger, docsPath ?? "unknown");
            }
        }
        catch (Exception ex)
        {
            GenerateViewModelLogs.OpenDocsFailed(_logger, ex);
        }
    }

    private static string? FindDocsFile()
    {
        // Start from the application's base directory
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        
        // Search upward for the docs folder (max 10 levels to prevent infinite loop)
        for (int i = 0; i < 10 && currentDir != null; i++)
        {
            var docsDir = Path.Combine(currentDir.FullName, "docs");
            var docsFile = Path.Combine(docsDir, "BlenderWorkflow.md");
            
            if (File.Exists(docsFile))
            {
                return docsFile;
            }
            
            currentDir = currentDir.Parent;
        }
        
        return null;
    }

    public bool CanGeneratePoseRig => !string.IsNullOrWhiteSpace(ExtendedPoseDescription) && !IsGenerating;
    public bool CanCopyToClipboard => HasGenerated && !string.IsNullOrWhiteSpace(GeneratedJson);
    public bool CanSaveJson => HasGenerated && !string.IsNullOrWhiteSpace(GeneratedJson);
}

/// <summary>
/// Strongly-typed logging messages for GenerateViewModel.
/// </summary>
static partial class GenerateViewModelLogs
{
    [LoggerMessage(LogLevel.Debug, "GenerateViewModel initialized")]
    public static partial void ViewModelInitialized(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "Generating pose rig from description (length: {DescriptionLength})")]
    public static partial void GeneratingPoseRig(ILogger? logger, int descriptionLength);

    [LoggerMessage(LogLevel.Warning, "No pose description available for rig generation")]
    public static partial void NoPoseDescription(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "Pose rig generated successfully with {BoneCount} bones")]
    public static partial void RigGenerated(ILogger? logger, int boneCount);

    [LoggerMessage(LogLevel.Debug, "JSON serialized successfully (length: {JsonLength})")]
    public static partial void JsonSerialized(ILogger? logger, int jsonLength);

    [LoggerMessage(LogLevel.Error, "Failed to generate pose rig")]
    public static partial void GenerationFailed(ILogger? logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Copying JSON to clipboard")]
    public static partial void CopyingToClipboard(ILogger? logger);

    [LoggerMessage(LogLevel.Warning, "No JSON available to copy")]
    public static partial void NoJsonToCopy(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "JSON copied to clipboard (length: {JsonLength})")]
    public static partial void CopiedToClipboard(ILogger? logger, int jsonLength);

    [LoggerMessage(LogLevel.Error, "Failed to copy JSON to clipboard")]
    public static partial void CopyToClipboardFailed(ILogger? logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Saving JSON to file")]
    public static partial void SavingJson(ILogger? logger);

    [LoggerMessage(LogLevel.Warning, "No JSON available to save")]
    public static partial void NoJsonToSave(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "JSON saved successfully")]
    public static partial void JsonSaved(ILogger? logger);

    [LoggerMessage(LogLevel.Error, "Failed to save JSON file")]
    public static partial void SaveJsonFailed(ILogger? logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Opening Blender workflow documentation")]
    public static partial void OpeningWorkflowDocs(ILogger? logger);

    [LoggerMessage(LogLevel.Debug, "Opening documentation file: {FilePath}")]
    public static partial void OpeningDocsFile(ILogger? logger, string filePath);

    [LoggerMessage(LogLevel.Warning, "Documentation file not found: {DocsPath}")]
    public static partial void DocsFileNotFound(ILogger? logger, string docsPath);

    [LoggerMessage(LogLevel.Warning, "Failed to open documentation file")]
    public static partial void OpenDocsFailed(ILogger? logger, Exception ex);
}
