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
}

public class OpenAIService : IOpenAIService
{
    private readonly ISettingsService _settingsService;
    private readonly IPromptLoader _promptLoader;

    // Session-scoped cache so we resolve models once
    private string? _chosenModel;
    private readonly string[] _preferredModels = new[] { "gpt-4.1-mini", "gpt-4.1", "o4-mini" }; // multimodal-capable list

    public OpenAIService(ISettingsService settingsService, IPromptLoader promptLoader)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;

        try
        {
            // 1) List models with THIS key (authoritative per-account view)
            var root = new OpenAIClient(apiKey);
            var modelClient = root.GetOpenAIModelClient();
            var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
            var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2) Pick & probe one working chat model
            string? picked = PickPreferred(available) ?? available.FirstOrDefault(m => m.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase));
            if (picked is null) return false;

            // tiny probe (auto-retries 5xx; we still wrap a small backoff loop)
            var ok = await ProbeChatAsync(root, picked, cancellationToken);
            if (!ok)
            {
                foreach (var alt in _preferredModels.Where(m => !m.Equals(picked, StringComparison.OrdinalIgnoreCase)))
                {
                    if (available.Contains(alt) && await ProbeChatAsync(root, alt, cancellationToken))
                    {
                        picked = alt;
                        ok = true;
                        break;
                    }
                }
            }

            if (!ok) return false;

            _chosenModel = picked; // cache for the session
            return true;
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

        var client = new OpenAIClient(options.ApiKey);

        // Resolve a model that actually works for this key
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

    private string? PickPreferred(HashSet<string> available)
        => _preferredModels.FirstOrDefault(m => available.Contains(m));

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

        string? picked = PickPreferred(available)
            ?? available.FirstOrDefault(m => m.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase))
            ?? available.FirstOrDefault(); // last resort

        if (picked is null)
            throw new InvalidOperationException("No models are available for this API key.");

        // If vision is required but pick isn't vision-capable, try an alt
        if (requireVision && !SupportsVision(picked))
        {
            picked = _preferredModels.FirstOrDefault(m => available.Contains(m) && SupportsVision(m))
                     ?? available.FirstOrDefault(m => SupportsVision(m))
                     ?? picked; // fallback to picked; request will error if truly unsupported
        }

        // Probe before caching
        if (!await ProbeChatAsync(root, picked, cancellationToken))
        {
            foreach (var alt in _preferredModels.Where(m => available.Contains(m) && (!requireVision || SupportsVision(m))))
            {
                if (await ProbeChatAsync(root, alt, cancellationToken))
                {
                    picked = alt; break;
                }
            }
        }

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
