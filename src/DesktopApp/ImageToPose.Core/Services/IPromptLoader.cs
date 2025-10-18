using Microsoft.Extensions.Logging;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for loading prompt templates from files
/// </summary>
public interface IPromptLoader
{
    Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default);
    Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation that loads prompts from embedded resources
/// </summary>
public class PromptLoader : IPromptLoader
{
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger<PromptLoader> _logger;
    private const string AnalyzeImagePromptResourceName = "ImageToPose.Desktop.Assets.step_1_prompt_analyse_image_and_get_pose_description.md";
    private const string GenerateRigPromptResourceName = "ImageToPose.Desktop.Assets.step_2_prompt_generate_bone_rotations.md";

    public PromptLoader(IResourceLoader resourceLoader, ILogger<PromptLoader> logger)
    {
        _resourceLoader = resourceLoader;
        _logger = logger;
        PromptLoaderLogs.ServiceInitialized(_logger);
    }

    public async Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "AnalyzeImage" });
        PromptLoaderLogs.LoadingPrompt(_logger, "AnalyzeImage");
        var content = await _resourceLoader.LoadEmbeddedResourceAsync(AnalyzeImagePromptResourceName, cancellationToken);
        PromptLoaderLogs.PromptLoaded(_logger, "AnalyzeImage");
        return content;
    }

    public async Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "GenerateRig" });
        PromptLoaderLogs.LoadingPrompt(_logger, "GenerateRig");
        var content = await _resourceLoader.LoadEmbeddedResourceAsync(GenerateRigPromptResourceName, cancellationToken);
        PromptLoaderLogs.PromptLoaded(_logger, "GenerateRig");
        return content;
    }
}

internal static partial class PromptLoaderLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "PromptLoader initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading {PromptType} prompt")]
    public static partial void LoadingPrompt(ILogger logger, string promptType);

    [LoggerMessage(Level = LogLevel.Information, Message = "{PromptType} prompt loaded successfully")]
    public static partial void PromptLoaded(ILogger logger, string promptType);
}
