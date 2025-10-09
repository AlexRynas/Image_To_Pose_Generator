namespace ImageToPose.Core.Models;

/// <summary>
/// Represents rotation values for a single bone
/// </summary>
public class BoneRotation
{
    public string BoneName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
