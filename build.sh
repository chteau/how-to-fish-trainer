#!/usr/bin/env bash
# Builds the trainer and the patcher, then stages everything into dist/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet not found. Install the .NET 8 SDK or set DOTNET_ROOT." >&2
  exit 1
fi

# Vendor reference assemblies into lib/. Compiling straight from Managed/ would bind against a
# patched Assembly-CSharp, which Roslyn refuses to load as a reference.
MANAGED="${GAME_MANAGED:-$HOME/.local/share/Steam/steamapps/common/How to Fish/How to Fish/How to Fish_Data/Managed}"
LIB="$ROOT/lib"
if [ -d "$MANAGED" ]; then
  mkdir -p "$LIB"
  if [ -f "$MANAGED/Assembly-CSharp.dll.htfbak" ]; then
    cp "$MANAGED/Assembly-CSharp.dll.htfbak" "$LIB/Assembly-CSharp.dll"
  else
    cp "$MANAGED/Assembly-CSharp.dll" "$LIB/Assembly-CSharp.dll"
  fi
  for f in FishNet.Runtime GameKit.Dependencies UnityEngine UnityEngine.CoreModule \
           UnityEngine.PhysicsModule UnityEngine.IMGUIModule UnityEngine.InputLegacyModule \
           UnityEngine.TextRenderingModule UnityEngine.AnimationModule Unity.InputSystem netstandard; do
    cp "$MANAGED/$f.dll" "$LIB/$f.dll"
  done
  echo "==> reference assemblies refreshed in lib/"
fi

echo "==> building trainer"
ionice -c3 nice -n 19 dotnet build "$ROOT/src/HtfTrainer" -c Release -v quiet --nologo

echo "==> building patcher"
ionice -c3 nice -n 19 dotnet build "$ROOT/src/HtfPatcher" -c Release -v quiet --nologo

DIST="$ROOT/dist"
rm -rf "$DIST"
mkdir -p "$DIST"

TRAINER_BIN="$ROOT/src/HtfTrainer/bin/Release"
PATCHER_BIN="$ROOT/src/HtfPatcher/bin/Release"

# Payload is just the trainer: its two behavioural hooks are woven into the game assembly at patch
# time, so there is no runtime patching library and nothing else to resolve.
cp "$TRAINER_BIN/HtfTrainer.dll" "$DIST/HtfTrainer.dll"

cp "$PATCHER_BIN"/HtfPatcher.dll "$PATCHER_BIN"/HtfPatcher.runtimeconfig.json "$DIST/"
cp "$PATCHER_BIN"/dnlib.dll "$DIST/"
[ -f "$PATCHER_BIN/HtfPatcher.deps.json" ] && cp "$PATCHER_BIN/HtfPatcher.deps.json" "$DIST/"

cat > "$DIST/htf" <<'WRAPPER'
#!/usr/bin/env bash
# Thin wrapper so the patcher can be run without a dotnet on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
exec "$DOTNET_ROOT/dotnet" "$HERE/HtfPatcher.dll" "$@"
WRAPPER
chmod +x "$DIST/htf"

echo
echo "==> staged in $DIST"
echo "    ./dist/htf status"
echo "    ./dist/htf patch"
echo "    ./dist/htf unpatch"
