using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;

namespace ImageToPose.Desktop.ViewModels;

public partial class ModeSelectionViewModel : ViewModelBase
{
    private readonly IOpenAIService _openAIService;
    private readonly IPriceEstimator _priceEstimator;
    private readonly IOpenAIErrorHandler _errorHandler;
    private bool _suppressSelectionUpdate;

    [ObservableProperty]
    private OperatingMode _mode = OperatingMode.Balanced;

    [ObservableProperty]
    private string _resolvedModelId = "";

    [ObservableProperty]
    private StepCostEstimate _visionEstimate = new();

    [ObservableProperty]
    private StepCostEstimate _textEstimate = new();

    [ObservableProperty]
    private bool _pricingNeedsUpdate;

    [ObservableProperty]
    private string _modeDescription = ModeModelMap.GetModeDescription(OperatingMode.Balanced);

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private List<string> _availableModelIds = ModeModelMap.GetPriorityList(OperatingMode.Balanced)
        .Select(model => model.GetModelId())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    [ObservableProperty]
    private string? _selectedModelId;

    public ModeSelectionViewModel(IOpenAIService openAIService, IPriceEstimator priceEstimator, IOpenAIErrorHandler errorHandler)
    {
        _openAIService = openAIService;
        _priceEstimator = priceEstimator;
        _errorHandler = errorHandler;
        _openAIService.SelectedMode = _mode;
        SetSelectedModelIdSilently(AvailableModelIds.FirstOrDefault());
        _ = ResolveModelAndRatesAsync(SelectedModelId);
    }

    partial void OnModeChanged(OperatingMode value)
    {
        _openAIService.SelectedMode = value;
        ModeDescription = ModeModelMap.GetModeDescription(value);
        AvailableModelIds = ModeModelMap.GetPriorityList(value)
            .Select(model => model.GetModelId())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!ContainsModelId(SelectedModelId))
        {
            var defaultModelId = AvailableModelIds.FirstOrDefault();
            SetSelectedModelIdSilently(defaultModelId);
        }

        _ = ResolveModelAndRatesAsync(SelectedModelId);
    }

    [RelayCommand]
    private void RefreshPricing()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://platform.openai.com/docs/pricing",
                UseShellExecute = true
            });
        }
        catch { }
        PricingNeedsUpdate = false; // Mark as updated after user visits page
    }

    public async Task RecomputeEstimates(string? imagePath = null, string? roughText = null)
    {
        try
        {
            ErrorMessage = string.Empty;

            // Determine assumed output tokens by mode
            int assumedOut = Mode switch
            {
                OperatingMode.Budget => 300,
                OperatingMode.Balanced => 600,
                OperatingMode.Quality => 800,
                _ => 600
            };

            // resolve rates
            var rates = await _openAIService.GetResolvedModelRatesAsync();
            if (rates is null)
            {
                VisionEstimate = new StepCostEstimate();
                TextEstimate = new StepCostEstimate();
                return;
            }

            // Step 1: Vision: combined prompt (rough text placeholder); image optional
            var combinedPrompt = (roughText ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                VisionEstimate = await _priceEstimator.EstimateVisionAsync(rates, imagePath, combinedPrompt);
                // override with assumed output tokens per mode
                VisionEstimate = new StepCostEstimate
                {
                    InputTokens = VisionEstimate.InputTokens,
                    OutputTokens = assumedOut,
                    InputUsd = VisionEstimate.InputUsd,
                    OutputUsd = Math.Round(assumedOut / 1_000_000m * rates.OutputPerMillion, 6),
                    TotalUsd = VisionEstimate.InputUsd + Math.Round(assumedOut / 1_000_000m * rates.OutputPerMillion, 6)
                };
            }
            else
            {
                VisionEstimate = new StepCostEstimate();
            }

            // Step 2: Text
            TextEstimate = await _priceEstimator.EstimateTextAsync(rates, roughText ?? string.Empty, assumedOut);
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
        }
    }

    public async Task ResolveModelAndRatesAsync(string? preferredModelId = null)
    {
        try
        {
            ErrorMessage = string.Empty;
            var modelId = await _openAIService.ResolveModelAsync(preferredModelId);
            ResolvedModelId = modelId;
            if (ContainsModelId(modelId))
            {
                SetSelectedModelIdSilently(modelId);
            }
            _ = await _openAIService.GetResolvedModelRatesAsync();
        }
        catch (Exception ex)
        {
            ResolvedModelId = string.Empty;
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
        }
    }

    partial void OnSelectedModelIdChanged(string? value)
    {
        if (_suppressSelectionUpdate)
        {
            return;
        }

        if (string.Equals(value, ResolvedModelId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = ResolveModelAndRatesAsync(value);
    }

    private void SetSelectedModelIdSilently(string? modelId)
    {
        try
        {
            _suppressSelectionUpdate = true;
            SelectedModelId = modelId;
        }
        finally
        {
            _suppressSelectionUpdate = false;
        }
    }

    private bool ContainsModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        return AvailableModelIds.Any(id => string.Equals(id, modelId, StringComparison.OrdinalIgnoreCase));
    }

}
