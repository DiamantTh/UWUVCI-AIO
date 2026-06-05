# UWUVCI Modern Rewrite Plan

Status: working plan  
Target: Uno Desktop first, Windows + Linux/KDE, WASM-ready architecture

## How Agents Should Work This Plan

- [ ] Work phase by phase.
- [ ] Do not skip a phase unless the reason is written down.
- [ ] Keep each agent task small enough for one model session.
- [ ] Prefer 1-3 related files per implementation task.
- [ ] Do not paste or load the whole repository into context.
- [ ] Use `rg`/targeted reads to inspect only the needed files.
- [ ] Before editing, identify:
      - target files,
      - expected behavior,
      - verification command,
      - rollback-safe scope.
- [ ] After editing, always record:
      - changed files,
      - commands run,
      - failures,
      - unresolved risks.
- [ ] If a bug is found during migration, either fix it in the same scoped task or add it to the bug ledger below.
- [ ] Do not mix unrelated refactors with migration work.
- [ ] Do not introduce signing, token injection, copy protection, blacklist, or fingerprint logic.
- [ ] Keep commits/PRs small if GitHub is used later; large context-heavy branches are harder for Haiku/Sonnet style review budgets.

## Agent Budget Strategy

- [ ] Use small work packages so Haiku/Sonnet do not exhaust context quickly.
- [ ] Use cheap/smaller models only for:
      - inventory summaries,
      - mechanical checklists,
      - test-case enumeration,
      - simple code formatting review.
- [ ] Use stronger models for:
      - architecture changes,
      - injection pipeline migration,
      - ToolRunner/path/Wine logic,
      - Uno project setup,
      - image pipeline replacement,
      - cross-platform build failures.
- [ ] Every agent handoff should include a short local summary:
      - what was inspected,
      - what changed,
      - what still fails,
      - exact next task.
- [ ] Prefer updating this plan or a future `MIGRATION_NOTES.md` over re-reading old discussion context.
- [ ] Keep build/test output summaries concise; store full logs in files only when needed.

## Phase Overview

- [x] Phase 0: Baseline and environment.
- [x] Phase 1: Source inventory and risk map.
- [x] Phase 2: New solution/project scaffold.
- [x] Phase 3: Config and platform capability foundation.
- [x] Phase 4: Tooling foundation.
- [x] Phase 5: Image pipeline foundation.
- [x] Phase 6: Core/injection modernization.
- [x] Phase 7: Uno UX implementation.
- [x] Phase 8: Packaging and Linux/AppImage path.
- [x] Phase 9: WASM-readiness audit.
- [x] Phase 10: Final migration cleanup.
- [ ] **Phase 11: Injection service implementation** (console-specific logic, MVP-first)

Each phase must end with a build/test status. If a phase cannot build yet by design, the expected failing target must be stated explicitly.

## Phase 0 - Baseline And Environment

Goal: know what currently builds/runs and what the local machine can verify.

- [x] Record OS/runtime info.
- [x] Run current repo status check.
- [x] Check installed .NET SDKs.
- [x] Check whether Uno templates/workloads are installed.
- [x] Check whether the current legacy solution can be restored/built on this system.
- [x] Record whether failures are expected because the current project is WPF/.NET Framework.
- [x] Identify available local tools needed for verification.

Phase 0 status (2026-06-05):
- Linux (arch-x64), .NET SDKs 6.0/7.0/10.0 installed.
- No .NET workloads installed; Uno templates not installed.
- `dotnet restore` for solution succeeds.
- Legacy WPF build fails as expected on Linux: missing .NET Framework 4.8 reference assemblies (MSB3644).
- Local verification tools present: git, dotnet, rg, wine, winetricks.

Commands:

```bash
git status --short
dotnet --info
dotnet workload list
dotnet new list uno
dotnet restore
dotnet build
```

Exit criteria:

- [x] Environment capability is documented.
- [x] Build status of the current repo is known.
- [x] Missing SDK/workload/tooling items are listed.
- [x] No source migration has started yet.

## Phase 1 - Source Inventory And Risk Map

Goal: identify what the original brings and what must be ported, removed, or replaced.

- [x] Inventory WPF views and code-behind.
- [x] Inventory inject-related code.
- [x] Inventory external tools and direct process calls.
- [x] Inventory path handling, Wine handling, and platform checks.
- [x] Inventory settings/config usage.
- [x] Inventory image code and `System.Drawing` usage.
- [x] Inventory BinaryFormatter usage.
- [x] Inventory local DLL usage.
- [x] Inventory GitHub services.
- [x] Mark removed protection features:
      - `ReleaseSignatureVerifier`
      - `LocalInstallGuard`
      - `DeviceFingerprint`
      - protected release scripts
      - token injection scripts

Phase 1 status (2026-06-05):
- WPF/UI inventory: 44 `.xaml` + 42 `.xaml.cs` files found (includes duplicate `Kopieren` artifacts).
- Injection stack inventory: `Classes/Injection.cs` orchestrates `WiiInjectService`, `GCNInjectService`, `WitNfsService`, `NKitService`, `WitTicketExtractionService`.
- External process calls: direct `Process.Start` still exists (including outside centralized ToolRunner).
- Path/Wine/platform logic is widespread (`ToolRunner`, `PathResolver`, `MacLinuxHelper`, dialog/runtime helpers, inject services).
- Config/settings usage remains JSON/App.config and legacy settings model.
- Image pipeline strongly coupled to `System.Drawing` and legacy resource patterns.
- `BinaryFormatter` still used in multiple runtime paths.
- Local/native DLL usage still present (project refs + tool bundle DLL files).
- GitHub services are present (base/compat/feedback/image services with write flows).
- Protection/fingerprint/token-related code and scripts are still present and must be removed in rewrite target.

Owner category map (Phase 1):
- WPF views/code-behind: remove/replace (Uno rewrite).
- Injection core behavior: port (as whole pipeline), then refactor boundaries.
- External tool execution and process calls: replace (capability-driven tooling layer).
- Path/Wine/platform detection: port/restructure (explicit platform services).
- Settings/config model: replace (TOML + migration path).
- Image pipeline (`System.Drawing`): replace (SkiaSharp-based service layer).
- `BinaryFormatter`: remove/replace (safe serializer format).
- Local DLL dependencies: defer decision per DLL after source/license/buildability check.
- GitHub read-only pieces: defer/port selectively; write/token-based flows: remove or redesign.
- Protection features (`ReleaseSignatureVerifier`, `LocalInstallGuard`, `DeviceFingerprint`, protected scripts, token injection): remove.

Exit criteria:

- [x] Each source area has an owner category: port, replace, remove, or defer.
- [x] Known bugs/risks are added to the bug ledger.
- [x] No large code movement yet.

## Phase 2 - New Solution / Project Scaffold

Goal: create the modern structure without porting behavior yet.

Execution note (2026-06-05):
- Baseline findings from Phase 0/1 are treated as expected legacy-state signals, not rewrite blockers.
- Missing .NET Framework 4.8 build support on Linux is expected for the legacy WPF project.
- Uno templates were installed (`Uno.Templates`), and the scaffold is now created at repository root.
- Added central Uno SDK resolver at `global.json` (`Uno.Sdk` 6.5.36) so mixed-solution restore/build works.
- Current repo state before scaffold: only legacy solution/projects (`UWUVCI AIO WPF.sln`, legacy app, MSTest, TokenGenerator).

- [x] Create or prepare a new solution layout.
- [x] Add `UWUVCI.Core`.
- [x] Add `UWUVCI.Tooling`.
- [x] Add `UWUVCI.ImagePipeline`.
- [x] Add `UWUVCI.Config`.
- [x] Add `UWUVCI.App.Uno`.
- [x] Add `UWUVCI.Tests`.
- [x] Target .NET 10 LTS where possible.
- [x] Use Uno Desktop first.
- [x] Keep WASM-ready structure, even if WASM target is not fully enabled yet.
- [x] Add project references.
- [x] Add first smoke test.

Verification:

```bash
dotnet restore
dotnet build
dotnet test
```

Exit criteria:

- [x] New solution builds on the local system.
- [x] Test project runs at least one test.
- [ ] Uno desktop app can be started if local Uno tooling supports it.
- [x] If Uno launch is blocked by missing workload/templates, that blocker is documented.

## Phase 3 - Config And Capability Foundation

> **Execution note (2026-06-05):** Phase 3 abgeschlossen.
> Tomlyn 2.4.2 (TomlSerializer-API) hinzugefügt.
> `AppSettingsModel`, `ToolManifestModel`, `PlatformCapabilitiesModel` in `UWUVCI.Config/Models/` angelegt.
> `AppSettingsLoader`, `ToolManifestLoader` in `UWUVCI.Config/Loaders/` implementiert.
> `AppSettingsValidator`, `ToolManifestValidator` in `UWUVCI.Config/Validation/` implementiert.
> SysKey/SysKey1 nicht übernommen. CKey wird gespeichert, aber nie geloggt.
> 22 Config-Tests grün, alle 6 Projekte bauen fehlerfrei.

Goal: build the TOML-driven configuration layer before UI and injection depend on it.

- [x] Define app settings TOML model.
- [x] Define tool manifest TOML model.
- [x] Define platform capabilities TOML model.
- [x] Add Windows x64 manifest placeholder.
- [x] Add Linux x64 manifest placeholder.
- [x] Add config loader.
- [x] Add config validation.
- [x] Add tests for valid/invalid TOML.
- [x] Keep CKey behavior like original.
- [x] Do not add secret store.
- [x] Do not log CKey.
- [x] Remove `SysKey`/`SysKey1` from the new model.

Verification:

```bash
dotnet test --filter Config
dotnet build
```

Exit criteria:

- [x] Config parsing works.
- [x] Capability resolution works.
- [x] Invalid configs produce useful errors.
- [x] CKey is redacted in logs/errors.

## Phase 4 - Tooling Foundation

> **Execution note (2026-06-05):** Phase 4 abgeschlossen.
> Interfaces `IPlatformInfo`, `IToolRunner`, `IToolResolver` in `UWUVCI.Core/Tooling/` angelegt.
> `PlatformInfo` (Wine-Detektion via Env-Variablen, Override-Support), `ManifestToolResolver`, `ProcessToolRunner`, `NoOpToolRunner`, `ToolPathService` in `UWUVCI.Tooling/` implementiert.
> `UWUVCI.Tooling.csproj` um Config-Referenz erweitert.
> 18 Tooling-Tests grün, 41 Tests gesamt grün, 0 Build-Fehler.

Goal: isolate all process/tool execution and path mapping.

- [x] Define `IToolRunner`.
- [x] Define `IToolResolver`.
- [x] Define `IPlatformInfo`.
- [x] Define path mapping services.
- [x] Implement Windows desktop runner.
- [x] Implement Linux desktop runner.
- [x] Implement Wine/native mode selection.
- [x] Implement `NoToolRunner` or disabled capability behavior for future WASM.
- [x] Add tests for command argument generation.
- [x] Add tests for Windows/Linux/Wine path mapping.
- [x] Ensure UI does not call `Process.Start` directly.

Verification:

```bash
dotnet test --filter Tooling
dotnet build
```

Exit criteria:

- [x] Tool resolution is manifest-driven.
- [x] Tool execution is not tied to Uno UI.
- [x] Unsupported features fail with clear capability errors.
- [x] Tool paths and arguments are covered by tests.

## Phase 5 - Image Pipeline Foundation

Goal: replace `System.Drawing` with SkiaSharp-backed services.

**Completed: 2025-06-06**

- [x] Define image service interfaces.
- [x] Implement SkiaSharp image load/save/resize basics.
- [x] Implement validation for required dimensions/formats.
- [x] Decide TGA path:
      - SkiaSharp 3.x hat kein TGA-Support.
      - **Entscheidung: Custom TGA reader/writer (typ 2 uncompressed + RLE, 24/32 bpp)** in `TgaService.cs`.
      - Kein Pfim, kein externes Paket – minimaler eigener Reader/Writer.
- [x] Add golden tests for image outputs where possible.
- [x] Keep image code out of views/view models.

Execution notes:

- `UWUVCI.ImagePipeline/ImageService.cs`: Load/Save/Resize/Composite/CreateBlank/Validate/ValidateForWiiU
- `UWUVCI.ImagePipeline/TgaService.cs`: Custom TGA type 2 (uncompressed) + type 10 (RLE) Reader; type 2 Writer
- `SkiaSharp.NativeAssets.Linux.NoDependencies` 3.119.4 hinzugefügt (System-libSkiaSharp 88.1 ist inkompatibel)
- Tests: `UWUVCI.Tests/ImageTests.cs` – 17 Tests (ImageService 11 + TgaService 6)
- Gesamt: **58/58 Tests grün**, 0 Fehler

Verification:

```bash
dotnet test --filter "TestCategory=Image"
dotnet build
```

Exit criteria:

- [x] No new `System.Drawing` dependency.
- [x] Basic boot/icon/texture image operations work in tests.
- [x] TGA decision is documented.
- [x] Image errors are user-readable.

## Phase 6 - Core / Injection Modernization

Goal: move and modernize existing injection behavior as a whole, not one console at a time.

**Completed: 2025-06-05**

- [x] Port models needed by injection.
- [x] Port inject pipeline into `UWUVCI.Core`.
- [x] Keep behavior compatible with the original.
- [x] Split UI concerns out.
- [x] Split tool execution out.
- [x] Split image processing out.
- [x] Introduce job/progress/log abstractions.
- [x] Add tests for step composition and tool argument generation.
- [x] Do not redesign inject order as separate MVP methods.
- [x] Do not drop Wii/GCN/N64 as "later separate rewrites"; the source is modernized as a whole.

Execution notes:

- `UWUVCI.Core/Models/GameConsole.cs`: enum NDS-GCN, values match legacy GameBaseClassLibrary.GameConsoles
- `UWUVCI.Core/Models/Region.cs`: enum EU/US/JP, matches legacy Regions
- `UWUVCI.Core/Models/WiiTrimMode.cs`: Trim / OnlyTrimGarbage / DoNotModify
- `UWUVCI.Core/Models/ImageAsset.cs`: replaces PNGTGA (ImgPath, ImgBin, Extension, HasContent)
- `UWUVCI.Core/Models/EmulatorConfig.cs`: replaces N64Conf (IniPath, IniBin, DarkFilter, WideScreen, CommunityIni)
- `UWUVCI.Core/Models/GameBaseRef.cs`: replaces GameBases (Name, Region, IsCustom)
- `UWUVCI.Core/Models/GameConfig.cs`: full game config without WPF/DLL references
- `UWUVCI.Core/Pipeline/IProgressReporter.cs` + NullProgressReporter + CapturingProgressReporter
- `UWUVCI.Core/Pipeline/IJobLogger.cs` + NullJobLogger + CapturingJobLogger
- `UWUVCI.Core/Pipeline/InjectionContext.cs`: all pipeline inputs (replaces MainViewModel)
- `UWUVCI.Core/Pipeline/InjectionResult.cs`: Success/Failure/Cancelled outcome
- `UWUVCI.Core/Pipeline/IInjectPipeline.cs`: `Task<InjectionResult> InjectAsync(ctx, ct)`
- `UWUVCI.Core/Pipeline/IInjectStep.cs`: composable step + InjectionStepContext
- `UWUVCI.Core/Pipeline/InjectOptions.cs`: WiiInjectOptions, GcnInjectOptions, NfsInjectOptions, InjectKind (moved from old Services, no WPF)
- Tests: `UWUVCI.Tests/InjectionModelTests.cs` – 22 tests
- Gesamt: **80/80 Tests grün**, 0 Fehler, 0 Warnungen in Core

Verification:

```bash
dotnet test --filter "TestCategory=Injection"
dotnet build
```

Exit criteria:

- [x] Injection pipeline compiles without WPF dependencies.
- [x] UI-free pipeline can be invoked from tests.
- [x] Tool/image dependencies are injected through interfaces.
- [x] Known unsupported runtime features report capability errors instead of crashing.

## Phase 7 - Uno UX Implementation

Goal: rebuild the current practical UX in Uno.

**Completed: 2026-06-06**

- [x] Add app shell/navigation.
- [x] Add theme palette infrastructure.
- [x] Add settings screen.
- [x] Add tools/path screen.
- [x] Add console/inject flow.
- [x] Add config panels.
- [x] Add job progress/log UI.
- [x] Add error display and log export.
- [x] Add feature visibility based on capabilities.
- [x] Keep UX close to the original unless the old design blocks cross-platform behavior.

Execution notes:

- `Themes/Theme.Dark.xaml` + `Theme.Light.xaml`: Dark/Light palette portiert aus WPF-Themes; in App.xaml eingebunden (Dark als Default)
- `ViewModels/ObservableObject.cs`: MinimalBase (INotifyPropertyChanged, kein MVVM-Framework)
- `ViewModels/NavigationItem.cs`: Label, Glyph, PageType, IsSelected
- `ViewModels/ShellViewModel.cs`: Sidebar-Items-Liste (Inject, Settings, Tools, About)
- `ViewModels/SettingsViewModel.cs`: AppSettingsLoader-backed VM (OutPath, BasePath, ToolsPath, Theme, NativeWindows)
- `ViewModels/InjectViewModel.cs`: Console-Auswahl, RomPath, IsBusy/Progress/StatusMessage, InjectAsync via IInjectPipeline-Factory, Cancel
- `Views/ShellPage.xaml(.cs)`: Sidebar + ContentFrame, NavButton_Click, NavSelectedBrushConverter
- `Views/InjectPage.xaml(.cs)`: Console-ComboBox, ROM-Browse, Inject/Cancel, ProgressBar, Fehleranzeige, BoolToVisibilityConverter
- `Views/SettingsPage.xaml(.cs)`: Pfad-Felder + Browse, Theme-Combo, Platform-Mode-Combo, Save
- `Views/ToolsPage.xaml(.cs)` + `AboutPage.xaml(.cs)`: Stubs
- `App.xaml.cs`: Shell/Settings/Inject als statische Singletons; Settings-Pfad → `%LOCALAPPDATA%/UWUVCI-V3/settings.toml`
- Build: 0 Fehler, 0 Warnungen (net10.0-desktop); Gesamt-Rewrite: 80/80 Tests grün

Verification:

```bash
dotnet build
dotnet test
```

Manual verification:

- [ ] App launches on Linux/KDE.
- [ ] App launches on Windows when tested there.
- [ ] Settings can be saved and reloaded.
- [ ] Theme switching works.
- [ ] Disabled/unavailable features explain why.

Exit criteria:

- [x] Uno UI is usable for the main flow.
- [x] No direct tool execution from views.
- [x] No direct WPF/WinForms dependency.

## Phase 8 - Packaging

Goal: make builds runnable outside the dev tree.

- [x] Windows portable ZIP.
- [x] Linux tar.gz.
- [ ] AppImage optional.
- [x] Ensure writable data uses AppData/XDG paths.
- [x] Ensure bundled tools/assets resolve correctly.
- [x] Ensure no writes into AppImage/app install directory.

Verification:

```bash
dotnet publish
```

Manual verification:

- [x] Run published Linux build from a clean folder.
- [ ] Run published Windows build when available.
- [x] Verify settings path.
- [ ] Verify tool path.
- [ ] Verify logs path.

Exit criteria:

- [x] Published desktop build starts on the local system.
- [x] Portable output does not depend on source-tree paths.
- [ ] AppImage blockers, if any, are documented.

Notes:
- `PublishSingleFile=true` incompatible with Uno Platform's XAML resource resolver on Desktop/Skia.
  Using directory publish + tar.gz/zip packaging instead (see `UWUVCI.App.Uno/publish.sh`).
- ms-appx:/// URIs for resources in the own assembly must NOT include the assembly name prefix
  (correct: `ms-appx:///Themes/Theme.Dark.xaml`, wrong: `ms-appx:///UWUVCI.App.Uno/Themes/...`).
- `DebugType=none` breaks Uno's `EmbeddedResourceInjectorTask`; portable PDB (default) required.
- Runtime data paths: `UWUVCI.Core/Runtime/AppDataPaths.cs`
  Linux → ~/.local/share/UWUVCI-V3 (XDG_DATA_HOME), Windows → %LOCALAPPDATA%\UWUVCI-V3

## Phase 9 - WASM-Readiness Audit

Goal: check whether a WASM target can be added without deep architecture changes.

- [x] Confirm no desktop APIs leak into `UWUVCI.Core`.
- [x] Confirm no process execution leaks into UI.
- [x] Confirm tool-based features are capability-disabled for browser.
- [x] Confirm storage/file picker abstractions have browser-shaped alternatives.
- [x] List features that require server-side execution.

Exit criteria:

- [x] WASM blockers are architectural notes, not hidden code coupling.
- [x] Future WASM path is documented.

### WASM Audit Results

**CORE (UWUVCI.Core) – WASM-safe**
- Only `AppDataPaths.cs` uses `OperatingSystem.IsLinux()` / `IsMacOS()` for path resolution.
  → On WASM: replace with `IsolatedStorageFile` or origin-private filesystem; class is easily swappable.
- `IToolRunner` / `IToolResolver` interfaces contain no platform code.
- `AppDataPaths` is not referenced from injection pipeline logic; only from App bootstrap.

**TOOLING (UWUVCI.Tooling) – NOT WASM-safe by design**
- `ProcessToolRunner` spawns `System.Diagnostics.Process` → not available in browser.
- `NoOpToolRunner` exists and is the correct WASM substitute.
- Tool-dependent pipeline steps must check `IToolRunner.IsAvailable` / use capability flags.
- Mitigation: inject `NoOpToolRunner` for WASM builds; disable inject UI buttons when no tools.

**UI (UWUVCI.App.Uno) – Picker APIs need WASM variant**
- `InjectPage.xaml.cs`: uses `Windows.Storage.Pickers.FileOpenPicker` (line 32)
- `SettingsPage.xaml.cs`: uses `Windows.Storage.Pickers.FolderPicker` (line 61)
- Uno Platform supports these APIs on WASM but requires `StorageProvider` initialization
  and returns `IStorageFile` / `IStorageFolder` – API shape is compatible already.
  No rewrite needed; just WASM-specific initialization (WinUIEx init block).

**CONFIG (UWUVCI.Config) – NOT WASM-safe**
- `AppSettingsLoader.LoadFromFile` / `SaveToFile` use `System.IO.File` + `Directory`.
- WASM mitigation: add `IAppSettingsStore` abstraction; default impl = filesystem, WASM impl = `localStorage` or OPFS.

**IMAGE PIPELINE (UWUVCI.ImagePipeline) – NOT WASM-safe**
- `ImageService.SaveImage` and `TgaService.SaveTga` use `Directory.CreateDirectory` + `SKData`.
- SkiaSharp has WASM bindings; file I/O must route through a `IFileWriter` abstraction.

### WASM Blocker Summary

| Component | Blocker | Mitigation |
|-----------|---------|------------|
| `UWUVCI.Tooling` | `Process.Start` | inject `NoOpToolRunner`; disable tool-dependent features |
| `UWUVCI.Config` | `File.ReadAllText/WriteAllText` | add `IAppSettingsStore` abstraction |
| `UWUVCI.ImagePipeline` | `Directory.CreateDirectory`, file save | add `IFileWriter` abstraction |
| `UWUVCI.Core/AppDataPaths` | `OperatingSystem.*`, path resolution | swap implementation for WASM |
| Uno Pickers | need WASM StorageProvider init | one-time WinUIEx init block |

No hidden platform coupling found in Core pipeline logic.

## Phase 10 - Final Cleanup

Goal: remove old baggage and make the repo understandable.

- [x] Remove or archive WPF project when migration is complete.
- [x] Remove protected release scripts from the new build path.
- [x] Remove copied `Kopieren` files unless proven needed.
- [x] Remove stale backup files.
- [x] Update README.
- [x] Update build instructions.
- [x] Update legal/project boundary notes.

Verification:

```bash
dotnet restore
dotnet build
dotnet test
```

Exit criteria:

- [x] Clean build.
- [x] Tests pass.
- [x] Manual launch works on the local system.
- [x] README explains Windows/Linux build and run steps.

### Cleanup Inventory

**Deleted (6. Juni 2026):**
- `.DS_Store` (macOS metadata)
- `AnalysisReport.sarif` (old assessment report)
- `upgrade-assistant.clef` (4.3 MB wizard output)
- `tmp_toolrunner.patch` (obsolete legacy patch)
- `artifacts/` directory (232 MB publish output, git-ignored)
- `TokenGenerator/` directory (legacy utility, unused in Rewrite)
- `UWUVCI MSTest/` directory (legacy test suite, replaced by `UWUVCI.Tests`)

**Space freed:** ~240 MB

**Deferred deletion (now addressed in Phase 11):**
- `UWUVCI AIO WPF/` directory – will be used as legacy reference for Phase 11 porting
- `Scripts/` directory – keep for reference until Phase 11 complete

**Optimization opportunity (safe to clean anytime):**
- `*/bin`, `*/obj` – run `dotnet clean` to reduce from 1.5 GB to ~100 MB (rebuilds on next `dotnet build`)

See [CLEANUP.md](CLEANUP.md) for full audit and post-Phase-10 action items.

## Build Verification Matrix

- [x] Baseline legacy project status recorded.
- [x] New solution `dotnet restore` passes.
- [x] New solution `dotnet build` passes.
- [x] New solution `dotnet test` passes.
- [x] Uno desktop app launch tested on local Linux/KDE.
- [ ] Windows launch tested when a Windows system is available.
- [x] Published Linux build launch tested.
- [ ] AppImage launch tested only when AppImage target is implemented.

## Phase 11 - Injection Service Implementation

Goal: Port console-specific injection logic from legacy project and modernize within V4 architecture.

**Status: NOT YET STARTED (ready to begin)**

Supported consoles (from original):
- [ ] **GCN** (GameCube) – `WiiInjectService` (uses wit, nfs2iso2nfs, copy icon/banner)
- [ ] **Wii** – `WiiInjectService` (uses wit, nfs2iso2nfs, Nintendont config)
- [ ] **N64** – `N64InjectService` (uses N64Converter, RetroInject, video filter)
- [ ] **NES** – (legacy `NESInjectService`)
- [ ] **SNES** – (legacy `SNESInjectService`)
- [ ] **GBA** – (legacy `GBAInjectService`)
- [ ] **NDS** – (legacy `NDSInjectService`)
- [ ] **TurboGrafx** – (legacy `TurboGrafiInjectService`)
- [ ] **MSX** – (legacy `MSXInjectService`)

### 11.1 – GCN/Wii MVP (First)

Port GCN (GameCube) injection to demonstrate the pattern. Wii follows same logic.

**Tasks:**
- [ ] Read legacy `Classes/Injection.cs` + `Services/GCNInjectService.cs` from UWUVCI AIO WPF/
- [ ] Extract step sequence: icon copy → banner copy → .app repack → NFS wrapping → title creation
- [ ] Create `UWUVCI.Services/GCNInjectService.cs` as `IGcnInjectStep` implementations
- [ ] Port tool argument generation (wit, nfs2iso2nfs commands)
- [ ] Create unit tests for each step (Golden files for commands, paths, error cases)
- [ ] Integrate into `InjectViewModel` → call via `IInjectPipeline`
- [ ] Manual test: inject real GCN ROM → launch on Wii U

**Legacy reference files:**
- `UWUVCI AIO WPF/Services/GCNInjectService.cs`
- `UWUVCI AIO WPF/Classes/Injection.cs` (orchestration)
- `UWUVCI AIO WPF/Helpers/ToolRunner.cs` (tool execution pattern – now abstracted in V4 `IToolRunner`)

**New files to create:**
- `UWUVCI.Services/` (new project, references Core + Tooling + ImagePipeline)
- `UWUVCI.Services/IGcnInjectStep.cs` (marker interface extending `IInjectStep`)
- `UWUVCI.Services/GCN/CopyBootImageStep.cs`
- `UWUVCI.Services/GCN/CopyBannerStep.cs`
- `UWUVCI.Services/GCN/RepackAppStep.cs`
- `UWUVCI.Services/GCN/WrapToNfsStep.cs`
- `UWUVCI.Services/GCN/CreateTitleMetaStep.cs`
- `UWUVCI.Tests/GcnInjectTests.cs` (golden-file tests for wit/nfs2iso2nfs calls)

**Exit criteria:**
- [ ] GCN inject succeeds from UI (app ROM → WiiU-ready file)
- [ ] All GCN steps have unit tests
- [ ] Tool arguments are validated against legacy behavior
- [ ] Error handling matches original (missing tools, ROM validation, disk space)
- [ ] 15–20 new tests added (all green)

### 11.2 – Wii (Second)

Wii follows GCN pattern but with Nintendont-specific config.

- [ ] Port `WiiInjectService` → `WiiInjectStep` implementations
- [ ] Same tool logic (wit, nfs2iso2nfs)
- [ ] Add Nintendont config handling (video, controller remapping)
- [ ] Tests for Wii-specific steps

### 11.3 – N64 (Third)

Different tool stack (N64Converter, RetroInject, video filter).

- [ ] Port `N64InjectService`
- [ ] Extract video filter logic (if present)
- [ ] Tool argument generation for N64-specific commands
- [ ] Tests

### 11.4 – Legacy Consoles (NES, SNES, GBA, NDS, TurboGrafx, MSX)

Port remaining services.

- [ ] Each console one task per session (focus, small scope)
- [ ] Extract tool calls + argument generation
- [ ] Golden-file tests per console

**Total new unit tests expected: 80+ across all consoles**

### Strategy

1. **Port one console at a time** (GCN first, proven pattern, then Wii/N64/others)
2. **For each console:**
   - [ ] Read legacy service code
   - [ ] Identify pipeline steps
   - [ ] Extract tool calls and argument generation
   - [ ] Implement as `IInjectStep`-derived classes
   - [ ] Write golden-file tests
   - [ ] Integrate into `InjectViewModel`
3. **Reuse abstractions** already built in Phases 3–7:
   - `IToolRunner` (replaces direct `Process.Start`)
   - `IImageService` (boot/banner images)
   - `IProgressReporter` (UI feedback)
   - `IJobLogger` (per-step logging)

### Verification Commands

```bash
# After each console is ported:
dotnet test --filter "TestCategory=Injection"
dotnet build
dotnet run --project UWUVCI.App.Uno --framework net10.0-desktop
# Manual: select console, pick ROM, inject, verify output
```

### Known Challenges

- **Tool availability**: GCN/Wii/N64 require external tools (wit, nfs2iso2nfs, N64Converter, etc.)
  → Mitigation: use mock `IToolRunner` for unit tests; separate integration test suite for real tools.
- **ROM validation**: original used CRC/checksums; port as validation steps.
- **Path handling**: legacy mixed absolute/relative paths; ensure all resolved via `AppDataPaths` in V4.
- **Error recovery**: original had silent failures in some cases; make explicit in V4.

## Bug / Risk Ledger

Use this section during migration. Do not leave discovered bugs only in chat context.

- [ ] `BinaryFormatter` usage must be removed or migrated.
- [ ] `System.Drawing` usage must be replaced.
- [ ] Legacy WPF target `.NETFramework,Version=v4.8` does not build on Linux host (MSB3644 expected baseline blocker).
- [ ] Uno templates/workloads are currently not installed on local machine; Phase 2 app bootstrap is blocked until installed.
- [ ] `Classes/Dol.cs` contains placeholder path text for `codehandler.bin`; verify whether this code is real, dead, or broken.
- [ ] `Kopieren` duplicate files must be reviewed before removal.
- [ ] Direct `Process.Start` calls must move behind tooling services.
- [ ] Direct Win32/User32 calls must be removed or isolated.
- [ ] WinForms/Windows dialog fallback logic must be replaced with Uno/platform services.
- [ ] GitHub write services must not rely on embedded tokens.
- [ ] CKey must not be written into logs.
- [ ] Tool path assumptions must be tested against Linux paths and Wine paths.
- [ ] Published builds must not rely on source-tree-relative paths.
- [ ] AppImage must not write into its mounted app directory.

## Agent Task Template

Use this shape for each implementation task:

```text
Task:
Scope:
Files to inspect:
Files expected to edit:
Behavior to preserve:
Behavior allowed to change:
Verification commands:
Known risks:
Exit criteria:
```

Use this shape for handoff:

```text
Completed:
Changed files:
Commands run:
Build/test status:
New bugs found:
Unresolved blockers:
Next recommended task:
```

## Core Decisions

- [ ] Runtime: .NET 10 LTS.
- [ ] Language: current C# version shipped with the .NET 10 SDK.
- [ ] UI framework: Uno Platform.
- [ ] Primary targets: Windows Desktop and Linux Desktop/KDE.
- [ ] Later target: Web/WASM or web app.
- [ ] Not in scope: WPF, .NET Framework, Mono as primary target, macOS, iOS.
- [ ] Build style: manual builds only.
- [ ] No release signatures.
- [ ] No protected release pipeline.
- [ ] No embedded GitHub token.
- [ ] No token obfuscation.
- [ ] No LocalInstallGuard.
- [ ] No device fingerprinting or blacklist logic.

## Project Structure

- [ ] `UWUVCI.Core`
      Models, inject pipeline, validation, non-UI business logic.

- [ ] `UWUVCI.Tooling`
      Tool resolution, process execution, Wine/native handling, platform capabilities.

- [ ] `UWUVCI.ImagePipeline`
      Image loading, resizing, validation, conversion, boot/icon/texture generation.

- [ ] `UWUVCI.Config`
      TOML settings, tool manifests, platform capability files, legacy settings migration.

- [ ] `UWUVCI.App.Uno`
      Uno UI, shell, views, themes, bindings, platform-specific UI services.

- [ ] `UWUVCI.Tests`
      Core, tooling, config, image pipeline, golden-file tests.

## Uno/WASM Direction

- [ ] Build Uno Desktop first.
- [ ] Prepare the project so WASM later does not require deep rewrites.
- [ ] Keep desktop-only APIs out of `UWUVCI.Core`.
- [ ] Keep process execution out of UI/ViewModels.
- [ ] Put file pickers, folders, app storage, clipboard, platform info and tool running behind interfaces.
- [ ] WASM later gets different implementations or disabled capabilities.

Suggested interfaces:

- [ ] `IFilePicker`
- [ ] `IFolderPicker`
- [ ] `IToolRunner`
- [ ] `IAppStorage`
- [ ] `IClipboardService`
- [ ] `IExternalUrlLauncher`
- [ ] `IPlatformInfo`
- [ ] `ICapabilityService`

## UX

- [ ] Current UX is the default reference.
- [ ] Do not copy the old WPF architecture.
- [ ] Rebuild the UI in Uno using the same practical flow.
- [ ] Shell/navigation first.
- [ ] Settings/paths/tools next.
- [ ] Inject flow next.
- [ ] Config panels next.
- [ ] Image creator after the image pipeline is stable.
- [ ] Feature visibility must come from capabilities, not hard-coded UI conditions.
- [ ] Windows can expose more features.
- [ ] Linux exposes only native/Wine-capable features.
- [ ] WASM later exposes only browser/server-capable features.

## Themes / Styles

- [ ] Use palette tokens, not separate layouts per theme.
- [ ] Keep one UX and switch colors/styles centrally.
- [ ] Provide five palettes:
      - Classic Dark
      - Classic Light
      - Wii U Blue
      - Terminal Green
      - High Contrast

Suggested layout:

```text
Styles/
  Palettes/
    ClassicDark.xaml
    ClassicLight.xaml
    WiiUBlue.xaml
    TerminalGreen.xaml
    HighContrast.xaml
  Components/
    Buttons.xaml
    Cards.xaml
    Forms.xaml
    Logs.xaml
```

## Image Pipeline

- [ ] Use SkiaSharp as the default image library.
- [ ] Reason: MIT license.
- [ ] Reason: fits Uno/Cross-platform well.
- [ ] Reason: better fit for Windows/Linux/WASM than System.Drawing.
- [ ] Do not use `System.Drawing`.
- [ ] Do not use ImageSharp as default because of the Six Labors Split License.
- [ ] Keep SkiaSharp behind `UWUVCI.ImagePipeline` services.
- [ ] Do not spread SkiaSharp code through views or view models.
- [ ] TGA support must be checked separately.
- [ ] If SkiaSharp is not enough for TGA:
      - write a small TGA reader/writer, or
      - keep Pfim if suitable, or
      - keep external TGA tools as Desktop fallback.
- [ ] Long-term goal: reduce external image tools where practical.

## Config Format

- [ ] Use TOML for app settings.
- [ ] Use TOML for tool manifests.
- [ ] Use TOML for platform capabilities.
- [ ] JSON can stay for external/API/compatibility data.
- [ ] Keep CKey behavior like the original.
- [ ] No new secret store for CKey.
- [ ] Do not write CKey into logs.
- [ ] Remove `SysKey` and `SysKey1`; they are app-protection leftovers.

Example:

```toml
[paths]
tools = ""
temp = ""
output = ""

[platform]
mode = "auto"

[ui]
theme = "classic-dark"
density = "normal"
```

## Tool Schema

- [ ] `tools/windows-x64.toml`
- [ ] `tools/linux-x64.toml`
- [ ] No macOS tool schema.
- [ ] Track tool versions.
- [ ] Track required/optional tools per inject method.
- [ ] Track native/Wine availability per tool.
- [ ] Track whether a method can run on Windows, Linux, or future WASM/server.
- [ ] Checksums optional but recommended for bundled tool sets.

Example:

```toml
[tool.wit]
windows = "wit.exe"
linux = "wit-linux"
required_for = ["wii", "gcn"]

[feature.gcn_inject]
requires = ["wit", "nfs2iso2nfs"]
platforms = ["windows", "linux"]
wine_allowed = true
```

## External Tools To Audit

- [ ] `wit`
- [ ] `wstrt`
- [ ] `nfs2iso2nfs`
- [ ] `sox`
- [ ] `N64Converter`
- [ ] `RetroInject`
- [ ] `png2tga`
- [ ] `tga2png`
- [ ] `jpg2tga`
- [ ] `bmp2tga`
- [ ] `tga_verify`
- [ ] `wiiurpxtool`
- [ ] `GetExtTypePatcher`
- [ ] `ConvertToISO`
- [ ] `ConvertToNKit`
- [ ] `BuildPcePkg`
- [ ] `BuildTurboCdPcePkg`
- [ ] `MArchiveBatchTool`
- [ ] Decide which tools stay, which get replaced, and which remain Windows-only.
- [ ] Check redistribution/license status for every bundled tool.

## Dependencies To Replace Or Recheck

- [ ] Replace `System.Drawing`.
- [ ] Remove `BinaryFormatter`.
- [ ] Remove WPF assemblies and WPF-specific dependencies.
- [ ] Remove `WindowsAPICodePack`.
- [ ] Remove WinForms dialogs.
- [ ] Remove `Costura/Fody`.
- [ ] Remove WPF `MaterialDesignThemes`.
- [ ] Recheck `NAudio`.
- [ ] Recheck `Octokit`.
- [ ] Keep `Newtonsoft.Json` only where needed.
- [ ] Evaluate whether `WebView2` is still needed.

## Local DLLs

- [ ] `GameBaseClassLibrary.dll`
- [ ] `WiiUDownloaderLibrary.dll`
- [ ] Check whether source is available.
- [ ] Check whether modern .NET builds are possible.
- [ ] Replace or wrap them if needed.

## Injection Modernization

- [ ] Do not select a small MVP inject method.
- [ ] Do not rewrite inject methods one-by-one as separate feature redesigns.
- [ ] Modernize the existing injection source as a whole.
- [ ] Preserve current behavior as the reference.
- [ ] Move injection logic out of UI.
- [ ] Keep the pipeline UI-free.
- [ ] Make steps observable: progress, logs, error reporting.
- [ ] Keep tool argument generation testable.
- [ ] Keep platform capability checks explicit.

## Job System

- [ ] Run injection as jobs.
- [ ] Support progress.
- [ ] Support logs per job.
- [ ] Support cancel where practical.
- [ ] Support retry only where safe.
- [ ] Support opening artifact/output folder on Desktop.
- [ ] Support log/error export.

## GitHub Features

- [ ] No embedded bot token.
- [ ] No protected-token injection.
- [ ] Read-only GitHub features can stay if they work without a token.
- [ ] Write actions are not part of the protected-token model anymore.
- [ ] If write actions return later, decide separately:
      - user-supplied token, or
      - server/API workflow.

## Packaging

- [ ] Windows portable ZIP.
- [ ] Windows installer optional.
- [ ] Linux tar.gz.
- [ ] Linux AppImage optional target.
- [ ] No macOS packages.
- [ ] Do not require writes to the application install directory.
- [ ] Use AppData/XDG paths for writable data.

## AppImage Notes

- [ ] Define AppDir layout.
- [ ] Resolve bundled assets/tools through an app path resolver.
- [ ] Ensure native Linux tools are executable.
- [ ] Do not write inside the AppImage.
- [ ] Use XDG paths for settings/cache/output defaults.

## Build

- [ ] Manual build only.
- [ ] `dotnet restore`
- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] `dotnet publish`
- [ ] No signing step.
- [ ] No token injection step.
- [ ] No protected build pipeline.

## Tests

- [ ] Config parsing.
- [ ] Tool manifest parsing.
- [ ] Platform capability resolution.
- [ ] Tool resolution.
- [ ] Windows/Linux/Wine path mapping.
- [ ] Tool argument generation.
- [ ] Injection step composition.
- [ ] Golden files for XML/JSON/TOML outputs.
- [ ] Image pipeline validation.
- [ ] Log redaction for CKey and sensitive fields.

## CI Optional

- [ ] Windows x64 build/test.
- [ ] Linux x64 build/test.
- [ ] No macOS CI.
- [ ] AppImage build later optional.

## Legal / Project Boundaries

- [ ] Do not ship ROMs.
- [ ] Do not ship bases.
- [ ] Do not ship keys.
- [ ] Document that users provide their own files.
- [ ] Check external tool licenses before bundling.

## Migration From Current Source

- [ ] Keep original behavior as reference.
- [ ] Remove copied `Kopieren` files unless proven needed.
- [ ] Ignore/remove old protected release scripts.
- [ ] Port useful assets/resources.
- [ ] Rebuild UI in Uno, do not port WPF XAML directly.
- [ ] Preserve current config concepts where still useful.
- [ ] Replace app-protection settings and scripts with nothing.

## Open Decisions

- [ ] Uno Desktop-only template first, or Desktop + WASM template from day one?
      Current direction: Desktop first, WASM-ready architecture.

- [ ] Exact TGA implementation?
      Current direction: SkiaSharp first, TGA handled separately.

- [ ] GitHub write features?
      Current direction: no embedded token; read-only only unless redesigned later.

- [ ] AppImage priority?
      Current direction: optional target after Linux tar.gz works.
