# Quick-Build.ps1
# Simple build script that handles file locking issues

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ProjectRoot = Split-Path $PSScriptRoot -Parent

Write-Host "PSMinIO Quick Build" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan

# Update version first
Write-Host "Updating version..." -ForegroundColor Yellow
& "$PSScriptRoot\Update-Version.ps1"

# Clean build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
dotnet clean "$ProjectRoot\PSMinIO.csproj" --configuration $Configuration --verbosity quiet

# Build without copying to avoid file locks
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build "$ProjectRoot\PSMinIO.csproj" --configuration $Configuration --verbosity minimal --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Manual copy to avoid file locking issues
Write-Host "Copying files manually..." -ForegroundColor Yellow

$buildOutput = "$ProjectRoot\bin\$Configuration\netstandard2.0"
$moduleDir = "$ProjectRoot\Module\PSMinIO\bin"

# Ensure module directory exists
if (!(Test-Path $moduleDir)) {
    New-Item -ItemType Directory -Path $moduleDir -Force | Out-Null
}

# Copy files with retry logic
$filesToCopy = @(
    @{ Source = "$buildOutput\PSMinIO.dll"; Dest = "$moduleDir\PSMinIO.dll" }
    @{ Source = "$buildOutput\PSMinIO.pdb"; Dest = "$moduleDir\PSMinIO.pdb" }
    @{ Source = "$buildOutput\Minio.dll"; Dest = "$moduleDir\Minio.dll" }
)

foreach ($file in $filesToCopy) {
    $retryCount = 0
    $maxRetries = 3
    $copied = $false
    
    while (-not $copied -and $retryCount -lt $maxRetries) {
        try {
            if (Test-Path $file.Dest) {
                Remove-Item $file.Dest -Force -ErrorAction Stop
            }
            Copy-Item $file.Source $file.Dest -Force -ErrorAction Stop
            Write-Host "  ✓ Copied: $(Split-Path $file.Source -Leaf)" -ForegroundColor Green
            $copied = $true
        } catch {
            $retryCount++
            if ($retryCount -lt $maxRetries) {
                Write-Host "  Retry $retryCount for $(Split-Path $file.Source -Leaf)..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
            } else {
                Write-Warning "  Failed to copy $(Split-Path $file.Source -Leaf): $($_.Exception.Message)"
            }
        }
    }
}

# Test the module
Write-Host "Testing module..." -ForegroundColor Yellow
try {
    $manifest = Test-ModuleManifest "$ProjectRoot\Module\PSMinIO\PSMinIO.psd1" -ErrorAction Stop
    Write-Host "  ✓ Module is valid: $($manifest.Name) v$($manifest.Version)" -ForegroundColor Green
} catch {
    Write-Warning "  Module validation failed: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "✅ Quick build completed!" -ForegroundColor Green
Write-Host "To import: Import-Module .\Module\PSMinIO\PSMinIO.psd1" -ForegroundColor Cyan
