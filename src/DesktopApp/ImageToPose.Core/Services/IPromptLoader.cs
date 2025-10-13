using Microsoft.Extensions.Logging;
using System.Reflection;

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
    private readonly ILogger<PromptLoader> _logger;
    private const string AnalyzeImagePromptResourceName = "ImageToPose.Desktop.Assets.analyse_image_and_get_pose_description_prompt.txt";
    private const string GenerateRigPromptResourceName = "ImageToPose.Desktop.Assets.chatgpt_prompt.txt";

    public PromptLoader(ILogger<PromptLoader> logger)
    {
        _logger = logger;
        PromptLoaderLogs.ServiceInitialized(_logger);
    }

    public async Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "AnalyzeImage" });
        return await LoadEmbeddedResourceAsync(AnalyzeImagePromptResourceName, cancellationToken);
    }

    public async Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["PromptType"] = "GenerateRig" });
        return await LoadEmbeddedResourceAsync(GenerateRigPromptResourceName, cancellationToken);
    }

    private async Task<string> LoadEmbeddedResourceAsync(string resourceName, CancellationToken cancellationToken)
    {
        // Try to find the assembly containing the embedded resources
        var assembly = FindAssemblyWithResource(resourceName);
        
        if (assembly == null)
        {
            PromptLoaderLogs.ResourceNotFound(_logger, resourceName);
            throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");
        }

        PromptLoaderLogs.LoadingResource(_logger, resourceName, assembly.GetName().Name ?? "Unknown");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            PromptLoaderLogs.ResourceNotFound(_logger, resourceName);
            throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        PromptLoaderLogs.ResourceLoaded(_logger, content.Length);
        return content;
    }

    private Assembly? FindAssemblyWithResource(string resourceName)
    {
        // First, try the entry assembly (the Desktop app)
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && entryAssembly.GetManifestResourceNames().Contains(resourceName))
        {
            return entryAssembly;
        }

        // Then try all loaded assemblies
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            if (assembly.GetManifestResourceNames().Contains(resourceName))
            {
                return assembly;
            }
        }

        return null;
    }
}

internal static partial class PromptLoaderLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "PromptLoader initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading resource {ResourceName} from assembly {AssemblyName}")]
    public static partial void LoadingResource(ILogger logger, string resourceName, string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resource loaded, length: {Length} characters")]
    public static partial void ResourceLoaded(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Embedded resource not found: {ResourceName}")]
    public static partial void ResourceNotFound(ILogger logger, string resourceName);
}
