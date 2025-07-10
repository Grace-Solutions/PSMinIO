#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple test script to verify PSMinIO module functionality

.DESCRIPTION
    This script performs basic tests to ensure the PSMinIO module can be built, loaded, and basic functionality works.

.PARAMETER ModulePath
    Path to the module manifest file. Default: Module\PSMinIO\PSMinIO.psd1

.PARAMETER SkipBuild
    Skip the build step and test existing module

.EXAMPLE
    .\Test-Module.ps1
    
.EXAMPLE
    .\Test-Module.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [string]$ModulePath = 'Module\PSMinIO\PSMinIO.psd1',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

Write-Host "=== PSMinIO Module Test ===" -ForegroundColor Cyan
Write-Host ""

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Success,
        [string]$Message = ""
    )
    
    $status = if ($Success) { "✓ PASS" } else { "✗ FAIL" }
    $color = if ($Success) { "Green" } else { "Red" }
    
    Write-Host "$status - $TestName" -ForegroundColor $color
    if ($Message) {
        Write-Host "    $Message" -ForegroundColor Gray
    }
}

$testResults = @{}

try {
    # Test 1: Build the module (unless skipped)
    if (!$SkipBuild) {
        Write-Host "Building module..." -ForegroundColor Yellow
        
        try {
            & .\Build.ps1 -Configuration Release
            $testResults['Build'] = $true
            Write-TestResult "Build Module" $true
        } catch {
            $testResults['Build'] = $false
            Write-TestResult "Build Module" $false $_.Exception.Message
            throw "Build failed, cannot continue with tests"
        }
    } else {
        Write-Host "Skipping build step..." -ForegroundColor Yellow
        $testResults['Build'] = $null
    }
    
    Write-Host ""
    
    # Test 2: Check if module files exist
    Write-Host "Checking module files..." -ForegroundColor Yellow
    
    $moduleExists = Test-Path $ModulePath
    $testResults['ModuleFiles'] = $moduleExists
    Write-TestResult "Module Manifest Exists" $moduleExists $ModulePath
    
    if (!$moduleExists) {
        throw "Module manifest not found: $ModulePath"
    }
    
    # Check for required DLL
    $dllPath = Join-Path (Split-Path $ModulePath) 'bin\PSMinIO.dll'
    $dllExists = Test-Path $dllPath
    $testResults['ModuleDLL'] = $dllExists
    Write-TestResult "Module DLL Exists" $dllExists $dllPath
    
    Write-Host ""
    
    # Test 3: Test module manifest
    Write-Host "Testing module manifest..." -ForegroundColor Yellow
    
    try {
        $moduleInfo = Test-ModuleManifest -Path $ModulePath -ErrorAction Stop
        $testResults['Manifest'] = $true
        Write-TestResult "Module Manifest Valid" $true "Version: $($moduleInfo.Version)"
        
        # Check cmdlet count
        $cmdletCount = $moduleInfo.ExportedCmdlets.Count
        $expectedCmdlets = 13 # Expected number of cmdlets
        $cmdletCountOk = $cmdletCount -eq $expectedCmdlets
        $testResults['CmdletCount'] = $cmdletCountOk
        Write-TestResult "Cmdlet Count" $cmdletCountOk "Found: $cmdletCount, Expected: $expectedCmdlets"
        
    } catch {
        $testResults['Manifest'] = $false
        Write-TestResult "Module Manifest Valid" $false $_.Exception.Message
    }
    
    Write-Host ""
    
    # Test 4: Import module
    Write-Host "Importing module..." -ForegroundColor Yellow
    
    try {
        # Remove module if already loaded
        if (Get-Module PSMinIO -ErrorAction SilentlyContinue) {
            Remove-Module PSMinIO -Force
        }
        
        Import-Module $ModulePath -Force -ErrorAction Stop
        $testResults['Import'] = $true
        Write-TestResult "Import Module" $true
        
        # Check if cmdlets are available
        $availableCmdlets = Get-Command -Module PSMinIO
        $cmdletAvailable = $availableCmdlets.Count -gt 0
        $testResults['CmdletsAvailable'] = $cmdletAvailable
        Write-TestResult "Cmdlets Available" $cmdletAvailable "Count: $($availableCmdlets.Count)"
        
    } catch {
        $testResults['Import'] = $false
        Write-TestResult "Import Module" $false $_.Exception.Message
    }
    
    Write-Host ""
    
    # Test 5: Test basic cmdlet functionality
    Write-Host "Testing basic cmdlet functionality..." -ForegroundColor Yellow
    
    try {
        # Test Get-MinIOConfig (should work without configuration)
        $config = Get-MinIOConfig -ErrorAction Stop
        $configTest = $config -ne $null
        $testResults['GetConfig'] = $configTest
        Write-TestResult "Get-MinIOConfig" $configTest
        
        # Test configuration validation
        $configValid = $config.IsValid
        $testResults['ConfigValid'] = !$configValid # Should be false initially
        Write-TestResult "Config Initially Invalid" (!$configValid) "Expected: not configured yet"
        
    } catch {
        $testResults['GetConfig'] = $false
        Write-TestResult "Get-MinIOConfig" $false $_.Exception.Message
    }
    
    try {
        # Test Set-MinIOConfig with test parameters (WhatIf)
        Set-MinIOConfig -Endpoint "test.example.com" `
                        -AccessKey "test-key" `
                        -SecretKey "test-secret" `
                        -WhatIf -ErrorAction Stop
        $testResults['SetConfig'] = $true
        Write-TestResult "Set-MinIOConfig (WhatIf)" $true
        
    } catch {
        $testResults['SetConfig'] = $false
        Write-TestResult "Set-MinIOConfig (WhatIf)" $false $_.Exception.Message
    }
    
    try {
        # Test help system
        $help = Get-Help Get-MinIOBucket -ErrorAction Stop
        $helpTest = $help -ne $null -and $help.Synopsis -ne $null
        $testResults['Help'] = $helpTest
        Write-TestResult "Help System" $helpTest
        
    } catch {
        $testResults['Help'] = $false
        Write-TestResult "Help System" $false $_.Exception.Message
    }
    
    Write-Host ""
    
    # Test 6: Test parameter validation
    Write-Host "Testing parameter validation..." -ForegroundColor Yellow
    
    try {
        # Test invalid bucket name validation
        $errorCaught = $false
        try {
            New-MinIOBucket -BucketName "" -WhatIf -ErrorAction Stop
        } catch {
            $errorCaught = $true
        }
        
        $testResults['Validation'] = $errorCaught
        Write-TestResult "Parameter Validation" $errorCaught "Empty bucket name should fail"
        
    } catch {
        $testResults['Validation'] = $false
        Write-TestResult "Parameter Validation" $false $_.Exception.Message
    }
    
    Write-Host ""
    
    # Summary
    Write-Host "=== Test Summary ===" -ForegroundColor Cyan
    
    $totalTests = $testResults.Count
    $passedTests = ($testResults.Values | Where-Object { $_ -eq $true }).Count
    $failedTests = ($testResults.Values | Where-Object { $_ -eq $false }).Count
    $skippedTests = ($testResults.Values | Where-Object { $_ -eq $null }).Count
    
    Write-Host "Total Tests: $totalTests" -ForegroundColor Gray
    Write-Host "Passed: $passedTests" -ForegroundColor Green
    Write-Host "Failed: $failedTests" -ForegroundColor Red
    Write-Host "Skipped: $skippedTests" -ForegroundColor Yellow
    
    Write-Host ""
    
    # Detailed results
    foreach ($test in $testResults.GetEnumerator()) {
        $status = switch ($test.Value) {
            $true { "PASS" }
            $false { "FAIL" }
            $null { "SKIP" }
        }
        $color = switch ($test.Value) {
            $true { "Green" }
            $false { "Red" }
            $null { "Yellow" }
        }
        Write-Host "  $($test.Key): $status" -ForegroundColor $color
    }
    
    Write-Host ""
    
    if ($failedTests -eq 0) {
        Write-Host "=== All Tests Passed! ===" -ForegroundColor Green
        Write-Host ""
        Write-Host "The PSMinIO module is ready for use." -ForegroundColor Cyan
        Write-Host "To get started, run:" -ForegroundColor Gray
        Write-Host "  Get-Help about_PSMinIO" -ForegroundColor White
        Write-Host "  Get-Command -Module PSMinIO" -ForegroundColor White
    } else {
        Write-Host "=== Some Tests Failed ===" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please review the failed tests and fix any issues before using the module." -ForegroundColor Yellow
        exit 1
    }
    
} catch {
    Write-Host ""
    Write-Host "=== Test Execution Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
} finally {
    # Clean up - remove module if loaded
    if (Get-Module PSMinIO -ErrorAction SilentlyContinue) {
        Remove-Module PSMinIO -Force
    }
}
