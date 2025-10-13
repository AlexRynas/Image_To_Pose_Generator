using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for loading embedded resources from assemblies
/// </summary>
public interface IResourceLoader
{
    Task<string> LoadEmbeddedResourceAsync(string resourceName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation that loads resources from embedded assembly resources
/// </summary>
public class ResourceLoader : IResourceLoader
{
    private readonly ILogger<ResourceLoader> _logger;

    public ResourceLoader(ILogger<ResourceLoader> logger)
    {
        _logger = logger;
        ResourceLoaderLogs.ServiceInitialized(_logger);
    }

    public async Task<string> LoadEmbeddedResourceAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["ResourceName"] = resourceName });
        
        // Try to find the assembly containing the embedded resources
        var assembly = FindAssemblyWithResource(resourceName);
        
        if (assembly == null)
        {
            ResourceLoaderLogs.ResourceNotFound(_logger, resourceName);
            throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");
        }

        ResourceLoaderLogs.LoadingResource(_logger, resourceName, assembly.GetName().Name ?? "Unknown");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            ResourceLoaderLogs.ResourceNotFound(_logger, resourceName);
            throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        ResourceLoaderLogs.ResourceLoaded(_logger, content.Length);
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

internal static partial class ResourceLoaderLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ResourceLoader initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading resource {ResourceName} from assembly {AssemblyName}")]
    public static partial void LoadingResource(ILogger logger, string resourceName, string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resource loaded, length: {Length} characters")]
    public static partial void ResourceLoaded(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Embedded resource not found: {ResourceName}")]
    public static partial void ResourceNotFound(ILogger logger, string resourceName);
}
