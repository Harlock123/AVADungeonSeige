#!/usr/bin/env bash
# Publishes AVADungeonSeige as a self-contained single-file executable for every release
# platform: Windows, macOS and Linux, each on x64 and ARM64. Output: BUILDS/<PLATFORM>/.
set -euo pipefail

SOLUTION_ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$SOLUTION_ROOT/src/DungeonSiege.App/DungeonSiege.App.csproj"
BUILDS_DIR="$SOLUTION_ROOT/BUILDS"

# Each entry: "RID|FolderName"
TARGETS=(
    "win-x64|WINDOWSx64"
    "win-arm64|WINDOWSarm64"
    "osx-x64|MACOSx64"
    "osx-arm64|MACOSarm64"
    "linux-x64|LINUXx64"
    "linux-arm64|LINUXarm64"
)

echo "========================================"
echo "  AVADungeonSeige - Publish Build Script"
echo "========================================"

if [ -d "$BUILDS_DIR" ]; then
    echo "Cleaning previous builds..."
    rm -rf "$BUILDS_DIR"
fi
mkdir -p "$BUILDS_DIR"

FAILED=()

for ENTRY in "${TARGETS[@]}"; do
    RID="${ENTRY%%|*}"
    FOLDER="${ENTRY##*|}"
    OUTPUT="$BUILDS_DIR/$FOLDER"

    echo ""
    echo "--- Building $FOLDER ($RID) ---"

    # Single-file, self-contained and compression settings live in the .csproj and switch on
    # whenever a runtime identifier is given.
    if dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$RID" \
        --output "$OUTPUT" \
        --nologo 2>&1 | tail -3; then
        [ -f "$OUTPUT/AVADungeonSeige" ] && chmod +x "$OUTPUT/AVADungeonSeige"
        echo "  -> $FOLDER OK"
    else
        echo "  -> $FOLDER FAILED"
        FAILED+=("$FOLDER")
    fi
done

echo ""
echo "========================================"
echo "  Build Summary"
echo "========================================"

for ENTRY in "${TARGETS[@]}"; do
    FOLDER="${ENTRY##*|}"
    OUTPUT="$BUILDS_DIR/$FOLDER"
    if [ -d "$OUTPUT" ] && [ "$(ls -A "$OUTPUT" 2>/dev/null)" ]; then
        echo "  $FOLDER  ->  $(du -sh "$OUTPUT" | cut -f1)  ($(ls "$OUTPUT" | tr '\n' ' '))"
    else
        echo "  $FOLDER  ->  MISSING"
    fi
done

if [ ${#FAILED[@]} -gt 0 ]; then
    echo ""
    echo "FAILED: ${FAILED[*]}"
    exit 1
fi
echo ""
echo "All builds completed successfully!"
echo "Output: $BUILDS_DIR/"
