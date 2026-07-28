#!/bin/sh
# unity-batch.sh — headless Unity launcher for agent automation
#
# Usage:
#   sh tools/unity-batch.sh Compile
#   sh tools/unity-batch.sh RunGenerator StartScreen
#   sh tools/unity-batch.sh RunGeneratorByMenuPath "LionQuest/Generate/Start Screen"
#   sh tools/unity-batch.sh RunGenerator CaptureSelection graphics   # needs a real GPU context
#
# Exit 0 = success (AGENT_BATCH_OK in log)
# Exit 1 = failure (AGENT_BATCH_FAIL or compile errors in log)

UNITY="/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity"
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
LOG="$PROJ/Logs/agent-batch.log"
METHOD="${1:?usage: unity-batch.sh <Method> [AGENT_GEN] [graphics]}"
export AGENT_GEN="${2:-}"

# -nographics can't render to a Camera/RenderTexture. Screenshot-capture entry points need a
# real graphics context — pass "graphics" as the 3rd arg to drop -nographics for this run.
GFX_FLAG="-nographics"
if [ "${3:-}" = "graphics" ]; then
    GFX_FLAG=""
fi

# Detect editor lockfile — but only treat it as "editor open" if a process
# actually holds it (a prior aborted batch run can leave a stale lockfile).
LOCKFILE="$PROJ/Temp/UnityLockfile"
if [ -f "$LOCKFILE" ] && lsof "$LOCKFILE" >/dev/null 2>&1; then
    echo "ERROR: Unity editor is open (lockfile held: $LOCKFILE)"
    echo "Close the editor first, or use the MCP bridge for interactive iteration."
    exit 1
fi

# Ensure Logs/ exists (gitignored but may not exist on fresh clone)
mkdir -p "$(dirname "$LOG")"
: > "$LOG"

echo "Running AgentBatch.$METHOD (AGENT_GEN='$AGENT_GEN') ..."

"$UNITY" \
    -batchmode \
    $GFX_FLAG \
    -quit \
    -projectPath "$PROJ" \
    -executeMethod "AgentBatch.$METHOD" \
    -logFile "$LOG"
CODE=$?

echo ""
echo "---- exit $CODE ----"
echo ""

# Surface the decisive lines so the agent sees pass/fail without reading the full log
grep -E "AGENT_BATCH_(OK|FAIL)|\): error CS[0-9]+|error CS[0-9]+:|Compilation failed|CompilerMessage" "$LOG" 2>/dev/null || true

# Unity can exit 0 without ever reaching the -executeMethod (e.g. licensing failure leaves
# module assemblies unloaded, so every script fails to compile). Require the sentinel.
if ! grep -q "AGENT_BATCH_OK" "$LOG" 2>/dev/null; then
    echo ""
    echo "ERROR: no AGENT_BATCH_OK sentinel in $LOG — the method never ran."
    if grep -q "Licensing::Module. Error" "$LOG" 2>/dev/null; then
        echo "Cause: Unity licensing client failed. Open the editor once to refresh the license, then retry."
    fi
    exit 1
fi

exit $CODE
