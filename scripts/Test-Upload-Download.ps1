#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick test of PSMinIO upload and download functionality

.DESCRIPTION
    Tests the new PSMinIO implementation with file upload and download
#>

# Import the module
Import-Module "$PSScriptRoot\..\Module\PSMinIO\PSMinIO.psd1" -Force

# Connect to MinIO
Write-Verbose "Connecting to MinIO..." -Verbose
Connect-MinIO -Endpoint "https://api.s3.gracesolution.info" -AccessKey "T34Wg85SAwezUa3sk3m4" -SecretKey "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe" -Verbose

# Create test file
$testFile = Join-Path $env:TEMP "psminiotest.txt"
$testContent = @"
PSMinIO Test File
================
Created: $(Get-Date)
Test data: $('A' * 1000)
"@

Write-Verbose "Creating test file: $testFile" -Verbose
$testContent | Out-File -FilePath $testFile -Encoding UTF8
$fileInfo = Get-Item $testFile
Write-Verbose "Test file created: $($fileInfo.Length) bytes" -Verbose

# Upload file
$testBucket = "psminiotest-20250711-142415"
Write-Verbose "Uploading file to bucket: $testBucket" -Verbose
$uploadResult = New-MinIOObject -BucketName $testBucket -File $fileInfo -PassThru -Verbose

Write-Output "Upload Result:"
if ($uploadResult) {
    $uploadResult | Format-List ObjectName, TotalSizeFormatted, DurationFormatted, AverageSpeedFormatted, Success
}

# List objects
Write-Verbose "Listing objects in bucket..." -Verbose
$objects = Get-MinIOObject -BucketName $testBucket -Verbose
$objects | Format-Table Name, Size, LastModified

# Download file
$downloadFile = Join-Path $env:TEMP "psminiotest-download.txt"
Write-Verbose "Downloading file to: $downloadFile" -Verbose
$downloadResult = Get-MinIOObjectContent -BucketName $testBucket -ObjectName $fileInfo.Name -LocalPath $downloadFile -PassThru -Verbose

Write-Output "Download Result:"
if ($downloadResult) {
    $downloadResult | Format-List ObjectName, TotalSizeFormatted, DurationFormatted, AverageSpeedFormatted, Success
}

# Verify content
$originalContent = Get-Content $testFile -Raw -ErrorAction SilentlyContinue
$downloadedContent = Get-Content $downloadFile -Raw -ErrorAction SilentlyContinue

if ($originalContent -and $downloadedContent -and ($originalContent -eq $downloadedContent)) {
    Write-Output "✅ File content verification PASSED!"
} else {
    Write-Output "❌ File content verification FAILED!"
}

# Cleanup
Remove-Item $testFile -Force -ErrorAction SilentlyContinue
Remove-Item $downloadFile -Force -ErrorAction SilentlyContinue

Write-Output "Test completed!"
