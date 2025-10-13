# Image To Pose Generator

A comprehensive toolkit that generates character poses from reference images using AI-powered pose analysis and automated rigging. Available as both a Windows desktop application and Python scripts for Blender integration.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Requirements](#requirements)
- [Downloads](#downloads)
- [Operating Modes](#operating-modes)
- [Cost Estimation](#cost-estimation)
- [Privacy & Security](#privacy--security)
- [Building from Source](#building-from-source)
- [Blender Workflow](#blender-workflow)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License & Terms of Use](#license--use)

## Overview

This project enables you to:
1. Analyze a reference image of a person to extract detailed pose descriptions
2. Convert those descriptions into precise bone rotations for Blender armatures
3. Automatically apply poses to your rigged characters
4. Retarget poses to different rig types (including Cyberpunk 2077)

The workflow combines computer vision analysis with biomechanical understanding to create accurate 3D poses from 2D references.

## Features

- **Step-by-step wizard workflow**: Intuitive interface guiding you from image selection to final pose generation
- **Operating modes**: Choose between Budget, Balanced, or Quality modes to optimize cost vs. quality
- **OpenAI Integration**: Uses GPT-4 Vision for image analysis with intelligent model selection and fallbacks
- **Real-time cost estimation**: See estimated API costs before making calls
- **Real-time validation**: Validates your API key before proceeding
- **Image preview**: See your reference image while working
- **Editable descriptions**: Review and refine AI-generated pose descriptions
- **JSON export**: Copy to clipboard or save bone rotations as JSON files
- **Single executable**: No installation required - just download and run


## Quick Start

1. **Download** the latest release for your platform from the link above
2. **Extract** the zip file and run the executable
3. **Provide your OpenAI API key** ([Get one here](https://platform.openai.com/api-keys))
4. **Select operating mode** (Budget/Balanced/Quality)
5. **Select a reference image** and describe the rough pose
6. **Review** the AI-generated extended pose description
7. **Generate** bone rotations for your MPFB GameEngine rig
8. **Copy or save** the JSON output

## Requirements

- **Windows 10/11** (64-bit)
- **OpenAI API key** (required - you provide your own)
- Internet connection for API calls

## Download

**[📦 Download Latest Release](https://github.com/AlexRynas/Image_To_Pose_Generator/releases/latest)**

Available for:
- **Windows 10/11** (64-bit Intel/AMD)
- **Windows 11** (ARM64 - for Surface and other ARM devices)

## Operating Modes

The desktop application offers **three operating modes** that balance quality vs. cost:

1. **Budget** - Fast & cheapest; ok for simple photos
   - Preferred Model: `gpt-4.1-nano` (fallback: `gpt-4.1-mini`)
   - Best for simple, straightforward poses
   - Expected Output: ~300 tokens per step

2. **Balanced** (Default) - Good quality for most cases
   - Preferred Model: `gpt-4.1-mini` (fallbacks: `o4-mini`, `gpt-4.1`)
   - Best for most everyday use cases
   - Expected Output: ~600 tokens per step

3. **Quality** - Best quality at a sensible price
   - Preferred Model: `gpt-4.1` (fallback: `o4-mini`)
   - Best for complex poses or maximum accuracy
   - Expected Output: ~800 tokens per step

## Cost Estimation

The app shows **real-time cost estimates** before API calls, calculated using:
- **SharpToken** (tiktoken for .NET) for token counting
- OpenAI's tile-based formula for image tokens

**Always verify pricing** at [OpenAI's Pricing Page](https://openai.com/api/pricing)

## Privacy & Security

- ✅ Your API key is stored **in memory only** during the session
- ✅ No data saved to disk without your explicit action
- ✅ API calls go directly to OpenAI - no third-party servers
- ⚠️ API usage charges apply (you provide your own key)

See the [Blender Workflow Guide](src/DesktopApp/ImageToPose.Desktop/Assets/BlenderWorkflow.md) for applying generated poses.

## Building from Source

```bash
cd src/DesktopApp
dotnet build ImageToPose.sln

# Create single-file executable:
dotnet publish ImageToPose.Desktop -c Release -r win-x64
```

The executable will be in `bin/Release/net9.0/win-x64/publish/`.

## Blender Workflow

### Usage

1. **Export Armature Data** (optional):
   ```python
   exec(open("armature_exporter.py").read())
   ```

2. **Analyze Reference Image**: Use `analyse_image_and_get_pose_description_prompt.txt`

3. **Generate Rotations**: Use `chatgpt_prompt.txt` for bone rotations

4. **Apply Pose**: Update `POSE_DEGREES` in `apply_pose_template.py` and run in Blender

### Example Format

```python
POSE_DEGREES = {
    "Hips":   [0.0, 0.0, 5.0],
    "Spine":  [0.0, 0.0, -2.0],
    "Head":   [10.0, -5.0, 15.0],
    "LeftArm": [45.0, -30.0, 20.0],
}
```

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

## Troubleshooting

- **API key validation fails**: Check internet, verify key at [OpenAI Platform](https://platform.openai.com/api-keys)
- **Prompt files not found**: Ensure txt files are in repository root
- **Build errors**: Run `dotnet restore`
- **Bones not moving**: Check armature and bone naming
- **Incorrect rotations**: Verify rotation order and axes
- **Constraint conflicts**: Temporarily disable IK
- **Extreme poses**: Start subtle, build gradually

## Contributing

This project is modular and extensible. Feel free to:
- Improve AI prompts for better analysis
- Add support for additional rig types
- Enhance Blender integration
- Create presets for common poses

## License & Terms of Use

This project uses the **BSD 3‑Clause License** with a project‑specific **Attribution Notice** (retain the copyright line with the repo link) and practical usage terms. See **[LICENSE+TERMS.md](LICENSE+TERMS.md)** for details, including disclaimers and responsibilities.