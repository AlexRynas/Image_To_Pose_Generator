# Image To Pose Generator - Desktop Application

A Windows desktop application for generating character poses from reference images using AI.

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
├── ImageToPose.sln                 # Solution file
├── ImageToPose.Desktop/            # Main application (Avalonia UI)
│   ├── Views/                      # XAML views for wizard steps
│   │   ├── WelcomeView.axaml
│   │   ├── ApiKeyView.axaml
│   │   ├── ***
│   ├── ViewModels/                 # View models with business logic
│   │   ├── WizardViewModel.cs
│   │   ├── ApiKeyViewModel.cs
│   │   ├── ***
│   ├── Services/                   # Platform-specific implementations
│   │   ├── FileService.cs
│   │   ├── ThemeService.cs
│   │   └── IThemeService.cs
│   ├── Styles/                     # XAML styling
│   └── App.axaml.cs                # Application entry with DI setup
├── ImageToPose.Core/               # Core business logic
│   ├── Models/                     # Data models
│   │   ├── BoneRotation.cs
│   │   ├── ExtendedPose.cs
│   │   ├── ***
│   └── Services/                   # Service interfaces and implementations
│       ├── IOpenAIService.cs
│       ├── IOpenAIErrorHandler.cs
│       ├── ***
└── ImageToPose.Tests/              # Unit tests
    ├── PoseRigParsingTests.cs
    ├── PricingEstimatorTests.cs
    └── ***
```

## Key Dependencies

- **Avalonia UI 11.3.7**: Cross-platform UI framework
- **OpenAI 2.5.0**: Official OpenAI .NET SDK
- **CommunityToolkit.Mvvm 8.4.0**: MVVM helpers
- **Microsoft.Extensions.DependencyInjection 9.0.9**: Dependency injection
- **SharpToken 1.2.1**: Token counting for cost estimation
- **SixLabors.ImageSharp 3.1.11**: Image processing
- **Serilog**: Logging framework

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

## License

Part of the Image To Pose Generator project. See main repository README for license information.
