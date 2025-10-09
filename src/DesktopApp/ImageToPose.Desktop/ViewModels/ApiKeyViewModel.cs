using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class ApiKeyViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;
    private readonly ISettingsService _settingsService;
    private readonly IOpenAIService _openAIService;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ApiKeyViewModel(
        WizardViewModel wizard,
        ISettingsService settingsService,
        IOpenAIService openAIService)
    {
        _wizard = wizard;
        _settingsService = settingsService;
        _openAIService = openAIService;
    }

    [RelayCommand]
    private async Task ValidateAndContinue()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "Please enter an API key";
            IsValid = false;
            return;
        }

        IsValidating = true;
        ErrorMessage = string.Empty;
        IsValid = false;

        try
        {
            var isValid = await _openAIService.ValidateApiKeyAsync(ApiKey);
            
            if (isValid)
            {
                // Save the API key
                _settingsService.SetOpenAIOptions(new OpenAIOptions { ApiKey = ApiKey });
                IsValid = true;
                
                // Navigate to next step
                _wizard.NavigateToStep(WizardStep.Input);
            }
            else
            {
                ErrorMessage = "Invalid API key. Please check and try again.";
                IsValid = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error validating API key: {ex.Message}";
            IsValid = false;
        }
        finally
        {
            IsValidating = false;
        }
    }

    [RelayCommand]
    private void OpenApiKeyLink()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore errors opening browser
        }
    }

    public bool CanValidateAndContinue => !string.IsNullOrWhiteSpace(ApiKey) && !IsValidating;
}
