using System.Text.Json;
using ImageToPose.Core.Models;
using SharpToken;
using SixLabors.ImageSharp;

namespace ImageToPose.Core.Services;

public class JsonPricingEstimator : IPriceEstimator
{
    private readonly Lazy<GptEncoding?> _encoding;
    private readonly string _configDir;
    private readonly string _pricingPath;

    public JsonPricingEstimator()
    {
        _encoding = new Lazy<GptEncoding?>(() =>
        {
            try { return GptEncoding.GetEncoding("cl100k_base"); } catch { return null; }
        });

        // config directory relative to executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _configDir = Path.Combine(baseDir, "config");
        _pricingPath = Path.Combine(_configDir, "pricing.json");
    }

    public async Task<PricingModelRates?> GetRatesAsync(string modelId, CancellationToken ct = default)
    {
        var dict = await EnsureAndLoadPricingAsync(ct);
        if (dict.TryGetValue(modelId, out var rates))
        {
            rates.ModelId = modelId;
            return rates;
        }
        return null;
    }

    public async Task<StepCostEstimate> EstimateVisionAsync(PricingModelRates rates, string imagePath, string combinedPrompt, CancellationToken ct = default)
    {
        // image tokens + text tokens
        var (tiles, imageTokens) = await EstimateImageTokensAsync(imagePath, ct);
        int textTokens = CountTextTokens(combinedPrompt);
        int inputTokens = Math.Max(1, imageTokens + textTokens);

        // Assume Balanced output if we don't have external info; caller can pass assumed separately via EstimateTextAsync
        int assumedOutputTokens = 600; // default

        return ComputeCosts(rates, inputTokens, assumedOutputTokens);
    }

    public Task<StepCostEstimate> EstimateTextAsync(PricingModelRates rates, string inputText, int assumedOutputTokens, CancellationToken ct = default)
    {
        int inputTokens = CountTextTokens(inputText);
        var estimate = ComputeCosts(rates, inputTokens, assumedOutputTokens);
        return Task.FromResult(estimate);
    }

    private async Task<Dictionary<string, PricingModelRates>> EnsureAndLoadPricingAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_configDir))
            Directory.CreateDirectory(_configDir);

        if (!File.Exists(_pricingPath))
        {
            var seed = new Dictionary<string, object>
            {
                ["gpt-4.1-nano"] = new { input_per_million = 0.10m, output_per_million = 0.40m },
                ["gpt-4.1-mini"] = new { input_per_million = 0.40m, output_per_million = 1.60m },
                ["gpt-4.1"]      = new { input_per_million = 2.50m, output_per_million = 10.00m },
                ["o4-mini"]      = new { input_per_million = 3.00m, output_per_million = 12.00m },
                ["_disclaimer"]  = "Approximate defaults. Please verify on https://openai.com/api/pricing"
            };
            var jsonSeed = JsonSerializer.Serialize(seed, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_pricingPath, jsonSeed, ct);
        }

        using var fs = File.OpenRead(_pricingPath);
        using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
        var result = new Dictionary<string, PricingModelRates>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name.StartsWith("_")) continue;
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                var input = prop.Value.TryGetProperty("input_per_million", out var inNode) ? inNode.GetDecimal() : 0m;
                var output = prop.Value.TryGetProperty("output_per_million", out var outNode) ? outNode.GetDecimal() : 0m;
                result[prop.Name] = new PricingModelRates
                {
                    ModelId = prop.Name,
                    InputPerMillion = input,
                    OutputPerMillion = output
                };
            }
        }
        return result;
    }

    private static decimal CostUsd(int tokens, decimal perMillion)
        => Math.Round(tokens / 1_000_000m * perMillion, 6, MidpointRounding.AwayFromZero);

    private StepCostEstimate ComputeCosts(PricingModelRates rates, int inputTokens, int outputTokens)
    {
        var inputUsd = CostUsd(inputTokens, rates.InputPerMillion);
        var outputUsd = CostUsd(outputTokens, rates.OutputPerMillion);
        return new StepCostEstimate
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            InputUsd = inputUsd,
            OutputUsd = outputUsd,
            TotalUsd = inputUsd + outputUsd
        };
    }

    private int CountTextTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var enc = _encoding.Value;
        if (enc is not null)
        {
            try
            {
                return enc.Encode(text).Count;
            }
            catch { /* fallthrough */ }
        }
        // fallback: ~4 chars per token heuristic
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    private async Task<(int tiles, int imageTokens)> EstimateImageTokensAsync(string imagePath, CancellationToken ct)
    {
        // Use ImageSharp to read dimensions (cross-platform)
        int width = 0, height = 0;
        try
        {
            using var img = await Image.LoadAsync(imagePath, ct);
            width = img.Width;
            height = img.Height;
        }
        catch
        {
            // fallback: assume one tile
        }

        int tilesX = (int)Math.Ceiling((width > 0 ? width : 512) / 512.0);
        int tilesY = (int)Math.Ceiling((height > 0 ? height : 512) / 512.0);
        int tiles = Math.Max(1, tilesX * tilesY);
        int baseTokens = 70;
        int tileTokens = 140 * tiles;
        int total = baseTokens + tileTokens;
        return (tiles, total);
    }
}
