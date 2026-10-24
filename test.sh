#!/usr/bin/env bash
# ./test.sh          tests the shell and every module, builds the game and loads it headless.
# ./test.sh <Name>   tests module <Name> and the modules whose projects reference its Contract,
#                    then the same build and load.
set -uo pipefail
cd "$(dirname "$0")"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

failed=0
fail() { echo "test.sh: FAILED: $*" >&2; failed=1; }

all_modules=()
for dir in modules/*/; do
  [ -d "$dir" ] && all_modules+=("$(basename "$dir")")
done

projects=()
if [ $# -eq 0 ]; then
  tested=("${all_modules[@]}")
  for p in tests/*/*.csproj; do [ -f "$p" ] && projects+=("$p"); done
elif [ $# -eq 1 ]; then
  name="$1"
  if [[ ! "$name" =~ ^[A-Za-z][A-Za-z0-9]*$ ]] || [ ! -d "modules/$name" ]; then
    echo "test.sh: no module named '$name' (modules/$name/ does not exist)" >&2
    exit 2
  fi
  tested=("$name")
  # Modules whose projects reference modules/<Name>/Contract.
  for other in "${all_modules[@]}"; do
    [ "$other" = "$name" ] && continue
    if grep -qsE "Include=\"[^\"]*[\\/]$name[\\/]Contract[\\/]" modules/"$other"/*/*.csproj; then
      tested+=("$other")
    fi
  done
else
  echo "usage: ./test.sh [ModuleName]" >&2
  exit 2
fi
echo "test.sh: modules under test: ${tested[*]:-(none)}"

for m in "${tested[@]}"; do
  found=0
  for p in modules/"$m"/Tests/*.csproj; do
    [ -f "$p" ] && projects+=("$p") && found=1
  done
  [ $found -eq 1 ] || fail "module $m has no Tests project (modules/$m/Tests/*.csproj)"
done

for p in "${projects[@]}"; do
  echo "test.sh: dotnet test $p"
  dotnet test "$p" || fail "tests in $p"
done

# Build the Godot project; it must target net10.0.
framework="$(dotnet msbuild Fungtopia.csproj -getProperty:TargetFramework 2>/dev/null | tr -d '[:space:]')"
[ "$framework" = "net10.0" ] || fail "Fungtopia.csproj targets '$framework', not net10.0"
# The test-only runtime settings of Directory.Build.props must not reach the game.
tiering="$(dotnet msbuild Fungtopia.csproj -getProperty:TieredCompilation -getProperty:TieredPGO 2>/dev/null | tr -d '[:space:]')"
[ "$tiering" = '{"Properties":{"TieredCompilation":"","TieredPGO":""}}' ] || fail "Fungtopia.csproj sets tiering: $tiering"
echo "test.sh: dotnet build Fungtopia.csproj"
if ! dotnet build Fungtopia.csproj; then
  fail "building Fungtopia.csproj"
  exit 1
fi

# Load the main scene headless; any ERROR in Godot's output fails.
echo "test.sh: loading the main scene headless"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT
timeout 300 godot --headless --path . --fixed-fps 60 --quit-after 10 >"$log" 2>&1
status=$?
cat "$log"
[ $status -eq 0 ] || fail "godot exited with status $status"
grep -q "ERROR" "$log" && fail "ERROR in Godot's output"
grep -q "Fungtopia: bootstrap ready" "$log" || fail "the Bootstrap scene did not start"
for m in "${tested[@]}"; do
  grep -qF "module '$m' disabled" "$log" && fail "module $m was disabled at runtime"
  grep -qF "module '$m' started" "$log" || fail "module $m did not start"
done

if [ $failed -ne 0 ]; then
  echo "test.sh: FAILED" >&2
  exit 1
fi
echo "test.sh: all passed"
