namespace ImageToPose.Core.Models;

public class PricingModelRates
{
    public string ModelId { get; set; } = string.Empty;
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}

public class StepCostEstimate
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal InputUsd { get; set; }
    public decimal OutputUsd { get; set; }
    public decimal TotalUsd { get; set; }
}
