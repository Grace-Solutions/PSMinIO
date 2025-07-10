# Test script for PSMinIO chunked operations
# This script tests if the module can be imported and basic functionality works

param(
    [string]$ModulePath = "Artifacts\PSMinIO\PSMinIO.psd1"
)

Write-Host "=== PSMinIO Chunked Module Test ===" -ForegroundColor Cyan

try {
    # Test 1: Import Module
    Write-Host "`n--- Test 1: Module Import ---" -ForegroundColor Yellow
    
    if (!(Test-Path $ModulePath)) {
        throw "Module manifest not found: $ModulePath"
    }
    
    # Remove any existing module
    Get-Module PSMinIO | Remove-Module -Force -ErrorAction SilentlyContinue
    
    # Import the module
    Import-Module $ModulePath -Force -ErrorAction Stop
    Write-Host "✓ Module imported successfully" -ForegroundColor Green
    
    # Test 2: Check Available Cmdlets
    Write-Host "`n--- Test 2: Available Cmdlets ---" -ForegroundColor Yellow
    
    $expectedCmdlets = @(
        'Connect-MinIO',
        'Get-MinIOBucket',
        'New-MinIOBucket',
        'Remove-MinIOBucket',
        'Test-MinIOBucketExists',
        'Get-MinIOObject',
        'New-MinIOObject',
        'New-MinIOObjectChunked',
        'New-MinIOFolder',
        'Get-MinIOObjectContent',
        'Get-MinIOObjectContentChunked',
        'Remove-MinIOObject',
        'Get-MinIOBucketPolicy',
        'Set-MinIOBucketPolicy',
        'Get-MinIOStats'
    )
    
    $availableCmdlets = Get-Command -Module PSMinIO | Select-Object -ExpandProperty Name
    
    foreach ($cmdlet in $expectedCmdlets) {
        if ($cmdlet -in $availableCmdlets) {
            Write-Host "✓ $cmdlet" -ForegroundColor Green
        } else {
            Write-Host "✗ $cmdlet (MISSING)" -ForegroundColor Red
        }
    }
    
    Write-Host "`nTotal cmdlets available: $($availableCmdlets.Count)" -ForegroundColor Cyan
    
    # Test 3: Check Chunked Cmdlets Specifically
    Write-Host "`n--- Test 3: Chunked Cmdlets ---" -ForegroundColor Yellow
    
    $chunkedCmdlets = @('New-MinIOObjectChunked', 'Get-MinIOObjectContentChunked')
    
    foreach ($cmdlet in $chunkedCmdlets) {
        try {
            $cmdletInfo = Get-Command $cmdlet -ErrorAction Stop
            Write-Host "✓ $cmdlet" -ForegroundColor Green
            
            # Check parameters
            $parameters = $cmdletInfo.Parameters.Keys | Where-Object { $_ -notin @('Verbose', 'Debug', 'ErrorAction', 'WarningAction', 'InformationAction', 'ErrorVariable', 'WarningVariable', 'InformationVariable', 'OutVariable', 'OutBuffer', 'PipelineVariable', 'ProgressAction') }
            Write-Host "  Parameters: $($parameters.Count)" -ForegroundColor Gray
            
            # Check for chunked-specific parameters
            $chunkedParams = @('ChunkSize', 'Resume', 'MaxRetries')
            foreach ($param in $chunkedParams) {
                if ($param -in $parameters) {
                    Write-Host "  ✓ $param" -ForegroundColor Green
                } else {
                    Write-Host "  ✗ $param (MISSING)" -ForegroundColor Red
                }
            }
        }
        catch {
            Write-Host "✗ $cmdlet (ERROR: $($_.Exception.Message))" -ForegroundColor Red
        }
    }
    
    # Test 4: Check Help Documentation
    Write-Host "`n--- Test 4: Help Documentation ---" -ForegroundColor Yellow
    
    foreach ($cmdlet in $chunkedCmdlets) {
        try {
            $help = Get-Help $cmdlet -ErrorAction Stop
            if ($help.Synopsis -and $help.Synopsis -ne $cmdlet) {
                Write-Host "✓ $cmdlet has help documentation" -ForegroundColor Green
            } else {
                Write-Host "⚠ $cmdlet has minimal help" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "✗ $cmdlet help error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # Test 5: Parameter Validation
    Write-Host "`n--- Test 5: Parameter Validation ---" -ForegroundColor Yellow
    
    try {
        # Test New-MinIOObjectChunked parameter validation
        $cmd = Get-Command New-MinIOObjectChunked
        
        # Check ChunkSize validation
        $chunkSizeParam = $cmd.Parameters['ChunkSize']
        if ($chunkSizeParam.Attributes | Where-Object { $_.TypeId.Name -eq 'ValidateRangeAttribute' }) {
            Write-Host "✓ ChunkSize has range validation" -ForegroundColor Green
        } else {
            Write-Host "⚠ ChunkSize missing range validation" -ForegroundColor Yellow
        }
        
        # Check MaxRetries validation
        $maxRetriesParam = $cmd.Parameters['MaxRetries']
        if ($maxRetriesParam.Attributes | Where-Object { $_.TypeId.Name -eq 'ValidateRangeAttribute' }) {
            Write-Host "✓ MaxRetries has range validation" -ForegroundColor Green
        } else {
            Write-Host "⚠ MaxRetries missing range validation" -ForegroundColor Yellow
        }
        
    }
    catch {
        Write-Host "✗ Parameter validation check failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 6: Type Definitions
    Write-Host "`n--- Test 6: Type Definitions ---" -ForegroundColor Yellow
    
    $expectedTypes = @(
        'PSMinIO.Models.ChunkedTransferState',
        'PSMinIO.Models.ChunkInfo'
    )
    
    foreach ($typeName in $expectedTypes) {
        try {
            $type = [Type]::GetType($typeName)
            if ($type) {
                Write-Host "✓ $typeName" -ForegroundColor Green
            } else {
                Write-Host "⚠ $typeName (not loaded)" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "✗ $typeName (error: $($_.Exception.Message))" -ForegroundColor Red
        }
    }
    
    Write-Host "`n=== Test Summary ===" -ForegroundColor Green
    Write-Host "Module Path: $ModulePath" -ForegroundColor Gray
    Write-Host "Available Cmdlets: $($availableCmdlets.Count)" -ForegroundColor Gray
    Write-Host "Chunked Cmdlets: $($chunkedCmdlets.Count)" -ForegroundColor Gray
    
    Write-Host "`n✓ Basic module tests completed successfully!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "1. Set up MinIO server for integration testing" -ForegroundColor White
    Write-Host "2. Run: .\tests\ChunkedOperationsTests.ps1" -ForegroundColor White
    Write-Host "3. Check examples: .\examples\ChunkedOperationsExamples.ps1" -ForegroundColor White
    
}
catch {
    Write-Host "`n=== Test Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    
    # Show module import errors if any
    if ($_.Exception.Message -like "*Import-Module*") {
        Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
        Write-Host "1. Check if all required assemblies are present" -ForegroundColor White
        Write-Host "2. Verify .NET dependencies are installed" -ForegroundColor White
        Write-Host "3. Check PowerShell execution policy" -ForegroundColor White
        Write-Host "4. Try running as Administrator" -ForegroundColor White
    }
    
    exit 1
}
finally {
    # Clean up
    Write-Host "`nCleaning up..." -ForegroundColor Gray
}
