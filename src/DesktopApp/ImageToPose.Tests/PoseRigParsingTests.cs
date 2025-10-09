using FluentAssertions;
using ImageToPose.Core.Services;
using Xunit;

namespace ImageToPose.Tests;

public class PoseRigParsingTests
{
    [Fact]
    public void ParsePoseRig_WithValidPythonDict_ShouldParseBoneRotations()
    {
        // Arrange
        var settingsService = new SettingsService();
        var promptLoader = new MockPromptLoader();
        var openAIService = new OpenAIService(settingsService, promptLoader);
        
        var llmResponse = @"```python
POSE_DEGREES = {
    ""pelvis"": [0.0, 0.0, 5.0],
    ""spine_01"": [10.0, 0.0, -2.0],
    ""head"": [15.0, -5.0, 3.0]
}
```";

        // Act
        var parseMethod = typeof(OpenAIService)
            .GetMethod("ParsePoseRig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = parseMethod?.Invoke(openAIService, new[] { llmResponse }) as ImageToPose.Core.Models.PoseRig;

        // Assert
        result.Should().NotBeNull();
        result!.Bones.Should().HaveCount(3);
        result.Bones[0].BoneName.Should().Be("pelvis");
        result.Bones[0].X.Should().Be(0.0);
        result.Bones[0].Y.Should().Be(0.0);
        result.Bones[0].Z.Should().Be(5.0);
    }

    [Fact]
    public void ParsePoseRig_WithoutCodeFences_ShouldParseBoneRotations()
    {
        // Arrange
        var settingsService = new SettingsService();
        var promptLoader = new MockPromptLoader();
        var openAIService = new OpenAIService(settingsService, promptLoader);
        
        var llmResponse = @"POSE_DEGREES = {
    ""upperarm_l"": [45.0, -30.0, 20.0],
    ""lowerarm_l"": [-45.0, 0.0, 0.0]
}";

        // Act
        var parseMethod = typeof(OpenAIService)
            .GetMethod("ParsePoseRig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = parseMethod?.Invoke(openAIService, new[] { llmResponse }) as ImageToPose.Core.Models.PoseRig;

        // Assert
        result.Should().NotBeNull();
        result!.Bones.Should().HaveCount(2);
        result.Bones[1].BoneName.Should().Be("lowerarm_l");
        result.Bones[1].X.Should().Be(-45.0);
    }

    private class MockPromptLoader : IPromptLoader
    {
        public Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Mock prompt");

        public Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("Mock prompt");
    }
}
