<#
.SYNOPSIS
    Automated k6 Performance Regression Gate.
.DESCRIPTION
    Runs load tests against LegalERP API to ensure all p95 response times are under 250ms
    and error rates remain strictly below 1%. Fails with non-zero exit code if violated.
.EXAMPLE
    .\run-performance-gate.ps1
    .\run-performance-gate.ps1 -BaseUrl "https://localhost:7148" -VUs 15 -Duration "20s"
#>
param (
    [string]$BaseUrl = "https://localhost:7148",
    [string]$Duration = "15s",
    [int]$VUs = 10,
    [string]$K6Path = "",
    [switch]$SkipApiCheck
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     LegalERP Automated k6 Performance Regression Gate     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target API   : $BaseUrl" -ForegroundColor Yellow
Write-Host "Virtual Users: $VUs" -ForegroundColor Yellow
Write-Host "Duration     : $Duration" -ForegroundColor Yellow
Write-Host "p95 Limit    : 250 ms" -ForegroundColor Yellow
Write-Host "Max Errors   : < 1%" -ForegroundColor Yellow
Write-Host "----------------------------------------------------------"

# 1. Locate k6 executable
$k6Exe = $K6Path
if (-not $k6Exe) {
    $cmd = Get-Command "k6" -ErrorAction SilentlyContinue
    if ($cmd) {
        $k6Exe = $cmd.Source
    } elseif (Test-Path "C:\Program Files\k6\k6.exe") {
        $k6Exe = "C:\Program Files\k6\k6.exe"
    } elseif (Test-Path "$env:LOCALAPPDATA\Programs\k6\k6.exe") {
        $k6Exe = "$env:LOCALAPPDATA\Programs\k6\k6.exe"
    }
}

if (-not $k6Exe -or -not (Test-Path $k6Exe)) {
    Write-Host "`n[ERROR] k6 executable could not be found!" -ForegroundColor Red
    Write-Host "Please install k6 using winget or chocolatey:" -ForegroundColor Yellow
    Write-Host "    winget install GrafanaLabs.k6" -ForegroundColor White
    Write-Host "or provide the path using -K6Path 'path\to\k6.exe'" -ForegroundColor White
    exit 1
}

Write-Host "[OK] Using k6 binary: $k6Exe" -ForegroundColor Green

# 2. Check if API is reachable
if (-not $SkipApiCheck) {
    Write-Host "[INFO] Checking API connectivity at $BaseUrl..." -ForegroundColor Gray
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        $req = [System.Net.WebRequest]::Create("$BaseUrl/api/cases?pageNumber=1&pageSize=1")
        $req.Timeout = 5000
        $req.Method = "HEAD"
        $null = $req.GetResponse()
        Write-Host "[OK] API is active and responsive." -ForegroundColor Green
    } catch [System.Net.WebException] {
        # 401 Unauthorized or 403 Forbidden means the endpoint is live and responding
        if ($_.Response -and ($_.Response.StatusCode -eq 401 -or $_.Response.StatusCode -eq 403 -or $_.Response.StatusCode -eq 200)) {
            Write-Host "[OK] API is active and responsive." -ForegroundColor Green
        } else {
            Write-Host "`n[WARNING] Could not connect to API at $BaseUrl" -ForegroundColor Yellow
            Write-Host "Make sure the API project is running:" -ForegroundColor Yellow
            Write-Host "    cd LegalERP.Api" -ForegroundColor White
            Write-Host "    dotnet run" -ForegroundColor White
            Write-Host "To bypass this check, pass -SkipApiCheck" -ForegroundColor Gray
        }
    } catch {
        Write-Host "`n[WARNING] API connectivity check error: $_" -ForegroundColor Yellow
    }
}

# 3. Locate script
$scriptPath = Join-Path $PSScriptRoot ".scratch\k6-performance\performance-gate.js"
if (-not (Test-Path $scriptPath)) {
    Write-Host "`n[ERROR] Benchmark script not found at: $scriptPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n[INFO] Starting k6 performance test..." -ForegroundColor Cyan

# 4. Run k6
$process = Start-Process -FilePath $k6Exe -ArgumentList @(
    "run",
    "--insecure-skip-tls-verify",
    "--env", "BASE_API_URL=$BaseUrl",
    "--env", "DURATION=$Duration",
    "--env", "VUS=$VUs",
    $scriptPath
) -NoNewWindow -PassThru -Wait

$exitCode = $process.ExitCode

Write-Host "----------------------------------------------------------"
if ($exitCode -eq 0) {
    Write-Host " SUCCESS: PERFORMANCE REGRESSION GATE PASSED! " -ForegroundColor Black -BackgroundColor Green
    Write-Host "All p95 latencies are below 250ms and error rates are under 1%." -ForegroundColor Green
    exit 0
} else {
    Write-Host " FAILURE: PERFORMANCE REGRESSION GATE FAILED! " -ForegroundColor White -BackgroundColor Red
    Write-Host "One or more performance thresholds were violated (p95 > 250ms or errors > 1%)." -ForegroundColor Red
    exit $exitCode
}
