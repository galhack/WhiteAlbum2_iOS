$ErrorActionPreference = "Stop"
$scriptPath = Join-Path $PSScriptRoot "build.py"

if (Get-Command py -ErrorAction SilentlyContinue) {
    & py -3 $scriptPath @args
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    & python $scriptPath @args
} else {
    throw "Python 3 was not found."
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
