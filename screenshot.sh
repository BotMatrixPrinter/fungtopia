#!/usr/bin/env bash
# ./screenshot.sh [scene] [out.png]
# Builds the game, renders a scene (default: the main scene) under a virtual X display with
# Vulkan (the Forward+ renderer, as players see it; software lavapipe works without a GPU), and
# saves a 1920x1080 PNG to $SCREENSHOT_DIR (or ./screenshots/ when it is unset). Only when
# Vulkan fails to start does it render again with OpenGL (the Compatibility renderer), and says so.
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

# render <driver>: renders into $work/<driver>/, its output also in $work/<driver>.log.
render() {
  mkdir -p "$work/$1"
  xvfb-run -a -s "-screen 0 1920x1080x24" \
    godot --rendering-driver "$1" --path . --resolution 1920x1080 --fixed-fps 60 \
    --write-movie "$work/$1/frame.png" --quit-after "$frames" ${scene:+"$scene"} \
    2>&1 | tee "$work/$1.log"
}

# Vulkan started when Godot exited cleanly, wrote frames and reported the Forward+ renderer
# (Godot itself may switch to OpenGL when Vulkan is missing; that does not count).
driver=vulkan
if ! render vulkan || ! grep -q "^Vulkan .* Forward+" "$work/vulkan.log" \
    || ! ls "$work"/vulkan/frame*.png >/dev/null 2>&1; then
  echo "screenshot.sh: Vulkan (Forward+) failed to start; falling back to OpenGL (Compatibility)," \
    "so the screenshot lacks Forward+ features such as volumetric fog" >&2
  driver=opengl3
  render opengl3
fi
last="$(ls "$work/$driver"/frame*.png | sort | tail -n 1)"
mv "$last" "$dir/$out"
echo "screenshot: $dir/$out (rendered with $driver)"
