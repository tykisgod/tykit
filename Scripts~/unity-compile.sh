#!/bin/bash
# Unity offline compile check script
# Usage:
#   ./scripts/unity-compile.sh                    # compile current project
#   ./scripts/unity-compile.sh /path/to/project   # compile specified project
#
# Compiles the project in batch mode without launching the Unity Editor UI and reports results.
# Note: if Unity Editor already has this project open, batch mode cannot access it simultaneously.
# In that case, close Unity first or use a different project path (e.g. a worktree).

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# Unity path (searched in priority order)
find_unity() {
    # 1. Environment variable
    if [ -n "${UNITY_PATH:-}" ] && [ -f "$UNITY_PATH" ]; then
        echo "$UNITY_PATH"
        return
    fi

    # 2. Direct install
    local direct="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
    if [ -f "$direct" ]; then
        echo "$direct"
        return
    fi

    # 3. Unity Hub (search by version)
    local hub_base="/Applications/Unity/Hub/Editor"
    if [ -d "$hub_base" ]; then
        # Prefer matching project version
        local project_version=""
        local version_file="$PROJECT_DIR/ProjectSettings/ProjectVersion.txt"
        if [ -f "$version_file" ]; then
            project_version=$(grep "m_EditorVersion:" "$version_file" | sed 's/.*: //')
        fi

        if [ -n "$project_version" ] && [ -f "$hub_base/$project_version/Unity.app/Contents/MacOS/Unity" ]; then
            echo "$hub_base/$project_version/Unity.app/Contents/MacOS/Unity"
            return
        fi

        # Fallback: use latest version
        local latest=$(ls -1 "$hub_base" 2>/dev/null | sort -V | tail -1)
        if [ -n "$latest" ] && [ -f "$hub_base/$latest/Unity.app/Contents/MacOS/Unity" ]; then
            echo "$hub_base/$latest/Unity.app/Contents/MacOS/Unity"
            return
        fi
    fi

    echo ""
}

# Project path
PROJECT_DIR="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
LOG_FILE="/tmp/unity-compile-$(date +%s).log"

# Validate project path
if [ ! -f "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" ]; then
    echo -e "${RED}Error: $PROJECT_DIR is not a valid Unity project${NC}"
    exit 1
fi

# Find Unity
UNITY_BIN=$(find_unity)
if [ -z "$UNITY_BIN" ]; then
    echo -e "${RED}Error: Unity installation not found${NC}"
    echo "Set the UNITY_PATH environment variable or ensure Unity is installed at the standard path"
    exit 1
fi

echo -e "${CYAN}Unity:${NC}   $UNITY_BIN"
echo -e "${CYAN}Project:${NC} $PROJECT_DIR"
echo -e "${CYAN}Log:${NC}     $LOG_FILE"
echo ""

# Check whether Unity has this project locked
LOCK_FILE="$PROJECT_DIR/Temp/UnityLockfile"
if [ -f "$LOCK_FILE" ]; then
    echo -e "${YELLOW}⚠️ Unity lock file detected; a Unity instance may be using this project${NC}"
    echo -e "${YELLOW}   If compile fails, close the Unity instance that has this project open${NC}"
    echo ""
fi

# Run compile
echo -e "${CYAN}Starting compile...${NC}"
START_TIME=$(date +%s)

# -batchmode: no UI
# -nographics: skip graphics device initialization
# -projectPath: project path
# -logFile: log file
# -quit: exit when done
# -buildTarget: keep current platform (no platform switch)
"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_DIR" \
    -logFile "$LOG_FILE" \
    -quit \
    2>&1 || true

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""

# Analyze log
if [ ! -f "$LOG_FILE" ]; then
    echo -e "${RED}❌ Compile log not generated${NC}"
    exit 1
fi

# Check whether batch mode was aborted (e.g. due to lock file)
ABORT_MSG=$(grep "Aborting batchmode" "$LOG_FILE" 2>/dev/null || true)
LOCKED_MSG=$(grep -E "another Unity instance is running with this project open|Multiple Unity instances cannot open the same project" "$LOG_FILE" 2>/dev/null || true)
if [ -n "$ABORT_MSG" ]; then
    # Even if batch mode aborted, compile checks may have completed.
    # Check for successful compilation signals separately.
    :
fi

# Check for compile errors (multiple patterns)
ERRORS=$(grep -E "error CS[0-9]+" "$LOG_FILE" 2>/dev/null | sort -u || true)
WARNING_COUNT=$(grep -cE "warning CS[0-9]+" "$LOG_FILE" 2>/dev/null || true)
if ! [[ "$WARNING_COUNT" =~ ^[0-9]+$ ]]; then
    WARNING_COUNT=0
fi

if [ -n "$ERRORS" ]; then
    ERROR_COUNT=$(echo "$ERRORS" | wc -l | tr -d ' ')
    echo -e "${RED}❌ Compile failed${NC} (${DURATION}s, ${ERROR_COUNT} errors, ${WARNING_COUNT} warnings)"
    echo ""
    echo -e "${RED}Errors:${NC}"
    echo "$ERRORS" | head -30 | while IFS= read -r line; do
        echo -e "  ${RED}$line${NC}"
    done
    if [ "$ERROR_COUNT" -gt 30 ]; then
        echo -e "  ... and $((ERROR_COUNT - 30)) more errors"
    fi
    echo ""
    echo -e "Full log: $LOG_FILE"
    exit 1
elif [ -n "$LOCKED_MSG" ]; then
    echo -e "${YELLOW}⚠️ Unity project is locked; batch mode cannot access it${NC} (${DURATION}s)"
    echo -e "${YELLOW}Reason:${NC} project is already open in another Unity instance"
    echo -e "Full log: $LOG_FILE"
    exit 2
elif [ -n "$ABORT_MSG" ] && ! grep -q "Refresh completed" "$LOG_FILE" 2>/dev/null; then
    echo -e "${YELLOW}⚠️ Unity batch mode aborted${NC} (${DURATION}s)"
    echo -e "${YELLOW}Reason:${NC} $ABORT_MSG"
    echo -e "Full log: $LOG_FILE"
    exit 2
else
    echo -e "${GREEN}✅ Compile succeeded${NC} (${DURATION}s, ${WARNING_COUNT} warnings)"
    rm -f "$LOG_FILE"
    exit 0
fi
