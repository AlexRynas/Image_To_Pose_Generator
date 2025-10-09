using ImageToPose.Core.Models;

namespace ImageToPose.Core.Services;

public interface IPriceEstimator
{
    Task<PricingModelRates?> GetRatesAsync(string modelId, CancellationToken ct = default);
    Task<StepCostEstimate> EstimateVisionAsync(PricingModelRates rates, string imagePath, string combinedPrompt, CancellationToken ct = default);
    Task<StepCostEstimate> EstimateTextAsync(PricingModelRates rates, string inputText, int assumedOutputTokens, CancellationToken ct = default);
}
