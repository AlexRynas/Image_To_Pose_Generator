# Blender Workflow Guide

This guide explains how to apply the generated bone rotations from the Image To Pose Generator to an MPFB GameEngine rig in Blender, and how to retarget them to a Cyberpunk 2077 character rig.

> **Note:** This guide assumes you've already generated bone rotations using either the desktop application or Python scripts. See the main [README.md](../../../../../README.md) for generation instructions.

## Prerequisites

- Blender 4.2 or later
- MPFB addon installed
- A character with an MPFB GameEngine rig
- Generated bone rotation JSON from the Image To Pose Generator
- (Optional) Cyberpunk 2077 character rig for retargeting

## Step 1: Apply Rotations to MPFB GameEngine Rig

### Using the Desktop Application

1. **Generate pose rotations** using the Image To Pose Generator desktop application
2. **Copy the JSON output** - it will look like this:
   ```json
   {
     "POSE_DEGREES": {
       "pelvis": [0.0, 0.0, 5.0],
       "spine_01": [10.0, 0.0, -2.0],
       "head": [15.0, -5.0, 3.0],
       ...
     }
   }
   ```

### Applying via Python Script

1. Open the `apply_pose_template.py` script from the repository
2. Replace the `POSE_DEGREES` dictionary with your generated data:
   ```python
   POSE_DEGREES = {
       "pelvis": [0.0, 0.0, 5.0],
       "spine_01": [10.0, 0.0, -2.0],
       # ... rest of your bones
   }
   ```

3. In Blender:
   - Select your armature with the MPFB GameEngine rig
   - Open the Scripting workspace
   - Load the `apply_pose_template.py` script
   - Run the script (Alt+P or click the ? button)

4. The script will:
   - Switch the armature to Pose Mode
   - Apply all bone rotations from the POSE_DEGREES dictionary
   - Leave the armature in Pose Mode for you to review

### Rotation Conventions

- **Spine/Torso**: X=left/right lean, Y=back/forward lean, Z=twist
- **Arms**: X=forward/back, Y=backward, Z=up
- **Legs**: Hip mechanics vary by side
- **Joints**: Y=hinge rotation (elbows, knees)

See `chatgpt_prompt.txt` for complete conventions.

### Configuration

- `SWAP_LR`: Flip left/right for mirrored references
- `AUTO_HINGE`: Auto-detect hinge joints from constraints
- `ARMATURE_NAME`: Change if armature name differs

### Manual Application

If you prefer to apply rotations manually:

1. Select your armature and switch to **Pose Mode** (Ctrl+Tab)
2. For each bone in the POSE_DEGREES output:
   - Select the bone (click on it or use the Outliner)
   - Press `R` then `X` and type the X rotation value
   - Press `R` then `Y` and type the Y rotation value
   - Press `R` then `Z` and type the Z rotation value
3. Press Enter after each rotation to confirm

## Step 2: Retargeting to Cyberpunk 2077 Rig

The MPFB GameEngine rig uses different bone naming and hierarchy than Cyberpunk 2077 rigs. Here's how to transfer the pose:

### Option A: Using Blender's Copy Pose Feature

1. **Set up both rigs:**
   - Import or have your CP2077 character with its rig in the scene
   - Ensure your MPFB rig is in the posed state from Step 1

2. **Position the rigs:**
   - Align both rigs in a similar location (not required but helpful)
   - Make sure both are in Pose Mode

3. **Create pose library:**
   - With the MPFB rig selected and in Pose Mode
   - Go to Pose ? Pose Library ? Add Pose
   - Name it (e.g., "Generated Pose")

4. **Copy to CP2077 rig:**
   - Select the CP2077 rig
   - You'll need to manually map the bones or use retargeting tools

### Option B: Using Retargeting Addon

Several Blender addons can help with retargeting between different rig types:

- **Rokoko Studio Live** - Free addon with retargeting features
- **Auto-Rig Pro** - Paid addon with advanced retargeting
- **BlenRig** - Free rigging system with retargeting tools

### Option C: Manual Retargeting

Create a bone mapping between MPFB and CP2077 rigs:

```
MPFB GameEngine ? CP2077
---------------------------------
pelvis ? Root
spine_01 ? Spine1
spine_02 ? Spine2
spine_03 ? Spine3
neck_01 ? Neck
head ? Head
clavicle_l ? LeftClavicle
upperarm_l ? LeftUpperArm
lowerarm_l ? LeftForearm
hand_l ? LeftHand
(and so on...)
```

Then manually copy rotation values or use a Python script to automate the transfer.

## Step 3: Fine-Tuning and Export

### Adjust the Pose

- Minor adjustments may be needed due to different bone rest positions
- Use the 3D Viewport and properties panel to tweak individual bones
- Pay attention to:
  - Elbow and knee bending (may need adjustment for natural look)
  - Hand and finger positions
  - Foot placement and weight distribution

### Save the Pose

1. **As a Pose Asset:**
   - Pose ? Pose Library ? Add Pose
   - This lets you reuse the pose on other characters

2. **As an Action:**
   - In the Dope Sheet, change mode to "Action Editor"
   - Click the shield icon to save the action
   - Name it appropriately

3. **Export for Game:**
   - For Cyberpunk 2077 modding:
     - Export as FBX with appropriate settings for the game
     - Follow CP2077 modding guidelines for animations
   - Consult CP2077 modding community resources for specific export parameters

## Troubleshooting

### Bones Not Rotating as Expected

- **Check rotation mode:** MPFB uses XYZ Euler. Make sure the target rig uses the same.
- **Verify bone names:** Ensure the script is finding the correct bones
- **Check constraints:** Disable IK or other constraints that might interfere

### Pose Looks Wrong After Retargeting

- **Different proportions:** CP2077 characters may have different proportions than MPFB
- **Rest pose differences:** The T-pose or rest pose might be different
- **Solution:** Apply a percentage of the rotation or adjust manually

### Script Errors

- **"Bone not found":** Check the armature name and bone naming convention
- **"No active object":** Make sure an armature is selected
- **Permission errors:** Make sure Blender has permission to run scripts

## Future Enhancements

The following features are planned or could be added:

### Automated Retargeting

- **Python script for direct retargeting:** Create a script that:
  - Takes the POSE_DEGREES dictionary
  - Maps MPFB bones to CP2077 bones automatically
  - Applies rotations with adjustment factors for proportion differences
  - Handles bone hierarchy differences

### Headless Blender Export

- **Command-line tool:**  
  ```bash
  blender --background --python auto_pose.py -- \
    --input pose_rotations.json \
    --rig-type cp2077 \
    --character MyCharacter.blend \
    --output posed_character.fbx
  ```

### Blender Addon

- Create a full Blender addon with UI panel that:
  - Imports pose JSON directly
  - Provides preset mappings for popular game rigs
  - Offers visual preview before applying
  - Batch processes multiple poses

### Pose Library Integration

- Integrate with Blender's Asset Browser
- Create tagged pose collections
- Share poses across projects

### Real-time Preview

- Desktop app could show a 3D preview of the rig with rotations applied
- Use a lightweight viewer (Three.js or similar)
- Adjust rotations interactively before export

## Resources

### Blender Documentation

- [Pose Mode](https://docs.blender.org/manual/en/latest/animation/armatures/posing/index.html)
- [Bone Constraints](https://docs.blender.org/manual/en/latest/animation/constraints/index.html)
- [Action Editor](https://docs.blender.org/manual/en/latest/editors/dope_sheet/action.html)

### MPFB Resources

- [MPFB Documentation](http://static.makehumancommunity.org/mpfb.html)
- [MPFB GitHub](https://github.com/makehumancommunity/mpfb)

### Cyberpunk 2077 Modding

- [CP2077 Modding Wiki](https://wiki.redmodding.org/)
- [CP2077 Modding Discord](https://discord.gg/redmodding)

## Contributing

If you develop improvements to this workflow:

- Share your retargeting scripts in the repository
- Document any CP2077-specific export settings
- Create tutorials or video guides
- Report issues or suggest enhancements via GitHub Issues

## License

This workflow guide is part of the Image To Pose Generator project and follows the same license terms.
