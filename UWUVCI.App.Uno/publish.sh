#!/usr/bin/env bash
# publish.sh – build portable releases for UWUVCI Rewrite
#
# Usage:
#   ./publish.sh              # Linux only (default on this host)
#   ./publish.sh linux win    # both targets
#
# Outputs:
#   artifacts/publish/linux-x64/  → single-file self-contained ELF
#   artifacts/publish/win-x64/    → single-file self-contained EXE  (cross-compile)
#   artifacts/dist/               → .tar.gz / .zip archives

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
APP_CSPROJ="$SCRIPT_DIR/UWUVCI.App.Uno/UWUVCI.App.Uno.csproj"
ARTIFACTS="$REPO_ROOT/artifacts"
DIST="$ARTIFACTS/dist"

TARGETS=("${@:-linux}")   # default: linux only

mkdir -p "$DIST"

publish_linux() {
    echo "=== Publishing linux-x64 ==="
    dotnet publish "$APP_CSPROJ" \
        -c Release \
        -f net10.0-desktop \
        -r linux-x64 \
        --self-contained true \
        -p:PublishReadyToRun=true \
        -o "$ARTIFACTS/publish/linux-x64"

    chmod +x "$ARTIFACTS/publish/linux-x64/UWUVCI.App.Uno" 2>/dev/null || true

    echo "--- Packaging linux-x64 → dist/uwuvci-linux-x64.tar.gz ---"
    # Exclude PDB files from the archive – keep them available locally for crash debugging
    tar -C "$ARTIFACTS/publish/linux-x64" \
        --exclude="*.pdb" \
        -czf "$DIST/uwuvci-linux-x64.tar.gz" \
        .
    echo "    $(du -sh "$DIST/uwuvci-linux-x64.tar.gz" | cut -f1)"
}

publish_win() {
    echo "=== Publishing win-x64 ==="
    dotnet publish "$APP_CSPROJ" \
        -c Release \
        -f net10.0-windows10.0.26100 \
        -r win-x64 \
        --self-contained true \
        -p:PublishReadyToRun=true \
        -o "$ARTIFACTS/publish/win-x64"

    echo "--- Packaging win-x64 → dist/uwuvci-win-x64.zip ---"
    (cd "$ARTIFACTS/publish/win-x64" && zip -qr --exclude="*.pdb" "$DIST/uwuvci-win-x64.zip" .)
    echo "    $(du -sh "$DIST/uwuvci-win-x64.zip" | cut -f1)"
}

for target in "${TARGETS[@]}"; do
    case "$target" in
        linux) publish_linux ;;
        win)   publish_win   ;;
        *)     echo "Unknown target: $target (use 'linux' or 'win')"; exit 1 ;;
    esac
done

echo ""
echo "=== Done. Artifacts in: $DIST ==="
ls -lh "$DIST"
