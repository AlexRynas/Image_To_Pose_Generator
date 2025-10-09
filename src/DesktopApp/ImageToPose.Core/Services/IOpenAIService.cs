using System.Text.RegularExpressions;
using ImageToPose.Core.Models;
using OpenAI;
using OpenAI.Chat;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for interacting with OpenAI API
/// </summary>
public interface IOpenAIService
{
    Task<ExtendedPose> AnalyzePoseAsync(string imagePath, string roughText, CancellationToken cancellationToken = default);
    Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default);
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    // New: Mode & Pricing hooks
    OperatingMode SelectedMode { get; set; }
    string? ResolvedModelId { get; }
    Task<string> ResolveModelAsync(bool requireVision, string? overrideModelId = null, CancellationToken cancellationToken = default);
    Task<PricingModelRates?> GetResolvedModelRatesAsync(CancellationToken cancellationToken = default);
}

public class OpenAIService : IOpenAIService
{
    private readonly ISettingsService _settingsService;
    private readonly IPromptLoader _promptLoader;
    private readonly IPriceEstimator _priceEstimator;

    // Session-scoped cache so we resolve models once
    private string? _chosenModel;

    public OperatingMode SelectedMode { get; set; } = OperatingMode.Balanced;
    public string? ResolvedModelId => _chosenModel;

    public OpenAIService(ISettingsService settingsService, IPromptLoader promptLoader, IPriceEstimator priceEstimator)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _priceEstimator = priceEstimator ?? throw new ArgumentNullException(nameof(priceEstimator));
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;

        try
        {
            var root = new OpenAIClient(apiKey);
            var modelClient = root.GetOpenAIModelClient();
            var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
            var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Try resolve according to selected mode
            var picked = await PickBestModelAsync(root, available, requireVision: true, overrideModelId: null, cancellationToken);
            if (picked is null) return false;

            _chosenModel = picked; // cache for the session
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ResolveModelAsync(bool requireVision, string? overrideModelId = null, CancellationToken cancellationToken = default)
    {
        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not set");

        var root = new OpenAIClient(options.ApiKey);
        var modelClient = root.GetOpenAIModelClient();
        var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
        var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = await PickBestModelAsync(root, available, requireVision, overrideModelId, cancellationToken)
                    ?? throw new InvalidOperationException("No compatible models available for this API key.");
        _chosenModel = picked;
        return picked;
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

        var client = new OpenAIClient(options.ApiKey);

        // Resolve a model that actually works for this key (vision required)
        var model = await EnsureModelAsync(client, requireVision: true, cancellationToken);

        // Load prompt #1
        var promptTemplate = await _promptLoader.LoadAnalyzeImagePromptAsync(cancellationToken);

        // Build the user message (text + image)
        var userMessageText =
            $"USER-SET ANCHORS\n{roughText}\n\n" +
            "Analyze the attached image and produce the extended pose description.";

        // Read the image and attach as an image content part (binary)
        byte[] bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var imagePart = ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(bytes), DetectImageMime(imagePath));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(promptTemplate),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(userMessageText),
                imagePart
            )
        };

        var chat = client.GetChatClient(model);
        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 1000
        };

        var response = await chat.CompleteChatAsync(messages, chatOptions, cancellationToken);
        var content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned an empty response");

        return new ExtendedPose { Text = content.Trim() };
    }

    public async Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extendedPoseText))
            throw new ArgumentException("Extended pose text cannot be empty", nameof(extendedPoseText));

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not set");

        var client = new OpenAIClient(options.ApiKey);
        var model = await EnsureModelAsync(client, requireVision: false, cancellationToken);

        var promptTemplate = await _promptLoader.LoadGenerateRigPromptAsync(cancellationToken);
        var prompt = promptTemplate.Replace(
            "POSE_TEXT_START\n(Insert pose paragraph here — anatomical left/right, global stance/lean, head/neck yaw/pitch/tilt, torso orientation/lean/side-bend, shoulders/arms/elbows/forearms/hands, hips/legs/knees/feet, occlusions/uncertainties.)\nPOSE_TEXT_END",
            $"POSE_TEXT_START\n{extendedPoseText}\nPOSE_TEXT_END"
        );

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(prompt)
        };

        var chat = client.GetChatClient(model);
        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            MaxOutputTokenCount = 2000
        };

        var response = await chat.CompleteChatAsync(messages, chatOptions, cancellationToken);
        var content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned an empty response");

        return ParsePoseRig(content);
    }

    public async Task<PricingModelRates?> GetResolvedModelRatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_chosenModel)) return null;
        return await _priceEstimator.GetRatesAsync(_chosenModel!, cancellationToken);
    }

    // -------- Helpers --------

    private static string DetectImageMime(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static async Task<bool> ProbeChatAsync(OpenAIClient root, string model, CancellationToken ct)
    {
        // small backoff loop around a one-token-ish probe
        var chat = root.GetChatClient(model);
        var msgs = new[] { new UserChatMessage("ping") };
        var delays = new[] { 250, 500, 1000 }; // ms

        for (int i = 0; i < delays.Length; i++)
        {
            try
            {
                var r = await chat.CompleteChatAsync(msgs, cancellationToken: ct);
                return r?.Value?.Content?.Count > 0;
            }
            catch when (i < delays.Length - 1)
            {
                await Task.Delay(delays[i], ct);
            }
        }
        return false;
    }

    private static bool SupportsVision(string modelId)
    {
        // Heuristic: 4.1/4o/o4 lines are vision-capable; extend if needed.
        return modelId.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase)
            || modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase)
            || modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> PickBestModelAsync(OpenAIClient root, HashSet<string> available, bool requireVision, string? overrideModelId, CancellationToken cancellationToken)
    {
        // If user forces a model id, try it first
        if (!string.IsNullOrWhiteSpace(overrideModelId) && available.Contains(overrideModelId!) && (!requireVision || SupportsVision(overrideModelId!)))
        {
            if (await ProbeChatAsync(root, overrideModelId!, cancellationToken))
                return overrideModelId;
        }

        var priority = ModeModelMap.GetPriorityList(SelectedMode);
        foreach (var candidate in priority)
        {
            if (!available.Contains(candidate)) continue;
            if (requireVision && !SupportsVision(candidate)) continue;
            if (await ProbeChatAsync(root, candidate, cancellationToken))
                return candidate;
        }

        // Fallbacks: any gpt-4* then any available
        string? picked = available.FirstOrDefault(m => m.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase) && (!requireVision || SupportsVision(m)))
            ?? available.FirstOrDefault(m => !requireVision || SupportsVision(m))
            ?? available.FirstOrDefault();

        if (picked is null) return null;
        if (await ProbeChatAsync(root, picked, cancellationToken)) return picked;
        return null;
    }

    private async Task<string> EnsureModelAsync(OpenAIClient root, bool requireVision, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_chosenModel)
            && (!requireVision || SupportsVision(_chosenModel)))
        {
            return _chosenModel!;
        }

        var modelClient = root.GetOpenAIModelClient();
        var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
        var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var picked = await PickBestModelAsync(root, available, requireVision, overrideModelId: null, cancellationToken)
                    ?? throw new InvalidOperationException("No models are available for this API key.");

        _chosenModel = picked;
        return picked;
    }

    private PoseRig ParsePoseRig(string content)
    {
        // Strip code fences
        var cleaned = Regex.Replace(content, @"```(?:[a-zA-Z]+)?\s*", "", RegexOptions.IgnoreCase).Trim();

        // Try POSE_DEGREES = {...}
        var match = Regex.Match(cleaned, @"POSE_DEGREES\s*=\s*\{([\s\S]*?)\}", RegexOptions.Multiline);
        if (!match.Success)
            throw new InvalidOperationException("Could not find POSE_DEGREES dictionary in response");

        var dictContent = match.Groups[1].Value;
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
                bones.Add(new BoneRotation { BoneName = boneName, X = values[0], Y = values[1], Z = values[2] });
            }
        }

        if (bones.Count == 0)
            throw new InvalidOperationException("No bone rotations found in response");

        return new PoseRig { Bones = bones };
    }
}
