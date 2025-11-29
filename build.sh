#!/usr/bin/env bash

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

CAKE_EXE_PATH="$SCRIPT_DIR/tools/Cake.Tool/.store/cake.tool/*/tools/net6.0/any/cake.exe"
CAKE_EXE=`ls -1 $CAKE_EXE_PATH | head -n 1`

if [ -z "$CAKE_EXE" ]; then
    echo "Could not find Cake executable"
    exit 1
fi

CAKE_ARGS=()

if [ -n "$SCRIPT" ]; then
    CAKE_ARGS+=("$SCRIPT")
fi

CAKE_ARGS+=(
    "--target=$TARGET"
    "--configuration=$CONFIGURATION" 
    "--verbosity=$VERBOSITY"
)

if [ "$DRYRUN" = "1" ] || [ "$DRYRUN" = "true" ]; then
    CAKE_ARGS+=("--dry-run")
fi

if [ "$MONO" = "1" ] || [ "$MONO" = "true" ]; then
    CAKE_ARGS+=("--mono")
fi

if [ "$SKIP_TOOLS" = "1" ] || [ "$SKIP_TOOLS" = "true" ]; then
    CAKE_ARGS+=("--skip-package-restore")
fi

echo "Executing: dotnet $CAKE_EXE ${CAKE_ARGS[@]}"
dotnet "$CAKE_EXE" "${CAKE_ARGS[@]}"