#!/usr/bin/env bash
#
# Stage 1 local bring-up on macOS: real Ollama, real embeddings, real retrieval,
# real answers. Speech-to-text and Azure speech come in stages 2 and 3 - this stage
# deliberately leaves them out so the first working loop is minutes away, not a
# 3 GB download away.
#
# Run from the service folder:
#
#     cd ~/AI-Hologram-and-Digital-Presence/service && bash scripts/mac_local_setup.sh
#
# Safe to re-run: every step checks before it acts. Everything it prints is also
# written to var/local-setup.log.
#
set -uo pipefail
cd "$(dirname "$0")/.."
SERVICE_DIR="$(pwd)"

mkdir -p var
LOG="$SERVICE_DIR/var/local-setup.log"
: > "$LOG"
exec > >(tee -a "$LOG") 2>&1

FAILURES=0
step()  { printf '\n=== %s\n' "$1"; }
ok()    { printf '  OK   %s\n' "$1"; }
warn()  { printf '  WARN %s\n' "$1"; }
fail()  { printf '  FAIL %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

printf '# Local setup log - %s\n' "$(date '+%Y-%m-%d %H:%M:%S')"
printf 'service dir: %s\n' "$SERVICE_DIR"

# --- 1. machine -------------------------------------------------------------
step "1. Machine"
if [ "$(uname -s)" != "Darwin" ]; then
    fail "This script is for macOS. Use run.ps1 on the VX PC."
    exit 1
fi
CHIP="$(sysctl -n machdep.cpu.brand_string 2>/dev/null || echo unknown)"
RAM_GB=$(( $(sysctl -n hw.memsize) / 1073741824 ))
DISK_FREE="$(df -h . | awk 'NR==2{print $4}')"
printf '  chip: %s\n  ram: %s GB\n  free disk: %s\n  macOS: %s\n' \
    "$CHIP" "$RAM_GB" "$DISK_FREE" "$(sw_vers -productVersion)"

# Model sizes chosen so the chat model plus the embedder fit in RAM alongside
# Unity. The VX PC gets benchmarked separately (bench/bench_ollama.py) - this is
# only about getting a working loop on the Mac.
if   [ "$RAM_GB" -ge 32 ]; then CHAT_MODELS=("llama3.1:8b-instruct-q4_K_M" "qwen2.5:7b-instruct-q4_K_M")
elif [ "$RAM_GB" -ge 16 ]; then CHAT_MODELS=("llama3.1:8b-instruct-q4_K_M")
elif [ "$RAM_GB" -ge 8  ]; then CHAT_MODELS=("llama3.2:3b-instruct-q4_K_M")
else                            CHAT_MODELS=("llama3.2:1b-instruct-q4_K_M")
fi
CHAT_MODEL="${CHAT_MODELS[0]}"
EMBED_MODEL="nomic-embed-text"
ok "chat model for this machine: $CHAT_MODEL"

# --- 2. ollama --------------------------------------------------------------
step "2. Ollama"
if command -v ollama >/dev/null 2>&1; then
    ok "installed: $(ollama --version 2>&1 | head -1)"
elif [ -x "/Applications/Ollama.app/Contents/Resources/ollama" ]; then
    export PATH="/Applications/Ollama.app/Contents/Resources:$PATH"
    ok "found in Ollama.app, added to PATH for this run"
elif command -v brew >/dev/null 2>&1; then
    printf '  installing via Homebrew (no admin password needed)...\n'
    brew install ollama && ok "installed" || fail "brew install ollama failed"
else
    fail "Ollama is not installed and Homebrew is not available."
    printf '\n  Install it one of these ways, then re-run this script:\n'
    printf '    A) Download https://ollama.com/download/mac and drag it to Applications\n'
    printf '    B) Install Homebrew from https://brew.sh then: brew install ollama\n\n'
    exit 1
fi

if curl -sf --max-time 3 http://127.0.0.1:11434/api/tags >/dev/null 2>&1; then
    ok "server already running on 11434"
else
    printf '  starting ollama serve in the background...\n'
    nohup ollama serve > "$SERVICE_DIR/var/ollama.log" 2>&1 &
    for i in $(seq 1 30); do
        sleep 1
        curl -sf --max-time 2 http://127.0.0.1:11434/api/tags >/dev/null 2>&1 && break
    done
    if curl -sf --max-time 3 http://127.0.0.1:11434/api/tags >/dev/null 2>&1; then
        ok "server started"
    else
        fail "server did not come up - see var/ollama.log"
        exit 1
    fi
fi

step "3. Models"
HAVE="$(ollama list 2>/dev/null | tail -n +2 | awk '{print $1}')"
for M in "$EMBED_MODEL" "${CHAT_MODELS[@]}"; do
    if printf '%s\n' "$HAVE" | grep -qx "$M"; then
        ok "already pulled: $M"
    else
        printf '  pulling %s (this is the slow part)...\n' "$M"
        ollama pull "$M" && ok "pulled $M" || fail "could not pull $M"
    fi
done

# --- 4. python --------------------------------------------------------------
step "4. Python environment"

# Which interpreter, and why it is not just `python3`:
#
# Homebrew installs python@3.14 as an ollama dependency, so `python3` on a fresh
# machine now points at 3.14. pydantic-core has no 3.14 wheel yet, pip falls back to
# compiling it, and the PyO3 version it uses caps out at 3.13 - the build fails after
# several minutes of Rust compilation. 3.12 is the target instead: every dependency
# across all three stages ships a prebuilt wheel for it, including the Azure Speech
# SDK, which is the slowest of them to support a new Python.
PY_WANTED="3.12"
PY_OK_RE='^3\.(10|11|12|13)$'

py_version() { "$1" -c 'import sys; print("%d.%d" % sys.version_info[:2])' 2>/dev/null; }

find_python() {
    local cand ver
    # Preference order, not PATH order: 3.12 first, then the rest of the supported range.
    for cand in python3.12 python3.11 python3.13 python3.10 \
                /opt/homebrew/opt/python@3.12/bin/python3.12 \
                /opt/homebrew/opt/python@3.11/bin/python3.11 \
                /opt/homebrew/opt/python@3.13/bin/python3.13 \
                /usr/bin/python3 python3; do
        command -v "$cand" >/dev/null 2>&1 || [ -x "$cand" ] || continue
        ver="$(py_version "$cand")"
        if printf '%s' "$ver" | grep -qE "$PY_OK_RE"; then
            printf '%s' "$(command -v "$cand" 2>/dev/null || printf '%s' "$cand")"
            return 0
        fi
    done
    return 1
}

PY3="$(find_python)" || PY3=""
if [ -z "$PY3" ]; then
    warn "no supported Python found (need 3.10-3.13; python3 is $(py_version python3 || echo unknown))"
    if command -v brew >/dev/null 2>&1; then
        printf '  installing python@%s via Homebrew (no admin password needed)...\n' "$PY_WANTED"
        brew install "python@$PY_WANTED" >/dev/null 2>&1
        PY3="$(find_python)" || PY3=""
    fi
fi
if [ -z "$PY3" ]; then
    fail "could not find or install a supported Python (3.10-3.13)."
    printf '\n  Install one, then re-run this script:\n      brew install python@%s\n\n' "$PY_WANTED"
    exit 1
fi
ok "using $PY3 (Python $(py_version "$PY3"))"

# An existing venv built on the wrong interpreter is worse than none - it fails
# later, further from the cause. Rebuild it rather than reusing it.
if [ -x .venv/bin/python ]; then
    EXISTING="$(py_version .venv/bin/python)"
    if printf '%s' "$EXISTING" | grep -qE "$PY_OK_RE"; then
        ok ".venv already exists (Python $EXISTING)"
    else
        warn ".venv was built with Python $EXISTING, which cannot install these packages - rebuilding"
        rm -rf .venv
    fi
fi
if [ ! -x .venv/bin/python ]; then
    "$PY3" -m venv .venv && ok "created .venv (Python $(py_version .venv/bin/python))" \
        || { fail "could not create .venv"; exit 1; }
fi

VENV_PY=".venv/bin/python"
$VENV_PY -m pip install --quiet --upgrade pip
printf '  installing dependencies (no torch - embeddings come from Ollama)...\n'
if $VENV_PY -m pip install --quiet -r requirements-core.txt; then
    ok "dependencies installed"
else
    fail "pip install failed"
    printf '\n  Everything after this depends on those packages, so stopping here rather\n'
    printf '  than reporting the same cause four more times. The pip output above is\n'
    printf '  the real error.\n\n'
    exit 1
fi

# --- 5. configuration -------------------------------------------------------
step "5. Configuration"
if [ ! -f .env ]; then
    cp .env.example .env
    ok "created .env from .env.example"
else
    ok ".env already exists - leaving it alone"
fi
# Point .env at the model this machine can actually run, without touching anything else.
$VENV_PY - "$CHAT_MODEL" "$EMBED_MODEL" << 'PYEOF'
import re, sys
from pathlib import Path
chat, embed = sys.argv[1], sys.argv[2]
p = Path(".env"); text = p.read_text()
for key, value in (("AIHOLO_OLLAMA_MODEL", chat), ("AIHOLO_EMBED_MODEL", embed)):
    if re.search(rf"^{key}=.*$", text, re.M):
        text = re.sub(rf"^{key}=.*$", f"{key}={value}", text, flags=re.M)
    else:
        text += f"\n{key}={value}\n"
p.write_text(text)
print(f"  set AIHOLO_OLLAMA_MODEL={chat}")
print(f"  set AIHOLO_EMBED_MODEL={embed}")
PYEOF
grep -q "^AIHOLO_AZURE_KEY=$" .env && warn "no Azure key yet - stage 3 adds real speech; /answer works without it"

# --- 6. index ---------------------------------------------------------------
step "6. Retrieval index"
if $VENV_PY -m app.cli index; then ok "index built"; else fail "index build failed"; fi

# --- 7. grounding floor -----------------------------------------------------
step "7. Grounding floor calibration"
$VENV_PY -m bench.calibrate_floor
CAL=$?
case $CAL in
    0) ok   "clean separation - set the recommended floor in .env" ;;
    2) warn "the answerable and unanswerable sets overlap - fix the corpus before" \
            "trusting /answer to refuse" ;;
    *) fail "calibration could not run - the traceback above is the real error" ;;
esac

# --- 8. tests ---------------------------------------------------------------
step "8. Contract tests (mock mode - no hardware needed)"
if AIHOLO_MOCK=true $VENV_PY -m pytest tests/ -q; then ok "contract tests pass"; else fail "contract tests failed"; fi

# --- 9. live smoke test -----------------------------------------------------
step "9. Live smoke test against real Ollama"
$VENV_PY -m uvicorn app.main:app --host 127.0.0.1 --port 8765 > var/service.log 2>&1 &
SVC_PID=$!
for i in $(seq 1 20); do sleep 1; curl -sf --max-time 2 http://127.0.0.1:8765/health >/dev/null 2>&1 && break; done

# Show the status line and the raw body on anything unexpected. Piping straight into
# json.load hid a plain-text 500 behind a JSONDecodeError traceback, which pointed at
# the test harness instead of at the service.
ask() {
    local label="$1" payload="$2" status
    printf '\n  --- POST /answer (%s) ---\n' "$label"
    status=$(curl -s -o var/.ask.json -w '%{http_code}' --max-time 180 \
             -X POST http://127.0.0.1:8765/answer \
             -H 'Content-Type: application/json' -d "$payload")
    printf '  HTTP %s\n' "$status"
    $VENV_PY - << 'PYEOF'
import json
raw = open("var/.ask.json", encoding="utf-8", errors="replace").read()
try:
    d = json.loads(raw)
except json.JSONDecodeError:
    print("  response was not JSON:")
    print("  " + (raw[:400] if raw.strip() else "(empty body)"))
    raise SystemExit(1)
if "error" in d:
    e = d["error"]
    print(f"  {e['code']}: {e['message'][:200]}")
    raise SystemExit(1)
print("  grounded :", d["grounded"])
print("  answer   :", d["text"][:300])
if d["citations"]:
    print("  cites    :", [(c["title"], c["section"] or c["page"], round(c["score"], 3))
                           for c in d["citations"]])
if d.get("speechUnavailable"):
    print("  no audio :", d["speechUnavailable"])
print("  timings  :", d["timings"])
PYEOF
    return $?
}

if curl -sf --max-time 5 http://127.0.0.1:8765/health >/dev/null 2>&1; then
    printf '\n  --- GET /health ---\n'
    curl -s http://127.0.0.1:8765/health | $VENV_PY -m json.tool | sed 's/^/  /'

    ask "answerable"  '{"text":"What is Telstra muru-D?","sessionId":"setup-check"}' \
        || fail "/answer on an answerable question did not work"
    ask "should refuse" '{"text":"Who won the AFL grand final?","sessionId":"setup-check"}' \
        || fail "/answer on an unanswerable question did not work"
    rm -f var/.ask.json

    # --- 10. end-to-end grounding -------------------------------------------
    # The number that actually matters: does it refuse what it should? The
    # retrieval floor alone cannot tell you - only the whole pipeline can.
    step "10. End-to-end grounding evaluation"
    $VENV_PY -m bench.eval_grounding
    case $? in
        0) ok   "refused everything it should have" ;;
        2) fail "it answered at least one question it should have refused - see above" ;;
        *) fail "grounding evaluation could not run" ;;
    esac

    # --- 11. multi-turn ------------------------------------------------------
    # Step 10 asks every question cold. Nobody talks to a hologram that way, and
    # the first hallucination found by hand came from a vague follow-up that no
    # isolated-question test could reach.
    step "11. Multi-turn conversations"
    $VENV_PY -m bench.eval_conversation
    case $? in
        0) ok   "every turn behaved" ;;
        2) fail "a conversation turn misbehaved - see above" ;;
        *) fail "conversation evaluation could not run" ;;
    esac
else
    fail "service did not start - see var/service.log"
    tail -30 var/service.log | sed 's/^/  /'
fi

kill $SVC_PID 2>/dev/null
wait $SVC_PID 2>/dev/null

# --- summary ----------------------------------------------------------------
step "Summary"
printf '  machine      : %s, %s GB RAM\n' "$CHIP" "$RAM_GB"
printf '  chat model   : %s\n' "$CHAT_MODEL"
printf '  embed model  : %s\n' "$EMBED_MODEL"
printf '  failures     : %s\n' "$FAILURES"
if [ "$FAILURES" -eq 0 ]; then
    printf '\n  Stage 1 is up. Start the service any time with:\n      ./run.sh\n'
    printf '\n  Next: stage 2 adds local speech-to-text, stage 3 adds Azure speech.\n'
else
    printf '\n  %s step(s) failed. The full log is at var/local-setup.log -\n' "$FAILURES"
    printf '  send that file back and it can be read directly.\n'
fi
printf '\n# end - %s\n' "$(date '+%Y-%m-%d %H:%M:%S')"
exit $FAILURES
