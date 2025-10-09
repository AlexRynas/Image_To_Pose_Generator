using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class InputViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private Bitmap? _imagePreview;

    [ObservableProperty]
    private string _roughPoseText = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public string ExtendedPoseDescription { get; set; } = string.Empty;

    public InputViewModel(
        WizardViewModel wizard,
        IOpenAIService openAIService,
        IFileService fileService)
    {
        _wizard = wizard;
        _openAIService = openAIService;
        _fileService = fileService;
    }

    [RelayCommand]
    private async Task SelectImage()
    {
        var path = await _fileService.OpenImageFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            ImagePath = path;
            
            // Load preview
            try
            {
                ImagePreview = new Bitmap(path);
            }
            catch
            {
                ImagePreview = null;
                ErrorMessage = "Failed to load image preview";
            }
        }
    }

    [RelayCommand]
    private async Task ProcessImageAndPose()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            ErrorMessage = "Please select an image first";
            return;
        }

        if (string.IsNullOrWhiteSpace(RoughPoseText))
        {
            ErrorMessage = "Please enter a rough pose description";
            return;
        }

        IsProcessing = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _openAIService.AnalyzePoseAsync(ImagePath, RoughPoseText);
            ExtendedPoseDescription = result.Text;
            
            // Pass to review view model
            _wizard.ReviewViewModel.ExtendedPoseText = result.Text;
            
            // Navigate to review step
            _wizard.NavigateToStep(WizardStep.Review);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error processing image: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public bool CanProcessImageAndPose => 
        !string.IsNullOrWhiteSpace(ImagePath) && 
        !string.IsNullOrWhiteSpace(RoughPoseText) && 
        !IsProcessing;
}
