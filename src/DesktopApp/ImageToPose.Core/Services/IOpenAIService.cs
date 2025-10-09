using System.Text.Json;
using System.Text.RegularExpressions;
using ImageToPose.Core.Models;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Files;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for interacting with OpenAI API
/// </summary>
public interface IOpenAIService
{
    Task<ExtendedPose> AnalyzePoseAsync(string imagePath, string roughText, CancellationToken cancellationToken = default);
    Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default);
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of OpenAI service using official SDK
/// </summary>
public class OpenAIService : IOpenAIService
{
    private readonly ISettingsService _settingsService;
    private readonly IPromptLoader _promptLoader;

    public OpenAIService(ISettingsService settingsService, IPromptLoader promptLoader)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new OpenAIClient(apiKey);
            var chatClient = client.GetChatClient("gpt-4o-mini");
            
            // Simple test message
            var messages = new[]
            {
                new UserChatMessage("Hello")
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            return response?.Value?.Content?.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ExtendedPose> AnalyzePoseAsync(string imagePath, string roughText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path cannot be empty", nameof(imagePath));

        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found", imagePath);

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not set");

        // Load the prompt
        var promptTemplate = await _promptLoader.LoadAnalyzeImagePromptAsync(cancellationToken);

        // Create OpenAI client
        var client = new OpenAIClient(options.ApiKey);
        var fileClient = client.GetOpenAIFileClient();
        var chatClient = client.GetChatClient("gpt-4o");

        // Upload the image file for vision
        OpenAIFile uploadedFile;
        using (var fileStream = File.OpenRead(imagePath))
        {
            uploadedFile = await fileClient.UploadFileAsync(
                fileStream,
                Path.GetFileName(imagePath),
                FileUploadPurpose.Vision,
                cancellationToken);
        }

        try
        {
            // Prepare the user message with anchors if provided
            var userMessage = $"USER-SET ANCHORS\n{roughText}\n\nPlease analyze the attached image and provide the pose description.";

            // Create chat messages with image
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(promptTemplate),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(userMessage),
                    ChatMessageContentPart.CreateImagePart(new Uri($"file://{uploadedFile.Id}"), "low")
                )
            };

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxOutputTokenCount = 1000
            };

            var response = await chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);

            var content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("OpenAI returned an empty response");

            return new ExtendedPose { Text = content.Trim() };
        }
        finally
        {
            // Clean up the uploaded file
            try
            {
                await fileClient.DeleteFileAsync(uploadedFile.Id, cancellationToken);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public async Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extendedPoseText))
            throw new ArgumentException("Extended pose text cannot be empty", nameof(extendedPoseText));

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not set");

        // Load the prompt
        var promptTemplate = await _promptLoader.LoadGenerateRigPromptAsync(cancellationToken);

        // Replace the POSE_TEXT markers with the actual pose description
        var prompt = promptTemplate.Replace("POSE_TEXT_START\n(Insert pose paragraph here — anatomical left/right, global stance/lean, head/neck yaw/pitch/tilt, torso orientation/lean/side-bend, shoulders/arms/elbows/forearms/hands, hips/legs/knees/feet, occlusions/uncertainties.)\nPOSE_TEXT_END",
            $"POSE_TEXT_START\n{extendedPoseText}\nPOSE_TEXT_END");

        // Create OpenAI client
        var client = new OpenAIClient(options.ApiKey);
        var chatClient = client.GetChatClient("gpt-4o");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            MaxOutputTokenCount = 2000
        };

        var response = await chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);

        var content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned an empty response");

        // Parse the response to extract the POSE_DEGREES dictionary
        return ParsePoseRig(content);
    }

    private PoseRig ParsePoseRig(string content)
    {
        // Remove code fences if present
        var cleaned = Regex.Replace(content, @"```(?:python)?\s*", "", RegexOptions.IgnoreCase);
        cleaned = cleaned.Trim();

        // Find the POSE_DEGREES dictionary
        var match = Regex.Match(cleaned, @"POSE_DEGREES\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidOperationException("Could not find POSE_DEGREES dictionary in response");

        var dictContent = match.Groups[1].Value;

        // Parse the dictionary content
        var bones = new List<BoneRotation>();
        var boneMatches = Regex.Matches(dictContent, @"""([^""]+)""\s*:\s*\[([^\]]+)\]");

        foreach (Match boneMatch in boneMatches)
        {
            var boneName = boneMatch.Groups[1].Value;
            var values = boneMatch.Groups[2].Value.Split(',')
                .Select(v => v.Trim())
                .Select(v => double.TryParse(v, out var d) ? d : 0.0)
                .ToArray();

            if (values.Length >= 3)
            {
                bones.Add(new BoneRotation
                {
                    BoneName = boneName,
                    X = values[0],
                    Y = values[1],
                    Z = values[2]
                });
            }
        }

        if (bones.Count == 0)
            throw new InvalidOperationException("No bone rotations found in response");

        return new PoseRig { Bones = bones };
    }
}
