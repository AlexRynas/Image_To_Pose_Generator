using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;
using ImageToPose.Core.Models;
using ImageToPose.Desktop.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class InputViewModel : ViewModelBase
{
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
        IOpenAIErrorHandler errorHandler)
    {
        _wizard = wizard;
        _openAIService = openAIService;
        _fileService = fileService;
        _errorHandler = errorHandler;

        ModeVM = new ModeSelectionViewModel(openAIService, priceEstimator, errorHandler);
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
        await ModeVM.RecomputeEstimates(ImagePath, BuildUserAnchorsBlock());
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

        if (!HasRequiredAnchors())
        {
            ErrorMessage = "Please fill all required fields: Facing, Left/Right Hand, Left/Right Foot";
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

            // Pass to review view model
            _wizard.ReviewViewModel.ExtendedPoseText = result.Text;

            // Navigate to review step
            _wizard.NavigateToStep(WizardStep.Review);
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
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
