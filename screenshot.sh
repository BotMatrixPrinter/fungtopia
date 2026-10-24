#!/usr/bin/env bash
# ./screenshot.sh [scene] [out.png]
# Builds the game, renders a scene (default: the main scene) under a virtual X display with
# OpenGL, and saves a 1920x1080 PNG to $SCREENSHOT_DIR (or ./screenshots/ when it is unset).
# Screenshots are never committed.
set -euo pipefail
cd "$(dirname "$0")"
scene="${1:-}"
out="$(basename "${2:-screenshot.png}")"
[[ "$out" == *.png ]] || out="$out.png"
frames="${SCREENSHOT_FRAMES:-90}"
dir="${SCREENSHOT_DIR:-$PWD/screenshots}"
mkdir -p "$dir"

if [ -n "$scene" ]; then
  [[ "$scene" == res://* ]] || scene="res://${scene#./}"
fi

dotnet build Fungtopia.csproj -nologo -v quiet

# Godot's movie writer saves one PNG per frame; keep the last one.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
xvfb-run -a -s "-screen 0 1920x1080x24" \
  godot --rendering-driver opengl3 --path . --resolution 1920x1080 --fixed-fps 60 \
  --write-movie "$work/frame.png" --quit-after "$frames" ${scene:+"$scene"}
last="$(ls "$work"/frame*.png | sort | tail -n 1)"
mv "$last" "$dir/$out"
echo "screenshot: $dir/$out"
