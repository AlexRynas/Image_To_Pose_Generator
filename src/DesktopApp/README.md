# Image To Pose Generator - Desktop Application

A Windows desktop application for generating character poses from reference images using AI.

## Overview

This application provides a user-friendly wizard interface that guides you through:
1. Providing your OpenAI API key
2. Selecting a reference image and describing the pose
3. AI-powered analysis to create an extended pose description
4. Review and refinement of the pose description
5. Generation of bone rotations for MPFB GameEngine rigs
6. Export as JSON for use in Blender

## Building the Application

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10/11 (for development)

### Build Commands

**Debug build:**
```bash
dotnet build ImageToPose.sln
```

**Release build:**
```bash
dotnet build ImageToPose.sln -c Release
```

**Run without building:**
```bash
dotnet run --project ImageToPose.Desktop
```

## Publishing as Single Executable

To create a standalone, single-file executable:

```bash
dotnet publish ImageToPose.Desktop -c Release -r win-x64 --self-contained
```

The resulting `.exe` file will be located at:
```
ImageToPose.Desktop/bin/Release/net9.0/win-x64/publish/ImageToPose.Desktop.exe
```

### Publish Options

The project is configured with the following publish settings:
- **Single File**: All dependencies bundled into one `.exe`
- **Self-Contained**: Includes the .NET runtime (no separate installation needed)
- **Native Libraries**: Extracted and included automatically
- **Compression**: Enabled to reduce file size

You can also publish for other platforms:
```bash
# Linux
dotnet publish ImageToPose.Desktop -c Release -r linux-x64 --self-contained

# macOS
dotnet publish ImageToPose.Desktop -c Release -r osx-x64 --self-contained
```

## Project Structure

```
src/DesktopApp/
??? ImageToPose.sln                 # Solution file
??? ImageToPose.Desktop/            # Main application (Avalonia UI)
?   ??? Views/                      # XAML views for each wizard step
?   ??? ViewModels/                 # View models with business logic
?   ??? Services/                   # Platform-specific implementations
?   ??? App.axaml.cs                # Application entry point with DI setup
??? ImageToPose.Core/               # Core business logic
?   ??? Models/                     # Data models
?   ??? Services/                   # Service interfaces and implementations
??? ImageToPose.Tests/              # Unit tests
    ??? PoseRigParsingTests.cs      # Tests for parsing LLM responses
```

## Key Dependencies

- **Avalonia UI 11.3.6**: Cross-platform UI framework
- **OpenAI 2.5.0**: Official OpenAI .NET SDK
- **CommunityToolkit.Mvvm 8.4.0**: MVVM helpers
- **Microsoft.Extensions.DependencyInjection 9.0.9**: Dependency injection

## Development

### Running Tests

```bash
dotnet test ImageToPose.Tests
```

### Adding New Features

1. **Models**: Add to `ImageToPose.Core/Models/`
2. **Services**: Add interfaces to `ImageToPose.Core/Services/` and implementations to `ImageToPose.Desktop/Services/`
3. **Views**: Add XAML + code-behind to `ImageToPose.Desktop/Views/`
4. **ViewModels**: Add to `ImageToPose.Desktop/ViewModels/`
5. **Register services**: Update `App.axaml.cs` `ConfigureServices()` method

### Code Style

- Use `nullable enable` throughout
- Follow C# naming conventions
- Use async/await for I/O operations
- Include XML documentation comments for public APIs

## Configuration

### API Key Storage

By default, the API key is stored **in memory only** for the current session. To add persistent storage:

1. Implement secure storage in `ISettingsService`
2. Use Windows Credential Manager or similar secure storage
3. Add UI toggle for "Remember API key" option

### Changing Models

To use different OpenAI models, update the model names in:
- `ImageToPose.Core/Services/IOpenAIService.cs`
  - Line ~47: `"gpt-4o-mini"` for API key validation
  - Line ~86: `"gpt-4o"` for image analysis (vision)
  - Line ~139: `"gpt-4o"` for pose generation

## Troubleshooting

### Build Errors

**"Avalonia templates not found":**
```bash
dotnet new install Avalonia.Templates
```

**"OpenAI namespace not found":**
```bash
dotnet restore
```

### Runtime Errors

**"Could not find prompt files":**
- Ensure `analyse_image_and_get_pose_description_prompt.txt` and `chatgpt_prompt.txt` are in the repository root
- The app searches up to 10 directory levels from the executable

**"API key validation fails":**
- Check internet connection
- Verify the API key is valid on [OpenAI Platform](https://platform.openai.com/api-keys)
- Check if there are API usage limits or billing issues

## Contributing

When adding features or fixing bugs:

1. Create a feature branch
2. Add tests for new functionality
3. Ensure all tests pass
4. Update documentation
5. Submit a pull request

## License

Part of the Image To Pose Generator project. See main repository README for license information.
