namespace ImageToPose.Core.Models;

public enum OperatingMode
{
    Budget = 0,
    Balanced = 1,
    Quality = 2,
}

public static class ModeModelMap
{
    public static readonly IReadOnlyList<string> BudgetPreferred = new[]
    {
        "gpt-4.1-nano",
        "gpt-4.1-mini",
    };

    public static readonly IReadOnlyList<string> BalancedPreferred = new[]
    {
        "gpt-4.1-mini",
        "o4-mini",
        "gpt-4.1",
    };

    public static readonly IReadOnlyList<string> QualityPreferred = new[]
    {
        "gpt-4.1",
        "o4-mini",
    };

    public static IReadOnlyList<string> GetPriorityList(OperatingMode mode) => mode switch
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
