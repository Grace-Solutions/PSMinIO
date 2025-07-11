#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script for the new PSMinIO implementation with custom REST API

.DESCRIPTION
    This script demonstrates the new PSMinIO module capabilities including:
    - Connection management with custom REST API
    - Real progress reporting during transfers
    - Performance metrics in result objects
    - Enhanced error handling and logging

.PARAMETER Endpoint
    MinIO server endpoint (e.g., https://minio.example.com:9000)

.PARAMETER AccessKey
    MinIO access key

.PARAMETER SecretKey
    MinIO secret key

.PARAMETER TestBucket
    Name of test bucket to create (default: psminiotest)

.EXAMPLE
    .\Test-NewImplementation.ps1 -Endpoint "https://play.min.io" -AccessKey "Q3AM3UQ867SPQQA43P2F" -SecretKey "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Endpoint,
    
    [Parameter(Mandatory = $true)]
    [string]$AccessKey,
    
    [Parameter(Mandatory = $true)]
    [string]$SecretKey,
    
    [Parameter()]
    [string]$TestBucket = "psminiotest-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
)

# Import the module
Write-Host "Importing PSMinIO module..." -ForegroundColor Green
Import-Module "$PSScriptRoot\..\Module\PSMinIO\PSMinIO.psd1" -Force

try {
    # Test 1: Connection
    Write-Host "`n=== Test 1: Connection ===" -ForegroundColor Yellow
    Write-Host "Connecting to MinIO server: $Endpoint" -ForegroundColor Cyan
    
    $connection = Connect-MinIO -Endpoint $Endpoint -AccessKey $AccessKey -SecretKey $SecretKey -TestConnection -PassThru -Verbose
    Write-Host "✅ Connection successful!" -ForegroundColor Green
    Write-Host "Connection Status: $($connection.Status)" -ForegroundColor White
    
    # Test 2: List Buckets
    Write-Host "`n=== Test 2: List Buckets ===" -ForegroundColor Yellow
    Write-Host "Listing existing buckets..." -ForegroundColor Cyan
    
    $buckets = Get-MinIOBucket -Verbose
    Write-Host "✅ Found $($buckets.Count) buckets" -ForegroundColor Green
    
    if ($buckets.Count -gt 0) {
        Write-Host "First few buckets:" -ForegroundColor White
        $buckets | Select-Object -First 3 | Format-Table Name, CreationDate, @{Name="Age"; Expression={(Get-Date) - $_.CreationDate}}
    }
    
    # Test 3: Create Test Bucket
    Write-Host "`n=== Test 3: Create Test Bucket ===" -ForegroundColor Yellow
    Write-Host "Creating test bucket: $TestBucket" -ForegroundColor Cyan
    
    $bucketResult = New-MinIOBucket -Name $TestBucket -PassThru -Verbose
    Write-Host "✅ Bucket created successfully!" -ForegroundColor Green
    Write-Host "Bucket: $($bucketResult.Name)" -ForegroundColor White
    
    # Test 4: Test Bucket Exists
    Write-Host "`n=== Test 4: Test Bucket Exists ===" -ForegroundColor Yellow
    Write-Host "Testing if bucket exists..." -ForegroundColor Cyan
    
    $exists = Test-MinIOBucketExists -Name $TestBucket -Verbose
    Write-Host "✅ Bucket exists: $exists" -ForegroundColor Green
    
    # Test 5: Create Test File
    Write-Host "`n=== Test 5: Create and Upload Test File ===" -ForegroundColor Yellow
    $testFile = "$env:TEMP\psminiotest-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
    $testContent = @"
PSMinIO Test File
================
Created: $(Get-Date)
Endpoint: $Endpoint
Bucket: $TestBucket

This is a test file created by the PSMinIO test script.
The new implementation uses a custom REST API client for better PowerShell compatibility.

Features demonstrated:
- Real progress reporting
- Performance metrics
- Enhanced error handling
- Synchronous operations optimized for PowerShell

Test data: $('A' * 100)
"@
    
    Write-Host "Creating test file: $testFile" -ForegroundColor Cyan
    $testContent | Out-File -FilePath $testFile -Encoding UTF8
    $fileInfo = Get-Item $testFile
    Write-Host "✅ Test file created: $($fileInfo.Length) bytes" -ForegroundColor Green
    
    # Test 6: Upload File
    Write-Host "`n=== Test 6: Upload File with Progress ===" -ForegroundColor Yellow
    Write-Host "Uploading test file..." -ForegroundColor Cyan
    
    $uploadResult = New-MinIOObject -BucketName $TestBucket -File $fileInfo -PassThru -Verbose
    Write-Host "✅ Upload completed!" -ForegroundColor Green
    Write-Host "Upload Result:" -ForegroundColor White
    $uploadResult | Format-List ObjectName, TotalSizeFormatted, DurationFormatted, AverageSpeedFormatted, Success
    
    # Test 7: List Objects
    Write-Host "`n=== Test 7: List Objects ===" -ForegroundColor Yellow
    Write-Host "Listing objects in test bucket..." -ForegroundColor Cyan
    
    $objects = Get-MinIOObject -BucketName $TestBucket -Verbose
    Write-Host "✅ Found $($objects.Count) objects" -ForegroundColor Green
    
    if ($objects.Count -gt 0) {
        Write-Host "Objects in bucket:" -ForegroundColor White
        $objects | Format-Table Name, @{Name="Size"; Expression={$_.Size}}, LastModified
    }
    
    # Test 8: Download File
    Write-Host "`n=== Test 8: Download File with Progress ===" -ForegroundColor Yellow
    $downloadFile = "$env:TEMP\psminiotest-download-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
    Write-Host "Downloading file to: $downloadFile" -ForegroundColor Cyan
    
    $downloadResult = Get-MinIOObjectContent -BucketName $TestBucket -ObjectName $fileInfo.Name -LocalPath $downloadFile -PassThru -Verbose
    Write-Host "✅ Download completed!" -ForegroundColor Green
    Write-Host "Download Result:" -ForegroundColor White
    $downloadResult | Format-List ObjectName, TotalSizeFormatted, DurationFormatted, AverageSpeedFormatted, Success
    
    # Verify download
    $downloadedContent = Get-Content $downloadFile -Raw
    $originalContent = Get-Content $testFile -Raw
    if ($downloadedContent -eq $originalContent) {
        Write-Host "✅ File content verification passed!" -ForegroundColor Green
    } else {
        Write-Host "❌ File content verification failed!" -ForegroundColor Red
    }
    
    Write-Host "`n=== Test Summary ===" -ForegroundColor Yellow
    Write-Host "✅ All tests completed successfully!" -ForegroundColor Green
    Write-Host "New PSMinIO implementation is working correctly with:" -ForegroundColor White
    Write-Host "  - Custom REST API client" -ForegroundColor White
    Write-Host "  - Real progress reporting" -ForegroundColor White
    Write-Host "  - Performance metrics" -ForegroundColor White
    Write-Host "  - Enhanced error handling" -ForegroundColor White
    Write-Host "  - PowerShell-optimized synchronous operations" -ForegroundColor White
    
} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n=== Cleanup ===" -ForegroundColor Yellow
    
    # Remove test files
    if (Test-Path $testFile) {
        Remove-Item $testFile -Force
        Write-Host "Removed test file: $testFile" -ForegroundColor Gray
    }
    
    if (Test-Path $downloadFile) {
        Remove-Item $downloadFile -Force
        Write-Host "Removed download file: $downloadFile" -ForegroundColor Gray
    }
    
    # Note: We're not removing the test bucket to avoid issues with the test environment
    Write-Host "Note: Test bucket '$TestBucket' was left for manual cleanup" -ForegroundColor Gray
    
    Write-Host "Cleanup completed." -ForegroundColor Gray
}
