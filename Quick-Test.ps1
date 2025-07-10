# Quick test to check module structure
Write-Host "=== Quick Module Structure Test ===" -ForegroundColor Cyan

# Check if manifest exists
$manifestPath = "Artifacts\PSMinIO\PSMinIO.psd1"
if (Test-Path $manifestPath) {
    Write-Host "✓ Module manifest found: $manifestPath" -ForegroundColor Green
    
    try {
        # Try to read the manifest
        $manifest = Import-PowerShellDataFile $manifestPath
        Write-Host "✓ Manifest can be parsed" -ForegroundColor Green
        Write-Host "  Module Name: $($manifest.ModuleVersion)" -ForegroundColor Gray
        Write-Host "  Cmdlets to Export: $($manifest.CmdletsToExport.Count)" -ForegroundColor Gray
        
        # List chunked cmdlets
        $chunkedCmdlets = $manifest.CmdletsToExport | Where-Object { $_ -like "*Chunked*" }
        Write-Host "  Chunked Cmdlets: $($chunkedCmdlets -join ', ')" -ForegroundColor Cyan
        
    } catch {
        Write-Host "✗ Manifest parse error: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Module manifest not found: $manifestPath" -ForegroundColor Red
}

# Check types file
$typesPath = "Artifacts\PSMinIO\types\PSMinIO.Types.ps1xml"
if (Test-Path $typesPath) {
    Write-Host "✓ Types file found: $typesPath" -ForegroundColor Green
} else {
    Write-Host "✗ Types file not found: $typesPath" -ForegroundColor Red
}

# Check bin directory
$binPath = "Artifacts\PSMinIO\bin"
if (Test-Path $binPath) {
    $binFiles = Get-ChildItem $binPath -File
    Write-Host "✓ Bin directory found with $($binFiles.Count) files" -ForegroundColor Green
    foreach ($file in $binFiles) {
        Write-Host "  - $($file.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ Bin directory not found: $binPath" -ForegroundColor Red
}

Write-Host "`n=== Structure Check Complete ===" -ForegroundColor Green
