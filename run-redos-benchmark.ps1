# run-redos-benchmark.ps1
# Runs ReDoS attack benchmark and exports to CSV

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ReDoS Attack Benchmark Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$BaseUrl = "http://localhost:5000"
$OutputDir = "experiments"
$CsvFile = "$OutputDir/redos_benchmark_real.csv"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Check if server is running
Write-Host "[1/4] Checking web server status..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/benchmark/info" -Method GET -TimeoutSec 5 -UseBasicParsing
    Write-Host "  [OK] Server is running" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Server is not running!" -ForegroundColor Red
    Write-Host "  Please start the web app first:" -ForegroundColor Red
    Write-Host "    cd StaffValidator.WebApp" -ForegroundColor Yellow
    Write-Host "    dotnet run" -ForegroundColor Yellow
    exit 1
}

# Run ReDoS benchmark
Write-Host ""
Write-Host "[2/4] Running ReDoS attack benchmark..." -ForegroundColor Yellow
Write-Host "  Type: redos" -ForegroundColor Gray
Write-Host "  Pattern: Email validation with nested quantifiers" -ForegroundColor Gray
Write-Host "  Dataset: Auto-generated malicious inputs" -ForegroundColor Gray
Write-Host ""

try {
    $uri = "$BaseUrl/api/benchmark/run?type=redos"
    Write-Host "  Calling: $uri" -ForegroundColor Gray
    
    $response = Invoke-RestMethod -Uri $uri -Method GET -TimeoutSec 300
    
    if ($response) {
        Write-Host "  [OK] Benchmark completed successfully" -ForegroundColor Green
        Write-Host ""
        
        # Display summary
        Write-Host "[3/4] Benchmark Summary:" -ForegroundColor Yellow
        Write-Host "  Dataset Type: $($response.datasetType)" -ForegroundColor White
        Write-Host "  Sample Count: $($response.sampleCount)" -ForegroundColor White
        Write-Host "  Total Duration: $([math]::Round($response.totalDurationMs, 2)) ms" -ForegroundColor White
        Write-Host ""
        
        Write-Host "  Results:" -ForegroundColor Cyan
        foreach ($result in $response.results) {
            $avgMs = [math]::Round($result.avgMs, 3)
            $fallback = [math]::Round($result.fallbackPercentage, 2)
            
            $color = "White"
            if ($avgMs -gt 100) { $color = "Red" }
            elseif ($avgMs -gt 10) { $color = "Yellow" }
            else { $color = "Green" }
            
            Write-Host "    $($result.method.PadRight(25)) Avg: $($avgMs.ToString().PadLeft(10)) ms  Fallback: $($fallback.ToString().PadLeft(6))%" -ForegroundColor $color
        }
        
        # Export to CSV
        Write-Host ""
        Write-Host "[4/4] Exporting to CSV..." -ForegroundColor Yellow
        
        $csvContent = "Method,Avg(ms),StdDev(ms),Min(ms),Max(ms),Fallback(%),Accuracy(%),Samples,Success,Failures`n"
        
        foreach ($result in $response.results) {
            $line = "$($result.method),$([math]::Round($result.avgMs, 3)),$([math]::Round($result.stdDevMs, 3))," +
                    "$([math]::Round($result.minMs, 3)),$([math]::Round($result.maxMs, 3)),$([math]::Round($result.fallbackPercentage, 2))," +
                    "$([math]::Round($result.accuracyPercentage, 2)),$($result.totalSamples),$($result.successCount),$($result.failureCount)"
            $csvContent += "$line`n"
        }
        
        $csvContent | Out-File -FilePath $CsvFile -Encoding UTF8
        Write-Host "  [OK] CSV exported to: $CsvFile" -ForegroundColor Green
        Write-Host ""
        
        # Show next steps
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Next Steps:" -ForegroundColor Cyan
        Write-Host "  1. Analyze the results:" -ForegroundColor White
        Write-Host "     cd experiments" -ForegroundColor Yellow
        Write-Host "     ..\.venv\Scripts\python.exe analyze_benchmarks.py" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  2. View generated plots in:" -ForegroundColor White
        Write-Host "     experiments/plots/" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  3. View markdown report:" -ForegroundColor White
        Write-Host "     experiments/reports/*.md" -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Cyan
        
    } else {
        Write-Host "  [ERROR] No response from benchmark API" -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Host "  [ERROR] Benchmark failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  $($_.Exception.InnerException)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[SUCCESS] ReDoS benchmark completed!" -ForegroundColor Green
Write-Host ""
