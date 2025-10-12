# GitHub Release Automation Guide

This guide explains how to use the automated GitHub release system for the Image To Pose Generator project.

## Overview

The project uses GitHub Actions to automatically:
- Build the application for multiple platforms (Windows x64/ARM64, Linux x64, macOS x64/ARM64)
- Run all tests to ensure quality
- Create GitHub releases with built executables
- Generate release notes automatically

## Setup Requirements

### 1. Repository Permissions

Ensure your repository has the correct permissions for GitHub Actions:

1. Go to your repository on GitHub
2. Navigate to **Settings** → **Actions** → **General**
3. Under "Workflow permissions", select **Read and write permissions**
4. Check **Allow GitHub Actions to create and approve pull requests**
5. Click **Save**

### 2. Branch Protection (Optional but Recommended)

To ensure code quality:

1. Go to **Settings** → **Branches**
2. Add a branch protection rule for `main`
3. Enable:
   - ✅ Require status checks to pass before merging
   - ✅ Require branches to be up to date before merging
   - Select the "CI" workflow as a required check

## How to Create a Release

### Method 1: Using Git Tags (Recommended)

1. **Commit all your changes:**
   ```powershell
   git add .
   git commit -m "Prepare for release v1.0.0"
   ```

2. **Create a version tag:**
   ```powershell
   git tag -a v1.0.0 -m "Release version 1.0.0"
   ```
   
   Use semantic versioning: `vMAJOR.MINOR.PATCH` (e.g., v1.0.0, v2.1.3)

3. **Push the tag to GitHub:**
   ```powershell
   git push origin v1.0.0
   ```

4. **Monitor the workflow:**
   - Go to your repository on GitHub
   - Click the **Actions** tab
   - Watch the "Release" workflow progress
   - It typically takes 10-15 minutes to build all platforms

5. **Check your release:**
   - Once complete, go to the **Releases** section
   - Your new release will be there with all platform builds attached

### Method 2: Using GitHub Web Interface

1. Go to your repository on GitHub
2. Click **Releases** → **Draft a new release**
3. Click **Choose a tag** → Type a new tag (e.g., `v1.0.0`) → **Create new tag**
4. Fill in the release title and description
5. Click **Publish release**
6. The workflow will trigger and add the built executables automatically

## Workflow Files Explained

### `.github/workflows/release.yml`

This is the main release workflow that:
- **Triggers on:** Version tags (v*.*.*)
- **Builds for:** Windows (x64, ARM64), Linux (x64), macOS (x64, ARM64)
- **Runs:** All unit tests before building
- **Creates:** A GitHub release with:
  - Auto-generated release notes from commits
  - Zipped executables for each platform
  - Proper version numbering

**Build artifacts naming:**
- `ImageToPose-Windows-x64.zip` - Windows 64-bit Intel/AMD
- `ImageToPose-Windows-ARM64.zip` - Windows ARM64 (Surface, etc.)
- `ImageToPose-Linux-x64.zip` - Linux 64-bit
- `ImageToPose-macOS-x64.zip` - macOS Intel
- `ImageToPose-macOS-ARM64.zip` - macOS Apple Silicon (M1/M2/M3)

### `.github/workflows/ci.yml`

This workflow runs on every push and pull request to:
- Build the solution
- Run all tests
- Report test results
- Ensure code quality before merging

## Version Numbering Guide

Follow [Semantic Versioning](https://semver.org/):

- **MAJOR version (v2.0.0):** Incompatible API changes or major rewrites
- **MINOR version (v1.1.0):** New features, backward compatible
- **PATCH version (v1.0.1):** Bug fixes, backward compatible

Examples:
```powershell
# Bug fix release
git tag -a v1.0.1 -m "Fix: Corrected API key validation"

# New feature release
git tag -a v1.1.0 -m "Feature: Added batch image processing"

# Breaking change release
git tag -a v2.0.0 -m "Breaking: New rig format, requires Blender 4.0+"
```

## Customizing Releases

### Adding Release Notes

Edit the tag message to include detailed release notes:

```powershell
git tag -a v1.0.0 -m "Release v1.0.0

Features:
- Added support for custom rigs
- Improved pose accuracy by 20%
- Added dark mode theme

Bug Fixes:
- Fixed crash when loading large images
- Corrected bone rotation calculations

Breaking Changes:
- None
"
```

### Pre-release Versions

For beta or alpha releases:

```powershell
git tag -a v1.0.0-beta.1 -m "Beta release for testing"
git push origin v1.0.0-beta.1
```

Then manually edit the release on GitHub and check "This is a pre-release".

### Draft Releases

To create a draft release that you can review before publishing:

1. Edit `.github/workflows/release.yml`
2. Change `draft: false` to `draft: true`
3. Releases will be created as drafts for manual review

## Troubleshooting

### Workflow Fails on Test Step

- Check the Actions log for specific test failures
- Fix the failing tests locally
- Create a new tag with an incremented version

### Insufficient Permissions Error

- Verify "Workflow permissions" are set to "Read and write"
- Check that GITHUB_TOKEN has proper scopes

### Build Fails for Specific Platform

- Check if you're using platform-specific code that needs conditional compilation
- Review the platform-specific error in the Actions log
- Consider temporarily removing that platform from the matrix

### Release Already Exists

- Delete the existing release on GitHub
- Delete the tag: `git tag -d v1.0.0`
- Delete the remote tag: `git push --delete origin v1.0.0`
- Create a new tag with a different version

## Best Practices

1. **Test locally first:**
   ```powershell
   dotnet test src/DesktopApp/ImageToPose.sln
   dotnet publish src/DesktopApp/ImageToPose.Desktop/ImageToPose.Desktop.csproj -c Release -r win-x64
   ```

2. **Use meaningful commit messages** - They appear in auto-generated release notes

3. **Create tags from main/master branch** - Ensure your release branch is stable

4. **Don't delete tags** - Keep version history intact

5. **Update README.md** - Include download links to latest release

6. **Create CHANGELOG.md** - Track changes between versions

## Manual Release (Fallback)

If automated releases fail, you can build and upload manually:

```powershell
# Build for Windows x64
dotnet publish src/DesktopApp/ImageToPose.Desktop/ImageToPose.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish/win-x64

# Create zip
Compress-Archive -Path ./publish/win-x64/* -DestinationPath ./ImageToPose-Windows-x64.zip

# Upload to GitHub release manually
```

## Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Semantic Versioning](https://semver.org/)
- [.NET Publishing Documentation](https://docs.microsoft.com/en-us/dotnet/core/deploying/)
- [GitHub Releases Guide](https://docs.github.com/en/repositories/releasing-projects-on-github)

## Support

If you encounter issues with the automated release system:
1. Check the Actions logs for detailed error messages
2. Verify all prerequisites are met
3. Review this guide's troubleshooting section
4. Open an issue on GitHub with the workflow logs attached
