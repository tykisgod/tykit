#!/bin/bash
# Unity smart compile entry point
# Usage:
#   ./scripts/unity-compile-smart.sh
#   ./scripts/unity-compile-smart.sh --timeout 20
#   ./scripts/unity-compile-smart.sh --project /path/to/project
#   ./scripts/unity-compile-smart.sh --editor   # force Editor trigger path
#   ./scripts/unity-compile-smart.sh --batch    # force batch mode path
#
# Auto strategy:
# 1) If Unity Editor is detected with this project open -> use unity-check.sh --trigger
# 2) Otherwise -> use unity-compile.sh (batch mode)
# 3) On Editor trigger timeout, read current status once; if still unknown, try batch mode

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
TIMEOUT=15
FORCE_MODE="auto" # auto/editor/batch

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  --project <path>   Specify Unity project path (default: current repo)"
    echo "  --timeout <sec>    Editor trigger wait timeout (default: 15)"
    echo "  --editor           Force unity-check.sh --trigger"
    echo "  --batch            Force unity-compile.sh"
    echo "  --help, -h         Show this help"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --project)
            PROJECT_DIR="$2"
            shift 2
            ;;
        --timeout)
            TIMEOUT="$2"
            shift 2
            ;;
        --editor)
            FORCE_MODE="editor"
            shift
            ;;
        --batch)
            FORCE_MODE="batch"
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown argument: $1${NC}"
            usage
            exit 1
            ;;
    esac
done

if [ ! -f "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" ]; then
    echo -e "${RED}Error: $PROJECT_DIR is not a valid Unity project${NC}"
    exit 1
fi

CHECK_SCRIPT="$PROJECT_DIR/scripts/unity-check.sh"
COMPILE_SCRIPT="$PROJECT_DIR/scripts/unity-compile.sh"

if [ ! -x "$CHECK_SCRIPT" ]; then
    echo -e "${RED}Error: missing script $CHECK_SCRIPT${NC}"
    exit 1
fi

if [ ! -x "$COMPILE_SCRIPT" ]; then
    echo -e "${RED}Error: missing script $COMPILE_SCRIPT${NC}"
    exit 1
fi

is_editor_open_for_project() {
    local lock_file="$PROJECT_DIR/Temp/UnityLockfile"

    # 1) Most reliable: lock file held by a process
    if [ -f "$lock_file" ] && command -v lsof >/dev/null 2>&1; then
        if lsof "$lock_file" >/dev/null 2>&1; then
            return 0
        fi
    fi

    # 2) Process args contain projectPath
    if command -v pgrep >/dev/null 2>&1; then
        if pgrep -af "/Unity.app/Contents/MacOS/Unity" | grep -F -- "-projectPath $PROJECT_DIR" >/dev/null 2>&1; then
            return 0
        fi
    fi

    # 3) Weak signal: lock file + recent compile_status update + Unity process running
    local status_file="$PROJECT_DIR/Temp/compile_status.json"
    if [ -f "$lock_file" ] && [ -f "$status_file" ] && command -v pgrep >/dev/null 2>&1; then
        if pgrep -af "/Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then
            local now mtime age
            now="$(date +%s)"
            mtime="$(stat -f %m "$status_file" 2>/dev/null || echo 0)"
            age=$((now - mtime))
            if [ "$age" -le 300 ]; then
                return 0
            fi
        fi
    fi

    return 1
}

run_editor_mode() {
    echo -e "${CYAN}[smart] Using Editor mode: unity-check --trigger ${TIMEOUT}${NC}"
    if "$CHECK_SCRIPT" --trigger "$TIMEOUT"; then
        return 0
    fi

    local rc=$?
    if [ "$rc" -ne 2 ]; then
        return "$rc"
    fi

    echo -e "${YELLOW}[smart] Editor trigger timed out; checking current status...${NC}"
    if "$CHECK_SCRIPT"; then
        return 0
    fi

    rc=$?
    if [ "$rc" -eq 1 ]; then
        return 1
    fi

    echo -e "${YELLOW}[smart] Status still unknown; trying batch mode...${NC}"
    "$COMPILE_SCRIPT" "$PROJECT_DIR"
}

run_batch_mode() {
    echo -e "${CYAN}[smart] Using batch mode: unity-compile${NC}"
    "$COMPILE_SCRIPT" "$PROJECT_DIR"
}

case "$FORCE_MODE" in
    editor)
        run_editor_mode
        ;;
    batch)
        run_batch_mode
        ;;
    auto)
        if is_editor_open_for_project; then
            echo -e "${CYAN}[smart] Unity Editor detected with this project open${NC}"
            run_editor_mode
        else
            echo -e "${CYAN}[smart] Unity Editor not detected for this project${NC}"
            run_batch_mode
        fi
        ;;
esac
