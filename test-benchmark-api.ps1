# Test Benchmark API Endpoints
# Usage: .\test-benchmark-api.ps1

$baseUrl = "http://localhost:5000"

Write-Host "🚀 Testing Benchmark API Endpoints" -ForegroundColor Cyan
Write-Host "==================================`n" -ForegroundColor Cyan

# Test 1: Get Info
Write-Host "1️⃣ Testing GET /api/benchmark/info" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/benchmark/info" -Method Get
    Write-Host "✅ Info endpoint working!" -ForegroundColor Green
    Write-Host "Available types: $($response.availableTypes -join ', ')" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 2: Preview Dataset
Write-Host "2️⃣ Testing GET /api/benchmark/preview?type=email&count=5" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/benchmark/preview?type=email&count=5" -Method Get
    Write-Host "✅ Preview endpoint working!" -ForegroundColor Green
    Write-Host "Sample emails:" -ForegroundColor Gray
    $response.samples | Select-Object -First 5 | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    Write-Host ""
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 3: Quick Benchmark
Write-Host "3️⃣ Testing GET /api/benchmark/quick?type=email" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/benchmark/quick?type=email" -Method Get
    Write-Host "✅ Quick benchmark completed!" -ForegroundColor Green
    Write-Host "Dataset: $($response.datasetType), Samples: $($response.sampleCount)" -ForegroundColor Gray
    Write-Host "Duration: $([math]::Round($response.totalDurationMs, 2)) ms" -ForegroundColor Gray
    Write-Host "`nResults:" -ForegroundColor Gray
    foreach ($result in $response.results) {
        $avg = [math]::Round($result.avgMs, 3)
        $fallback = [math]::Round($result.fallbackPercentage, 2)
        Write-Host "  $($result.method.PadRight(20)) | Avg: ${avg}ms | Fallback: ${fallback}%" -ForegroundColor Gray
    }
    Write-Host ""
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

# Test 4: Full Benchmark (small dataset)
Write-Host "4️⃣ Testing GET /api/benchmark/run?type=phone&samples=200" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/benchmark/run?type=phone&samples=200" -Method Get
    Write-Host "✅ Full benchmark completed!" -ForegroundColor Green
    Write-Host "Dataset: $($response.datasetType), Samples: $($response.sampleCount)" -ForegroundColor Gray
    Write-Host "Duration: $([math]::Round($response.totalDurationMs, 2)) ms" -ForegroundColor Gray
    Write-Host "`nResults:" -ForegroundColor Gray
    foreach ($result in $response.results) {
        $avg = [math]::Round($result.avgMs, 3)
        $stdDev = [math]::Round($result.stdDevMs, 3)
        $fallback = [math]::Round($result.fallbackPercentage, 2)
        Write-Host "  $($result.method.PadRight(20)) | Avg: ${avg}ms | StdDev: ${stdDev}ms | Fallback: ${fallback}%" -ForegroundColor Gray
    }
    Write-Host ""
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

Write-Host "==================================`n" -ForegroundColor Cyan
Write-Host "🎉 Benchmark API Testing Complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 Notes:" -ForegroundColor Yellow
Write-Host "  - Make sure the app is running (dotnet run --project StaffValidator.WebApp)" -ForegroundColor Gray
Write-Host "  - App should be listening on http://localhost:5000" -ForegroundColor Gray
Write-Host "  - For larger benchmarks, use: Invoke-RestMethod -Uri 'http://localhost:5000/api/benchmark/run?type=email&samples=2000&export=true'" -ForegroundColor Gray
Write-Host ""
