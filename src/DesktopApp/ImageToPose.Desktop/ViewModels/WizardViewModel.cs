using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Services;
using ImageToPose.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.ViewModels;

public partial class WizardViewModel : ViewModelBase
{
    private readonly ILogger<WizardViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IOpenAIService _openAIService;
    private readonly IFileService _fileService;
    private readonly IPriceEstimator _priceEstimator;
    private readonly IOpenAIErrorHandler _errorHandler;

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
        ILogger<WizardViewModel> logger,
        ILoggerFactory loggerFactory,
        ISettingsService settingsService,
        IOpenAIService openAIService,
        IFileService fileService,
        IPriceEstimator priceEstimator,
        IOpenAIErrorHandler errorHandler)
    {
        _logger = logger;
        _settingsService = settingsService;
        _openAIService = openAIService;
        _fileService = fileService;
        _priceEstimator = priceEstimator;
        _errorHandler = errorHandler;

        WizardViewModelLogs.ViewModelInitialized(_logger);

        // Initialize step view models
        WelcomeViewModel = new WelcomeViewModel(this);
        ApiKeyViewModel = new ApiKeyViewModel(this, settingsService, openAIService, errorHandler, 
            loggerFactory.CreateLogger<ApiKeyViewModel>());
        InputViewModel = new InputViewModel(this, openAIService, fileService, priceEstimator, errorHandler,
            loggerFactory, loggerFactory.CreateLogger<InputViewModel>());
        ReviewViewModel = new ReviewViewModel(this);
        GenerateViewModel = new GenerateViewModel(this, openAIService, fileService, errorHandler,
            loggerFactory.CreateLogger<GenerateViewModel>());

        // Start with welcome
        NavigateToStep(WizardStep.Welcome);
    }

    public void NavigateToStep(WizardStep step)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["Step"] = step.ToString() });
        WizardViewModelLogs.NavigatingToStep(_logger, step.ToString());
        
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
        
        WizardViewModelLogs.NavigatedToStep(_logger, step.ToString());
    }

    [RelayCommand]
    private void NavigateBack()
    {
        WizardViewModelLogs.NavigatingBack(_logger, CurrentStep.ToString());
        
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

internal static partial class WizardViewModelLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "WizardViewModel initialized")]
    public static partial void ViewModelInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Navigating to step: {Step}")]
    public static partial void NavigatingToStep(ILogger logger, string step);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigated to step: {Step}")]
    public static partial void NavigatedToStep(ILogger logger, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "Navigating back from step: {CurrentStep}")]
    public static partial void NavigatingBack(ILogger logger, string currentStep);
}