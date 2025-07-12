# Force update the PSMinIO DLL with the enhanced version

Write-Host "=== Updating PSMinIO DLL ===" -ForegroundColor Cyan

try {
    # Stop all PowerShell processes to release the DLL
    Write-Host "1. Stopping PowerShell processes..." -ForegroundColor Yellow
    Get-Process | Where-Object {$_.ProcessName -like "*powershell*" -or $_.ProcessName -like "*pwsh*"} | ForEach-Object {
        Write-Host "  Stopping process: $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray
        try {
            $_ | Stop-Process -Force -ErrorAction SilentlyContinue
        } catch {
            Write-Host "    Could not stop process $($_.Id): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    # Wait a moment for processes to fully stop
    Start-Sleep -Seconds 3
    
    # Copy the new DLL
    Write-Host "2. Copying enhanced DLL..." -ForegroundColor Yellow
    $sourceDLL = "bin\Release\netstandard2.0\PSMinIO.dll"
    $targetDLL = "Module\PSMinIO\bin\PSMinIO.dll"
    
    if (Test-Path $sourceDLL) {
        Copy-Item $sourceDLL $targetDLL -Force
        Write-Host "✅ DLL updated successfully!" -ForegroundColor Green
        
        # Verify the copy
        $sourceSize = (Get-Item $sourceDLL).Length
        $targetSize = (Get-Item $targetDLL).Length
        Write-Host "   Source size: $($sourceSize) bytes" -ForegroundColor Gray
        Write-Host "   Target size: $($targetSize) bytes" -ForegroundColor Gray
        
        if ($sourceSize -eq $targetSize) {
            Write-Host "✅ File sizes match - copy successful!" -ForegroundColor Green
        } else {
            Write-Host "⚠️ File sizes don't match - copy may have failed!" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ Source DLL not found: $sourceDLL" -ForegroundColor Red
        Write-Host "   Run 'dotnet build PSMinIO.csproj --configuration Release' first" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "❌ Update failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Update Complete ===" -ForegroundColor Cyan
Write-Host "You can now test the enhanced functionality with:" -ForegroundColor Yellow
Write-Host "  .\scripts\Test-50-Files-Upload.ps1 -Verbose" -ForegroundColor White
