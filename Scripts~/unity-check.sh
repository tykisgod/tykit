#!/bin/bash
# Unity compile status check script (automation-friendly)
# Usage:
#   ./scripts/unity-check.sh              # check current status
#   ./scripts/unity-check.sh --wait       # wait for next compile to finish
#   ./scripts/unity-check.sh --trigger    # trigger compile and wait for result

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
STATUS_FILE="$PROJECT_DIR/Temp/compile_status.json"
TRIGGER_FILE="$PROJECT_DIR/Temp/refresh_trigger"
BATCH_COMPILE_SCRIPT="$PROJECT_DIR/scripts/unity-compile.sh"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# Read status file
read_status() {
    if [ ! -f "$STATUS_FILE" ]; then
        echo ""
        return
    fi
    cat "$STATUS_FILE"
}

# Get a scalar field
get_field() {
    local json="$1"
    local field="$2"
    echo "$json" | grep "\"$field\"" | sed 's/.*: *"\([^"]*\)".*/\1/' | head -1
}

# Get errors array
get_errors() {
    local json="$1"
    echo "$json" | grep -A100 '"errors"' | grep -E '^\s+"' | sed 's/.*"\(.*\)".*/\1/' | head -10
}

# Detect whether Unity Editor has this project open
is_editor_open_for_project() {
    local lock_file="$PROJECT_DIR/Temp/UnityLockfile"

    # 1) Lock file held by a process
    if [ -f "$lock_file" ] && command -v lsof >/dev/null 2>&1; then
        if lsof "$lock_file" >/dev/null 2>&1; then
            return 0
        fi
    fi

    # 2) Unity process args explicitly contain projectPath
    if command -v pgrep >/dev/null 2>&1; then
        local line
        while IFS= read -r line; do
            case "$line" in
                *"-projectPath $PROJECT_DIR"*|*"-projectpath $PROJECT_DIR"*)
                    return 0
                    ;;
            esac
        done < <(pgrep -af "/Unity.app/Contents/MacOS/Unity" || true)
    fi

    return 1
}

# Fall back to batch compile when Editor is unavailable
run_batch_fallback() {
    if [ ! -x "$BATCH_COMPILE_SCRIPT" ]; then
        echo -e "${RED}❌ Missing batch compile script: $BATCH_COMPILE_SCRIPT${NC}"
        return 2
    fi

    echo -e "${CYAN}Unity Editor not detected for this project; falling back to batch compile...${NC}"
    "$BATCH_COMPILE_SCRIPT" "$PROJECT_DIR"
}

# Check current compile status
check_status() {
    local json=$(read_status)

    if [ -z "$json" ]; then
        echo -e "${YELLOW}⚠️ No status file found (Unity may not be running or first-time use)${NC}"
        echo "Ensure Unity Editor has the project open and has completed its first compile"
        return 2
    fi

    local state=$(get_field "$json" "state")
    local timestamp=$(get_field "$json" "timestamp")
    local duration=$(echo "$json" | grep '"duration"' | sed 's/.*: *\([0-9.]*\).*/\1/')

    case "$state" in
        "success")
            echo -e "${GREEN}✅ Compile succeeded${NC} (${duration}s @ $timestamp)"
            return 0
            ;;
        "failed")
            echo -e "${RED}❌ Compile failed${NC} @ $timestamp"
            local errors=$(get_errors "$json")
            if [ -n "$errors" ]; then
                echo -e "${RED}Errors:${NC}"
                echo "$errors" | while read line; do
                    echo "  $line"
                done
            fi
            return 1
            ;;
        "compiling")
            echo -e "${YELLOW}⏳ Compiling...${NC}"
            return 2
            ;;
        *)
            echo -e "${YELLOW}⚠️ Unknown state: $state${NC}"
            return 2
            ;;
    esac
}

# Wait for compile to finish (with timeout)
wait_compile() {
    local timeout=${1:-60}
    local start_time=$(date +%s)
    local last_timestamp=""

    echo -e "${CYAN}Waiting for Unity compile...${NC}"

    # Capture current timestamp to detect a new compile result
    local json=$(read_status)
    if [ -n "$json" ]; then
        last_timestamp=$(get_field "$json" "timestamp")
    fi

    while true; do
        local now=$(date +%s)
        local elapsed=$((now - start_time))

        if [ $elapsed -ge $timeout ]; then
            echo -e "\n${YELLOW}⚠️ Wait timed out (${timeout}s)${NC}"
            check_status
            return 2
        fi

        local json=$(read_status)
        local state=$(get_field "$json" "state")
        local timestamp=$(get_field "$json" "timestamp")

        # Check for a new compile result
        if [ "$state" = "success" ] || [ "$state" = "failed" ]; then
            if [ "$timestamp" != "$last_timestamp" ] || [ -z "$last_timestamp" ]; then
                echo ""
                check_status
                return $?
            fi
        fi

        # Show progress
        printf "\rWaiting... %ds " $elapsed
        sleep 0.5
    done
}

# Trigger Unity refresh and wait for compile to finish
trigger_and_wait() {
    local timeout=${1:-60}

    if ! is_editor_open_for_project; then
        run_batch_fallback
        return $?
    fi

    # Record current timestamp
    local json=$(read_status)
    local last_timestamp=$(get_field "$json" "timestamp")
    local saw_compiling=0
    local trigger_consumed=0
    local consumed_elapsed=0
    local no_compile_grace=8

    # If timeout is very small, shrink the no-compile grace window to avoid exceeding total timeout
    if [ $timeout -le $no_compile_grace ]; then
        no_compile_grace=$((timeout - 1))
        if [ $no_compile_grace -lt 1 ]; then
            no_compile_grace=1
        fi
    fi

    # Briefly activate the Unity window to trigger Auto Refresh, then switch back
    echo -e "${CYAN}Triggering Unity refresh...${NC}"
    osascript -e '
        tell application "System Events"
            set frontApp to name of first application process whose frontmost is true
        end tell
        tell application "Unity" to activate
        delay 0.5
        tell application frontApp to activate
    ' 2>/dev/null || true

    # Also create trigger file as a fallback
    mkdir -p "$(dirname "$TRIGGER_FILE")"
    touch "$TRIGGER_FILE"

    # Wait for compile to finish
    local start_time=$(date +%s)

    while true; do
        local now=$(date +%s)
        local elapsed=$((now - start_time))

        if [ $elapsed -ge $timeout ]; then
            echo -e "\n${YELLOW}⚠️ Wait timed out (${timeout}s)${NC}"
            echo "Unity may be inactive in the background; ensure the Unity Editor window is visible"

            # On timeout, if this project's Editor is no longer detected, try batch fallback
            if ! is_editor_open_for_project; then
                run_batch_fallback
                return $?
            fi

            check_status
            return 2
        fi

        local json=$(read_status)
        local state=$(get_field "$json" "state")
        local timestamp=$(get_field "$json" "timestamp")

        # Trigger file deleted by Unity means the refresh request was consumed
        if [ $trigger_consumed -eq 0 ] && [ ! -f "$TRIGGER_FILE" ]; then
            trigger_consumed=1
            consumed_elapsed=$elapsed
        fi

        # Check for a new compile result
        if [ "$state" = "success" ] || [ "$state" = "failed" ]; then
            if [ "$timestamp" != "$last_timestamp" ]; then
                echo ""
                check_status
                return $?
            fi
        fi

        # Check whether compiling
        if [ "$state" = "compiling" ]; then
            saw_compiling=1
            printf "\rCompiling... %ds " $elapsed
            sleep 0.5
            continue
        fi

        # If no code changed, Refresh may not trigger a compile; consumed trigger file is enough to conclude
        if [ $trigger_consumed -eq 1 ] && [ $saw_compiling -eq 0 ]; then
            if [ "$state" = "success" ] || [ "$state" = "failed" ]; then
                if [ $((elapsed - consumed_elapsed)) -ge $no_compile_grace ]; then
                    echo ""
                    echo -e "${CYAN}Refresh processed (no code changes; recompile not needed)${NC}"
                    check_status
                    return $?
                fi
            fi
            printf "\rWaiting for possible compile start... %ds " $elapsed
        else
            printf "\rWaiting for Unity response... %ds " $elapsed
        fi

        sleep 0.5
    done
}

# Main
case "$1" in
    --trigger|-t)
        trigger_and_wait ${2:-60}
        ;;
    --wait|-w)
        wait_compile ${2:-60}
        ;;
    --help|-h)
        echo "Usage: $0 [options]"
        echo ""
        echo "Options:"
        echo "  (no args)       Check current compile status"
        echo "  --trigger, -t   Trigger compile and wait for result (for agents)"
        echo "  --wait, -w      Wait for next compile to finish"
        echo "  --trigger 120   Trigger compile with 120s timeout"
        echo ""
        echo "Files:"
        echo "  Status file:  $STATUS_FILE"
        echo "  Trigger file: $TRIGGER_FILE"
        echo ""
        echo "Prerequisites:"
        echo "  1. Prefers Unity Editor with the project open + CompileWatcher"
        echo "  2. If project is not open in Editor, automatically falls back to unity-compile.sh (batch mode)"
        ;;
    *)
        check_status
        ;;
esac
