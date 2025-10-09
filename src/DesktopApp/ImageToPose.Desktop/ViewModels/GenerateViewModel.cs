using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class GenerateViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string _extendedPoseDescription = string.Empty;

    [ObservableProperty]
    private string _generatedJson = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasGenerated;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public GenerateViewModel(
        WizardViewModel wizard,
        IOpenAIService openAIService,
        IFileService fileService)
    {
        _wizard = wizard;
        _openAIService = openAIService;
        _fileService = fileService;
    }

    [RelayCommand]
    private async Task GeneratePoseRig()
    {
        if (string.IsNullOrWhiteSpace(ExtendedPoseDescription))
        {
            ErrorMessage = "No pose description available";
            return;
        }

        IsGenerating = true;
        ErrorMessage = string.Empty;
        GeneratedJson = string.Empty;
        HasGenerated = false;

        try
        {
            var result = await _openAIService.GenerateRigAsync(ExtendedPoseDescription);
            
            // Convert to pretty JSON
            var jsonObject = new Dictionary<string, double[]>();
            foreach (var bone in result.Bones)
            {
                jsonObject[bone.BoneName] = new[] { bone.X, bone.Y, bone.Z };
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            GeneratedJson = JsonSerializer.Serialize(new { POSE_DEGREES = jsonObject }, options);
            HasGenerated = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error generating pose rig: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(GeneratedJson))
            return;

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(GeneratedJson);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to copy to clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveJson()
    {
        if (string.IsNullOrWhiteSpace(GeneratedJson))
            return;

        try
        {
            await _fileService.SaveJsonFileAsync(GeneratedJson, "pose_rotations.json");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenBlenderWorkflowDocs()
    {
        try
        {
            // Try to open the docs file from the repository
            var docsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "BlenderWorkflow.md");
            
            if (File.Exists(docsPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(docsPath),
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public bool CanGeneratePoseRig => !string.IsNullOrWhiteSpace(ExtendedPoseDescription) && !IsGenerating;
    public bool CanCopyToClipboard => HasGenerated && !string.IsNullOrWhiteSpace(GeneratedJson);
    public bool CanSaveJson => HasGenerated && !string.IsNullOrWhiteSpace(GeneratedJson);
}
