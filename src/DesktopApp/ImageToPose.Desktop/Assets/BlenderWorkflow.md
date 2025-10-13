# Blender Workflow Guide

This guide explains how to apply the generated bone rotations from the Image To Pose Generator to an MPFB GameEngine rig in Blender, and how to retarget them to a Cyberpunk 2077 character rig.

> **Note:** This guide assumes you've already generated bone rotations using either the desktop application or Python scripts. See the main [README.md](../../../../../README.md) for generation instructions.

## Prerequisites

### Required Software
- **Blender 4.2 or later**
- **[MPFB addon](http://static.makehumancommunity.org/mpfb.html)** - For creating base character rigs
- **[Blender-Animation-Retargeting addon](https://github.com/BlenderDefender/blender_animation_retargeting)** - For retargeting poses to CP2077 rigs

### Required Assets
- **Generated POSE_DEGREES dictionary** from Image To Pose Generator
- **[xBaebsae Pose Templates (Google Drive)](https://drive.google.com/file/d/1ifzMpMfCNK0mJABz56YDfw-2JcMjswDE/view?usp=sharing)** - Download `xBaebsae_Pose_Templates_-_Zwei_Multibody.rar` archive
- **[MPFB_GameEngine_To_C2077_Rig_Retarget_Preset.blend-retarget](./MPFB_GameEngine_To_C2077_Rig_Retarget_Preset.blend-retarget)** - Included in this repository

> **Important:** The xBaebsae archive contains old C2077 rig templates **without IK controls**. The new templates with IK controls have not been tested and may not work correctly with retargeting.

## Workflow Overview

1. Generate POSE_DEGREES dictionary using Image To Pose Generator
2. Download and prepare xBaebsae C2077 rig template
3. Create MPFB character with Game Engine rig
4. Apply pose to MPFB rig using Python script
5. Retarget pose to C2077 rig using Blender-Animation-Retargeting addon
6. Create pose actions and export to WolvenKit

## Step 1: Generate POSE_DEGREES Dictionary

Use the Image To Pose Generator desktop application or Python scripts to generate bone rotations from your reference image. The output will be a JSON dictionary like this:

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

Copy this dictionary - you'll need it in Step 4.

## Step 2: Prepare C2077 Rig Template

1. **Download the xBaebsae archive:**
   - Get `xBaebsae_Pose_Templates_-_Zwei_Multibody.rar` from [Google Drive](https://drive.google.com/file/d/1ifzMpMfCNK0mJABz56YDfw-2JcMjswDE/view?usp=sharing)
   - Extract the archive

2. **Select appropriate template:**
   - For female characters: `xBaebsae_AnimTemplate_FemaleAverage_Zwei_Multibody_AIO.blend`
   - For male characters: Use the corresponding male template from the archive

3. **Open the template in Blender:**
   - File → Open
   - Navigate to the extracted archive
   - Open your selected `.blend` file

## Step 3: Create MPFB Character

1. **Create MPFB collection:**
   - In the Outliner, create a new collection (right-click → New Collection)
   - Name it "MPFB" or similar

2. **Install MPFB addon** (if not already installed):
   - Edit → Preferences → Add-ons
   - Click Install and select the MPFB addon
   - Enable the addon

3. **Create base human:**
   - Open the MPFB panel (usually in the N-panel sidebar)
   - Go to **New Human → From Scratch** tab
   - Configure parameters:
     - **Gender:** Female (for female rig) or Male (for male rig)
     - **Height:** Tall
     - Leave all other parameters at default
   - Click **Create Human**

4. **Add Game Engine rig:**
   - Go to **Rigging → Add Rig** tab
   - Select **"Game Engine"** rig type
   - Click **Add Standard Rig**

5. **Transform the rig:**
   - Select the MPFB armature
   - **Rotate:** Press `R` → `Z` → type `180` → Press `Enter`
   - **Scale:**
     - Press `S` → type `0.7852` (for female) or `0.744` (for male) → Press `Enter`

## Step 4: Apply Pose to MPFB Rig

1. **Open Scripting workspace:**
   - Switch to the **Scripting** workspace tab in Blender

2. **Load the pose script:**
   - Open the [apply_pose_template.py](./apply_pose_template.py) script from this repository
   - Copy the script content into Blender's text editor

3. **Insert your POSE_DEGREES data:**
   - In the script, find the `POSE_DEGREES` dictionary
   - Replace it with your generated dictionary from Step 1:
   ```python
   POSE_DEGREES = {
       "pelvis": [0.0, 0.0, 5.0],
       "spine_01": [10.0, 0.0, -2.0],
       # ... paste your complete dictionary here
   }
   ```

4. **Run the script:**
   - Select your MPFB armature in the 3D viewport
   - Press `Alt+P` or click the **Run Script** button (▶)
   - The script will apply all rotations to the armature

5. **Review and adjust the pose:**
   - The armature will be in Pose Mode
   - Check the pose in the 3D viewport
   - Make manual adjustments if needed:
     - Select individual bones
     - Rotate using `R` + axis key (`X`, `Y`, or `Z`)

### Rotation Conventions Reference

- **Spine/Torso:** X=left/right lean, Y=back/forward lean, Z=twist
- **Arms:** X=forward/back, Y=backward, Z=up
- **Legs:** Hip mechanics vary by side
- **Joints:** Y=hinge rotation (elbows, knees)

See [chatgpt_prompt.txt](./chatgpt_prompt.txt) for complete rotation conventions.

### Script Configuration Options

- `SWAP_LR`: Set to `True` to flip left/right for mirrored references
- `AUTO_HINGE`: Set to `True` to auto-detect hinge joints from constraints
- `ARMATURE_NAME`: Change if your armature has a different name

## Step 5: Retarget Pose to C2077 Rig

Now that you have the pose applied to the MPFB rig, retarget it to the C2077 rig using the Blender-Animation-Retargeting addon.

1. **Install Blender-Animation-Retargeting addon** (if not already installed):
   - Download from [GitHub](https://github.com/BlenderDefender/blender_animation_retargeting)
   - Edit → Preferences → Add-ons → Install
   - Enable the addon

2. **Load the retargeting preset:**
   - Use the included [MPFB_GameEngine_To_C2077_Rig_Retarget_Preset.blend-retarget](./MPFB_GameEngine_To_C2077_Rig_Retarget_Preset.blend-retarget) file
   - This preset contains the bone mapping between MPFB Game Engine and C2077 rigs

3. **Apply retargeting:**
   - Follow the [Blender-Animation-Retargeting addon instructions](https://github.com/BlenderDefender/blender_animation_retargeting)
   - Select the MPFB armature as source
   - Select the C2077 armature as target
   - Load the preset and apply retargeting

4. **Fine-tune the retargeted pose:**
   - Check for any issues with bone rotations
   - Adjust individual bones on the C2077 rig as needed
   - Pay special attention to:
     - Hand and finger positions
     - Foot placement
     - Elbow and knee angles

## Step 6: Create Pose Actions and Export

1. **Create pose action:**
   - With the C2077 rig selected and posed
   - In the Dope Sheet, change mode to **Action Editor**
   - Click the **New Action** button (+)
   - Name your action appropriately (e.g., "CustomPose_01")
   - Insert keyframes for all bones (Pose → Animation → Insert Keyframe → Available)

2. **Export to WolvenKit:**
   - Follow the [Cyberpunk 2077 Modding Wiki - Animations guide](https://wiki.redmodding.org/cyberpunk-2077-modding/modding-guides/animations/animations)
   - Export as FBX with appropriate settings for CP2077
   - Import into WolvenKit and configure for your mod

### Additional Resources for Export

- **[CP2077 Animation Guide by xBaebsae](https://docs.google.com/document/d/1CrPTKiGJzy2Tj_klJVHhRdXZgqD7yC2ZsJuRu9nqQuc/edit?tab=t.0)**
- **[CP2077 Animation Retargeting Guide](https://docs.google.com/document/d/1nHPQvkK6ijwb8iQ8y1X8CBG-wnNUCTYCjrdUCGMenW4/edit?tab=t.0#heading=h.kvak42tu0v94)**
- **[CP2077 Pose/Animation Tutorial](https://docs.google.com/document/d/1e7NsVgWHH19mTNw60E3H3u7G3Rlw3dUVWzLUHGvBUwY/edit?tab=t.0)**
- **[CP2077 Modding Wiki - Animations](https://wiki.redmodding.org/cyberpunk-2077-modding/modding-guides/animations/animations)**

## Troubleshooting

### Bones Not Rotating as Expected

**Problem:** Bones don't rotate when running the script or rotations look incorrect.

**Solutions:**
- **Check rotation mode:** MPFB uses XYZ Euler. Verify both rigs use the same rotation mode
  - Select the armature → Pose Mode → Select a bone → Properties panel (N) → Transform → Rotation Mode
- **Verify bone names:** Ensure the script is finding the correct bones
  - Check the Console window for error messages about missing bones
- **Check constraints:** Disable IK or other constraints that might interfere
  - Select bone → Properties panel → Bone Constraints tab → Disable constraints temporarily

### Pose Looks Wrong After Retargeting

**Problem:** The pose transfers to the C2077 rig but looks distorted or unnatural.

**Causes and Solutions:**
- **Different proportions:** CP2077 characters have different proportions than MPFB
  - *Solution:* Manually adjust individual bones on the C2077 rig
- **Rest pose differences:** The T-pose or A-pose might differ between rigs
  - *Solution:* Apply a percentage of the rotation or adjust manually
- **Bone hierarchy differences:** Some bones may have different parent-child relationships
  - *Solution:* Check the retargeting preset and adjust bone mappings if needed

### Script Errors

**Common errors and solutions:**

- **"Bone not found" error:**
  - Check the `ARMATURE_NAME` variable in the script matches your armature name
  - Verify bone naming conventions match MPFB Game Engine rig
  
- **"No active object" error:**
  - Make sure an armature is selected before running the script
  - The armature must be the active object (orange outline)
  
- **Permission errors:**
  - Blender may have restrictions on running Python scripts
  - Go to Edit → Preferences → Save & Load → Enable "Auto Run Python Scripts"

### Retargeting Issues

**Problem:** Retargeting addon doesn't work or produces errors.

**Solutions:**
- Ensure both armatures are in the same scene
- Check that the retargeting preset file is loaded correctly
- Verify both source (MPFB) and target (C2077) rigs are properly selected
- Make sure the C2077 rig is in rest pose before applying retargeting

### Scale Issues

**Problem:** The MPFB character is the wrong size compared to C2077 rig.

**Solutions:**
- Double-check the scale values: `0.7852` for female, `0.744` for male
- Make sure to press Enter after typing the scale value
- If the scale is still wrong, apply the scale (Ctrl+A → Scale) and readjust

## Tips and Best Practices

### For Better Results

1. **Always save your work:**
   - Save the Blender file before applying scripts or retargeting
   - Use incremental saves (File → Save As) to keep versions

2. **Test with simple poses first:**
   - Start with basic standing or sitting poses to understand the workflow
   - Move to complex poses once you're comfortable with the process

3. **Check pose from multiple angles:**
   - Use the numpad keys (1, 3, 7) to view from different orthographic views
   - Rotate the viewport to check the pose from all angles

4. **Use bone layers:**
   - Hide unnecessary bones using bone layers to reduce clutter
   - Focus on main body bones first, then refine extremities

5. **Save pose presets:**
   - Create a pose library with your successful poses
   - This allows you to reuse and combine poses

### Performance Tips

- **Work with low-poly models** during posing and retargeting
- **Disable viewport overlays** (Alt+Shift+Z) for better viewport performance
- **Use solid shading** instead of rendered view when posing

## Additional Resources

### Blender Documentation

- **[Pose Mode](https://docs.blender.org/manual/en/latest/animation/armatures/posing/index.html)** - Official Blender pose mode documentation
- **[Bone Constraints](https://docs.blender.org/manual/en/latest/animation/constraints/index.html)** - Understanding bone constraints
- **[Action Editor](https://docs.blender.org/manual/en/latest/editors/dope_sheet/action.html)** - Working with animation actions

### MPFB Resources

- **[MPFB Documentation](http://static.makehumancommunity.org/mpfb.html)** - Official MPFB documentation
- **[MPFB GitHub Repository](https://github.com/makehumancommunity/mpfb)** - Source code and issue tracker

### Cyberpunk 2077 Modding

- **[CP2077 Modding Wiki](https://wiki.redmodding.org/)** - Comprehensive modding documentation
- **[CP2077 Modding Discord](https://discord.gg/redmodding)** - Community support and discussions
- **[WolvenKit](https://github.com/WolvenKit/WolvenKit)** - Essential tool for CP2077 modding

## License

This workflow guide is part of the Image To Pose Generator project and follows the same license terms. See [LICENSE+TERMS.md](../../../../../LICENSE+TERMS.md) for details.
