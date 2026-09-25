#!/usr/bin/env bash
# Assemble a Thunderstore-ready zip in dist/.
#
# Thunderstore requires, at the ZIP ROOT (not nested in a folder):
#   manifest.json   name matching ^[a-zA-Z0-9_]+$, semver version_number,
#                   description <= 250 chars, dependencies as "Namespace-Name-Version"
#   README.md       rendered as the package page
#   icon.png        exactly 256x256
#   CHANGELOG.md    optional, rendered as the changelog tab
# A version can never be re-uploaded, so version_number must be bumped each release.
set -euo pipefail

# The SDK on the build box has no ICU; without this dotnet aborts before parsing arguments.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$ROOT/dist/stage"
PROJ="$ROOT/src/DamageSplash/DamageSplash.csproj"
DLL="$ROOT/src/DamageSplash/bin/Release/net472/DamageSplash.dll"

echo "==> building"
dotnet build "$PROJ" -c Release --nologo -v minimal

VERSION=$(python3 -c "import json;print(json.load(open('$ROOT/thunderstore/manifest.json'))['version_number'])")
ASM_VERSION=$(grep -oP '(?<=PluginVersion = ")[^"]+' "$ROOT/src/DamageSplash/Plugin.cs")

CSPROJ_VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$PROJ")

if [ "$VERSION" != "$ASM_VERSION" ] || [ "$VERSION" != "$CSPROJ_VERSION" ]; then
    echo "version mismatch: manifest.json says $VERSION, Plugin.cs says $ASM_VERSION, csproj says $CSPROJ_VERSION" >&2
    exit 1
fi

echo "==> checking the DLL carries no dev-only code"
# The command file (Plugin.RunCommandFile) is compiled into Debug builds only. Thunderstore's
# moderators hold a client mod that runs commands from a file for manual review, so a Release
# DLL that still mentions it must never ship.
python3 - "$DLL" <<'PY2'
import sys
data = open(sys.argv[1], "rb").read()
for word in ("DamageSplash.cmd", "DevCommandFile"):
    if word.encode("utf-16-le") in data or word.encode() in data:
        print(f"  {sys.argv[1]} contains {word!r}: dev-only code in a release build", file=sys.stderr)
        sys.exit(1)
print("  ok: no command file")
PY2

echo "==> validating manifest"
python3 - "$ROOT" <<'PY'
import json, re, sys, os
root = sys.argv[1]
m = json.load(open(os.path.join(root, "thunderstore/manifest.json")))
problems = []
if not re.fullmatch(r"[a-zA-Z0-9_]+", m.get("name", "")):
    problems.append("name must match ^[a-zA-Z0-9_]+$")
if not re.fullmatch(r"\d+\.\d+\.\d+", m.get("version_number", "")):
    problems.append("version_number must be x.y.z")
if len(m.get("description", "")) > 250:
    problems.append("description exceeds 250 characters")
for dep in m.get("dependencies", []):
    if not re.fullmatch(r"[^-]+-[^-]+-\d+\.\d+\.\d+", dep):
        problems.append(f"dependency not Namespace-Name-Version: {dep}")
icon = os.path.join(root, "thunderstore/icon.png")
with open(icon, "rb") as f:
    head = f.read(24)
assert head[:8] == b"\x89PNG\r\n\x1a\n", "icon.png is not a PNG"
import struct
w, h = struct.unpack(">II", head[16:24])
if (w, h) != (256, 256):
    problems.append(f"icon.png must be exactly 256x256, got {w}x{h}")
if problems:
    print("\n".join("  - " + p for p in problems)); sys.exit(1)
print(f"  ok: {m['name']} {m['version_number']}, {len(m['dependencies'])} dependencies")
PY

echo "==> staging"
# Flat, with the DLL at the zip root: this mirrors packages Thunderstore has accepted
# (e.g. ComfyMods-ColorfulDamage), and mod managers place root files into
# BepInEx/plugins/<package>/ anyway.
rm -rf "$STAGE"
mkdir -p "$STAGE"
cp "$DLL"                        "$STAGE/"
cp "$ROOT/thunderstore/manifest.json" "$STAGE/"
cp "$ROOT/thunderstore/README.md"     "$STAGE/"
cp "$ROOT/thunderstore/icon.png"      "$STAGE/"
cp "$ROOT/CHANGELOG.md"               "$STAGE/"
cp "$ROOT/LICENSE"                    "$STAGE/"

OUT="$ROOT/dist/DamageSplash-$VERSION.zip"
rm -f "$OUT"
# -D omits directory entries; -X drops extra file attributes. Keeps the archive to
# exactly the files Thunderstore expects to see and nothing else.
( cd "$STAGE" && zip -qrXD "$OUT" . )

echo "==> $OUT"
unzip -l "$OUT"
