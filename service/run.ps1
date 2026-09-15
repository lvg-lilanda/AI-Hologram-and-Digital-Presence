# VX PC start script.  .\run.ps1  from the service folder.
#
# Kept at parity with run.sh. NOTE: written on macOS and never executed on Windows -
# step through VXLAB_BRINGUP.md the first time rather than trusting this blind.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path ".env")) {
    Write-Host "service\.env is missing. Copy .env.example to .env and fill it in."
    exit 1
}

# --- Python -----------------------------------------------------------------
# 3.14 has no pydantic-core wheel and its source build fails (PyO3 caps at 3.13).
# That cost hours on the Mac; the same trap exists wherever a new Python is on PATH.
$python = if (Test-Path ".venv\Scripts\python.exe") { ".venv\Scripts\python.exe" } else { "python" }
$pyv = & $python -c "import sys; print('%d.%d' % sys.version_info[:2])"
if ($pyv -notin @("3.10", "3.11", "3.12", "3.13")) {
    Write-Host "Python $pyv is not supported - the dependencies need 3.10-3.13 (3.12 preferred)."
    Write-Host "Rebuild the venv with a supported interpreter: see docs\VXLAB_BRINGUP.md"
    exit 1
}

# --- Ollama -----------------------------------------------------------------
if (-not (Get-Process ollama -ErrorAction SilentlyContinue)) {
    Write-Host "Starting Ollama..."
    Start-Process ollama -ArgumentList "serve" -WindowStyle Hidden
}

# Wait for it to answer rather than sleeping a fixed few seconds - a cold model load
# on a lab PC takes longer than any number worth hard-coding.
$ready = $false
foreach ($i in 1..30) {
    try {
        Invoke-WebRequest -Uri "http://127.0.0.1:11434/api/tags" -TimeoutSec 2 -UseBasicParsing | Out-Null
        $ready = $true
        break
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) {
    Write-Host "Ollama did not come up on 127.0.0.1:11434. Start it manually and retry."
    exit 1
}

& $python -m app.cli check
& $python -m uvicorn app.main:app --host 127.0.0.1 --port 8765
