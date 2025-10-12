# Avalonia OpenAI Wizard - Implementation Plan

## STATUS: ✅ COMPLETED

This implementation plan has been successfully completed. The desktop application is fully functional with all planned features and more.

---

## IMPLEMENTATION SUMMARY

### ✅ Completed Features

**Core Wizard Flow:**
- ✅ Welcome screen with project overview
- ✅ API key validation with real-time testing
- ✅ Operating mode selection (Budget/Balanced/Quality)
- ✅ Image input with preview and pose description
- ✅ Extended pose description review with editing
- ✅ Bone rotation generation with JSON output
- ✅ Copy to clipboard and save to file functionality

**Advanced Features:**
- ✅ Three operating modes with different model preferences
- ✅ Intelligent model selection with automatic fallbacks
- ✅ Model probing to ensure compatibility
- ✅ Real-time cost estimation using SharpToken
- ✅ Configurable pricing rates via `config/pricing.json`
- ✅ Image token counting using OpenAI's tile-based formula
- ✅ Error handling and user-friendly error messages
- ✅ Theme support (Light/Dark modes)
- ✅ Logging with Serilog

**Testing:**
- ✅ Unit tests for pose rig parsing
- ✅ Unit tests for pricing estimation
- ✅ Integration tests for mode selection

**Deployment:**
- ✅ Single-file executable with self-contained .NET 9 runtime
- ✅ All dependencies bundled
- ✅ Native libraries included
- ✅ Compression enabled

### Project Structure

The implementation follows the planned structure with three projects:
- `ImageToPose.Desktop` - Avalonia UI application
- `ImageToPose.Core` - Business logic and services
- `ImageToPose.Tests` - Unit and integration tests

---

## ORIGINAL IMPLEMENTATION PLAN

Below is the original implementation plan that guided the development.

---

## ROLE
You are a senior .NET desktop engineer, UX-minded, and a meticulous release manager. Your task is to scaffold and implement a Windows desktop application (optionally cross‑platform later) using **.NET 9**, **Avalonia UI**, and the **official OpenAI .NET SDK**, then package it as a **single, portable EXE** (self-contained publish). You may create files, folders, commits, branches, and PRs in the current repository.

## GOAL (User Journey)
A simple step-by-step **wizard** that guides users from zero to a generated pose rig:
1. **Welcome:** Explain what the app does and why; big “Get Started” button.
2. **API Key:** Collect an **OpenAI API key**. Provide a link to create/manage keys. Validate basic connectivity.
3. **Input:** Let the user **pick one image** and enter a **rough pose description**. Primary action: **“Process photo and pose description.”**
4. **LLM Call #1 (Vision + Text):** Use the prompt in `analyze_image_and_get_pose_description_prompt.txt` to produce an **extended pose description** from the selected image + rough text.
5. **Review:** Show the extended description **editable**. User can tweak/clean it.
6. **LLM Call #2 (Text):** Use the prompt in `chatgpt_prompt.txt` to produce a **bone rotation object** for an MPFB GameEngine rig.
7. **Result:** Display the rotations as JSON (copy & save). Provide a short **Blender workflow** note for applying to MPFB and retargeting to Cyberpunk 2077 (with ideas to automate later).

> Important: Do **not** hardcode or require the developer’s API key. The user must supply their own; without it the app won’t proceed.

---

## CONSTRAINTS & PRINCIPLES
- **Stack:** .NET 9, **Avalonia UI** (XAML/MVVM), **official OpenAI .NET SDK** (`OpenAI` NuGet). No Azure-specific SDK in this app.
- **Distribution:** single-file, **self-contained** Windows build (`win-x64`) producing one portable `.exe`.
- **Architecture:** MVVM. Keep **Core** logic separate from UI.
- **UX:** Clear copy, few controls, sensible defaults, obvious errors. Back is always available; show a spinner/progress during network calls.
- **Security:** API key is held in memory for the session by default. Offer an **optional** “Remember this key” toggle (future enhancement; off by default). Never commit keys.
- **Robustness:** Parse LLM responses defensively (tolerate extra prose, code fences; extract JSON safely).
- **Tests:** Unit tests for prompt interpolation and JSON parsing.

---

## REPO INPUTS (USE EXISTING FILES)
- `analyze_image_and_get_pose_description_prompt.txt` → First LLM call (vision + text) to produce an extended pose description.
- `chatgpt_prompt.txt` → Second LLM call (text-only) to produce bone rotations.
If paths differ, **search the repo** and reference the correct locations. Don’t modify their core text unless you must add a minimal wrapper to combine with the user’s image/rough text (call #1) or edited description (call #2).

---

## SOLUTION & PROJECT STRUCTURE
Create a new branch `feature/avalonia-openai-wizard` and a solution under `src/DesktopApp/` with three projects:

```
src/DesktopApp/
  ImageToPose.Desktop/     # Avalonia App (Views, ViewModels, DI)
  ImageToPose.Core/        # Services, Models, Prompt adapters, Parsers
  ImageToPose.Tests/       # Unit tests for Core
```

**NuGet packages (minimum):**
- `OpenAI` (official) — OpenAI .NET SDK
- `Avalonia`, `Avalonia.Desktop`
- `CommunityToolkit.Mvvm` (MVVM helpers)
- `System.Text.Json` (or `Newtonsoft.Json` if you prefer; ensure safe parsing)
- `xUnit` (or `NUnit`) + `FluentAssertions`

---

## MODELS (Core)
- `PoseInput` { `string ImagePath`, `string RoughPoseText` }
- `ExtendedPose` { `string Text` }
- `BoneRotation` { `string BoneName`; `double X`, `double Y`, `double Z` }
- `PoseRig` { `List<BoneRotation> Bones` }
- `OpenAIOptions` { `string ApiKey` } (session-scoped)

---

## SERVICES (Core)
- `ISettingsService` — holds `OpenAIOptions` in-memory for the session (later, optional secure persistence).
- `IFileService` — abstraction for file pickers (image open/save dialogs).
- `IPromptLoader` — loads the two text prompts from the repo at runtime (by relative path or embedded resource).
- `IOpenAIService`
  - `Task<ExtendedPose> AnalyzePoseAsync(string imagePath, string roughText, CancellationToken ct)`  
    - Loads prompt #1, uploads the local image for **vision** use, and combines with the rough text for the LLM request. Returns extended text.
  - `Task<PoseRig> GenerateRigAsync(string extendedPoseText, CancellationToken ct)`  
    - Loads prompt #2, sends extended pose text, expects a JSON object/dict of bone rotations.

**Notes for OpenAI SDK usage:**
- Create a root client: `var client = new OpenAIClient(apiKey);`
- Get specific clients as needed: `var chat = client.GetChatClient("gpt-4o-mini"); var files = client.GetOpenAIFileClient();`
- **Vision flow:** upload the local image with `FileUploadPurpose.Vision`, then reference the uploaded file id as an **image part** in a user message for the chat request along with your text parts.
- **Parsing:** Trim code fences, then parse JSON. If parsing fails, surface a helpful error and instructions.

---

## UI / WIZARD FLOW (Desktop)
A single `MainWindow` hosts a `ContentControl` bound to `WizardViewModel.CurrentStep` (enum/index).

**Views (XAML) & ViewModels:**
1. **WelcomeView** — Title + 2–3 short paragraphs and a **Get Started** button.
2. **ApiKeyView** — Password-style textbox for key; link “Create or manage your key”. Validate with a light chat call or a “models list” call (or a minimal sanity ping). Next enabled only when validation passes.
3. **InputView** — 
   - Image **OpenFilePicker** (accept .png/.jpg/.jpeg), thumbnail preview.
   - Multiline textbox for the rough pose description.
   - Primary button: **Process photo and pose description** → calls `AnalyzePoseAsync` and navigates on success.
4. **ReviewView** — Multiline editable textbox prefilled with the **extended description**. Buttons: Back, **Continue**.
5. **GenerateView** — Button **Generate Pose Rig Rotations** → calls `GenerateRigAsync`. Show pretty-printed JSON in a read-only textbox. Buttons: **Copy**, **Save JSON…**. Include a link to **docs/BlenderWorkflow.md**.

**Common UI elements:**
- Error banner area for exceptions (network, API parsing).
- Progress indicator (spinner or ProgressBar) during calls.
- Back button always enabled (except on Welcome).

---

## PROMPT ADAPTERS (Core)
- **Call #1:** Load `analyze_image_and_get_pose_description_prompt.txt`. Prepend/append minimal instructions if needed to (a) accept an **image** (uploaded file id) and (b) combine it with the user’s **rough pose** text. Ask for concise, well-structured extended description.
- **Call #2:** Load `chatgpt_prompt.txt`. Interpolate the **edited** extended description; request **only** the JSON/object of bone rotations for the MPFB GameEngine rig (tolerate minor format drift—strip code fences).

---

## PACKAGING (Single Portable EXE)
In `ImageToPose.Desktop.csproj` add:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>

  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

  <!-- Optional: test thoroughly if enabling AOT with Avalonia & reflection -->
  <!-- <PublishAot>true</PublishAot> -->
</PropertyGroup>
```

Publish:
```bash
dotnet publish src/DesktopApp/ImageToPose.Desktop -c Release -r win-x64
```
Resulting single EXE will be under `bin/Release/net9.0/win-x64/publish/`.

---

## README & DOCS
- **README.md** (root): Add a new **“Desktop App (Avalonia)”** section with a screenshot and the 5-step flow. Include a bold note that an **OpenAI API key is required** and a link to create/manage keys.
- **docs/BlenderWorkflow.md**: Short guide to apply the rotations onto an **MPFB GameEngine** rig in Blender via Python, then **retarget to the Cyberpunk 2077 rig**. Include a small **Future Work** list (e.g., headless Blender script to auto-apply rotations; export to a CP2077-friendly format).

---

## TESTS (Core)
- **PromptInterpolationTests:** Given inputs, ensure final messages include both file prompt text and user text/image instructions.
- **PoseRigParsingTests:** Provide representative LLM outputs (with and without code fences) and assert robust JSON extraction into `PoseRig`.

---

## ACCEPTANCE CRITERIA (Definition of Done)
- Running `dotnet run` shows the **Welcome → API Key → Input → Review → Generate** flow.
- **Call #1** produces a non-empty **extended pose description** from a user image + rough text.
- **Review** allows edits and proceeds.
- **Call #2** returns a **bone rotation object** displayed as pretty JSON, with working **Copy** and **Save** actions.
- Publishing yields a **single, portable EXE** (no installer).
- README and **docs/BlenderWorkflow.md** exist and are accurate.
- Unit tests pass locally in CI (if you add a simple GitHub Action).

---

## IMPLEMENTATION TIPS
- Use `async/await` with `CancellationToken` in all API calls.
- Display helpful errors (e.g., invalid key, network issues, malformed JSON).
- Keep methods small; comment non-obvious logic; enable `<Nullable>enable</Nullable>`.
- For preview thumbnails, load the picked image via Avalonia `Bitmap` and bind to an `Image` control.
- For the JSON output, pretty print with `JsonSerializerOptions { WriteIndented = true }` (or equivalent).

---

## REFERENCES (Helpful for the build)
- **Official OpenAI .NET SDK — GitHub (examples of Chat, Files, Vision)**  
  https://github.com/openai/openai-dotnet
- **NuGet: `OpenAI` (official package)**  
  https://www.nuget.org/packages/OpenAI
- **Create/Manage API Keys (OpenAI Platform)**  
  https://platform.openai.com/api-keys
- **OpenAI API Production Best Practices (auth & safety tips)**  
  https://platform.openai.com/docs/guides/production-best-practices
- **Avalonia Storage Provider & File Dialogs (file picker)**  
  https://docs.avaloniaui.net/docs/concepts/services/storage-provider/
  https://docs.avaloniaui.net/docs/basics/user-interface/file-dialogs
  https://docs.avaloniaui.net/docs/concepts/services/storage-provider/file-picker-options
- **Avalonia TextBox (multiline via `AcceptsReturn`)**  
  https://docs.avaloniaui.net/docs/reference/controls/textbox
- **.NET Single-file publish (official docs)**  
  https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- **Native AOT (optional) — Avalonia guide & .NET overview**  
  https://docs.avaloniaui.net/docs/deployment/native-aot
  https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/

---

## WORK PLAN (What you should do now)
1. Create branch `feature/avalonia-openai-wizard` and solution under `src/DesktopApp/` with three projects.
2. Wire DI, services, and MVVM skeleton. Implement `IPromptLoader`, `OpenAIService` stubs.
3. Build **Welcome**, **API Key**, and **Input** steps first; verify an MVP chat call works with the user key (e.g., short “ping” message).
4. Implement **AnalyzePoseAsync** with image upload + file prompt #1 + rough text.
5. Implement **Review** step (editable extended description).
6. Implement **GenerateRigAsync** with file prompt #2 and robust JSON parsing.
7. Add copy & save JSON utilities.
8. Draft **docs/BlenderWorkflow.md**; update **README.md** with screenshots.
9. Add tests; ensure `dotnet publish -c Release -r win-x64` yields a single EXE.
10. Open a PR with summary, commands to run/publish, and screenshots.
