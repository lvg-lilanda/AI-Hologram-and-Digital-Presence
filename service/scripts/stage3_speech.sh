#!/usr/bin/env bash
#
# Stage 3: real voice and real facial animation.
#
#     cd ~/AI-Hologram-and-Digital-Presence/service && bash scripts/stage3_speech.sh
#
# Before running, create the key (about three minutes, no cost on the free tier):
#
#   1. portal.azure.com, sign in with your RMIT account (Azure for Students)
#   2. Create a resource -> "Speech service"
#   3. Region: Australia East.  Pricing tier: F0 (free)
#   4. Once deployed: Keys and Endpoint -> copy KEY 1
#   5. Put it in service/.env:
#          AIHOLO_AZURE_KEY=<the key>
#          AIHOLO_AZURE_REGION=australiaeast
#
# The key lives in service/.env and nowhere else. That file is gitignored, and this
# script checks that it is genuinely untracked before doing anything.
#
set -uo pipefail
cd "$(dirname "$0")/.."
mkdir -p var
LOG="$(pwd)/var/stage3-speech.log"
: > "$LOG"
exec > >(tee -a "$LOG") 2>&1

FAILURES=0
step() { printf '\n=== %s\n' "$1"; }
ok()   { printf '  OK   %s\n' "$1"; }
fail() { printf '  FAIL %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

printf '# Stage 3 speech - %s\n' "$(date '+%Y-%m-%d %H:%M:%S')"

step "1. Credentials"
if [ ! -f .env ]; then
    fail "service/.env is missing. Run scripts/mac_local_setup.sh first."
    exit 1
fi
# Checked before anything else: a key that has already been committed is a key that
# has to be rotated, not hidden.
if git ls-files --error-unmatch service/.env >/dev/null 2>&1 \
   || (cd .. && git ls-files --error-unmatch service/.env >/dev/null 2>&1); then
    fail "service/.env is TRACKED BY GIT. Untrack it before putting a key in it:"
    printf '         git rm --cached service/.env\n'
    exit 1
fi
ok "service/.env is untracked"

KEY_LINE="$(grep '^AIHOLO_AZURE_KEY=' .env || true)"
if [ -z "${KEY_LINE#AIHOLO_AZURE_KEY=}" ]; then
    fail "AIHOLO_AZURE_KEY is empty in service/.env"
    printf '\n  Create the key first - the steps are at the top of this script:\n'
    printf '      head -20 scripts/stage3_speech.sh\n\n'
    exit 1
fi
ok "a key is set (${#KEY_LINE} characters on the line, not printed)"
grep -q '^AIHOLO_AZURE_REGION=.\+' .env && ok "region is set" || fail "AIHOLO_AZURE_REGION is empty"

step "2. Azure Speech SDK"
VENV_PY=.venv/bin/python
[ -x "$VENV_PY" ] || { fail "no .venv - run scripts/mac_local_setup.sh first"; exit 1; }
if $VENV_PY -c "import azure.cognitiveservices.speech" 2>/dev/null; then
    ok "already installed"
else
    printf '  installing...\n'
    $VENV_PY -m pip install --quiet -r requirements-tts.txt \
        && ok "installed" || { fail "pip install failed"; exit 1; }
fi

step "3. Service"
if curl -sf --max-time 3 http://127.0.0.1:8765/health >/dev/null 2>&1; then
    printf '  a service is already running - restarting it so it picks up the key\n'
    pkill -f "uvicorn app.main:app" 2>/dev/null
    sleep 2
fi
$VENV_PY -m uvicorn app.main:app --host 127.0.0.1 --port 8765 > var/service.log 2>&1 &
SVC_PID=$!
for i in $(seq 1 20); do sleep 1; curl -sf --max-time 2 http://127.0.0.1:8765/health >/dev/null 2>&1 && break; done

if curl -s http://127.0.0.1:8765/health | grep -q '"azure":{"ready":true'; then
    ok "Azure reports ready"
else
    fail "Azure still not ready:"
    curl -s http://127.0.0.1:8765/health | $VENV_PY -m json.tool | sed 's/^/    /'
    kill $SVC_PID 2>/dev/null
    exit 1
fi

step "4. Verification against the Unity contract"
$VENV_PY -m bench.verify_speech
case $? in
    0) ok "every check passed - T25 evidence is in bench/results/" ;;
    2) fail "a contract check failed - see above" ;;
    *) fail "verification could not run" ;;
esac

kill $SVC_PID 2>/dev/null
wait $SVC_PID 2>/dev/null

step "Summary"
printf '  failures: %s\n' "$FAILURES"
if [ "$FAILURES" -eq 0 ]; then
    printf '\n  Stage 3 is up. /answer now returns real audio and a real viseme track.\n'
    printf '  Unity has something worth connecting to - see docs/AI_SERVICE_CONTRACT.md.\n'
fi
printf '\n# end - %s\n' "$(date '+%Y-%m-%d %H:%M:%S')"
exit $FAILURES
