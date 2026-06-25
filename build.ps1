# R2WAI Build & Run Script
# Run from: C:\Users\LENOVO\Bots\R2WAI
# Usage: .\build.ps1

$ErrorActionPreference = "Continue"
$LogFile = "$PSScriptRoot\build.log"
$SolutionDir = $PSScriptRoot

Write-Host "=== R2WAI Build ===" -ForegroundColor Cyan
Write-Host "Log: $LogFile"
Write-Host ""

# Restore
Write-Host "[1/3] Restoring packages..." -ForegroundColor Yellow
& dotnet restore "$SolutionDir\R2WAI.slnx" 2>&1 | Tee-Object -FilePath $LogFile

# Build
Write-Host ""
Write-Host "[2/3] Building solution..." -ForegroundColor Yellow
& dotnet build "$SolutionDir\R2WAI.slnx" --no-restore -c Debug 2>&1 | Tee-Object -FilePath $LogFile -Append

$BuildResult = $LASTEXITCODE

if ($BuildResult -ne 0) {
    Write-Host ""
    Write-Host "BUILD FAILED - see build.log for errors." -ForegroundColor Red
    Write-Host "Errors summary:" -ForegroundColor Red
    Select-String -Path $LogFile -Pattern "error CS" | Select-Object -First 30
    exit 1
}

Write-Host ""
Write-Host "BUILD SUCCEEDED" -ForegroundColor Green

# Optional run
$Run = Read-Host "[3/3] Start the API? (y/n)"
if ($Run -eq "y") {
    Write-Host "Starting R2WAI.Api..." -ForegroundColor Cyan
    & dotnet run --project "$SolutionDir\src\R2WAI.Api\R2WAI.Api.csproj" --no-build
}
