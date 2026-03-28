#!/bin/bash
# Tykit E2E Test — runs against a live Unity Editor with tykit
# Usage: ./Scripts~/e2e-test.sh [path-to-unity-eval.sh]
#
# Requires Unity Editor running with tykit loaded.

set -euo pipefail

EVAL="${1:-./scripts/unity-eval.sh}"
PASS=0
FAIL=0
ERRORS=""

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# --- helpers ---

assert_ok() {
    local label="$1" result="$2"
    local success
    success=$(echo "$result" | python3 -c "import json,sys; print(json.load(sys.stdin).get('success',''))" 2>/dev/null || echo "")
    if [[ "$success" == "True" ]]; then
        echo -e "  ${GREEN}✓${NC} $label"
        ((PASS++))
    else
        echo -e "  ${RED}✗${NC} $label"
        echo "    $result" | head -3
        ((FAIL++))
        ERRORS+="  - $label\n"
    fi
}

assert_error() {
    local label="$1" result="$2"
    local success
    success=$(echo "$result" | python3 -c "import json,sys; print(json.load(sys.stdin).get('success',''))" 2>/dev/null || echo "")
    if [[ "$success" == "False" ]]; then
        echo -e "  ${GREEN}✓${NC} $label (expected error)"
        ((PASS++))
    else
        echo -e "  ${RED}✗${NC} $label (expected error, got success)"
        ((FAIL++))
        ERRORS+="  - $label\n"
    fi
}

extract() {
    echo "$1" | python3 -c "import json,sys; d=json.load(sys.stdin); exec(open('/dev/stdin').read() if False else None); print(eval(sys.argv[1]))" "$2" 2>/dev/null
}

json_field() {
    echo "$1" | python3 -c "
import json,sys
d = json.load(sys.stdin)
keys = sys.argv[1].split('.')
for k in keys:
    if isinstance(d, list):
        d = d[int(k)]
    else:
        d = d[k]
print(d)
" "$2" 2>/dev/null
}

# --- ping ---
echo -e "${YELLOW}Checking tykit...${NC}"
PING=$("$EVAL" --ping 2>&1) || { echo -e "${RED}tykit not reachable. Is Unity open?${NC}"; exit 1; }
echo -e "  ${GREEN}✓${NC} ping ok"

# --- commands ---
echo -e "\n${YELLOW}[commands] Verify all commands registered${NC}"
CMDS=$("$EVAL" commands '{}' 2>&1)
for cmd in compile-status get-selection list-assets list-scenes open-scene \
           duplicate set-parent set-active set-layer set-tag remove-component; do
    if echo "$CMDS" | grep -q "\"$cmd\""; then
        echo -e "  ${GREEN}✓${NC} $cmd registered"
        ((PASS++))
    else
        echo -e "  ${RED}✗${NC} $cmd NOT registered"
        ((FAIL++))
        ERRORS+="  - $cmd not registered\n"
    fi
done

# --- compile-status ---
echo -e "\n${YELLOW}[compile-status]${NC}"
R=$("$EVAL" compile-status '{}' 2>&1)
assert_ok "returns ok" "$R"

# --- list-scenes ---
echo -e "\n${YELLOW}[list-scenes]${NC}"
R=$("$EVAL" list-scenes '{}' 2>&1)
assert_ok "returns scene list" "$R"

# --- list-assets ---
echo -e "\n${YELLOW}[list-assets]${NC}"
R=$("$EVAL" list-assets '{"filter":"t:Scene"}' 2>&1)
assert_ok "list scenes via list-assets" "$R"
R=$("$EVAL" list-assets '{}' 2>&1)
assert_ok "list all assets" "$R"

# --- open-scene error cases ---
echo -e "\n${YELLOW}[open-scene] Error cases${NC}"
R=$("$EVAL" open-scene '{}' 2>&1)
assert_error "missing path" "$R"
R=$("$EVAL" open-scene '{"path":"Assets/NoSuchScene.unity"}' 2>&1)
assert_error "nonexistent scene" "$R"

# --- create + duplicate + set-parent + set-active + set-layer + set-tag + remove-component + destroy ---
echo -e "\n${YELLOW}[GameObject lifecycle]${NC}"

# create test object
R=$("$EVAL" create '{"name":"E2E_Test","primitiveType":"Cube","position":[0,100,0]}' 2>&1)
assert_ok "create E2E_Test" "$R"
OBJ_ID=$(json_field "$R" "data.instanceId")

# add a component to later remove
R=$("$EVAL" add-component "{\"id\":$OBJ_ID,\"component\":\"Rigidbody\"}" 2>&1)
assert_ok "add Rigidbody" "$R"

# duplicate
R=$("$EVAL" duplicate "{\"id\":$OBJ_ID}" 2>&1)
assert_ok "duplicate" "$R"
CLONE_ID=$(json_field "$R" "data.instanceId")

# set-parent (clone -> original)
R=$("$EVAL" set-parent "{\"id\":$CLONE_ID,\"parentId\":$OBJ_ID}" 2>&1)
assert_ok "set-parent by id" "$R"

# set-active (deactivate clone)
R=$("$EVAL" set-active "{\"id\":$CLONE_ID,\"active\":false}" 2>&1)
assert_ok "set-active false" "$R"

# verify via inspect
R=$("$EVAL" inspect "{\"id\":$CLONE_ID}" 2>&1)
ACTIVE=$(json_field "$R" "data.active")
if [[ "$ACTIVE" == "False" ]]; then
    echo -e "  ${GREEN}✓${NC} inspect confirms inactive"
    ((PASS++))
else
    echo -e "  ${RED}✗${NC} inspect shows active=$ACTIVE, expected False"
    ((FAIL++))
    ERRORS+="  - set-active verify\n"
fi

# set-layer
R=$("$EVAL" set-layer "{\"id\":$OBJ_ID,\"layer\":\"Ignore Raycast\"}" 2>&1)
assert_ok "set-layer by name" "$R"

R=$("$EVAL" set-layer "{\"id\":$CLONE_ID,\"layer\":5}" 2>&1)
assert_ok "set-layer by index" "$R"

# set-tag
R=$("$EVAL" set-tag "{\"id\":$OBJ_ID,\"tag\":\"Player\"}" 2>&1)
assert_ok "set-tag" "$R"

# remove-component
R=$("$EVAL" remove-component "{\"id\":$OBJ_ID,\"component\":\"Rigidbody\"}" 2>&1)
assert_ok "remove-component" "$R"

# remove-component error: already removed
R=$("$EVAL" remove-component "{\"id\":$OBJ_ID,\"component\":\"Rigidbody\"}" 2>&1)
assert_error "remove-component (already removed)" "$R"

# get-selection
R=$("$EVAL" select "{\"id\":$OBJ_ID}" 2>&1)
assert_ok "select" "$R"
R=$("$EVAL" get-selection '{}' 2>&1)
assert_ok "get-selection" "$R"
SEL_NAME=$(json_field "$R" "data.0.name")
if [[ "$SEL_NAME" == "E2E_Test" ]]; then
    echo -e "  ${GREEN}✓${NC} get-selection returns selected object"
    ((PASS++))
else
    echo -e "  ${RED}✗${NC} get-selection name=$SEL_NAME, expected E2E_Test"
    ((FAIL++))
    ERRORS+="  - get-selection verify\n"
fi

# cleanup
"$EVAL" destroy "{\"id\":$CLONE_ID}" 2>&1 >/dev/null
"$EVAL" destroy "{\"id\":$OBJ_ID}" 2>&1 >/dev/null
echo -e "  ${GREEN}✓${NC} cleanup done"

# --- summary ---
echo ""
TOTAL=$((PASS + FAIL))
if [[ $FAIL -eq 0 ]]; then
    echo -e "${GREEN}All $TOTAL tests passed ✓${NC}"
else
    echo -e "${RED}$FAIL/$TOTAL tests failed:${NC}"
    echo -e "$ERRORS"
fi
exit $FAIL
