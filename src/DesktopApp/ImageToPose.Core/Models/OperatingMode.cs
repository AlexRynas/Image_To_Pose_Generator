using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Serialization;

namespace ImageToPose.Core.Models;

public enum OperatingMode
{
    Budget = 0,
    Balanced = 1,
    Quality = 2,
}

public enum OpenAIModel
{
    [EnumMember(Value = "gpt-4.1-nano")]
    Gpt41Nano = 0,
    [EnumMember(Value = "gpt-4.1-mini")]
    Gpt41Mini = 1,
    [EnumMember(Value = "gpt-4.1")]
    Gpt41 = 2,
    [EnumMember(Value = "o4-mini")]
    O4Mini = 3,
    [EnumMember(Value = "gpt-5")]
    Gpt5 = 4,
    [EnumMember(Value = "o3")]
    O3 = 5,
}

public static class OpenAIModelExtensions
{
    private static readonly IReadOnlyDictionary<OpenAIModel, string> _idByModel;
    private static readonly IReadOnlyDictionary<string, OpenAIModel> _modelById;
    private static readonly IReadOnlyDictionary<OpenAIModel, ModelCapability> _capabilities;

    static OpenAIModelExtensions()
    {
        var allValues = Enum.GetValues<OpenAIModel>();
        var idByModel = new Dictionary<OpenAIModel, string>(allValues.Length);

        foreach (var model in allValues)
        {
            var id = model.GetEnumMemberValue() ?? model.ToString();
            idByModel[model] = id;
        }

        _idByModel = new ReadOnlyDictionary<OpenAIModel, string>(idByModel);
        _modelById = new ReadOnlyDictionary<string, OpenAIModel>(
            idByModel.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase));
        // Capability matrix derived from OpenAI model documentation.
        // Conservative defaults: many o-series and 4.1-family models do not allow token logprobs.
        var caps = new Dictionary<OpenAIModel, ModelCapability>
        {
            [OpenAIModel.Gpt41Nano] = new ModelCapability(SupportsLogProbs: true, SupportsTemperature: true),
            [OpenAIModel.Gpt41Mini] = new ModelCapability(SupportsLogProbs: true, SupportsTemperature: true),
            [OpenAIModel.Gpt41] = new ModelCapability(SupportsLogProbs: true, SupportsTemperature: true),
            [OpenAIModel.O4Mini] = new ModelCapability(SupportsLogProbs: false, SupportsTemperature: false),
            [OpenAIModel.Gpt5] = new ModelCapability(SupportsLogProbs: false, SupportsTemperature: false),
            [OpenAIModel.O3] = new ModelCapability(SupportsLogProbs: false, SupportsTemperature: false)
        };
        _capabilities = new ReadOnlyDictionary<OpenAIModel, ModelCapability>(caps);
        All = Array.AsReadOnly(allValues);
    }

    public static IReadOnlyList<OpenAIModel> All { get; }

    public static string GetModelId(this OpenAIModel model) => _idByModel[model];

    public static bool TryParse(string? modelId, out OpenAIModel model)
    {
        if (!string.IsNullOrWhiteSpace(modelId) && _modelById.TryGetValue(modelId, out model))
        {
            return true;
        }

        model = default;
        return false;
    }

    private static string? GetEnumMemberValue(this OpenAIModel model)
    {
        var member = typeof(OpenAIModel).GetField(model.ToString(), BindingFlags.Public | BindingFlags.Static);
        return member?.GetCustomAttribute<EnumMemberAttribute>()?.Value;
    }

    public static bool SupportsLogProbs(this OpenAIModel model)
        => _capabilities.TryGetValue(model, out var cap) && cap.SupportsLogProbs;

    public static bool SupportsLogProbs(string modelId)
        => TryParse(modelId, out var m) && m.SupportsLogProbs();

    public static bool SupportsTemperature(this OpenAIModel model)
        => _capabilities.TryGetValue(model, out var cap) && cap.SupportsTemperature;

    public static bool SupportsTemperature(string modelId)
        => TryParse(modelId, out var m) && m.SupportsTemperature();
}

public readonly record struct ModelCapability(bool SupportsLogProbs, bool SupportsTemperature);

public static class ModeModelMap
{
    public static readonly IReadOnlyList<OpenAIModel> BudgetPreferred = new[]
    {
        OpenAIModel.Gpt41Nano,
        OpenAIModel.Gpt41Mini,
    };
    // For now I have disabled the o-series models because they do not work and I do not yet know how to fix this.
    public static readonly IReadOnlyList<OpenAIModel> BalancedPreferred = new[]
    {
        //OpenAIModel.O4Mini,
        OpenAIModel.Gpt41,
    };

    public static readonly IReadOnlyList<OpenAIModel> QualityPreferred = new[]
    {
        OpenAIModel.Gpt5,
        //OpenAIModel.O3,
    };

    public static IReadOnlyList<OpenAIModel> GetPriorityList(OperatingMode mode) => mode switch
    {
        OperatingMode.Budget => BudgetPreferred,
        OperatingMode.Balanced => BalancedPreferred,
        OperatingMode.Quality => QualityPreferred,
        _ => BalancedPreferred,
    };

    public static string GetModeDescription(OperatingMode mode) => mode switch
    {
        OperatingMode.Budget => "Fast & cheapest; ok for simple photos.",
        OperatingMode.Balanced => "Good quality for most cases.",
        OperatingMode.Quality => "Best quality at a sensible price.",
        _ => string.Empty
    };
}
