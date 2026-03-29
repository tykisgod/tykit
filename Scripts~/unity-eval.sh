#!/bin/bash
# tykit CLI
# Usage:
#   ./scripts/unity-eval.sh <command> [args-json]
#   ./scripts/unity-eval.sh hierarchy
#   ./scripts/unity-eval.sh find '{"name":"Ship"}'
#   ./scripts/unity-eval.sh create '{"name":"Barrel","primitiveType":"Cube"}'
#   ./scripts/unity-eval.sh --ping

set -euo pipefail

# Project directory: prefer UNITY_PROJECT_DIR env var, then current working directory, then script location parent
if [ -n "${UNITY_PROJECT_DIR:-}" ]; then
    PROJECT_DIR="$UNITY_PROJECT_DIR"
elif [ -f "Temp/tykit.json" ]; then
    PROJECT_DIR="$(pwd)"
elif [ -f "$(cd "$(dirname "$0")/.." && pwd)/Temp/tykit.json" ]; then
    PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
else
    PROJECT_DIR="$(pwd)"
fi
SERVER_INFO="$PROJECT_DIR/Temp/tykit.json"

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

# JSON formatter
json_fmt() {
    if command -v jq &>/dev/null; then
        jq .
    elif command -v python3 &>/dev/null; then
        python3 -m json.tool
    else
        cat
    fi
}

# Discover server port
get_port() {
    if [ ! -f "$SERVER_INFO" ]; then
        echo -e "${RED}Error: tykit.json not found. Is Unity Editor running?${NC}" >&2
        exit 1
    fi
    grep '"port"' "$SERVER_INFO" | sed 's/[^0-9]//g'
}

# Send command
send() {
    local port
    port=$(get_port)
    local command="$1"
    local args="${2:-}"

    local body
    if [ -n "$args" ]; then
        # args is already JSON — embed directly as nested object
        body="{\"command\":\"$command\",\"args\":$args}"
    else
        body="{\"command\":\"$command\"}"
    fi

    local response
    response=$(curl -s --noproxy localhost --max-time 10 -X POST "http://localhost:${port}/" \
        -H "Content-Type: application/json" \
        -d "$body" 2>/dev/null)

    if [ $? -ne 0 ]; then
        echo -e "${RED}Error: Cannot connect to tykit on port $port${NC}" >&2
        exit 1
    fi

    echo "$response" | json_fmt
}

# Main
case "${1:-}" in
    --ping|-p)
        port=$(get_port)
        curl -s --noproxy localhost --max-time 5 "http://localhost:${port}/ping" | json_fmt
        ;;
    --compile|-c)
        # refresh + wait for compile to finish + check result via console errors
        # Usage: unity-eval.sh --compile [timeout]
        compile_timeout="${2:-15}"
        port=$(get_port)

        # ping check
        if ! curl -s --noproxy localhost --max-time 3 "http://localhost:${port}/ping" &>/dev/null; then
            echo -e "${RED}tykit unreachable${NC}" >&2
            exit 2
        fi

        # trigger incremental compile via refresh
        send refresh >/dev/null 2>&1

        # poll compile-status
        start_time=$(date +%s)
        saw_compiling=0
        while true; do
            now=$(date +%s)
            elapsed=$((now - start_time))
            if [ $elapsed -ge "$compile_timeout" ]; then
                echo -e "\n${RED}Compile wait timed out (${compile_timeout}s)${NC}" >&2
                exit 2
            fi

            cs=$(curl -s --noproxy localhost --connect-timeout 2 --max-time 5 -X POST "http://localhost:${port}/" \
                -d '{"command":"compile-status"}' -H 'Content-Type: application/json' 2>/dev/null) || true
            compiling=""
            if [ -n "$cs" ]; then
                compiling=$(echo "$cs" | python3 -c "import sys,json; print(json.load(sys.stdin).get('data',{}).get('isCompiling',''))" 2>/dev/null) || true
            fi

            if [ "$compiling" = "True" ]; then
                saw_compiling=1
                printf "\rCompiling... %ds " $elapsed >&2
            elif [ "$compiling" = "False" ]; then
                if [ $saw_compiling -eq 1 ] || [ $elapsed -ge 5 ]; then
                    break
                fi
                printf "\rWaiting for compile to start... %ds " $elapsed >&2
            else
                printf "\rWaiting for response... %ds " $elapsed >&2
            fi
            sleep 0.5
        done
        echo "" >&2

        # check result via console compile errors
        error_log=$(curl -s --noproxy localhost --connect-timeout 2 --max-time 5 -X POST "http://localhost:${port}/" \
            -d '{"command":"console","args":{"count":50,"filter":"error"}}' \
            -H 'Content-Type: application/json' 2>/dev/null) || true
        compile_errors=$(echo "$error_log" | python3 -c "
import sys, json
try:
    logs = json.load(sys.stdin).get('data', [])
    seen = set()
    for log in logs:
        if not isinstance(log, dict): continue
        msg = log.get('message', '')
        if 'error CS' in msg and msg not in seen:
            seen.add(msg)
            print(msg)
except:
    pass
" 2>/dev/null) || true

        if [ -z "$compile_errors" ]; then
            echo -e "${GREEN}✅ Compile succeeded${NC}"
            exit 0
        else
            echo -e "${RED}❌ Compile failed${NC}"
            echo "$compile_errors" | while IFS= read -r line; do
                echo "  $line"
            done
            exit 1
        fi
        ;;
    --wait-ready|-w)
        max_wait="${2:-60}"
        echo -e "${GREEN}Waiting for tykit (max ${max_wait}s)...${NC}" >&2
        for i in $(seq 1 "$max_wait"); do
            if [ -f "$SERVER_INFO" ]; then
                port=$(get_port)
                if curl -s --noproxy localhost --max-time 2 "http://localhost:${port}/ping" &>/dev/null; then
                    echo -e "${GREEN}tykit ready (${i}s)${NC}" >&2
                    curl -s --noproxy localhost --max-time 2 "http://localhost:${port}/ping" | json_fmt
                    exit 0
                fi
            fi
            sleep 1
        done
        echo -e "${RED}Timeout: tykit not ready after ${max_wait}s${NC}" >&2
        exit 1
        ;;
    --help|-h)
        cat <<'HELP'
Usage: unity-eval.sh <command> [args-json]

Read commands:
  hierarchy                         Scene hierarchy (default depth 3)
  hierarchy '{"depth":5}'           Custom depth
  find '{"name":"Ship"}'            Find by name
  find '{"tag":"Player"}'           Find by tag
  find '{"type":"Camera"}'          Find by component type
  inspect '{"id":12345}'            Inspect by instanceId
  inspect '{"name":"Ship"}'         Inspect by name
  get-properties '{"id":12345}'     List serialized properties
  console '{"count":20}'            Read console logs
  status                            Editor state (playing, compiling, etc.)
  commands                          List all available commands

Write commands (prefer instanceId from find/create results):
  create '{"name":"X","primitiveType":"Cube","position":[0,1,0]}'
  instantiate '{"prefab":"Assets/...","name":"X"}'
  destroy '{"id":12345}'
  set-transform '{"id":12345,"position":[1,2,3]}'
  add-component '{"id":12345,"component":"Rigidbody"}'
  set-property '{"id":12345,"component":"T","property":"p","value":"v"}'

Editor control:
  play / stop / pause               Play mode control
  save-scene                        Save open scenes
  menu '{"item":"Edit/Preferences"}'  Execute menu item

Asset commands:
  create-prefab '{"source":"Ship","path":"Assets/.../Ship.prefab"}'
  create-material '{"path":"Assets/.../Mat.mat"}'
  refresh                           AssetDatabase.Refresh()

Batch (execute multiple commands in one call, $N references prior instanceIds):
  echo '{"command":"batch","args":{"commands":[
    {"command":"create","args":{"name":"Sun","primitiveType":"Sphere"}},
    {"command":"set-transform","args":{"id":"$0","scale":[3,3,3]}}
  ]}}' | curl -s --noproxy localhost -X POST http://localhost:8090/ -d @-

Options:
  --ping, -p          Check server status
  --compile, -c [N]   Refresh + wait compile + check result (timeout N, default 15)
  --wait-ready, -w    Wait for server to be ready (after domain reload)
  --help, -h          Show this help
HELP
        ;;
    "")
        echo "Usage: unity-eval.sh <command> [args-json]"
        echo "Run 'unity-eval.sh --help' for details"
        ;;
    *)
        send "$1" "${2:-}"
        ;;
esac
