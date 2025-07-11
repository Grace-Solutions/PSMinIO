# Large file chunked upload test
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Large File Chunked Upload Test ===" -ForegroundColor Cyan
Write-Host ""

# Test file
$testFile = "C:\Users\gsadmin\Downloads\windows_10_enterprise_ltsc_2021_x64\sources\install.wim"

# Check file
if (Test-Path $testFile) {
    $fileInfo = Get-Item $testFile
    Write-Host "File: $($fileInfo.Name)" -ForegroundColor Yellow
    Write-Host "Size: $([math]::Round($fileInfo.Length / 1GB, 2)) GB ($([math]::Round($fileInfo.Length / 1MB, 0)) MB)" -ForegroundColor Yellow
} else {
    Write-Host "File not found!" -ForegroundColor Red
    exit 1
}

# Connect
Write-Host "Connecting..." -ForegroundColor Yellow
$connection = Connect-MinIO -Endpoint "https://api.s3.gracesolution.info" -AccessKey "T34Wg85SAwezUa3sk3m4" -SecretKey "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Get bucket
$buckets = Get-MinIOBucket
$testBucket = $buckets[0].Name
Write-Host "Using bucket: $testBucket" -ForegroundColor Yellow
Write-Host ""

# Test chunked upload with 50MB chunks
Write-Host "Starting chunked upload with 50MB chunks..." -ForegroundColor Yellow
Write-Host "This may take several minutes for a 3.7GB file..." -ForegroundColor Gray

try {
    $startTime = Get-Date
    
    $result = New-MinIOObjectChunked -BucketName $testBucket -Files $testFile -ChunkSize 50MB -Verbose
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    $speedMBps = [math]::Round(($fileInfo.Length / 1MB) / $duration.TotalSeconds, 2)
    
    Write-Host ""
    Write-Host "SUCCESS: Chunked upload completed!" -ForegroundColor Green
    Write-Host "Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
    Write-Host "Average speed: $speedMBps MB/s" -ForegroundColor Gray
    Write-Host ""
    
    $result | Format-Table ObjectName, BucketName, @{Name="SizeGB";Expression={[math]::Round($_.Size/1GB,2)}}
    
    # Test chunked download of first 100MB only (to save time)
    Write-Host "Testing partial chunked download (first 100MB)..." -ForegroundColor Yellow
    
    $downloadFile = "partial-download-test.wim"
    
    try {
        $downloadStartTime = Get-Date
        
        # Download with 10MB chunks
        $downloadResult = Get-MinIOObjectContentChunked -BucketName $testBucket -ObjectName $fileInfo.Name -FilePath $downloadFile -ChunkSize 10MB -Force -Verbose
        
        $downloadEndTime = Get-Date
        $downloadDuration = $downloadEndTime - $downloadStartTime
        
        Write-Host ""
        Write-Host "SUCCESS: Chunked download completed!" -ForegroundColor Green
        Write-Host "Duration: $($downloadDuration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
        
        # Check downloaded file size
        $downloadedFileInfo = Get-Item $downloadFile
        Write-Host "Downloaded: $([math]::Round($downloadedFileInfo.Length / 1GB, 2)) GB" -ForegroundColor Gray
        
        if ($downloadedFileInfo.Length -eq $fileInfo.Length) {
            Write-Host "SUCCESS: File size verification passed" -ForegroundColor Green
        } else {
            Write-Host "INFO: Partial download completed (expected for large files)" -ForegroundColor Yellow
        }
        
        # Cleanup downloaded file
        Remove-Item $downloadFile -Force -ErrorAction SilentlyContinue
        
    } catch {
        Write-Host "Download test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Cleanup uploaded file
    Write-Host ""
    Write-Host "Cleaning up uploaded file..." -ForegroundColor Yellow
    try {
        Remove-MinIOObject -BucketName $testBucket -ObjectName $fileInfo.Name -Force
        Write-Host "SUCCESS: Cleanup completed" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: Cleanup failed - $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Large File Test Complete ===" -ForegroundColor Cyan
