using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;
using ImageToPose.Core.Models;
using ImageToPose.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.ViewModels;

public partial class InputViewModel : ViewModelBase
{
    private readonly ILogger<InputViewModel>? _logger;
    private readonly WizardViewModel _wizard;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;
    private readonly IOpenAIErrorHandler _errorHandler;

    public ModeSelectionViewModel ModeVM { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private Bitmap? _imagePreview;

    // === Anchor fields ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _leftHand = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _rightHand = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _leftFoot = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _rightFoot = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _facing = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProcessImageAndPose))]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public string ExtendedPoseDescription { get; set; } = string.Empty;

    public InputViewModel(
        WizardViewModel wizard,
        IOpenAIService openAIService,
        IFileService fileService,
        IPriceEstimator priceEstimator,
        IOpenAIErrorHandler errorHandler,
        ILoggerFactory loggerFactory,
        ILogger<InputViewModel>? logger = null)
    {
        _logger = logger;
        _wizard = wizard;
        _openAIService = openAIService;
        _fileService = fileService;
        _errorHandler = errorHandler;

        ModeVM = new ModeSelectionViewModel(openAIService, priceEstimator, errorHandler, 
            loggerFactory.CreateLogger<ModeSelectionViewModel>());
        
        InputViewModelLogs.ViewModelInitialized(_logger);
        
        // Initialize mode to Balanced and resolve model/rates
        _ = ModeVM.ResolveModelAndRatesAsync();
    }

    partial void OnImagePathChanged(string value) => _ = RecomputeEstimates();
    partial void OnLeftHandChanged(string value) => _ = RecomputeEstimates();
    partial void OnRightHandChanged(string value) => _ = RecomputeEstimates();
    partial void OnLeftFootChanged(string value) => _ = RecomputeEstimates();
    partial void OnRightFootChanged(string value) => _ = RecomputeEstimates();
    partial void OnFacingChanged(string value) => _ = RecomputeEstimates();
    partial void OnNotesChanged(string value) => _ = RecomputeEstimates();

    private string BuildUserAnchorsBlock()
    {
        // Compose block consistent with the analyze prompt
        var input = new PoseInput
        {
            LeftHand = LeftHand,
            RightHand = RightHand,
            LeftFoot = LeftFoot,
            RightFoot = RightFoot,
            Facing = Facing,
            Notes = Notes
        };
        return input.ToUserAnchorsBlock();
    }

    private async Task RecomputeEstimates()
    {
        using var _ = _logger?.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "RecomputeEstimates",
            ["HasImage"] = !string.IsNullOrEmpty(ImagePath)
        });
        
        InputViewModelLogs.RecomputingEstimates(_logger, !string.IsNullOrEmpty(ImagePath));
        
        await ModeVM.RecomputeEstimates(ImagePath, BuildUserAnchorsBlock());
    }

    [RelayCommand]
    private async Task SelectImage()
    {
        InputViewModelLogs.SelectingImage(_logger);
        
        var path = await _fileService.OpenImageFileAsync();
        if (!string.IsNullOrEmpty(path))
        {
            ImagePath = path;
            InputViewModelLogs.ImageSelected(_logger, path);

            // Load preview
            try
            {
                ImagePreview = new Bitmap(path);
                InputViewModelLogs.ImagePreviewLoaded(_logger);
            }
            catch (Exception ex)
            {
                ImagePreview = null;
                ErrorMessage = "Failed to load image preview";
                InputViewModelLogs.ImagePreviewFailed(_logger, ex);
            }
        }
        else
        {
            InputViewModelLogs.ImageSelectionCanceled(_logger);
        }
    }

    [RelayCommand]
    private async Task ProcessImageAndPose()
    {
        using var _ = _logger?.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "ProcessImageAndPose",
            ["ImagePath"] = ImagePath
        });
        
        InputViewModelLogs.ProcessingImageAndPose(_logger, ImagePath);
        
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            ErrorMessage = "Please select an image first";
            InputViewModelLogs.NoImageSelected(_logger);
            return;
        }

        if (!HasRequiredAnchors())
        {
            ErrorMessage = "Please fill all required fields: Facing, Left/Right Hand, Left/Right Foot";
            InputViewModelLogs.MissingRequiredAnchors(_logger);
            return;
        }

        IsProcessing = true;
        ErrorMessage = string.Empty;

        try
        {
            var input = new PoseInput
            {
                ImagePath = ImagePath,
                LeftHand = LeftHand,
                RightHand = RightHand,
                LeftFoot = LeftFoot,
                RightFoot = RightFoot,
                Facing = Facing,
                Notes = Notes
            };

            var result = await _openAIService.AnalyzePoseAsync(input);
            ExtendedPoseDescription = result.Text;

            InputViewModelLogs.PoseAnalyzed(_logger, result.Text.Length);

            // Pass to review view model
            _wizard.ReviewViewModel.ExtendedPoseText = result.Text;

            // Navigate to review step
            _wizard.NavigateToStep(WizardStep.Review);
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
            InputViewModelLogs.ProcessingFailed(_logger, ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool HasRequiredAnchors() =>
        !string.IsNullOrWhiteSpace(LeftHand) &&
        !string.IsNullOrWhiteSpace(RightHand) &&
        !string.IsNullOrWhiteSpace(LeftFoot) &&
        !string.IsNullOrWhiteSpace(RightFoot) &&
        !string.IsNullOrWhiteSpace(Facing);

    public bool CanProcessImageAndPose =>
        !string.IsNullOrWhiteSpace(ImagePath) &&
        HasRequiredAnchors() &&
        !IsProcessing;
}

/// <summary>
/// Strongly-typed logging messages for InputViewModel.
/// </summary>
static partial class InputViewModelLogs
{
    [LoggerMessage(LogLevel.Debug, "InputViewModel initialized")]
    public static partial void ViewModelInitialized(ILogger? logger);

    [LoggerMessage(LogLevel.Debug, "Selecting image via file dialog")]
    public static partial void SelectingImage(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "Image selected: {ImagePath}")]
    public static partial void ImageSelected(ILogger? logger, string imagePath);

    [LoggerMessage(LogLevel.Debug, "Image preview loaded successfully")]
    public static partial void ImagePreviewLoaded(ILogger? logger);

    [LoggerMessage(LogLevel.Warning, "Failed to load image preview")]
    public static partial void ImagePreviewFailed(ILogger? logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Image selection canceled by user")]
    public static partial void ImageSelectionCanceled(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "Processing image and pose: {ImagePath}")]
    public static partial void ProcessingImageAndPose(ILogger? logger, string imagePath);

    [LoggerMessage(LogLevel.Warning, "No image selected for processing")]
    public static partial void NoImageSelected(ILogger? logger);

    [LoggerMessage(LogLevel.Warning, "Missing required anchor fields")]
    public static partial void MissingRequiredAnchors(ILogger? logger);

    [LoggerMessage(LogLevel.Information, "Pose analyzed successfully. Description length: {DescriptionLength}")]
    public static partial void PoseAnalyzed(ILogger? logger, int descriptionLength);

    [LoggerMessage(LogLevel.Error, "Failed to process image and pose")]
    public static partial void ProcessingFailed(ILogger? logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Recomputing estimates. HasImage: {HasImage}")]
    public static partial void RecomputingEstimates(ILogger? logger, bool hasImage);
}
