# Final clean test for PSMinIO module with MinIO 4.0.7
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== PSMinIO Module Final Test ===" -ForegroundColor Cyan
Write-Host ""

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Test 1: Connection with automatic session variable
Write-Host "1. Connection Test (with automatic session variable)..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey -Verbose
    Write-Host "SUCCESS: Connected to $($connection.EndpointUrl)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Bucket Listing (using session variable)
Write-Host "2. Bucket Listing Test (using session variable)..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket -Verbose
    Write-Host "SUCCESS: Found $($buckets.Count) buckets" -ForegroundColor Green
    if ($buckets.Count -gt 0) {
        $buckets | Format-Table Name, CreationDate
    }
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Bucket Listing (with explicit connection)
Write-Host "3. Bucket Listing Test (with explicit connection)..." -ForegroundColor Yellow
try {
    $buckets2 = Get-MinIOBucket -MinIOConnection $connection -Verbose
    Write-Host "SUCCESS: Found $($buckets2.Count) buckets with explicit connection" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Create test bucket if none exist
if ($buckets.Count -eq 0) {
    Write-Host "4. Creating Test Bucket..." -ForegroundColor Yellow
    try {
        $testBucketName = "psminiotest$(Get-Random -Minimum 1000 -Maximum 9999)"
        New-MinIOBucket -BucketName $testBucketName -Verbose
        Write-Host "SUCCESS: Created bucket $testBucketName" -ForegroundColor Green
        $buckets = Get-MinIOBucket
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 5: File Upload (if we have buckets)
if ($buckets.Count -gt 0) {
    $testBucket = $buckets[0].Name
    Write-Host "5. File Upload Test to bucket '$testBucket'..." -ForegroundColor Yellow
    
    # Create test file
    $testFile = "test-upload.txt"
    "Test content created at $(Get-Date)" | Out-File -FilePath $testFile -Encoding UTF8
    
    try {
        $uploadResult = New-MinIOObject -BucketName $testBucket -Files $testFile -Verbose
        Write-Host "SUCCESS: Uploaded $testFile" -ForegroundColor Green
        $uploadResult | Format-Table ObjectName, BucketName, Size
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 6: File Download
    Write-Host "6. File Download Test..." -ForegroundColor Yellow
    try {
        $downloadFile = "downloaded-test.txt"
        $downloadResult = Get-MinIOObjectContent -BucketName $testBucket -ObjectName $testFile -FilePath $downloadFile -Force -Verbose
        Write-Host "SUCCESS: Downloaded to $($downloadResult.FullName)" -ForegroundColor Green
        Write-Host "Content: $(Get-Content $downloadFile)" -ForegroundColor Gray
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Cleanup test files
    @($testFile, $downloadFile) | ForEach-Object {
        if (Test-Path $_) { Remove-Item $_ -Force }
    }
}

Write-Host ""
Write-Host "=== Test Suite Complete ===" -ForegroundColor Cyan
Write-Host "✅ MinIO 4.0.7 with clean logging and automatic session management" -ForegroundColor Green
