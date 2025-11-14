# Run the Python benchmark analysis script
# Usage: .\run-analysis.ps1 [-Csv path] [-OutDir experiments]
param(
    [string]$Csv = '',
    [string]$OutDir = 'experiments'
)

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Host "Python not found on PATH. Install Python 3 and ensure 'python' is available." -ForegroundColor Red
    exit 1
}

if ($Csv -ne '') {
    & python "./experiments/analyze_benchmarks.py" --csv "$Csv" --outdir "$OutDir"
} else {
    & python "./experiments/analyze_benchmarks.py" --outdir "$OutDir"
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Analysis finished." -ForegroundColor Green
} else {
    Write-Host "Analysis failed; check messages above." -ForegroundColor Red
}
