#!/bin/sh
# unity-test.sh — headless Unity PlayMode test runner for agent self-verification
#
# Usage:
#   sh tools/unity-test.sh                  # run all PlayMode tests
#   sh tools/unity-test.sh SkillTree         # -testFilter SkillTree (substring match on test/class name)
#
# Exit 0 = all tests passed (AGENT_TEST_OK + zero failures in results XML)
# Exit 1 = any test failed, or the run never produced a results file
#
# Screenshots dropped by HarnessFixture.Shot() land in screenshots/test-*.png — read
# them back with the Read tool to verify visuals, not just assertions.
# Never Temp/: Unity deletes that directory when the editor quits, and ./temp is the
# same directory on a case-insensitive filesystem.

UNITY="/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity"
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
LOG="$PROJ/Logs/agent-test.log"
RESULTS="$PROJ/Logs/test-results.xml"
SHOTS="$PROJ/screenshots"
FILTER="${1:-}"

# Detect editor lockfile — but only treat it as "editor open" if a process
# actually holds it (a prior aborted run can leave a stale lockfile).
LOCKFILE="$PROJ/Temp/UnityLockfile"
if [ -f "$LOCKFILE" ] && lsof "$LOCKFILE" >/dev/null 2>&1; then
    echo "ERROR: Unity editor is open (lockfile held: $LOCKFILE)"
    echo "Close the editor first, or use the Agent Bridge for interactive iteration."
    exit 1
fi

mkdir -p "$(dirname "$LOG")"
mkdir -p "$SHOTS"
: > "$LOG"
rm -f "$RESULTS"

set -- -batchmode -runTests -testPlatform PlayMode -projectPath "$PROJ" -testResults "$RESULTS" -logFile "$LOG"
if [ -n "$FILTER" ]; then
    set -- "$@" -testFilter "$FILTER"
fi

# No -nographics: screenshot capture needs a real graphics context.
# No -quit: the test runner exits the process itself when done.
echo "Running PlayMode tests (filter='$FILTER') ..."
"$UNITY" "$@"
CODE=$?

echo ""
echo "---- exit $CODE ----"
echo ""

if [ ! -f "$RESULTS" ]; then
    echo "ERROR: no results file at $RESULTS — the test run never completed."
    tail -n 40 "$LOG" 2>/dev/null
    if grep -q "Licensing::Module. Error" "$LOG" 2>/dev/null; then
        echo "Cause: Unity licensing client failed. Open the editor once to refresh the license, then retry."
    fi
    exit 1
fi

# NUnit3 root <test-run> carries summary attributes directly — no XML lib needed.
SUMMARY="$(grep -o '<test-run[^>]*' "$RESULTS" | head -1)"
TOTAL="$(echo "$SUMMARY" | sed -n 's/.*total="\([0-9]*\)".*/\1/p')"
PASSED="$(echo "$SUMMARY" | sed -n 's/.*passed="\([0-9]*\)".*/\1/p')"
FAILED="$(echo "$SUMMARY" | sed -n 's/.*failed="\([0-9]*\)".*/\1/p')"

echo "Results: total=$TOTAL passed=$PASSED failed=$FAILED"
echo "XML: $RESULTS"
echo "Screenshots: $SHOTS"
grep -oE 'CAPTURE\|[^"[:space:]]+\.png' "$LOG" | sort -u

if [ "$FAILED" != "0" ] || [ -z "$TOTAL" ]; then
    echo ""
    echo "AGENT_TEST_FAIL|$FAILED of $TOTAL tests failed"
    xmllint --xpath '//test-case[@result="Failed"]/@fullname | //test-case[@result="Failed"]/failure/message' "$RESULTS" 2>/dev/null
    exit 1
fi

echo "AGENT_TEST_OK|all $TOTAL tests passed"
exit 0
