using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;
using Microsoft.Extensions.Logging;

namespace ImageToPose.Desktop.ViewModels;

public partial class ModeSelectionViewModel : ViewModelBase
{
    private readonly ILogger<ModeSelectionViewModel>? _logger;
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

    public ModeSelectionViewModel(IOpenAIService openAIService, IPriceEstimator priceEstimator, IOpenAIErrorHandler errorHandler, ILogger<ModeSelectionViewModel>? logger = null)
    {
        _logger = logger;
        _openAIService = openAIService;
        _priceEstimator = priceEstimator;
        _errorHandler = errorHandler;
        _openAIService.SelectedMode = _mode;
        SetSelectedModelIdSilently(AvailableModelIds.FirstOrDefault());
        
        ModeSelectionViewModelLogs.ViewModelInitialized(_logger, _mode.ToString());
        _ = ResolveModelAndRatesAsync(SelectedModelId);
    }

    partial void OnModeChanged(OperatingMode value)
    {
        ModeSelectionViewModelLogs.ModeChanged(_logger, value.ToString());
        
        _openAIService.SelectedMode = value;
        ModeDescription = ModeModelMap.GetModeDescription(value);
        AvailableModelIds = ModeModelMap.GetPriorityList(value)
            .Select(model => model.GetModelId())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!ContainsModelId(SelectedModelId))
        {
            var defaultModelId = AvailableModelIds.FirstOrDefault();
            ModeSelectionViewModelLogs.ModelNotAvailableInMode(_logger, SelectedModelId ?? "null", value.ToString(), defaultModelId ?? "null");
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
        using var _ = _logger?.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "RecomputeEstimates",
            ["Mode"] = Mode.ToString(),
            ["HasImage"] = !string.IsNullOrWhiteSpace(imagePath)
        });
        
        ModeSelectionViewModelLogs.RecomputingEstimates(_logger, Mode.ToString(), !string.IsNullOrWhiteSpace(imagePath));
        
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
                ModeSelectionViewModelLogs.NoRatesAvailable(_logger);
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

            // Step 2: Text - now also uses vision if image is present
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                // GenerateRigAsync now also uses vision, so estimate as vision request
                TextEstimate = await _priceEstimator.EstimateVisionAsync(rates, imagePath, roughText ?? string.Empty);
                // override with assumed output tokens per mode
                TextEstimate = new StepCostEstimate
                {
                    InputTokens = TextEstimate.InputTokens,
                    OutputTokens = assumedOut,
                    InputUsd = TextEstimate.InputUsd,
                    OutputUsd = Math.Round(assumedOut / 1_000_000m * rates.OutputPerMillion, 6),
                    TotalUsd = TextEstimate.InputUsd + Math.Round(assumedOut / 1_000_000m * rates.OutputPerMillion, 6)
                };
            }
            else
            {
                TextEstimate = await _priceEstimator.EstimateTextAsync(rates, roughText ?? string.Empty, assumedOut);
            }
            
            ModeSelectionViewModelLogs.EstimatesComputed(_logger, VisionEstimate.TotalUsd, TextEstimate.TotalUsd);
        }
        catch (Exception ex)
        {
            var info = _errorHandler.Translate(ex);
            ErrorMessage = info.Message;
            ModeSelectionViewModelLogs.EstimatesFailed(_logger, ex);
        }
    }

    public async Task ResolveModelAndRatesAsync(string? preferredModelId = null)
    {
        using var scope = _logger?.BeginScope(new Dictionary<string, object> 
        { 
            ["Operation"] = "ResolveModel",
            ["PreferredModel"] = preferredModelId ?? "default"
        });
        
        ModeSelectionViewModelLogs.ResolvingModel(_logger, preferredModelId ?? "default");
        
        try
        {
            ErrorMessage = string.Empty;
            var modelId = await _openAIService.ResolveModelAsync(preferredModelId);
            ResolvedModelId = modelId;
            
            ModeSelectionViewModelLogs.ModelResolved(_logger, modelId);
            
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
            ModeSelectionViewModelLogs.ModelResolutionFailed(_logger, ex);
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

internal static partial class ModeSelectionViewModelLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ModeSelectionViewModel initialized with mode: {Mode}")]
    public static partial void ViewModelInitialized(ILogger? logger, string mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Operating mode changed to: {Mode}")]
    public static partial void ModeChanged(ILogger? logger, string mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Selected model {PreviousModel} not available in mode {Mode}, switching to {NewModel}")]
    public static partial void ModelNotAvailableInMode(ILogger? logger, string previousModel, string mode, string newModel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recomputing estimates for mode {Mode}, hasImage: {HasImage}")]
    public static partial void RecomputingEstimates(ILogger? logger, string mode, bool hasImage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No pricing rates available for estimate computation")]
    public static partial void NoRatesAvailable(ILogger? logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Estimates computed - Vision: ${VisionCost:F6}, Text: ${TextCost:F6}")]
    public static partial void EstimatesComputed(ILogger? logger, decimal visionCost, decimal textCost);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to compute estimates")]
    public static partial void EstimatesFailed(ILogger? logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolving model, preferred: {PreferredModel}")]
    public static partial void ResolvingModel(ILogger? logger, string preferredModel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Model resolved: {ModelId}")]
    public static partial void ModelResolved(ILogger? logger, string modelId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Model resolution failed")]
    public static partial void ModelResolutionFailed(ILogger? logger, Exception exception);
}
