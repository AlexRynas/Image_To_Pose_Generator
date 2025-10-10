namespace ImageToPose.Core.Models;

/// <summary>
/// Input for pose analysis containing an image and structured USER-SET ANCHORS.
/// </summary>
public class PoseInput
{
    public string ImagePath { get; set; } = string.Empty;

    // USER-SET ANCHORS fields
    public string LeftHand { get; set; } = string.Empty;
    public string RightHand { get; set; } = string.Empty;
    public string LeftFoot { get; set; } = string.Empty;
    public string RightFoot { get; set; } = string.Empty;
    public string Facing { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Build the USER-SET ANCHORS block expected by the analyze-image prompt.
    /// </summary>
    public string ToUserAnchorsBlock()
    {
        // Use empty placeholders if missing so format stays stable.
        string leftHand = string.IsNullOrWhiteSpace(LeftHand) ? "unknown" : LeftHand.Trim();
        string rightHand = string.IsNullOrWhiteSpace(RightHand) ? "unknown" : RightHand.Trim();
        string leftFoot = string.IsNullOrWhiteSpace(LeftFoot) ? "unknown" : LeftFoot.Trim();
        string rightFoot = string.IsNullOrWhiteSpace(RightFoot) ? "unknown" : RightFoot.Trim();
        string facing = string.IsNullOrWhiteSpace(Facing) ? "uncertain" : Facing.Trim();
        string notes = string.IsNullOrWhiteSpace(Notes) ? "none" : Notes.Trim();

        return $"[USER_ANCHORS] Left hand={leftHand}; Right hand={rightHand}; Left foot={leftFoot}; Right foot={rightFoot}; Facing={facing}; Notes={notes}";
    }
}