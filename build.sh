#!/usr/bin/env bash
# Builds the trainer and the patcher, then stages everything into dist/.
# Works on Linux and macOS. Windows users: use build.ps1 (or this, under Git Bash / WSL).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# dotnet may be on PATH, or in the usual private install locations.
if command -v dotnet >/dev/null 2>&1; then
  DOTNET="$(command -v dotnet)"
elif [ -x "${DOTNET_ROOT:-$HOME/.dotnet}/dotnet" ]; then
  DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
  export DOTNET_ROOT="$(dirname "$DOTNET")"
else
  echo "dotnet not found. Install the .NET 8 SDK: https://dotnet.microsoft.com/download" >&2
  exit 1
fi

# Be a good citizen on Linux; these do not exist elsewhere.
NICE=""
command -v ionice >/dev/null 2>&1 && NICE="ionice -c3"
command -v nice   >/dev/null 2>&1 && NICE="$NICE nice -n 19"

build() { $NICE "$DOTNET" build "$1" -c Release -v quiet --nologo; }

echo "==> building patcher"
build "$ROOT/src/HtfPatcher"

PATCHER="$ROOT/src/HtfPatcher/bin/Release/HtfPatcher.dll"

# The patcher knows how to find the game on every platform, so it vendors the reference
# assemblies rather than each build script reimplementing Steam library discovery.
echo "==> vendoring game references into lib/"
"$DOTNET" "$PATCHER" refs --out "$ROOT/lib" ${GAME_DIR:+--game "$GAME_DIR"}

echo "==> building trainer"
build "$ROOT/src/HtfTrainer"

DIST="$ROOT/dist"
rm -rf "$DIST"
mkdir -p "$DIST"

cp "$ROOT/src/HtfTrainer/bin/Release/HtfTrainer.dll" "$DIST/"
cp "$ROOT/src/HtfPatcher/bin/Release/HtfPatcher.dll" \
   "$ROOT/src/HtfPatcher/bin/Release/HtfPatcher.runtimeconfig.json" \
   "$ROOT/src/HtfPatcher/bin/Release/dnlib.dll" "$DIST/"
[ -f "$ROOT/src/HtfPatcher/bin/Release/HtfPatcher.deps.json" ] && \
  cp "$ROOT/src/HtfPatcher/bin/Release/HtfPatcher.deps.json" "$DIST/"

cat > "$DIST/htf" <<'WRAPPER'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v dotnet >/dev/null 2>&1; then DOTNET="$(command -v dotnet)"
else DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"; fi
exec "$DOTNET" "$HERE/HtfPatcher.dll" "$@"
WRAPPER
chmod +x "$DIST/htf"

cat > "$DIST/htf.cmd" <<'WRAPPER'
@echo off
setlocal
where dotnet >nul 2>nul || (
  echo dotnet not found. Install the .NET 8 runtime: https://dotnet.microsoft.com/download
  exit /b 1
)
dotnet "%~dp0HtfPatcher.dll" %*
WRAPPER

echo
echo "==> staged in $DIST"
echo "    ./dist/htf status     (Windows: dist\\htf.cmd status)"
echo "    ./dist/htf patch"
echo "    ./dist/htf unpatch"
