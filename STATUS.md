# UWUVCI-AIO-WPF – Rewrite Abschluss-Status

Datum: 6. Juni 2026

---

## 🎯 Projektüberblick

**Zielsetzung:** Rewrite von UWUVCI-AIO-WPF von WPF (.NET Framework 4.8) zu Cross-Platform (.NET 10 + Uno Platform).

**Status:** ✅ **ALLE 10 PHASEN ABGESCHLOSSEN**

---

## 📊 Rewrite-Status nach Phase

| Phase | Fokus | Status | Tests | Dateien |
|-------|-------|--------|-------|---------|
| 0 | Env + Baseline | ✅ | – | – |
| 1 | Quell-Inventar | ✅ | – | – |
| 2 | Scaffold (6 Projekte) | ✅ | – | 6 .csproj |
| 3 | TOML Config Layer | ✅ | 22 | AppSettingsLoader, ToolManifestLoader |
| 4 | Tooling Foundation | ✅ | 18 | ProcessToolRunner, ManifestToolResolver, Wine-Detect |
| 5 | Image Pipeline | ✅ | 17 | ImageService, TgaService (custom typ 2 + typ 10) |
| 6 | Core + Pipeline-Modelle | ✅ | 22 | GameConsole, GameConfig, InjectionContext, IInjectStep |
| 7 | Uno UX Shell | ✅ | 0 | ShellPage, InjectPage, SettingsPage, Themes |
| 8 | Packaging | ✅ | – | publish.sh, Linux tar.gz, Windows ZIP |
| 9 | WASM-Audit | ✅ | – | Blocker-Matrix dokumentiert |
| 10 | Final Cleanup | ✅ | – | Struktur reorganisiert |

**Gesamt:** 80 Tests ✅ | 0 Fehler | 0 Warnungen (außer MSTEST0037 style hints)

---

## 📦 Projekt-Struktur (Post-Cleanup)

### Root-Level (sauber)
```
UWUVCI-AIO-WPF/
├── Rewrite/                    ← 1.5 GB (AKTIV)
│   ├── UWUVCI.Core/
│   ├── UWUVCI.Config/
│   ├── UWUVCI.Tooling/
│   ├── UWUVCI.ImagePipeline/
│   ├── UWUVCI.App.Uno/
│   ├── UWUVCI.Tests/           ← 80 Tests
│   └── UWUVCI.Rewrite.slnx
│
├── UWUVCI AIO WPF/             ← 13 MB (Legacy Referenz, später löschen)
├── Scripts/                    ← 52 KB (historisch, später löschen)
├── .vscode/                    ← 12 KB (Projekt-Konfiguration)
│
├── README.md                   ← Rewrite-fokussiert
├── REWRITE_PLAN.md             ← 10 Phasen, alle abgehakt
├── CLEANUP.md                  ← Audit + Post-Phase-10 Tasks
├── LICENSE                     ← AGPL-3.0-or-later
├── .gitignore                  ← bin/, obj/, artifacts/, .DS_Store
└── .git/                       ← 553 MB
```

### Cleanup durchgeführt (6. Juni)
- ❌ `.DS_Store` (11 KB)
- ❌ `AnalysisReport.sarif` (2.2 KB)
- ❌ `upgrade-assistant.clef` (4.3 MB)
- ❌ `tmp_toolrunner.patch` (obsolet)
- ❌ `artifacts/` Verzeichnis (232 MB)
- ❌ `TokenGenerator/` Ordner (ungenutzt)
- ❌ `UWUVCI MSTest/` Ordner (von Rewrite ersetzt)

**Speicher befreit:** ~240 MB

---

## 🔄 Build- & Test-Status

### Rewrite bauen
```bash
cd Rewrite
dotnet build UWUVCI.Rewrite.slnx
# Ergebnis: 0 Fehler, 45 Warnungen (style-hints von MSTest)
```

### Tests ausführen
```bash
cd Rewrite
dotnet test UWUVCI.Rewrite.slnx
# Ergebnis: 80/80 Tests ✅ (727 ms)
```

### App starten (Dev)
```bash
cd Rewrite/UWUVCI.App.Uno
dotnet run --project UWUVCI.App.Uno/UWUVCI.App.Uno.csproj -f net10.0-desktop
```

### Portable Release bauen
```bash
cd Rewrite/UWUVCI.App.Uno
bash publish.sh linux        # → artifacts/dist/uwuvci-linux-x64.tar.gz (64 MB)
bash publish.sh win          # → artifacts/dist/uwuvci-win-x64.zip (cross-compile)
```

---

## 💾 Datenpfade (Rewrite)

| Plattform | Pfad | Beschreibung |
|-----------|------|-------------|
| Linux | `~/.local/share/UWUVCI-V3/` | XDG Data Home |
| Windows | `%LOCALAPPDATA%\UWUVCI-V3\` | Local App Data |
| macOS | `~/Library/Application Support/UWUVCI-V3/` | App Support |

Klasse: `Rewrite/UWUVCI.Core/Runtime/AppDataPaths.cs`

---

## ⚙️ Tech-Stack (Rewrite)

| Komponente | Version | Zweck |
|------------|---------|-------|
| **.NET SDK** | 10.0.108 | Runtime |
| **Uno Platform** | 6.5.36 | UI Framework (Desktop + WASM) |
| **Tomlyn** | 2.4.2 | TOML Config-Serialisierung |
| **SkiaSharp** | 3.119.4 | Image-Bibliothek |
| **SkiaSharp.NativeAssets.Linux.NoDependencies** | 3.119.4 | Linux native fix |
| **MSTest** | v3 | Testing Framework |

---

## 🚀 Was fertig ist

✅ **Core Injection Pipeline**
- GameConfig, GameConsole (enum), Region models
- InjectionContext + Abstraktion für alle Pipeline-Eingaben
- IInjectStep + IInjectPipeline – Composable injection steps
- Alle Models 1:1 aus Legacy portiert, ohne WPF/UI-Dependencies

✅ **Config Layer**
- TOML-basiert via Tomlyn
- AppSettingsModel + ToolManifestModel
- Safe CKey-Handling (nie geloggt)
- Validierung + Load/Save

✅ **Tooling Foundation**
- `ProcessToolRunner` für lokale Ausführung
- `NoOpToolRunner` für WASM (Browser can't execute processes)
- `ManifestToolResolver` – Tool-Pfade aus TOML
- Wine-Detektion + macOS-Unterstützung
- `IToolRunner` / `IToolResolver` Interfaces – testbar

✅ **Image Pipeline**
- Custom TGA Reader/Writer (typ 2 uncompressed, typ 10 RLE)
- SkiaSharp-based Image Load/Save/Resize/Composite
- Validation + WiiU-spezifische Formatierung

✅ **Uno UX**
- Themes (Dark + Light, portiert aus WPF)
- ShellPage (Sidebar Navigation)
- InjectPage (Console-Picker, ROM-Picker, Progress)
- SettingsPage (Pfade + Theme + Platform-Mode)
- ViewModels (ObservableObject, MVVM)
- Data-Binding (Settings ↔ UI)

✅ **Packaging**
- Linux: `.tar.gz` self-contained
- Windows: `.zip` self-contained (cross-compile von Linux)
- PublishReadyToRun + Compression
- Keine Abhängigkeit von Quell-Tree nach Publish

✅ **WASM-Readiness**
- Keine versteckten Platform-APIs im Core
- Klare Blocker-Matrix mit Mitigationen dokumentiert
- `IAppSettingsStore` / `IFileWriter` Abstraktion geplant
- Picker-APIs kompatibel mit Uno WASM

✅ **Repository Health**
- Saubere .gitignore
- Build-Artifacts git-ignored
- 0 Stale-Dateien im Root
- README + REWRITE_PLAN + CLEANUP dokumentiert
- 80 Tests, alle grün

---

## ⚠️ Bekannte Limitationen / Nächste Schritte

### Noch nicht implementiert
- ❌ Echte Injection-Logik (Service-Schicht noch zu bauen)
- ❌ Feature-Matrix: welche Features pro Console
- ❌ Integration mit externen Tools (WIT, CDECRYPT, etc.) – nur Abstraktion vorhanden
- ❌ Server-Backend für WASM (falls Web-App später kommt)
- ❌ AppImage support (Uno Desktop kann zu AppImage gebündelt werden, aber noch nicht getestet)

### Post-Phase-10 Cleanup
- 🔄 Legacy `UWUVCI AIO WPF/` löschen – nach Injection-Pipeline Feature-Complete
- 🔄 `Scripts/` löschen – nach erster Rewrite-Release
- 🔄 `dotnet clean` lokal ausführen – 1.5 GB → 100 MB (regenerable)

### WASM-Blocker (dokumentiert in REWRITE_PLAN.md Phase 9)
- `IAppSettingsStore` Abstraktion für `File.ReadAllText/WriteAllText`
- `IFileWriter` für Image-Speichern
- `AppDataPaths` WASM-Alternate (z.B. IndexedDB, OPFS)
- Tool-Execution nur lokal (ProcessToolRunner)

---

## 📖 Dokumentation

| Datei | Inhalt |
|-------|--------|
| [README.md](README.md) | Repo-Layout, Build-Anleitung, Unterstützung |
| [REWRITE_PLAN.md](REWRITE_PLAN.md) | 10 Phasen, Exit-Criteria, WASM-Audit |
| [CLEANUP.md](CLEANUP.md) | Post-Phase-10 Task-Liste, Disk-Optimization |

---

## 🎓 Lernpunkte / Architectural Notes

### Wo der Rewrite besser ist als Legacy
- ✅ Testbar: Alle Services via Interfaces, DI-ready
- ✅ Plattformunabhängig: Linux + Windows + macOS Unterstützung
- ✅ WASM-ready: Core-Pipeline hat keine Desktop-APIs
- ✅ Saubere Abstraktionen: ToolRunner, FileWriter, AppSettings – nicht hart gekoppelt
- ✅ Moderne .NET: nullable references, records, file-scoped types

### Uno Platform Erkenntnisse
- ⚠️ `PublishSingleFile=true` inkompatibel mit XAML-Resource-Resolver auf Desktop/Skia
  → Stattdessen directory publish + tar.gz/zip
- ⚠️ ms-appx:/// URIs für eigene Ressourcen: NICHT Assembly-Name-Prefix verwenden
  → Korrekt: `ms-appx:///Themes/Theme.Dark.xaml`
  → Falsch: `ms-appx:///UWUVCI.App.Uno/Themes/...`
- ✅ SkiaSharp 3.119.4 + Linux Native-Asset-Fix = stabil
- ✅ WinRT-Pickers (FolderPicker, FileOpenPicker) arbeiten auf Uno Desktop via WinRT-Compat

### Migration-Patterns
- **Enums:** 1:1 aus Legacy kopiert (GameConsole, Region, etc.)
- **Models:** Ohne WPF/DLL, serialisierbar (TOML, JSON)
- **Services:** Behind Interfaces → leicht testbar + austauschbar
- **ToolRunner:** Generisch für alle Executable + Wine-Fallback
- **Config:** TOML statt .json oder XML – human-readable + verwaltet

---

## 🚦 Nächste Empfehlung

1. **Sofort:** Commit + Push (alle Cleanup-Deletions + neue Dateien)
2. **Feature-fokussiert:** Implementation der echten Injection-Service-Logik starten
3. **Testing:** Legacy-Behavior gegen Rewrite-Behavior validieren
4. **Später:** WPF-Ordner löschen nach Feature-Parity

---

**Gesamtbilanz:** 10 Phasen, 0 Blockierung, vollständig dokumentiert. Rewrite ist architektonisch solide und produktionsreif für die nächste Implementierungsphase. ✨
