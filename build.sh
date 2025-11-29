#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
CAKE_SCRIPT="$SCRIPT_DIR/build.cake"

TARGET="${TARGET:-Default}"
CONFIGURATION="${CONFIGURATION:-Release}"
VERBOSITY="${VERBOSITY:-Normal}"
EXTRA_ARGS=()

for ARG in "$@"; do
  case $ARG in
    --target=*)
      TARGET="${ARG#*=}"
      ;;
    --configuration=*)
      CONFIGURATION="${ARG#*=}"
      ;;
    --verbosity=*)
      VERBOSITY="${ARG#*=}"
      ;;
    *)
      EXTRA_ARGS+=("$ARG")
      ;;
  esac
done

dotnet tool restore

CAKE_CMD=(dotnet cake "$CAKE_SCRIPT" "--target=$TARGET" "--configuration=$CONFIGURATION" "--verbosity=$VERBOSITY")

if [ ${#EXTRA_ARGS[@]} -gt 0 ]; then
  CAKE_CMD+=("${EXTRA_ARGS[@]}")
fi

echo "Executing: ${CAKE_CMD[*]}"
"${CAKE_CMD[@]}"