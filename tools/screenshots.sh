#!/usr/bin/env bash
# Regenerates the README's screenshots in screenshots/. They are rendered headlessly from the real
# window (tests/DungeonSiege.App.Tests/ReadmeScreenshots.cs) with a fixed seed and frame-by-frame
# play, so no desktop is captured and the pictures only change when the game's look does.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$root/screenshots"
AVADS_README_SHOTS="$root/screenshots" dotnet test "$root/tests/DungeonSiege.App.Tests" \
  --filter "FullyQualifiedName~ReadmeScreenshots" --nologo -v quiet
ls -1 "$root/screenshots"
