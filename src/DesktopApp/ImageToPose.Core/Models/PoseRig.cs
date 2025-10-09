namespace ImageToPose.Core.Models;

/// <summary>
/// Complete pose rig with all bone rotations
/// </summary>
public class PoseRig
{
    public List<BoneRotation> Bones { get; set; } = new();
}
