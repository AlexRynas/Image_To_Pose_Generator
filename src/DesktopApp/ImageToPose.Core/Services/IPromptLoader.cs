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
/// Implementation that loads prompts from the root repository
/// </summary>
public class PromptLoader : IPromptLoader
{
    private readonly ILogger<PromptLoader> _logger;
    private const string AnalyzeImagePromptFileName = "analyse_image_and_get_pose_description_prompt.txt";
    private const string GenerateRigPromptFileName = "chatgpt_prompt.txt";

    public PromptLoader(ILogger<PromptLoader> logger)
    {
        _logger = logger;
        PromptLoaderLogs.ServiceInitialized(_logger);
    }

    public async Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "AnalyzeImage" });
        
        var filePath = FindPromptFile(AnalyzeImagePromptFileName);
        if (filePath == null)
        {
            PromptLoaderLogs.PromptFileNotFound(_logger, AnalyzeImagePromptFileName);
            throw new FileNotFoundException($"Could not find prompt file: {AnalyzeImagePromptFileName}");
        }

        PromptLoaderLogs.LoadingPrompt(_logger, filePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        PromptLoaderLogs.PromptLoaded(_logger, content.Length);
        return content;
    }

    public async Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "GenerateRig" });
        
        var filePath = FindPromptFile(GenerateRigPromptFileName);
        if (filePath == null)
        {
            PromptLoaderLogs.PromptFileNotFound(_logger, GenerateRigPromptFileName);
            throw new FileNotFoundException($"Could not find prompt file: {GenerateRigPromptFileName}");
        }

        PromptLoaderLogs.LoadingPrompt(_logger, filePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        PromptLoaderLogs.PromptLoaded(_logger, content.Length);
        return content;
    }

    private string? FindPromptFile(string fileName)
    {
        // Start from the executable directory and search up to find the repo root
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        
        for (int i = 0; i < 10; i++) // Search up to 10 levels
        {
            var testPath = Path.Combine(currentDir, fileName);
            if (File.Exists(testPath))
                return testPath;

            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null)
                break;

            currentDir = parentDir.FullName;
        }

        return null;
    }
}

internal static partial class PromptLoaderLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "PromptLoader initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading prompt from {FilePath}")]
    public static partial void LoadingPrompt(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prompt loaded, length: {Length} characters")]
    public static partial void PromptLoaded(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Prompt file not found: {FileName}")]
    public static partial void PromptFileNotFound(ILogger logger, string fileName);
}
