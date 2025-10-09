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
    private const string AnalyzeImagePromptFileName = "analyse_image_and_get_pose_description_prompt.txt";
    private const string GenerateRigPromptFileName = "chatgpt_prompt.txt";

    public async Task<string> LoadAnalyzeImagePromptAsync(CancellationToken cancellationToken = default)
    {
        var filePath = FindPromptFile(AnalyzeImagePromptFileName);
        if (filePath == null)
            throw new FileNotFoundException($"Could not find prompt file: {AnalyzeImagePromptFileName}");

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task<string> LoadGenerateRigPromptAsync(CancellationToken cancellationToken = default)
    {
        var filePath = FindPromptFile(GenerateRigPromptFileName);
        if (filePath == null)
            throw new FileNotFoundException($"Could not find prompt file: {GenerateRigPromptFileName}");

        return await File.ReadAllTextAsync(filePath, cancellationToken);
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
