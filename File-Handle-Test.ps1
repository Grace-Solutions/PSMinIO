# Test file handle release
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== File Handle Test ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Connect
$connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
$buckets = Get-MinIOBucket
$testBucket = $buckets[0].Name

Write-Host "Testing file handle release with bucket: $testBucket" -ForegroundColor Yellow

# Test 1: Create file, upload, try to delete immediately
Write-Host "1. Testing immediate file deletion after upload..." -ForegroundColor Yellow
$testFile1 = "handle-test-1.txt"
"Test content 1" | Out-File -FilePath $testFile1 -Encoding UTF8

try {
    # Upload
    $result = New-MinIOObject -BucketName $testBucket -Files $testFile1
    Write-Host "Upload successful" -ForegroundColor Green
    
    # Try to delete immediately
    Remove-Item $testFile1 -Force
    Write-Host "SUCCESS: File deleted immediately after upload" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Create file, upload, wait a bit, then delete
Write-Host "2. Testing file deletion after short delay..." -ForegroundColor Yellow
$testFile2 = "handle-test-2.txt"
"Test content 2" | Out-File -FilePath $testFile2 -Encoding UTF8

try {
    # Upload
    $result = New-MinIOObject -BucketName $testBucket -Files $testFile2
    Write-Host "Upload successful" -ForegroundColor Green
    
    # Wait a bit
    Start-Sleep -Seconds 2
    
    # Try to delete
    Remove-Item $testFile2 -Force
    Write-Host "SUCCESS: File deleted after delay" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Download and immediate delete
Write-Host "3. Testing download and immediate delete..." -ForegroundColor Yellow
$downloadFile = "downloaded-handle-test.txt"

try {
    # Download
    $result = Get-MinIOObjectContent -BucketName $testBucket -ObjectName $testFile1 -FilePath $downloadFile -Force
    Write-Host "Download successful" -ForegroundColor Green
    
    # Try to delete immediately
    Remove-Item $downloadFile -Force
    Write-Host "SUCCESS: Downloaded file deleted immediately" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Force garbage collection and retry
Write-Host "4. Testing with garbage collection..." -ForegroundColor Yellow
$testFile3 = "handle-test-3.txt"
"Test content 3" | Out-File -FilePath $testFile3 -Encoding UTF8

try {
    # Upload
    $result = New-MinIOObject -BucketName $testBucket -Files $testFile3
    Write-Host "Upload successful" -ForegroundColor Green
    
    # Force garbage collection
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    [System.GC]::Collect()
    
    # Try to delete
    Remove-Item $testFile3 -Force
    Write-Host "SUCCESS: File deleted after GC" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "File handle test completed!" -ForegroundColor Cyan
