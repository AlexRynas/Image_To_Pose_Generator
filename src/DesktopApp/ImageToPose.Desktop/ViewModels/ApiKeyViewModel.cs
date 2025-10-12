using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.ViewModels;

public partial class ApiKeyViewModel : ViewModelBase
{
    private readonly ILogger<ApiKeyViewModel>? _logger;
    private readonly WizardViewModel _wizard;
    private readonly ISettingsService _settingsService;
    private readonly IOpenAIService _openAIService;
    private readonly IOpenAIErrorHandler _errorHandler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanValidateAndContinue))]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanValidateAndContinue))]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ApiKeyViewModel(
        WizardViewModel wizard,
        ISettingsService settingsService,
        IOpenAIService openAIService,
        IOpenAIErrorHandler errorHandler,
        ILogger<ApiKeyViewModel>? logger = null)
    {
        _logger = logger;
        _wizard = wizard;
        _settingsService = settingsService;
        _openAIService = openAIService;
        _errorHandler = errorHandler;
        
        ApiKeyViewModelLogs.ViewModelInitialized(_logger);
    }

    [RelayCommand]
    private async Task ValidateAndContinue()
    {
        using var _ = _logger?.BeginScope(new Dictionary<string, object> { ["Operation"] = "ValidateApiKey" });
        
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "Please enter an API key";
            IsValid = false;
            ApiKeyViewModelLogs.EmptyApiKey(_logger);
            return;
        }

        IsValidating = true;
        ErrorMessage = string.Empty;
        IsValid = false;
        
        ApiKeyViewModelLogs.ValidatingApiKey(_logger);

        try
        {
            var isValid = await _openAIService.ValidateApiKeyAsync(ApiKey);
            
            if (isValid)
            {
                // Save the API key
                _settingsService.SetOpenAIOptions(new OpenAIOptions { ApiKey = ApiKey });
                IsValid = true;
                
                ApiKeyViewModelLogs.ApiKeyValidatedSuccessfully(_logger);
                
                // Trigger model resolution for the InputViewModel's ModeVM
                await _wizard.InputViewModel.ModeVM.ResolveModelAndRatesAsync();
                
                // Navigate to next step
                _wizard.NavigateToStep(WizardStep.Input);
            }
            else
            {
                ErrorMessage = "Invalid API key. Please check and try again.";
                IsValid = false;
                ApiKeyViewModelLogs.ApiKeyInvalid(_logger);
            }
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
            IsValid = false;
            ApiKeyViewModelLogs.ApiKeyValidationFailed(_logger, ex);
        }
        finally
        {
            IsValidating = false;
        }
    }

    [RelayCommand]
    private void OpenApiKeyLink()
    {
        ApiKeyViewModelLogs.OpeningApiKeyLink(_logger);
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ApiKeyViewModelLogs.OpenLinkFailed(_logger, ex);
            // Ignore errors opening browser
        }
    }

    public bool CanValidateAndContinue => !string.IsNullOrWhiteSpace(ApiKey) && !IsValidating;
}

internal static partial class ApiKeyViewModelLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ApiKeyViewModel initialized")]
    public static partial void ViewModelInitialized(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validating OpenAI API key")]
    public static partial void ValidatingApiKey(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key is empty")]
    public static partial void EmptyApiKey(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key validated successfully")]
    public static partial void ApiKeyValidatedSuccessfully(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key validation returned invalid")]
    public static partial void ApiKeyInvalid(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "API key validation failed with exception")]
    public static partial void ApiKeyValidationFailed(ILogger? logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Opening OpenAI API keys link")]
    public static partial void OpeningApiKeyLink(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to open API key link in browser")]
    public static partial void OpenLinkFailed(ILogger? logger, Exception exception);
}
