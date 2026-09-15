#!/usr/bin/env bash
# Development start script. From the service folder:  ./run.sh
set -euo pipefail
cd "$(dirname "$0")"

[ -f .env ] || { echo "service/.env is missing. Copy .env.example to .env."; exit 1; }

PY=.venv/bin/python
if [ ! -x "$PY" ]; then
    echo "No .venv here. Run: bash scripts/mac_local_setup.sh"; exit 1
fi
# Guard against the venv having been built on an unsupported interpreter - the
# symptom otherwise is a ModuleNotFoundError that looks like a missing install.
PYV="$("$PY" -c 'import sys; print("%d.%d" % sys.version_info[:2])')"
case "$PYV" in
    3.10|3.11|3.12|3.13) ;;
    *) echo "This .venv is Python $PYV; the dependencies need 3.10-3.13."
       echo "Rebuild it: rm -rf .venv && bash scripts/mac_local_setup.sh"; exit 1 ;;
esac

# "--" marks a component belonging to a later bring-up stage. Only a real failure
# gets the warning line, so the warning keeps meaning something.
"$PY" -m app.cli check || echo "(a component has FAILED - see above)"
exec "$PY" -m uvicorn app.main:app --host 127.0.0.1 --port 8765
