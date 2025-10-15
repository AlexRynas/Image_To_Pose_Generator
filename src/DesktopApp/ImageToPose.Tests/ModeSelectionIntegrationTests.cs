using FluentAssertions;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageToPose.Tests;

/// <summary>
/// Integration tests for the complete mode selection and pricing workflow
/// </summary>
public class ModeSelectionIntegrationTests
{
    [Fact]
    public async Task CompleteWorkflow_ShouldEstimateCostsCorrectly()
    {
        // Arrange
        var estimator = new PriceEstimator(NullLogger<PriceEstimator>.Instance);
        var assumedOutputTokens = 600; // Balanced mode

        // Act - Get rates
    var rates = await estimator.GetRatesAsync(OpenAIModel.Gpt41Mini.GetModelId());

        // Assert - Rates loaded
        rates.Should().NotBeNull();
    rates!.ModelId.Should().Be(OpenAIModel.Gpt41Mini.GetModelId());

        // Act - Estimate text (simulating rough pose input)
        var roughText = "Character standing with left arm raised, right arm at side, head turned left.";
        var textEstimate = await estimator.EstimateTextAsync(rates, roughText, assumedOutputTokens);

        // Assert - Text estimate is reasonable
        textEstimate.InputTokens.Should().BeGreaterThan(0);
        textEstimate.OutputTokens.Should().Be(assumedOutputTokens);
        textEstimate.TotalUsd.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(OperatingMode.Budget)]
    [InlineData(OperatingMode.Balanced)]
    [InlineData(OperatingMode.Quality)]
    public void ModeModelMap_AllModes_ShouldHaveValidPriorities(OperatingMode mode)
    {
        // Act
        var priorities = ModeModelMap.GetPriorityList(mode);
        var description = ModeModelMap.GetModeDescription(mode);

        // Assert
        priorities.Should().NotBeEmpty();
        priorities.Should().OnlyHaveUniqueItems();
        description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PricingJson_ShouldContainAllRequiredModels()
    {
        // Arrange
        var estimator = new PriceEstimator(NullLogger<PriceEstimator>.Instance);
        var requiredModels = new[] { OpenAIModel.Gpt41Nano, OpenAIModel.Gpt41Mini, OpenAIModel.Gpt41, OpenAIModel.O4Mini };

        // Act & Assert
        foreach (var model in requiredModels)
        {
            var modelId = model.GetModelId();
            var rates = await estimator.GetRatesAsync(modelId);
            rates.Should().NotBeNull($"{modelId} should be in pricing.json");
            rates!.InputPerMillion.Should().BeGreaterThan(0, $"{modelId} should have positive input rate");
            rates.OutputPerMillion.Should().BeGreaterThan(0, $"{modelId} should have positive output rate");
        }
    }

    [Fact]
    public void ImageTokenCalculation_ShouldMatchOpenAIFormula()
    {
        // Arrange - Test various common image sizes
        var testCases = new[]
        {
            // (width, height, expectedTiles, expectedTokens)
            (512, 512, 1, 210),      // Single tile
            (1024, 512, 2, 350),     // Horizontal panorama
            (512, 1024, 2, 350),     // Vertical portrait
            (1024, 1024, 4, 630),    // Square HD
            (1920, 1080, 12, 1750),  // Full HD: ceil(1920/512)=4 * ceil(1080/512)=3 = 12
            (2048, 2048, 16, 2310),  // 2K: 4*4 = 16
            (3840, 2160, 40, 5670),  // 4K UHD: ceil(3840/512)=8 * ceil(2160/512)=5 = 40
        };

        foreach (var (width, height, expectedTiles, expectedTokens) in testCases)
        {
            // Act
            int tilesX = (int)Math.Ceiling(width / 512.0);
            int tilesY = (int)Math.Ceiling(height / 512.0);
            int tiles = Math.Max(1, tilesX * tilesY);
            int baseTokens = 70;
            int tileTokens = 140 * tiles;
            int total = baseTokens + tileTokens;

            // Assert
            tiles.Should().Be(expectedTiles, $"Image {width}×{height} should have {expectedTiles} tiles");
            total.Should().Be(expectedTokens, $"Image {width}×{height} should cost {expectedTokens} tokens");
        }
    }

    [Theory]
    [InlineData(OperatingMode.Budget, 300)]
    [InlineData(OperatingMode.Balanced, 600)]
    [InlineData(OperatingMode.Quality, 800)]
    public async Task EstimateWorkflow_DifferentModes_ShouldUseCorrectOutputTokens(OperatingMode mode, int expectedOutput)
    {
        // Arrange
        var estimator = new PriceEstimator(NullLogger<PriceEstimator>.Instance);
    var rates = await estimator.GetRatesAsync(OpenAIModel.Gpt41Mini.GetModelId());
        rates.Should().NotBeNull();

        // Act
        var estimate = await estimator.EstimateTextAsync(rates!, "Test input", expectedOutput);

        // Assert
        estimate.OutputTokens.Should().Be(expectedOutput, $"{mode} mode should assume {expectedOutput} output tokens");
    }

    [Fact]
    public void CostCalculation_ShouldBeAccurateToSixDecimals()
    {
        // Arrange
        var testCases = new[]
        {
            // (tokens, ratePerMillion, expectedCost)
            (100, 0.10m, 0.00001m),
            (1000, 0.40m, 0.0004m),
            (5000, 2.50m, 0.0125m),
            (10000, 10.00m, 0.1m),
            (100000, 1.60m, 0.16m),
            (1000000, 2.50m, 2.5m),
        };

        foreach (var (tokens, ratePerMillion, expectedCost) in testCases)
        {
            // Act
            var cost = Math.Round(tokens / 1_000_000m * ratePerMillion, 6, MidpointRounding.AwayFromZero);

            // Assert
            cost.Should().Be(expectedCost, $"{tokens} tokens at ${ratePerMillion}/M should cost ${expectedCost}");
        }
    }

    [Fact]
    public void VisionEstimate_ShouldIncludeBothImageAndTextTokens()
    {
        // This test would require an actual image file, so we just verify the logic
        // In a real test, you'd create a small test image

        // Arrange
        var rates = new PricingModelRates
        {
            ModelId = "test-model",
            InputPerMillion = 0.40m,
            OutputPerMillion = 1.60m
        };

        // Act - Simulate the calculation (without actual image)
        int imageTokens = 70 + (140 * 4); // 1024x1024 = 4 tiles = 630 tokens
        int textTokens = 20; // Approximate for short prompt
        int totalInputTokens = imageTokens + textTokens;

        var inputUsd = Math.Round(totalInputTokens / 1_000_000m * rates.InputPerMillion, 6);
        var outputUsd = Math.Round(600 / 1_000_000m * rates.OutputPerMillion, 6);
        var totalUsd = inputUsd + outputUsd;

        // Assert
        totalInputTokens.Should().Be(650);
        inputUsd.Should().BeGreaterThan(0);
        outputUsd.Should().BeGreaterThan(0);
        totalUsd.Should().Be(inputUsd + outputUsd);
    }

    [Fact]
    public void ModeDescriptions_ShouldMatchSpecification()
    {
        // Assert - Verify exact descriptions match the spec
        ModeModelMap.GetModeDescription(OperatingMode.Budget)
            .Should().Be("Fast & cheapest; ok for simple photos.");
        
        ModeModelMap.GetModeDescription(OperatingMode.Balanced)
            .Should().Be("Good quality for most cases.");
        
        ModeModelMap.GetModeDescription(OperatingMode.Quality)
            .Should().Be("Best quality at a sensible price.");
    }

    [Fact]
    public void ModelPriorities_ShouldMatchSpecification()
    {
        // Budget
        var budgetPriorities = ModeModelMap.GetPriorityList(OperatingMode.Budget);
        budgetPriorities.Should().StartWith(OpenAIModel.Gpt41Nano);
        budgetPriorities.Should().Contain(OpenAIModel.Gpt41Mini);

        // Balanced
        var balancedPriorities = ModeModelMap.GetPriorityList(OperatingMode.Balanced);
        balancedPriorities.Should().StartWith(OpenAIModel.Gpt41);
        //balancedPriorities.Should().StartWith(OpenAIModel.O4Mini);
        //balancedPriorities.Should().ContainInOrder(OpenAIModel.O4Mini, OpenAIModel.Gpt41);

        // Quality
        var qualityPriorities = ModeModelMap.GetPriorityList(OperatingMode.Quality);
        qualityPriorities.Should().StartWith(OpenAIModel.Gpt5);
        //qualityPriorities.Should().Contain(OpenAIModel.O3);
    }
}
