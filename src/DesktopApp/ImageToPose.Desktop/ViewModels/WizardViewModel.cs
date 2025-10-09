using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class WizardViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private WizardStep _currentStep = WizardStep.Welcome;

    [ObservableProperty]
    private ViewModelBase? _currentStepViewModel;

    public WelcomeViewModel WelcomeViewModel { get; }
    public ApiKeyViewModel ApiKeyViewModel { get; }
    public InputViewModel InputViewModel { get; }
    public ReviewViewModel ReviewViewModel { get; }
    public GenerateViewModel GenerateViewModel { get; }

    public WizardViewModel(
        ISettingsService settingsService,
        IOpenAIService openAIService,
        IFileService fileService)
    {
        _settingsService = settingsService;
        _openAIService = openAIService;
        _fileService = fileService;

        // Initialize step view models
        WelcomeViewModel = new WelcomeViewModel(this);
        ApiKeyViewModel = new ApiKeyViewModel(this, settingsService, openAIService);
        InputViewModel = new InputViewModel(this, openAIService, fileService);
        ReviewViewModel = new ReviewViewModel(this);
        GenerateViewModel = new GenerateViewModel(this, openAIService, fileService);

        // Start with welcome
        NavigateToStep(WizardStep.Welcome);
    }

    public void NavigateToStep(WizardStep step)
    {
        CurrentStep = step;
        CurrentStepViewModel = step switch
        {
            WizardStep.Welcome => WelcomeViewModel,
            WizardStep.ApiKey => ApiKeyViewModel,
            WizardStep.Input => InputViewModel,
            WizardStep.Review => ReviewViewModel,
            WizardStep.Generate => GenerateViewModel,
            _ => WelcomeViewModel
        };
    }

    [RelayCommand]
    private void NavigateBack()
    {
        var previousStep = CurrentStep switch
        {
            WizardStep.ApiKey => WizardStep.Welcome,
            WizardStep.Input => WizardStep.ApiKey,
            WizardStep.Review => WizardStep.Input,
            WizardStep.Generate => WizardStep.Review,
            _ => CurrentStep
        };

        if (previousStep != CurrentStep)
        {
            NavigateToStep(previousStep);
        }
    }

    public bool CanNavigateBack => CurrentStep != WizardStep.Welcome;
}
