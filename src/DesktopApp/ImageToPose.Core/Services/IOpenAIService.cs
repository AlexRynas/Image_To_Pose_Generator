using System.Text.RegularExpressions;
using ImageToPose.Core.Models;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using System.ClientModel;

namespace ImageToPose.Core.Services;

/// <summary>
/// Service for interacting with OpenAI API
/// </summary>
public interface IOpenAIService
{
    Task<ExtendedPose> AnalyzePoseAsync(PoseInput input, CancellationToken cancellationToken = default);
    Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default);
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    // Mode & Pricing hooks
    OperatingMode SelectedMode { get; set; }
    string? ResolvedModelId { get; }
    Task<string> ResolveModelAsync(string? overrideModelId = null, CancellationToken cancellationToken = default);
    Task<PricingModelRates?> GetResolvedModelRatesAsync(CancellationToken cancellationToken = default);
}

public class OpenAIService : IOpenAIService
{
    private readonly ILogger<OpenAIService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPromptLoader _promptLoader;
    private readonly IPriceEstimator _priceEstimator;
    private readonly IOpenAIErrorHandler _errorHandler;
    private readonly DiagnosticsLogger _diagnosticsLogger;

    // Session-scoped cache so we resolve models once
    private string? _chosenModel;

    public OperatingMode SelectedMode { get; set; } = OperatingMode.Balanced;
    public string? ResolvedModelId => _chosenModel;

    public OpenAIService(
        ILogger<OpenAIService> logger,
        ISettingsService settingsService,
        IPromptLoader promptLoader,
        IPriceEstimator priceEstimator,
        IOpenAIErrorHandler errorHandler)
    {
        _logger = logger;
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _priceEstimator = priceEstimator ?? throw new ArgumentNullException(nameof(priceEstimator));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _diagnosticsLogger = new DiagnosticsLogger(logger);

        OpenAIServiceLogs.ServiceInitialized(_logger);
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["Operation"] = "ValidateApiKey" });
        OpenAIServiceLogs.ValidatingApiKey(_logger);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            OpenAIServiceLogs.ApiKeyEmpty(_logger);
            return false;
        }

        try
        {
            var root = new OpenAIClient(apiKey);
            var modelClient = root.GetOpenAIModelClient();
            var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
            var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            OpenAIServiceLogs.ModelsListRetrieved(_logger, available.Count);

            // Try resolve according to selected mode
            var picked = await PickBestModelAsync(root, available, overrideModelId: null, cancellationToken);

            // If no model is available for the current mode, still validate key but don't cache a model
            // This allows the key validation to succeed even if mode-specific models aren't available
            if (picked is not null)
            {
                _chosenModel = picked; // cache for the session
                OpenAIServiceLogs.ApiKeyValidated(_logger, picked);
            }
            else
            {
                OpenAIServiceLogs.ApiKeyValidatedNoModel(_logger);
            }

            // Key is valid if we can list models, even if none match the current mode
            return available.Count > 0;
        }
        catch (Exception ex)
        {
            OpenAIServiceLogs.ApiKeyValidationFailed(_logger, ex);
            return false;
        }
    }

    public async Task<string> ResolveModelAsync(string? overrideModelId = null, CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "ResolveModel",
            ["Mode"] = SelectedMode.ToString()
        });

        OpenAIServiceLogs.ResolvingModel(_logger, SelectedMode.ToString());

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            OpenAIServiceLogs.ApiKeyNotSet(_logger);
            throw new InvalidOperationException("OpenAI API key is not set");
        }

        // Check if cached model is still valid for the current mode
        if (!string.IsNullOrWhiteSpace(_chosenModel) && string.IsNullOrWhiteSpace(overrideModelId))
        {
            var priority = ModeModelMap.GetPriorityList(SelectedMode);
            if (OpenAIModelExtensions.TryParse(_chosenModel, out var cachedModel)
                && priority.Contains(cachedModel))
            {
                // Cached model is appropriate for current mode, reuse it
                OpenAIServiceLogs.UsingCachedModel(_logger, _chosenModel!);
                return _chosenModel!;
            }
            // Cached model is not in current mode's priority list, invalidate cache
            OpenAIServiceLogs.InvalidatingCachedModel(_logger, _chosenModel!);
            _chosenModel = null;
        }

        var root = new OpenAIClient(options.ApiKey);
        var modelClient = root.GetOpenAIModelClient();
        var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
        var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = await PickBestModelAsync(root, available, overrideModelId, cancellationToken);

        if (picked is null)
        {
            // Build helpful error message based on selected mode
            var priority = ModeModelMap.GetPriorityList(SelectedMode);
            var requiredModels = string.Join(" or ", priority.Select(m => m.GetModelId()));
            var modeName = SelectedMode.ToString();

            OpenAIServiceLogs.NoCompatibleModel(_logger, modeName, requiredModels);
            throw new InvalidOperationException(
                $"No compatible models available for {modeName} mode. " +
                $"Your API key needs access to: {requiredModels}. " +
                $"Please check your OpenAI account or select a different mode.");
        }

        _chosenModel = picked;
        OpenAIServiceLogs.ModelResolved(_logger, picked);
        return picked;
    }

    public async Task<ExtendedPose> AnalyzePoseAsync(PoseInput input, CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "AnalyzePose",
            ["ImagePath"] = input?.ImagePath ?? "null"
        });

        OpenAIServiceLogs.AnalyzingPose(_logger, input?.ImagePath ?? "null");

        if (input is null) throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrWhiteSpace(input.ImagePath))
            throw new ArgumentException("Image path cannot be empty", nameof(input.ImagePath));

        if (!File.Exists(input.ImagePath))
        {
            OpenAIServiceLogs.ImageFileNotFound(_logger, input.ImagePath);
            throw new FileNotFoundException("Image file not found", input.ImagePath);
        }

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            OpenAIServiceLogs.ApiKeyNotSet(_logger);
            throw new InvalidOperationException("OpenAI API key is not set");
        }

        var client = new OpenAIClient(options.ApiKey);

        // Resolve a model that actually works for this key (vision required)
        var model = await EnsureModelAsync(client, cancellationToken);
        OpenAIServiceLogs.UsingModel(_logger, model);

        // Load prompt #1
        var promptTemplate = await _promptLoader.LoadAnalyzeImagePromptAsync(cancellationToken);

        // Build the user message (text + image) using USER-SET ANCHORS format
        var anchorsBlock = input.ToUserAnchorsBlock();
        var userMessageText =
            $"USER-SET ANCHORS\n{anchorsBlock}\n\n" +
            "Analyze the attached image and produce the extended pose description.";

        // Read the image and attach as an image content part (binary)
        byte[] bytes = await File.ReadAllBytesAsync(input.ImagePath, cancellationToken);
        var imagePart = ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(bytes), DetectImageMime(input.ImagePath));

        var messages = new List<ChatMessage>
            {
                new SystemChatMessage(promptTemplate),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(userMessageText),
                    imagePart
                )
            };

        var chat = client.GetChatClient(model);
        var temperature = 0.3f;
        var chatOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 1000
        };

        // O-series models only support default temperature (1)
        if (!IsOSeriesModel(model))
        {
            chatOptions.Temperature = temperature;
        }
        else
        {
            temperature = 1.0f; // Use default for O-series models
        }

        // Enable logprobs only when explicitly supported by our capability map
        var requestedLogProbs = false;
        if (OpenAIModelExtensions.SupportsLogProbs(model))
        {
            chatOptions.IncludeLogProbabilities = true;
            chatOptions.TopLogProbabilityCount = 3;
            requestedLogProbs = true;
        }

        OpenAIServiceLogs.SendingVisionRequest(_logger, model);

        var startedAt = DateTimeOffset.UtcNow;
        string? content = null;
        ClientResult<ChatCompletion>? response = null;
        OpenAIErrorInfo? error = null;

        try
        {
            response = await chat.CompleteChatAsync(messages, chatOptions, cancellationToken);
            content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                OpenAIServiceLogs.EmptyResponse(_logger);
                throw new InvalidOperationException("OpenAI returned an empty response");
            }

            OpenAIServiceLogs.PoseAnalysisComplete(_logger, content.Length);
        }
        catch (Exception ex)
        {
            error = _errorHandler.Translate(ex);

            // Write diagnostics even on error
            await WriteDiagnosticsAsync(
                startedAt,
                model,
                temperature,
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["operation"] = "AnalyzePose",
                    ["imagePath"] = Path.GetFileName(input.ImagePath),
                    ["anchors"] = anchorsBlock,
                    ["requestedLogProbs"] = requestedLogProbs
                },
                new Dictionary<string, object?>(),
                error,
                cancellationToken);

            throw;
        }

        // Write diagnostics on success
        await WriteDiagnosticsAsync(
            startedAt,
            model,
            temperature,
            response,
            null,
            new Dictionary<string, object?>
            {
                ["operation"] = "AnalyzePose",
                ["imagePath"] = Path.GetFileName(input.ImagePath),
                ["anchors"] = anchorsBlock,
                ["requestedLogProbs"] = requestedLogProbs
            },
            new Dictionary<string, object?>
            {
                ["contentLength"] = content?.Length ?? 0
            },
            null,
            cancellationToken);

        return new ExtendedPose { Text = content!.Trim() };
    }

    public async Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken cancellationToken = default)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["Operation"] = "GenerateRig" });
        OpenAIServiceLogs.GeneratingRig(_logger, extendedPoseText.Length);

        if (string.IsNullOrWhiteSpace(extendedPoseText))
            throw new ArgumentException("Extended pose text cannot be empty", nameof(extendedPoseText));

        var options = _settingsService.GetOpenAIOptions();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            OpenAIServiceLogs.ApiKeyNotSet(_logger);
            throw new InvalidOperationException("OpenAI API key is not set");
        }

        var client = new OpenAIClient(options.ApiKey);
        var model = await EnsureModelAsync(client, cancellationToken);
        OpenAIServiceLogs.UsingModel(_logger, model);

        var promptTemplate = await _promptLoader.LoadGenerateRigPromptAsync(cancellationToken);

        // Replace anything between POSE_TEXT_START and POSE_TEXT_END in a robust way
        // Handles differing whitespace and line endings in the template
        var pattern = @"POSE_TEXT_START\s*(?:\r?\n)?[\s\S]*?(?:\r?\n)?POSE_TEXT_END";
        var replacement = $"POSE_TEXT_START{Environment.NewLine}{extendedPoseText}{Environment.NewLine}POSE_TEXT_END";
        var prompt = Regex.Replace(promptTemplate, pattern, replacement, RegexOptions.IgnoreCase);

        var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt)
            };

        var chat = client.GetChatClient(model);
        var temperature = 0.2f;
        var chatOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 2000
        };

        // O-series models only support default temperature (1)
        if (!IsOSeriesModel(model))
        {
            chatOptions.Temperature = temperature;
        }
        else
        {
            temperature = 1.0f; // Use default for O-series models
        }

        // Enable logprobs only when explicitly supported by our capability map
        var requestedLogProbsGen = false;
        if (OpenAIModelExtensions.SupportsLogProbs(model))
        {
            chatOptions.IncludeLogProbabilities = true;
            chatOptions.TopLogProbabilityCount = 3;
            requestedLogProbsGen = true;
        }

        OpenAIServiceLogs.SendingGenerateRequest(_logger, model);

        var startedAt = DateTimeOffset.UtcNow;
        string? content = null;
        ClientResult<ChatCompletion>? response = null;
        OpenAIErrorInfo? error = null;
        PoseRig? rig = null;

        try
        {
            response = await chat.CompleteChatAsync(messages, chatOptions, cancellationToken);
            content = response?.Value?.Content?.FirstOrDefault()?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                OpenAIServiceLogs.EmptyResponse(_logger);
                throw new InvalidOperationException("OpenAI returned an empty response");
            }

            OpenAIServiceLogs.ParsingPoseRig(_logger);
            rig = ParsePoseRig(content);
            OpenAIServiceLogs.RigGenerated(_logger, rig.Bones.Count);
        }
        catch (Exception ex)
        {
            error = _errorHandler.Translate(ex);

            // Write diagnostics even on error
            await WriteDiagnosticsAsync(
                startedAt,
                model,
                temperature,
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["operation"] = "GenerateRig",
                    ["inputLength"] = extendedPoseText.Length,
                    ["requestedLogProbs"] = requestedLogProbsGen
                },
                new Dictionary<string, object?>(),
                error,
                cancellationToken);

            throw;
        }

        // Write diagnostics on success
        await WriteDiagnosticsAsync(
            startedAt,
            model,
            temperature,
            response,
            null,
            new Dictionary<string, object?>
            {
                ["operation"] = "GenerateRig",
                ["inputLength"] = extendedPoseText.Length,
                ["requestedLogProbs"] = requestedLogProbsGen
            },
            new Dictionary<string, object?>
            {
                ["boneCount"] = rig?.Bones.Count ?? 0,
                ["contentLength"] = content?.Length ?? 0
            },
            null,
            cancellationToken);

        return rig!;
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

    private async Task<bool> ProbeChatAsync(OpenAIClient root, string model, CancellationToken ct)
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
            catch (Exception ex) when (i < delays.Length - 1)
            {
                var errorInfo = _errorHandler.Translate(ex);

                // Don't retry on auth errors or quota errors - they won't get better with retries
                if (errorInfo.Code.Contains("401") || errorInfo.Code.Contains("403") || errorInfo.Code.Contains("insufficient_quota"))
                {
                    return false;
                }

                // For rate limits and transient errors, wait before retrying
                await Task.Delay(delays[i], ct);
            }
            catch (Exception ex)
            {
                // Last attempt failed, translate the error for logging/debugging purposes
                var errorInfo = _errorHandler.Translate(ex);
                // Could log errorInfo here if you have a logger
                return false;
            }
        }
        return false;
    }

    private async Task<string?> PickBestModelAsync(OpenAIClient root, HashSet<string> available, string? overrideModelId, CancellationToken cancellationToken)
    {
        var priority = ModeModelMap.GetPriorityList(SelectedMode);

        // If user forces a model id, try it first IF it's in the current mode's priority list
        if (!string.IsNullOrWhiteSpace(overrideModelId)
            && OpenAIModelExtensions.TryParse(overrideModelId, out var overrideModel)
            && available.Contains(overrideModelId!))
        {
            // Check if the override model is appropriate for the selected mode
            if (priority.Contains(overrideModel))
            {
                if (await ProbeChatAsync(root, overrideModelId!, cancellationToken))
                    return overrideModelId;
            }
            // If override is not in priority list, still try it but with lower priority
            // This allows manual model selection to work across modes
            else if (await ProbeChatAsync(root, overrideModelId!, cancellationToken))
            {
                return overrideModelId;
            }
        }

        // Try models from the priority list for the current mode
        foreach (var candidate in priority)
        {
            var candidateId = candidate.GetModelId();
            if (!available.Contains(candidateId)) continue;
            if (await ProbeChatAsync(root, candidateId, cancellationToken))
                return candidateId;
        }

        // NO FALLBACK to other modes - return null to provide clear error
        // This prevents Budget models from being selected when user chose Balanced/Quality mode
        return null;
    }

    private async Task<string> EnsureModelAsync(OpenAIClient root, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_chosenModel)
            && OpenAIModelExtensions.TryParse(_chosenModel, out _))
        {
            return _chosenModel!;
        }

        _chosenModel = null;

        var modelClient = root.GetOpenAIModelClient();
        var modelsPage = await modelClient.GetModelsAsync(cancellationToken);
        var available = modelsPage.Value.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var picked = await PickBestModelAsync(root, available, overrideModelId: null, cancellationToken);

        if (picked is null)
        {
            // Build helpful error message
            var priority = ModeModelMap.GetPriorityList(SelectedMode);
            var requiredModels = string.Join(" or ", priority.Select(m => m.GetModelId()));
            var modeName = SelectedMode.ToString();

            throw new InvalidOperationException(
                $"No models available for {modeName} mode. " +
                $"Required: {requiredModels}. " +
                $"Please check your OpenAI account or select a different mode.");
        }

        _chosenModel = picked;
        return picked;
    }

    private static bool IsOSeriesModel(string modelId)
    {
        var lower = modelId.ToLowerInvariant();
        return lower.StartsWith("o3") || lower.StartsWith("o4");
    }

    private async Task WriteDiagnosticsAsync(
        DateTimeOffset startedAt,
        string model,
        float temperature,
        ClientResult<ChatCompletion>? response,
        object? responsesApiData,
        Dictionary<string, object?> requestMetadata,
        Dictionary<string, object?> responseMetadata,
        OpenAIErrorInfo? error,
        CancellationToken cancellationToken)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var latency = finishedAt - startedAt;

        // Extract usage metrics
        UsageMetrics? usage = null;
        if (response?.Value?.Usage is not null)
        {
            var u = response.Value.Usage;
            usage = new UsageMetrics(
                PromptTokens: u.InputTokenCount,
                CompletionTokens: u.OutputTokenCount,
                TotalTokens: u.TotalTokenCount
            );
        }

        // Extract logprobs if available
        LogProbsBundle? logProbs = null;
        if (response?.Value?.ContentTokenLogProbabilities?.Count > 0)
        {
            var tokens = new List<TokenLogProb>();

            foreach (var contentLogProb in response.Value.ContentTokenLogProbabilities)
            {
                if (contentLogProb?.TopLogProbabilities is null) continue;

                foreach (var tokenLogProb in contentLogProb.TopLogProbabilities)
                {
                    var topLogProbs = contentLogProb.TopLogProbabilities
                        .Select(tlp => new TopLogProb(tlp.Token, tlp.LogProbability))
                        .ToList();

                    tokens.Add(new TokenLogProb(
                        tokenLogProb.Token,
                        tokenLogProb.LogProbability,
                        topLogProbs
                    ));

                    break; // Only take the first token from this content item
                }
            }

            if (tokens.Count > 0)
            {
                logProbs = new LogProbsBundle(tokens);
            }
        }

        // For o-series models, we would extract reasoning items here
        // The current OpenAI SDK 2.5.0 doesn't expose Responses API yet,
        // but we prepare the structure for when it's available
        var reasoningItems = new List<string>();
        string? reasoningSummary = null;

        // Note: When SDK supports Responses API, we'll extract:
        // - reasoning summary from response metadata
        // - reasoning/status items from streaming chunks
        // For now, we check if it's an o-series model and log that info
        if (IsOSeriesModel(model) && response is not null)
        {
            // Future: Extract reasoning data when SDK supports it
            // For now, just note that this was an o-series model
            responseMetadata["modelSeries"] = "o-series";
        }

        var diagnostics = new AiDiagnostics(
            ReasoningSummary: reasoningSummary,
            ReasoningItems: reasoningItems,
            Usage: usage,
            Model: model,
            Temperature: temperature,
            Seed: null, // SDK doesn't expose seed in responses yet
            LogProbs: logProbs,
            ToolTrace: Array.Empty<ToolCallTrace>(), // No tool use in current implementation
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            Latency: latency
        );

        await _diagnosticsLogger.WriteAsync(
            diagnostics,
            requestMetadata,
            responseMetadata,
            error,
            cancellationToken);
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

internal static partial class OpenAIServiceLogs
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "OpenAIService initialized")]
    public static partial void ServiceInitialized(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validating OpenAI API key")]
    public static partial void ValidatingApiKey(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key is empty")]
    public static partial void ApiKeyEmpty(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retrieved {Count} models from OpenAI")]
    public static partial void ModelsListRetrieved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key validated successfully, selected model: {Model}")]
    public static partial void ApiKeyValidated(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key validated successfully, no model matched current mode")]
    public static partial void ApiKeyValidatedNoModel(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "API key validation failed")]
    public static partial void ApiKeyValidationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolving model for mode: {Mode}")]
    public static partial void ResolvingModel(ILogger logger, string mode);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenAI API key is not set")]
    public static partial void ApiKeyNotSet(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using cached model: {Model}")]
    public static partial void UsingCachedModel(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Invalidating cached model: {Model}")]
    public static partial void InvalidatingCachedModel(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Error, Message = "No compatible model found for mode: {Mode}, required: {RequiredModels}")]
    public static partial void NoCompatibleModel(ILogger logger, string mode, string requiredModels);

    [LoggerMessage(Level = LogLevel.Information, Message = "Model resolved: {Model}")]
    public static partial void ModelResolved(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyzing pose from image: {ImagePath}")]
    public static partial void AnalyzingPose(ILogger logger, string imagePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image file not found: {ImagePath}")]
    public static partial void ImageFileNotFound(ILogger logger, string imagePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Using model: {Model}")]
    public static partial void UsingModel(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending vision request to OpenAI model: {Model}")]
    public static partial void SendingVisionRequest(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenAI returned empty response")]
    public static partial void EmptyResponse(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pose analysis complete, response length: {Length}")]
    public static partial void PoseAnalysisComplete(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating rig from extended pose text, length: {Length}")]
    public static partial void GeneratingRig(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending generate rig request to OpenAI model: {Model}")]
    public static partial void SendingGenerateRequest(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parsing pose rig from OpenAI response")]
    public static partial void ParsingPoseRig(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rig generated successfully with {BoneCount} bones")]
    public static partial void RigGenerated(ILogger logger, int boneCount);
}
