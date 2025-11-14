# Benchmark and Export Results
# Generates CSV and HTML report with charts
# Usage: .\benchmark-and-export.ps1 [-Samples 2000] [-Type "email"]

param(
    [int]$Samples = 2000,
    [string]$Type = "email",
    [string]$OutputDir = "BenchmarkReports",
    [double]$HybridRatio = 0.05
)

$baseUrl = "http://localhost:5000"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "🚀 Running Benchmark: Type=$Type, Samples=$Samples" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Run benchmark with CSV export
Write-Host "📊 Executing benchmark..." -ForegroundColor Yellow
try {
    $uri = "$baseUrl/api/benchmark/run?type=$Type`&samples=$Samples`&export=true"
    if ($Type -eq "email-hybrid") {
        $uri += "`&hybridEvilRatio=$HybridRatio"
    }
    $response = Invoke-RestMethod -Uri $uri -Method Get
    Write-Host "✅ Benchmark completed successfully!" -ForegroundColor Green
    Write-Host "   Duration: $([math]::Round($response.totalDurationMs, 2)) ms" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "❌ Failed to run benchmark: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Make sure the app is running on $baseUrl" -ForegroundColor Yellow
    exit 1
}

# Export to CSV
$csvFile = "$OutputDir\benchmark_${Type}_${Samples}_${timestamp}.csv"
Write-Host "📄 Generating CSV report..." -ForegroundColor Yellow

$csvContent = 'Method,Avg(ms),StdDev(ms),Min(ms),Max(ms),Fallback(%),Accuracy(%),Samples,Success,Failures' + "`n"
foreach ($result in $response.results) {
    $csvContent += "$($result.method),"
    $csvContent += "$([math]::Round($result.avgMs, 4)),"
    $csvContent += "$([math]::Round($result.stdDevMs, 4)),"
    $csvContent += "$([math]::Round($result.minMs, 4)),"
    $csvContent += "$([math]::Round($result.maxMs, 4)),"
    $csvContent += "$([math]::Round($result.fallbackPercentage, 2)),"
    $csvContent += "$([math]::Round($result.accuracyPercentage, 2)),"
    $csvContent += "$($result.totalSamples),"
    $csvContent += "$($result.successCount),"
    $csvContent += "$($result.failureCount)`n"
}

$csvContent | Out-File -FilePath $csvFile -Encoding UTF8
Write-Host "✅ CSV saved: $csvFile" -ForegroundColor Green
Write-Host ""

# Generate HTML report with Chart.js
$htmlFile = "$OutputDir\benchmark_${Type}_${Samples}_${timestamp}.html"
Write-Host "📊 Generating HTML report with charts..." -ForegroundColor Yellow

$methods = ($response.results | ForEach-Object { "'$($_.method)'" }) -join ","
$avgTimes = ($response.results | ForEach-Object { [math]::Round($_.avgMs, 3) }) -join ","
$stdDevs = ($response.results | ForEach-Object { [math]::Round($_.stdDevMs, 3) }) -join ","
$fallbacks = ($response.results | ForEach-Object { [math]::Round($_.fallbackPercentage, 2) }) -join ","

# Generate comparison table
$tableRows = ""
foreach ($result in $response.results) {
    $avgMs = [math]::Round($result.avgMs, 3)
    $stdDev = [math]::Round($result.stdDevMs, 3)
    $minMs = [math]::Round($result.minMs, 3)
    $maxMs = [math]::Round($result.maxMs, 3)
    $fallback = [math]::Round($result.fallbackPercentage, 2)
    
    # Performance indicator
    if ($avgMs -lt 0.5) { 
        $perfIcon = "Green" 
    } elseif ($avgMs -lt 1.0) { 
        $perfIcon = "Yellow" 
    } else { 
        $perfIcon = "Red" 
    }
    
    $tableRows += "<tr>"
    $tableRows += "<td style='color: $perfIcon;'>● $($result.method)</td>"
    $tableRows += "<td><strong>$avgMs</strong> ms</td>"
    $tableRows += "<td>$stdDev ms</td>"
    $tableRows += "<td>$minMs ms</td>"
    $tableRows += "<td>$maxMs ms</td>"
    $tableRows += "<td>$fallback%</td>"
    $tableRows += "<td>$($result.totalSamples)</td>"
    $tableRows += "<td>$($result.successCount)</td>"
    $tableRows += "</tr>"
}

$htmlContent = @'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Benchmark Report</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 20px;
            color: #333;
        }
        .container {
            max-width: 1400px;
            margin: 0 auto;
            background: white;
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }
        h1 {
            color: #667eea;
            margin-bottom: 10px;
            font-size: 2.5em;
            text-align: center;
        }
        .subtitle {
            text-align: center;
            color: #666;
            margin-bottom: 30px;
            font-size: 1.1em;
        }
        .stats {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }
        .stat-card {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 15px;
            text-align: center;
            box-shadow: 0 5px 15px rgba(0,0,0,0.2);
        }
        .stat-card h3 {
            font-size: 0.9em;
            opacity: 0.9;
            margin-bottom: 10px;
        }
        .stat-card .value {
            font-size: 2em;
            font-weight: bold;
        }
        .charts {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 30px;
            margin-bottom: 40px;
        }
        .chart-container {
            background: #f8f9fa;
            padding: 20px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
        }
        .chart-container h2 {
            color: #667eea;
            margin-bottom: 20px;
            font-size: 1.3em;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
        }
        th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
        }
        td {
            padding: 12px 15px;
            border-bottom: 1px solid #eee;
        }
        tr:hover {
            background: #f8f9fa;
        }
        .footer {
            margin-top: 40px;
            text-align: center;
            color: #999;
            font-size: 0.9em;
        }
        .legend {
            margin: 20px 0;
            padding: 15px;
            background: #f8f9fa;
            border-radius: 10px;
            border-left: 4px solid #667eea;
        }
        .legend h3 {
            color: #667eea;
            margin-bottom: 10px;
        }
        @media print {
            body { background: white; padding: 0; }
            .container { box-shadow: none; }
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>📊 Validation Benchmark Report</h1>
        <div class="subtitle">
            Dataset: <strong>TYPE_PLACEHOLDER</strong> | 
            Samples: <strong>SAMPLES_PLACEHOLDER</strong> | 
            Date: <strong>DATE_PLACEHOLDER</strong>
        </div>

        <div class="stats">
            <div class="stat-card">
                <h3>Total Duration</h3>
                <div class="value">DURATION_PLACEHOLDER ms</div>
            </div>
            <div class="stat-card">
                <h3>Total Samples</h3>
                <div class="value">SAMPLES_PLACEHOLDER</div>
            </div>
            <div class="stat-card">
                <h3>Methods Tested</h3>
                <div class="value">COUNT_PLACEHOLDER</div>
            </div>
            <div class="stat-card">
                <h3>Best Avg Time</h3>
                <div class="value">BEST_TIME_PLACEHOLDER ms</div>
            </div>
        </div>

        <div class="charts">
            <div class="chart-container">
                <h2>⚡ Average Execution Time</h2>
                <canvas id="avgTimeChart"></canvas>
            </div>
            <div class="chart-container">
                <h2>📊 Standard Deviation</h2>
                <canvas id="stdDevChart"></canvas>
            </div>
            <div class="chart-container">
                <h2>🔄 DFA Fallback Rate</h2>
                <canvas id="fallbackChart"></canvas>
            </div>
            <div class="chart-container">
                <h2>⏱️ Min vs Max Times</h2>
                <canvas id="minMaxChart"></canvas>
            </div>
        </div>

        <div class="legend">
            <h3>📖 Method Descriptions</h3>
            <ul>
                <li><strong>Regex Uncached:</strong> Creates new Regex instance for each validation (baseline)</li>
                <li><strong>Regex Cached:</strong> Pre-compiled Regex with caching (recommended for production)</li>
                <li><strong>Hybrid (Regex→DFA):</strong> Tries Regex first, falls back to DFA on timeout (ReDoS resistant)</li>
                <li><strong>DFA Only:</strong> Pure deterministic finite automaton validation (maximum security)</li>
            </ul>
        </div>

        <h2 style="color: #667eea; margin-top: 40px; margin-bottom: 20px;">📈 Detailed Results</h2>
        <table>
            <thead>
                <tr>
                    <th>Method</th>
                    <th>Avg Time</th>
                    <th>Std Dev</th>
                    <th>Min Time</th>
                    <th>Max Time</th>
                    <th>Fallback %</th>
                    <th>Samples</th>
                    <th>Success</th>
                </tr>
            </thead>
            <tbody>
                TABLE_ROWS_PLACEHOLDER
            </tbody>
        </table>

        <div class="footer">
            <p>Generated by StaffValidator Benchmark Tool | DATE_PLACEHOLDER</p>
            <p>Machine: COMPUTER_NAME_PLACEHOLDER | Processor: PROCESSOR_COUNT_PLACEHOLDER cores</p>
        </div>
    </div>

    <script>
        // Chart.js configuration
        const chartColors = {
            purple: 'rgba(102, 126, 234, 0.8)',
            pink: 'rgba(118, 75, 162, 0.8)',
            blue: 'rgba(54, 162, 235, 0.8)',
            green: 'rgba(75, 192, 192, 0.8)',
            red: 'rgba(255, 99, 132, 0.8)'
        };

        const methods = [METHODS_PLACEHOLDER];
        const avgTimes = [AVG_TIMES_PLACEHOLDER];
        const stdDevs = [STD_DEVS_PLACEHOLDER];
        const fallbacks = [FALLBACKS_PLACEHOLDER];
        const minTimes = [MIN_TIMES_PLACEHOLDER];
        const maxTimes = [MAX_TIMES_PLACEHOLDER];

        // Avg Time Chart
        new Chart(document.getElementById('avgTimeChart'), {
            type: 'bar',
            data: {
                labels: methods,
                datasets: [{
                    label: 'Average Time (ms)',
                    data: avgTimes,
                    backgroundColor: [chartColors.purple, chartColors.pink, chartColors.blue, chartColors.green],
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    y: { beginAtZero: true, title: { display: true, text: 'Time (ms)' } }
                }
            }
        });

        // Std Dev Chart
        new Chart(document.getElementById('stdDevChart'), {
            type: 'bar',
            data: {
                labels: methods,
                datasets: [{
                    label: 'Standard Deviation (ms)',
                    data: stdDevs,
                    backgroundColor: chartColors.pink,
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    y: { beginAtZero: true, title: { display: true, text: 'Std Dev (ms)' } }
                }
            }
        });

        // Fallback Chart
        new Chart(document.getElementById('fallbackChart'), {
            type: 'doughnut',
            data: {
                labels: methods,
                datasets: [{
                    label: 'Fallback %',
                    data: fallbacks,
                    backgroundColor: [chartColors.purple, chartColors.pink, chartColors.blue, chartColors.green],
                    borderWidth: 3,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { position: 'bottom' }
                }
            }
        });

        // Min vs Max Chart
        new Chart(document.getElementById('minMaxChart'), {
            type: 'line',
            data: {
                labels: methods,
                datasets: [{
                    label: 'Min Time (ms)',
                    data: minTimes,
                    borderColor: chartColors.green,
                    backgroundColor: 'rgba(75, 192, 192, 0.2)',
                    tension: 0.4
                }, {
                    label: 'Max Time (ms)',
                    data: maxTimes,
                    borderColor: chartColors.red,
                    backgroundColor: 'rgba(255, 99, 132, 0.2)',
                    tension: 0.4
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: { beginAtZero: true, title: { display: true, text: 'Time (ms)' } }
                }
            }
        });
    </script>
</body>
</html>
'@

# Replace placeholders in HTML
$htmlContent = $htmlContent.Replace('TYPE_PLACEHOLDER', $Type.ToUpper())
$htmlContent = $htmlContent.Replace('SAMPLES_PLACEHOLDER', $Samples.ToString())
$htmlContent = $htmlContent.Replace('DATE_PLACEHOLDER', (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
$htmlContent = $htmlContent.Replace('DURATION_PLACEHOLDER', [math]::Round($response.totalDurationMs, 0).ToString())
$htmlContent = $htmlContent.Replace('COUNT_PLACEHOLDER', $response.results.Count.ToString())
$htmlContent = $htmlContent.Replace('BEST_TIME_PLACEHOLDER', [math]::Round(($response.results | Measure-Object -Property avgMs -Minimum).Minimum, 3).ToString())
$htmlContent = $htmlContent.Replace('TABLE_ROWS_PLACEHOLDER', $tableRows)
$htmlContent = $htmlContent.Replace('METHODS_PLACEHOLDER', $methods)
$htmlContent = $htmlContent.Replace('AVG_TIMES_PLACEHOLDER', $avgTimes)
$htmlContent = $htmlContent.Replace('STD_DEVS_PLACEHOLDER', $stdDevs)
$htmlContent = $htmlContent.Replace('FALLBACKS_PLACEHOLDER', $fallbacks)
$minTimes = ($response.results | ForEach-Object { [math]::Round($_.minMs, 3) }) -join ","
$maxTimes = ($response.results | ForEach-Object { [math]::Round($_.maxMs, 3) }) -join ","
$htmlContent = $htmlContent.Replace('MIN_TIMES_PLACEHOLDER', $minTimes)
$htmlContent = $htmlContent.Replace('MAX_TIMES_PLACEHOLDER', $maxTimes)
$htmlContent = $htmlContent.Replace('COMPUTER_NAME_PLACEHOLDER', $env:COMPUTERNAME)
$htmlContent = $htmlContent.Replace('PROCESSOR_COUNT_PLACEHOLDER', $env:NUMBER_OF_PROCESSORS)

$htmlContent | Out-File -FilePath $htmlFile -Encoding UTF8
Write-Host "✅ HTML report saved: $htmlFile" -ForegroundColor Green
Write-Host ""

# Print summary
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "📊 Benchmark Summary" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

foreach ($result in $response.results) {
    $avgMs = [math]::Round($result.avgMs, 3)
    $fallback = [math]::Round($result.fallbackPercentage, 2)
    if ($avgMs -lt 0.5) { 
        $perfEmoji = 'FAST' 
    } elseif ($avgMs -lt 1.0) { 
        $perfEmoji = 'OK  ' 
    } else { 
        $perfEmoji = 'SLOW' 
    }
    
    $methodName = $result.method.PadRight(25)
    Write-Host "[$perfEmoji] $methodName | Avg: ${avgMs}ms | Fallback: ${fallback}%" -ForegroundColor Gray
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "✅ Reports Generated:" -ForegroundColor Green
Write-Host "   📄 CSV:  $csvFile" -ForegroundColor White
Write-Host "   📊 HTML: $htmlFile" -ForegroundColor White
Write-Host "`n💡 Open HTML file in browser to view charts!" -ForegroundColor Yellow
Write-Host ""

# Auto-open HTML report
$openReport = Read-Host "Open HTML report now? (Y/n)"
if ($openReport -ne "n" -and $openReport -ne "N") {
    Start-Process $htmlFile
}
