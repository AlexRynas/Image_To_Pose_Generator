# Image To Pose Generator

A Blender addon/tool that generates character poses from reference images using AI-powered pose analysis and automated rigging.

## Overview

This project enables you to:
1. Analyze a reference image of a person to extract detailed pose descriptions
2. Convert those descriptions into precise bone rotations for Blender armatures
3. Automatically apply poses to your rigged characters

The workflow combines computer vision analysis with biomechanical understanding to create accurate 3D poses from 2D references.

## Features

- **AI-Powered Pose Analysis**: Uses structured prompts to analyze human poses in images with anatomical precision
- **Blender Integration**: Seamlessly works with Blender 4.2+ armatures
- **FK Rigging Support**: Designed for Forward Kinematics (FK) rigs with local XYZ Euler rotations
- **Anatomical Accuracy**: Respects proper anatomical conventions (left/right from character's perspective)
- **Comprehensive Bone Coverage**: Supports full body rigs including:
  - Spine chain (Hips, Spine, Spine1-3, Neck, Head)
  - Arms (Shoulders, Arms, Forearms, Hands)
  - Legs (Upper legs, Lower legs, Feet, Toes)
- **Armature Export**: Export detailed armature data for analysis and processing

## Project Structure

```
Image_To_Pose_Generator/
├── analyse_image_and_get_pose_description_prompt.txt  # AI prompt for pose analysis
├── chatgpt_prompt.txt                                 # Instructions for pose-to-rotation conversion
├── apply_pose_template.py                             # Blender script to apply poses
├── armature_exporter.py                               # Export armature data to JSON
└── README.md                                          # This file
```

## How It Works

### 1. Image Analysis
Using the prompts in `analyse_image_and_get_pose_description_prompt.txt`, an AI vision model analyzes reference images to generate detailed pose descriptions covering:
- Camera angle and view
- Weight distribution and stance
- Head and neck positioning
- Torso orientation and lean
- Shoulder and arm positions
- Hand gestures and orientations
- Hip and leg positioning
- Foot angles and ankle positions

### 2. Pose Conversion
The `chatgpt_prompt.txt` contains detailed instructions for converting natural language pose descriptions into precise bone rotations, including:
- Rotation conventions and axis definitions
- Anatomical left/right conventions
- Bone hierarchy and constraints
- Angle calculations for each bone

### 3. Pose Application
The `apply_pose_template.py` script applies the calculated rotations to Blender armatures:
- Sets up pose mode
- Applies rotations to each bone in the correct order
- Respects bone constraints and limitations
- Supports left/right swapping for mirrored poses

## Usage

### Prerequisites
- Blender 4.2 or later
- A rigged character with standard bone naming conventions
- AI model access (ChatGPT, Claude, etc.) for pose analysis

### Basic Workflow

1. **Prepare Your Armature**
   - Ensure your character rig uses standard bone names (Hips, Spine, LeftArm, etc.)
   - Verify the armature is named "Armature" (or modify the script accordingly)

2. **Export Armature Data** (optional)
   ```python
   # Run in Blender's Python console
   exec(open("armature_exporter.py").read())
   ```

3. **Analyze Your Reference Image**
   - Use the prompt from `analyse_image_and_get_pose_description_prompt.txt` with an AI vision model
   - Provide your reference image to get a detailed pose description

4. **Generate Pose Rotations**
   - Use the instructions in `chatgpt_prompt.txt` along with your pose description
   - The AI will output precise rotation values for each bone

5. **Apply the Pose**
   - Copy the generated rotation values into the `POSE_DEGREES` dictionary in `apply_pose_template.py`
   - Run the script in Blender to apply the pose to your character

### Example Bone Rotation Format
```python
POSE_DEGREES = {
    "Hips":   [0.0, 0.0, 5.0],     # Slight right hip rotation
    "Spine":  [0.0, 0.0, -2.0],    # Counter-rotation
    "Head":   [10.0, -5.0, 15.0],  # Head turned and tilted
    "LeftArm": [45.0, -30.0, 20.0], # Arm raised and positioned
    # ... more bones
}
```

## Rotation Conventions

The system uses specific rotation conventions for each bone type:

### Spine/Torso (X=left, Y=up, Z=right)
- X: Left lean (positive) / Right lean (negative)
- Y: Backward lean (positive) / Forward lean (negative)  
- Z: Right twist (positive) / Left twist (negative)

### Arms
- **Shoulders**: X=backward, Y=backward, Z=up
- **Upper Arms**: X=forward, Y=backward, Z=up
- **Forearms**: Y=unclenches (hinge joint)
- **Hands**: Varies by side (see chatgpt_prompt.txt for details)

### Legs
- **Upper Legs**: Varies by side for proper hip mechanics
- **Lower Legs**: Y=forward (hinge joint)
- **Feet**: Ankle rotations for realistic foot positioning

## Configuration Options

### apply_pose_template.py Settings
- `SWAP_LR`: Flip left/right if working with mirrored reference images
- `AUTO_HINGE`: Automatically detect hinge joints from bone constraints
- `ARMATURE_NAME`: Change if your armature has a different name

## Tips and Best Practices

1. **Reference Image Quality**
   - Use clear, well-lit images with visible body positioning
   - Avoid heavily cropped or partial body shots
   - Front, 3/4, or profile views work best

2. **Pose Descriptions**
   - Be specific about weight distribution and subtle angles
   - Include information about hidden or occluded limbs
   - Note any unusual or extreme positions

3. **Blender Setup**
   - Ensure bone constraints are properly configured
   - Test with simple poses before attempting complex ones
   - Keep backup copies of your default character pose

## Troubleshooting

- **Bones not moving**: Check armature name and bone naming conventions
- **Incorrect rotations**: Verify rotation order and axis conventions
- **Constraint conflicts**: Temporarily disable IK or other constraints
- **Extreme poses**: Start with subtle adjustments and build up gradually

## Contributing

This project is designed to be modular and extensible. Feel free to:
- Improve the AI prompts for better pose analysis
- Add support for additional bone types or rigs
- Enhance the Blender integration scripts
- Create presets for common pose types

## License

This project is open source. Please respect any licensing terms for AI models or Blender addons you use in conjunction with this tool.

## Acknowledgments

- Built for Blender 4.2+ and modern AI vision models
- Designed with anatomical accuracy and biomechanical principles in mind
- Inspired by the need for efficient pose reference workflows in 3D animation