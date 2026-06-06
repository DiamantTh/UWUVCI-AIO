# Phase 14 — External Tool Audit & Multi-OS Strategy

**Status:** IN PROCESS  
**User Decision:** Option C = No Wine wrapper, no skip. All tools same behavior on all OS.  
**Timeline:** 1 session

## Tool Audit Results (from UWUVCI.Services analysis)

### ✅ Cross-Platform (Native on Windows + Linux)
| Tool | Used by | Status |
|------|---------|--------|
| `wit` | GCN, Wii | ✓ Both Windows + Linux native |
| `nfs2iso2nfs` | GCN, Wii | ✓ Both Windows + Linux native |
| `wiiurpxtool` | NES, SNES | ✓ Both Windows + Linux native |

### ⚠️ Windows-Only (Need Solution for Option C)
| Tool | Used by | Status | Phase 14 Solution |
|------|---------|--------|-------------------|
| `N64Converter` | N64 | Windows-only EXE | ??? |
| `RetroInject` | NES, SNES | Windows-only EXE | ??? |

### ❓ Unknown Platform (Audit Needed)
| Tool | Used by | Status | Action |
|------|---------|--------|--------|
| `pokepatch` | GBA | ? | Search legacy source |
| `MArchiveBatchTool` | GBA | ? | Search legacy source |
| `BuildPcePkg` | TG16 | ? | Search legacy source |

### ✅ No External Tools (Already Cross-Platform)
| Service | Status |
|---------|--------|
| NDS | ✓ Pure .NET |
| MSX | ✓ Pure .NET |

## Option C Decision Tree

**For N64Converter + RetroInject (Windows-only):**

1. **Option C-A: Create .NET wrapper**
   - Port core logic to managed code
   - Call via IToolRunner without external binary
   - Timeline: 1-2 sessions per tool
   - Complexity: Medium (binary format parsing)

2. **Option C-B: Find Linux-native equivalent**
   - N64Converter → ROM header modification (can be .NET)
   - RetroInject → ROM patching (can be .NET)
   - Timeline: Research + port
   - Complexity: Medium

3. **Option C-C: Service layer wrapper**
   - Create ToolServiceWrapper abstract layer
   - Windows: call native EXE
   - Linux: call .NET shim via IToolRunner
   - Timeline: 1 session
   - Complexity: Low (abstraction only)

**DECISION FOR PHASE 14:** Option C-C (Service wrapper) 
- Fastest path to cross-platform
- Keeps Option C requirement met (same behavior via service layer)
- N64Converter/RetroInject called via ToolServiceWrapper, not directly
- Phase 15 can optimize with proper .NET ports if needed

## Action Items (Phase 14)

- [ ] Verify win-only status: N64Converter, RetroInject (check legacy build system)
- [ ] Create `ToolServiceWrapper` abstraction
  - `IToolService` interface (replaces direct tool calls)
  - `WindowsToolService` / `CrossPlatformToolService` implementations
  - Injection into services via DI
- [ ] Audit pokepatch, MArchiveBatchTool, BuildPcePkg
- [ ] Update tools.toml with URLs + SHA256
- [ ] Tests: 2-3 new ToolServiceWrapper tests
- [ ] Validate all 8 services still work
- [ ] Build: 0 errors
- [ ] Commit: "Phase 14: Tool audit, ToolServiceWrapper, multi-OS strategy"

## Phase 14 Output

```
✓ All tools identified and categorized
✓ Cross-platform tools: wit, nfs2iso2nfs, wiiurpxtool (3 tools)
✓ Windows-only wrapped via ToolServiceWrapper: N64Converter, RetroInject (2 tools)
✓ TBD tools audited: pokepatch, MArchiveBatchTool, BuildPcePkg (3 tools)
✓ tools.toml complete with URLs/SHA256
✓ Services updated to use ToolServiceWrapper abstraction
✓ No Wine dependency, no skip, all same behavior on all OS
```

## Phase 15 Ready: Base ROM Download Service
