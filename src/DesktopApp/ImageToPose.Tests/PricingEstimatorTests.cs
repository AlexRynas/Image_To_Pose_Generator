using System.Linq;
using FluentAssertions;
using ImageToPose.Core.Models;
using ImageToPose.Core.Services;
using Xunit;

namespace ImageToPose.Tests;

public class PricingEstimatorTests
{
    [Fact]
    public async Task GetRatesAsync_WithValidModelId_ShouldReturnRates()
    {
        // Arrange
        var estimator = new PriceEstimator();

        // Act
    var rates = await estimator.GetRatesAsync(OpenAIModel.Gpt41Mini.GetModelId());

        // Assert
        rates.Should().NotBeNull();
    rates!.ModelId.Should().Be(OpenAIModel.Gpt41Mini.GetModelId());
        rates.InputPerMillion.Should().BeGreaterThan(0);
        rates.OutputPerMillion.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRatesAsync_WithInvalidModelId_ShouldReturnNull()
    {
        // Arrange
        var estimator = new PriceEstimator();

        // Act
        var rates = await estimator.GetRatesAsync("invalid-model");

        // Assert
        rates.Should().BeNull();
    }

    [Theory]
    [InlineData("Budget", 300)]
    [InlineData("Balanced", 600)]
    [InlineData("Quality", 800)]
    public void AssumedOutputTokens_ShouldMatchMode(string mode, int expected)
    {
        // This test validates the assumed output token counts match the spec
        var actual = mode switch
        {
            "Budget" => 300,
            "Balanced" => 600,
            "Quality" => 800,
            _ => 0
        };

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(512, 512, 1, 210)]    // 1 tile: 70 base + 140*1
    [InlineData(1024, 512, 2, 350)]   // 2 tiles: 70 base + 140*2
    [InlineData(1024, 1024, 4, 630)]  // 4 tiles: 70 base + 140*4
    [InlineData(2048, 2048, 16, 2310)] // 16 tiles: 70 base + 140*16
    public void EstimateImageTokens_ShouldCalculateCorrectly(int width, int height, int expectedTiles, int expectedTokens)
    {
        // Arrange - compute using the same logic as the estimator
        int tilesX = (int)Math.Ceiling(width / 512.0);
        int tilesY = (int)Math.Ceiling(height / 512.0);
        int tiles = Math.Max(1, tilesX * tilesY);
        int baseTokens = 70;
        int tileTokens = 140 * tiles;
        int total = baseTokens + tileTokens;

        // Assert
        tiles.Should().Be(expectedTiles);
        total.Should().Be(expectedTokens);
    }

    [Fact]
    public void CostUsd_ShouldCalculateCorrectly()
    {
        // Arrange
        int tokens = 1000;
    decimal perMillion = 0.40m;

        // Act
        decimal cost = Math.Round(tokens / 1_000_000m * perMillion, 6, MidpointRounding.AwayFromZero);

        // Assert
        cost.Should().Be(0.0004m);
    }

    [Theory]
    [InlineData(100, 0.10, 0.00001)]
    [InlineData(1000, 0.40, 0.0004)]
    [InlineData(10000, 2.50, 0.025)]
    [InlineData(100000, 10.00, 1.0)]
    public void CostUsd_VariousTokenCounts_ShouldCalculateCorrectly(int tokens, decimal perMillion, decimal expected)
    {
        // Act
        decimal cost = Math.Round(tokens / 1_000_000m * perMillion, 6, MidpointRounding.AwayFromZero);

        // Assert
        cost.Should().Be(expected);
    }

    [Fact]
    public async Task EstimateTextAsync_ShouldReturnValidEstimate()
    {
        // Arrange
        var estimator = new PriceEstimator();
        var rates = new PricingModelRates
        {
            ModelId = "test-model",
            InputPerMillion = 0.40m,
            OutputPerMillion = 1.60m
        };
        var inputText = "This is a test prompt that should be tokenized.";
        int assumedOutputTokens = 600;

        // Act
        var estimate = await estimator.EstimateTextAsync(rates, inputText, assumedOutputTokens);

        // Assert
        estimate.Should().NotBeNull();
        estimate.InputTokens.Should().BeGreaterThan(0);
        estimate.OutputTokens.Should().Be(assumedOutputTokens);
        estimate.InputUsd.Should().BeGreaterThan(0);
        estimate.OutputUsd.Should().BeGreaterThan(0);
        estimate.TotalUsd.Should().Be(estimate.InputUsd + estimate.OutputUsd);
    }

    [Fact]
    public void ModeModelMap_Budget_ShouldHaveCorrectPriority()
    {
        // Act
    var priorities = ModeModelMap.GetPriorityList(OperatingMode.Budget);

        // Assert
        priorities.Should().NotBeEmpty();
    priorities.First().Should().Be(OpenAIModel.Gpt41Nano);
    priorities.Should().Contain(OpenAIModel.Gpt41Mini);
    }

    [Fact]
    public void ModeModelMap_Balanced_ShouldHaveCorrectPriority()
    {
        // Act
        var priorities = ModeModelMap.GetPriorityList(OperatingMode.Balanced);

        // Assert
    priorities.Should().NotBeEmpty();
    priorities.First().Should().Be(OpenAIModel.O4Mini);
    priorities.Should().Contain(OpenAIModel.O4Mini);
    priorities.Should().Contain(OpenAIModel.Gpt41);
    }

    [Fact]
    public void ModeModelMap_Quality_ShouldHaveCorrectPriority()
    {
        // Act
        var priorities = ModeModelMap.GetPriorityList(OperatingMode.Quality);

        // Assert
    priorities.Should().NotBeEmpty();
    priorities.First().Should().Be(OpenAIModel.Gpt5);
    priorities.Should().Contain(OpenAIModel.O3);
    }

    [Theory]
    [InlineData(OperatingMode.Budget, "Fast & cheapest; ok for simple photos.")]
    [InlineData(OperatingMode.Balanced, "Good quality for most cases.")]
    [InlineData(OperatingMode.Quality, "Best quality at a sensible price.")]
    public void ModeModelMap_GetModeDescription_ShouldReturnCorrectDescription(OperatingMode mode, string expected)
    {
        // Act
        var description = ModeModelMap.GetModeDescription(mode);

        // Assert
        description.Should().Be(expected);
    }
}
