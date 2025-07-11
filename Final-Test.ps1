# Final comprehensive test for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== PSMinIO Module Test Suite ===" -ForegroundColor Cyan
Write-Host ""

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Test 1: Connection
Write-Host "1. Connection Test..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    Write-Host "SUCCESS: Connected to $($connection.EndpointUrl)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Bucket Listing
Write-Host "2. Bucket Listing Test..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket
    Write-Host "SUCCESS: Found $($buckets.Count) buckets" -ForegroundColor Green
    if ($buckets.Count -gt 0) {
        $buckets | Select-Object Name, CreationDate | Format-Table
    }
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Create test bucket if none exist
if ($buckets.Count -eq 0) {
    Write-Host "3. Creating Test Bucket..." -ForegroundColor Yellow
    try {
        $testBucketName = "psminiotest$(Get-Random -Minimum 1000 -Maximum 9999)"
        New-MinIOBucket -BucketName $testBucketName
        Write-Host "SUCCESS: Created bucket $testBucketName" -ForegroundColor Green
        $buckets = Get-MinIOBucket
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 4: File Upload
if ($buckets.Count -gt 0) {
    $testBucket = $buckets[0].Name
    Write-Host "4. File Upload Test to bucket '$testBucket'..." -ForegroundColor Yellow
    
    # Create test file
    $testFile = "test-upload.txt"
    "Test content created at $(Get-Date)" | Out-File -FilePath $testFile -Encoding UTF8
    
    try {
        $uploadResult = New-MinIOObject -BucketName $testBucket -Files $testFile
        Write-Host "SUCCESS: Uploaded $testFile" -ForegroundColor Green
        $uploadResult | Format-Table ObjectName, BucketName, Size
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 5: File Download
    Write-Host "5. File Download Test..." -ForegroundColor Yellow
    try {
        $downloadFile = "downloaded-test.txt"
        $downloadResult = Get-MinIOObjectContent -BucketName $testBucket -ObjectName $testFile -FilePath $downloadFile -Force
        Write-Host "SUCCESS: Downloaded to $($downloadResult.FullName)" -ForegroundColor Green
        Write-Host "Content: $(Get-Content $downloadFile)" -ForegroundColor Gray
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 6: Chunked Upload
    Write-Host "6. Chunked Upload Test..." -ForegroundColor Yellow
    try {
        $chunkedFile = "chunked-test.txt"
        $content = @()
        for ($i = 1; $i -le 100; $i++) {
            $content += "Line $i of chunked test file created at $(Get-Date)"
        }
        $content | Out-File -FilePath $chunkedFile -Encoding UTF8
        
        $chunkedResult = New-MinIOObjectChunked -BucketName $testBucket -Files $chunkedFile -ChunkSize 1KB
        Write-Host "SUCCESS: Chunked upload completed" -ForegroundColor Green
        $chunkedResult | Format-Table ObjectName, BucketName, Size
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 7: Chunked Download
    Write-Host "7. Chunked Download Test..." -ForegroundColor Yellow
    try {
        $chunkedDownload = "chunked-downloaded.txt"
        $chunkedDownloadResult = Get-MinIOObjectContentChunked -BucketName $testBucket -ObjectName $chunkedFile -FilePath $chunkedDownload -ChunkSize 1KB -Force
        Write-Host "SUCCESS: Chunked download completed to $($chunkedDownloadResult.FullName)" -ForegroundColor Green
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Cleanup test files
    @($testFile, $downloadFile, $chunkedFile, $chunkedDownload) | ForEach-Object {
        if (Test-Path $_) { Remove-Item $_ -Force }
    }
}

Write-Host ""
Write-Host "=== Test Suite Complete ===" -ForegroundColor Cyan
