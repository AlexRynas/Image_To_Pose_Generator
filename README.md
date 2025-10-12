# Image To Pose Generator

A comprehensive toolkit that generates character poses from reference images using AI-powered pose analysis and automated rigging. Available as both a Windows desktop application and Python scripts for Blender integration.

## Overview

This project enables you to:
1. Analyze a reference image of a person to extract detailed pose descriptions
2. Convert those descriptions into precise bone rotations for Blender armatures
3. Automatically apply poses to your rigged characters
4. Retarget poses to different rig types (including Cyberpunk 2077)

The workflow combines computer vision analysis with biomechanical understanding to create accurate 3D poses from 2D references.

## 🖥️ Desktop Application

**A standalone Windows application with a user-friendly wizard interface for generating poses from images.**

### Features

- **Step-by-step wizard workflow**: Intuitive interface guiding you from image selection to final pose generation
- **Operating modes**: Choose between Budget, Balanced, or Quality modes to optimize cost vs. quality
- **OpenAI Integration**: Uses GPT-4 Vision for image analysis with intelligent model selection and fallbacks
- **Real-time cost estimation**: See estimated API costs before making calls
- **Real-time validation**: Validates your API key before proceeding
- **Image preview**: See your reference image while working
- **Editable descriptions**: Review and refine AI-generated pose descriptions
- **JSON export**: Copy to clipboard or save bone rotations as JSON files
- **Single executable**: No installation required - just download and run

### Quick Start

1. **Download** the latest release (single `.exe` file)
2. **Run** `ImageToPose.Desktop.exe`
3. **Provide your OpenAI API key** ([Get one here](https://platform.openai.com/api-keys))
4. **Select operating mode** (Budget/Balanced/Quality)
5. **Select a reference image** and describe the rough pose
6. **Review** the AI-generated extended pose description
7. **Generate** bone rotations for your MPFB GameEngine rig
8. **Copy or save** the JSON output

### Requirements

- **Windows 10/11** (64-bit)
- **OpenAI API key** (required - you provide your own)
- Internet connection for API calls

## Operating Modes & Cost Estimates

The desktop application offers **three operating modes** that balance quality vs. cost:

### Operating Modes

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

### Cost Estimation

The app shows **real-time cost estimates** before API calls, calculated using:
- **SharpToken** (tiktoken for .NET) for token counting
- OpenAI's tile-based formula for image tokens
- Pricing rates from `config/pricing.json` (auto-generated, user-editable)

**Always verify pricing** at [OpenAI's Pricing Page](https://openai.com/api/pricing)

### Model Selection & Fallbacks

The app automatically:
1. Lists available models from your API key
2. Selects the best available model from your mode's priority list
3. Tests the model with a probe request
4. Falls back to alternatives if needed
5. Displays the resolved model in the UI

### Building from Source

```bash
cd src/DesktopApp
dotnet build ImageToPose.sln

# Create single-file executable:
dotnet publish ImageToPose.Desktop -c Release -r win-x64
```

The executable will be in `bin/Release/net9.0/win-x64/publish/`.

### Privacy & Security

- ✅ Your API key is stored **in memory only** during the session
- ✅ No data saved to disk without your explicit action
- ✅ API calls go directly to OpenAI - no third-party servers
- ⚠️ API usage charges apply (you provide your own key)

See the [Blender Workflow Guide](docs/BlenderWorkflow.md) for applying generated poses.

---

## 🐍 Python Scripts & Blender Integration

Python scripts for direct Blender integration, allowing greater customization and control.

### Features

- **AI-Powered Pose Analysis**: Structured prompts for anatomical precision
- **Blender Integration**: Works with Blender 4.2+ armatures
- **FK Rigging Support**: Forward Kinematics with local XYZ Euler rotations
- **Anatomical Accuracy**: Proper left/right conventions
- **Comprehensive Coverage**: Full body including spine, arms, legs
- **Armature Export**: Export armature data for analysis

### Project Structure

```
Image_To_Pose_Generator/
├── analyse_image_and_get_pose_description_prompt.txt
├── chatgpt_prompt.txt
├── apply_pose_template.py
├── armature_exporter.py
└── README.md
```

### Workflow

1. **Image Analysis**: Use AI vision model with provided prompts
2. **Pose Conversion**: Convert description to bone rotations
3. **Pose Application**: Apply to Blender armatures via script

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

---

## Troubleshooting

### Desktop App

- **API key validation fails**: Check internet, verify key at [OpenAI Platform](https://platform.openai.com/api-keys)
- **Prompt files not found**: Ensure txt files are in repository root
- **Build errors**: Run `dotnet restore`

### Python Scripts

- **Bones not moving**: Check armature and bone naming
- **Incorrect rotations**: Verify rotation order and axes
- **Constraint conflicts**: Temporarily disable IK
- **Extreme poses**: Start subtle, build gradually

---

## Contributing

This project is modular and extensible. Feel free to:
- Improve AI prompts for better analysis
- Add support for additional rig types
- Enhance Blender integration
- Create presets for common poses

## License & Terms of Use

### License

This project is provided as-is under an open source license. You are free to:
- ✅ Use the software for personal or commercial projects
- ✅ Modify and adapt the code to your needs
- ✅ Distribute modified or unmodified versions
- ✅ Use the generated poses in your creative works

**Attribution Requirement:**

If you use this project, any part of its code, or incorporate it into your own project, you **MUST**:
- 📝 Credit the original author: **AlexRynas**
- 🔗 Include a link to this repository: [https://github.com/AlexRynas/Image_To_Pose_Generator](https://github.com/AlexRynas/Image_To_Pose_Generator)
- 📄 Mention the attribution in your project's documentation, README, credits screen, or appropriate location

**Example Attribution:**
```
This project uses Image To Pose Generator by AlexRynas
https://github.com/AlexRynas/Image_To_Pose_Generator
```

### Important Disclaimers

**NO WARRANTIES OR GUARANTEES:**

This software is provided **"AS IS"**, without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose, and noninfringement.

- ⚠️ **No Accuracy Guarantees**: The AI-generated poses may not always be anatomically correct or match your expectations
- ⚠️ **No API Cost Guarantees**: Cost estimates are approximate. Always verify actual charges on your OpenAI account
- ⚠️ **No Availability Guarantees**: The software depends on external services (OpenAI API) which may change or become unavailable
- ⚠️ **No Support Guarantees**: No obligation to provide updates, bug fixes, or technical support
- ⚠️ **No Liability**: In no event shall the author be liable for any claim, damages, or other liability arising from the use of this software

### User Responsibilities

By using this software, you acknowledge that:

1. **You provide your own API key**: You are responsible for:
   - Obtaining and securing your OpenAI API key
   - All costs incurred from API usage
   - Compliance with OpenAI's terms of service and usage policies

2. **You verify the output**: You are responsible for:
   - Reviewing and validating all AI-generated content
   - Ensuring poses are appropriate for your use case
   - Making necessary adjustments to generated data

3. **You respect third-party terms**: When using this software with:
   - OpenAI services: Follow [OpenAI's Terms of Use](https://openai.com/policies/terms-of-use)
   - Blender: Follow [Blender's GPL License](https://www.blender.org/about/license/)
   - MPFB addon: Follow MPFB's licensing terms
   - Any AI models or addons: Respect their respective licenses

### Privacy & Data

- **Your API key**: Stored in memory only during runtime; never logged or transmitted except to OpenAI
- **Your images**: Sent directly to OpenAI for analysis; subject to [OpenAI's Privacy Policy](https://openai.com/policies/privacy-policy)
- **Your data**: No telemetry, analytics, or data collection by this application
- **Your responsibility**: Ensure you have rights to any images you analyze

### Pricing Information

All pricing information in this software is:
- Provided for estimation purposes only
- Based on publicly available rate cards at the time of implementation
- Subject to change without notice by OpenAI
- Not guaranteed to be accurate or current

**Always verify current pricing** at the [official OpenAI Pricing page](https://openai.com/api/pricing).

### Changes to Terms

The author reserves the right to modify these terms, the software, or discontinue the project at any time without notice.

## Acknowledgments

- Built for Blender 4.2+ and modern AI vision models
- Designed with anatomical accuracy and biomechanical principles
- Inspired by efficient pose reference workflows in 3D animation
- Uses the official [OpenAI .NET SDK](https://github.com/openai/openai-dotnet)
- UI built with [Avalonia UI](https://avaloniaui.net/)