namespace ImageToPose.Core.Models;

/// <summary>
/// Input for pose generation containing an image and text description
/// </summary>
public class PoseInput
{
    public string ImagePath { get; set; } = string.Empty;
    public string RoughPoseText { get; set; } = string.Empty;
}
