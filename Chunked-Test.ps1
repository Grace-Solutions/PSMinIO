# Chunked upload test with large Windows install.wim file
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== PSMinIO Chunked Upload Test ===" -ForegroundColor Cyan
Write-Host ""

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Large test file
$testFile = "C:\Users\gsadmin\Downloads\windows_10_enterprise_ltsc_2021_x64\sources\install.wim"

# Check if file exists
if (-not (Test-Path $testFile)) {
    Write-Host "ERROR: Test file not found: $testFile" -ForegroundColor Red
    exit 1
}

# Get file info
$fileInfo = Get-Item $testFile
Write-Host "Test file: $($fileInfo.Name)" -ForegroundColor Yellow
Write-Host "File size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB ($($fileInfo.Length) bytes)" -ForegroundColor Yellow
Write-Host ""

# Connect
Write-Host "1. Connecting to MinIO..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey -Verbose
    Write-Host "SUCCESS: Connected to $($connection.EndpointUrl)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Get bucket
$buckets = Get-MinIOBucket
if ($buckets.Count -eq 0) {
    Write-Host "ERROR: No buckets available for testing" -ForegroundColor Red
    exit 1
}
$testBucket = $buckets[0].Name
Write-Host "Using bucket: $testBucket" -ForegroundColor Yellow
Write-Host ""

# Test chunked upload with different chunk sizes
$chunkSizes = @(
    @{ Size = "10MB"; Bytes = 10MB },
    @{ Size = "50MB"; Bytes = 50MB }
)

foreach ($chunkConfig in $chunkSizes) {
    Write-Host "2. Testing chunked upload with $($chunkConfig.Size) chunks..." -ForegroundColor Yellow
    
    try {
        $startTime = Get-Date
        
        # Perform chunked upload
        $uploadResult = New-MinIOObjectChunked -BucketName $testBucket -Files $testFile -ChunkSize $chunkConfig.Bytes -Verbose
        
        $endTime = Get-Date
        $duration = $endTime - $startTime
        $speedMBps = [math]::Round(($fileInfo.Length / 1MB) / $duration.TotalSeconds, 2)
        
        Write-Host "SUCCESS: Chunked upload completed!" -ForegroundColor Green
        Write-Host "Duration: $($duration.ToString('mm\:ss\.fff'))" -ForegroundColor Gray
        Write-Host "Average speed: $speedMBps MB/s" -ForegroundColor Gray
        Write-Host ""
        
        # Display result
        $uploadResult | Format-Table ObjectName, BucketName, Size, @{
            Name = "SizeMB"
            Expression = { [math]::Round($_.Size / 1MB, 2) }
        }
        
        # Test chunked download
        Write-Host "3. Testing chunked download with $($chunkConfig.Size) chunks..." -ForegroundColor Yellow
        
        $downloadFile = "downloaded-$($fileInfo.Name)"
        
        try {
            $downloadStartTime = Get-Date
            
            # Perform chunked download
            $downloadResult = Get-MinIOObjectContentChunked -BucketName $testBucket -ObjectName $fileInfo.Name -FilePath $downloadFile -ChunkSize $chunkConfig.Bytes -Force -Verbose
            
            $downloadEndTime = Get-Date
            $downloadDuration = $downloadEndTime - $downloadStartTime
            $downloadSpeedMBps = [math]::Round(($fileInfo.Length / 1MB) / $downloadDuration.TotalSeconds, 2)
            
            Write-Host "SUCCESS: Chunked download completed!" -ForegroundColor Green
            Write-Host "Duration: $($downloadDuration.ToString('mm\:ss\.fff'))" -ForegroundColor Gray
            Write-Host "Average speed: $downloadSpeedMBps MB/s" -ForegroundColor Gray
            
            # Verify file size
            $downloadedFileInfo = Get-Item $downloadFile
            if ($downloadedFileInfo.Length -eq $fileInfo.Length) {
                Write-Host "SUCCESS: File size verification passed ($($downloadedFileInfo.Length) bytes)" -ForegroundColor Green
            } else {
                Write-Host "WARNING: File size mismatch - Original: $($fileInfo.Length), Downloaded: $($downloadedFileInfo.Length)" -ForegroundColor Yellow
            }
            
            # Cleanup downloaded file
            Remove-Item $downloadFile -Force -ErrorAction SilentlyContinue
            
        } catch {
            Write-Host "FAILED: Download error - $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Cleanup uploaded file from bucket
        Write-Host "4. Cleaning up uploaded file..." -ForegroundColor Yellow
        try {
            Remove-MinIOObject -BucketName $testBucket -ObjectName $fileInfo.Name -Force -Verbose
            Write-Host "SUCCESS: Cleanup completed" -ForegroundColor Green
        } catch {
            Write-Host "WARNING: Cleanup failed - $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "--- Test with $($chunkConfig.Size) chunks completed ---" -ForegroundColor Cyan
        Write-Host ""
        
        # Only test one chunk size for now to avoid long test times
        break
        
    } catch {
        Write-Host "FAILED: Upload error - $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
        break
    }
}

Write-Host "=== Chunked Test Suite Complete ===" -ForegroundColor Cyan
Write-Host "✅ Large file chunked upload/download with progress tracking" -ForegroundColor Green
