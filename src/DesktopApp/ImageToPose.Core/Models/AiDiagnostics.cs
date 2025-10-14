namespace ImageToPose.Core.Models;

/// <summary>
/// Comprehensive diagnostics data for an AI model execution.
/// </summary>
public sealed record AiDiagnostics(
    string? ReasoningSummary,
    IReadOnlyList<string> ReasoningItems,
    UsageMetrics? Usage,
    string? Model,
    double? Temperature,
    int? Seed,
    LogProbsBundle? LogProbs,
    IReadOnlyList<ToolCallTrace> ToolTrace,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    TimeSpan Latency
);

/// <summary>
/// Token usage metrics for a model execution.
/// </summary>
public sealed record UsageMetrics(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int? ReasoningTokens = null
);

/// <summary>
/// Log probabilities bundle for model output tokens.
/// </summary>
public sealed record LogProbsBundle(
    IReadOnlyList<TokenLogProb> Tokens
);

/// <summary>
/// Log probability information for a single token.
/// </summary>
public sealed record TokenLogProb(
    string Token,
    double LogProb,
    IReadOnlyList<TopLogProb> TopLogProbs
);

/// <summary>
/// Top alternative tokens with their log probabilities.
/// </summary>
public sealed record TopLogProb(
    string Token,
    double LogProb
);

/// <summary>
/// Trace of a tool call during model execution.
/// </summary>
public sealed record ToolCallTrace(
    string ToolName,
    string Arguments,
    string? Result
);
