# Simple Benchmark Script
# Usage: .\run-benchmark.ps1 [-Samples 1000] [-Type "email"]

param(
    [int]$Samples = 1000,
    [string]$Type = "email",
    [string]$OutputDir = "BenchmarkReports"
)

$baseUrl = "http://localhost:5000"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "Running Benchmark: Type=$Type, Samples=$Samples" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Build URI
$uri = $baseUrl + "/api/benchmark/run?type=" + $Type + "&samples=" + $Samples + "&export=true"

Write-Host "Calling API: $uri" -ForegroundColor Yellow

# Run benchmark
try {
    $response = Invoke-RestMethod -Uri $uri -Method Get
    Write-Host "Benchmark completed successfully!" -ForegroundColor Green
    Write-Host "Duration: $($response.totalDurationMs) ms" -ForegroundColor Gray
} catch {
    Write-Host "ERROR: Failed to run benchmark" -ForegroundColor Red
    Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the web app is running on $baseUrl" -ForegroundColor Yellow
    exit 1
}

# Export to CSV
$csvFile = Join-Path $OutputDir "benchmark_${Type}_${Samples}_${timestamp}.csv"
Write-Host "`nGenerating CSV report..." -ForegroundColor Yellow

# CSV Header
$csv = @()
$csv += "Method,AvgMs,StdDevMs,MinMs,MaxMs,FallbackPct,AccuracyPct,Samples,Success,Failures"

# CSV Data
foreach ($result in $response.results) {
    $line = @(
        $result.method,
        [math]::Round($result.avgMs, 4),
        [math]::Round($result.stdDevMs, 4),
        [math]::Round($result.minMs, 4),
        [math]::Round($result.maxMs, 4),
        [math]::Round($result.fallbackPercentage, 2),
        [math]::Round($result.accuracyPercentage, 2),
        $result.totalSamples,
        $result.successCount,
        $result.failureCount
    ) -join ","
    $csv += $line
}

$csv | Out-File -FilePath $csvFile -Encoding UTF8
Write-Host "CSV saved: $csvFile" -ForegroundColor Green

# Print summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Benchmark Summary" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

foreach ($result in $response.results) {
    $avgMs = [math]::Round($result.avgMs, 3)
    $fallback = [math]::Round($result.fallbackPercentage, 2)
    $methodPadded = $result.method.PadRight(30)
    Write-Host "$methodPadded | Avg: ${avgMs}ms | Fallback: ${fallback}%" -ForegroundColor White
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "CSV file created: $csvFile" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Run Python analysis script:" -ForegroundColor White
Write-Host '   .\.venv\Scripts\python.exe experiments\analyze_benchmarks.py' -ForegroundColor Gray
Write-Host ""
