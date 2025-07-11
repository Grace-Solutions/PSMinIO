# Test script for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Testing PSMinIO Module ===" -ForegroundColor Cyan
Write-Host ""

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Test 1: Connection
Write-Host "1. Testing Connection..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey -TestConnection
    Write-Host "✓ Connection successful" -ForegroundColor Green
    Write-Host "  Endpoint: $($connection.EndpointUrl)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 2: Bucket Listing
Write-Host "2. Testing Bucket Listing..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket
    Write-Host "✓ Found $($buckets.Count) buckets" -ForegroundColor Green
    if ($buckets.Count -gt 0) {
        $buckets | Select-Object -First 3 | Format-Table Name, CreationDate, @{Name="Size";Expression={$_.SizeFormatted}}
    }
} catch {
    Write-Host "✗ Bucket listing failed: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Create test file for upload
Write-Host "3. Creating test file..." -ForegroundColor Yellow
$testFile = "test-upload.txt"
$testContent = "This is a test file created at $(Get-Date) for PSMinIO testing."
Set-Content -Path $testFile -Value $testContent
Write-Host "✓ Created test file: $testFile" -ForegroundColor Green
Write-Host ""

# Test 4: File Upload (if we have buckets)
if ($buckets -and $buckets.Count -gt 0) {
    $testBucket = $buckets[0].Name
    Write-Host "4. Testing File Upload to bucket '$testBucket'..." -ForegroundColor Yellow
    try {
        $uploadResult = New-MinIOObject -BucketName $testBucket -Files $testFile
        Write-Host "✓ File upload successful" -ForegroundColor Green
        $uploadResult | Format-Table ObjectName, BucketName, Size
    } catch {
        Write-Host "✗ File upload failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""

    # Test 5: File Download
    Write-Host "5. Testing File Download..." -ForegroundColor Yellow
    $downloadFile = "downloaded-test.txt"
    try {
        $downloadResult = Get-MinIOObjectContent -BucketName $testBucket -ObjectName $testFile -FilePath $downloadFile -Force
        Write-Host "✓ File download successful" -ForegroundColor Green
        Write-Host "  Downloaded to: $($downloadResult.FullName)" -ForegroundColor Gray
        Write-Host "  Content: $(Get-Content $downloadFile)" -ForegroundColor Gray
    } catch {
        Write-Host "✗ File download failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""

    # Test 6: Chunked Upload
    Write-Host "6. Testing Chunked Upload..." -ForegroundColor Yellow
    $chunkedFile = "chunked-test.txt"
    $chunkedContent = ("This is a chunked upload test file created at $(Get-Date).`n") * 100
    Set-Content -Path $chunkedFile -Value $chunkedContent
    try {
        $chunkedResult = New-MinIOObjectChunked -BucketName $testBucket -Files $chunkedFile -ChunkSize 1KB
        Write-Host "✓ Chunked upload successful" -ForegroundColor Green
        $chunkedResult | Format-Table ObjectName, BucketName, Size
    } catch {
        Write-Host "✗ Chunked upload failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""

    # Test 7: Chunked Download
    Write-Host "7. Testing Chunked Download..." -ForegroundColor Yellow
    $chunkedDownload = "chunked-downloaded.txt"
    try {
        $chunkedDownloadResult = Get-MinIOObjectContentChunked -BucketName $testBucket -ObjectName $chunkedFile -FilePath $chunkedDownload -ChunkSize 1KB -Force
        Write-Host "✓ Chunked download successful" -ForegroundColor Green
        Write-Host "  Downloaded to: $($chunkedDownloadResult.FullName)" -ForegroundColor Gray
    } catch {
        Write-Host "✗ Chunked download failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "4-7. Skipping upload/download tests - no buckets available" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "All tests completed. Check results above." -ForegroundColor White

# Cleanup
if (Test-Path $testFile) { Remove-Item $testFile -Force }
if (Test-Path $downloadFile) { Remove-Item $downloadFile -Force }
if (Test-Path $chunkedFile) { Remove-Item $chunkedFile -Force }
if (Test-Path $chunkedDownload) { Remove-Item $chunkedDownload -Force }
