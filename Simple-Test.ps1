# Simple test script for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Testing PSMinIO Module ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Test 1: Connection
Write-Host "1. Testing Connection..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    Write-Host "✓ Connection successful" -ForegroundColor Green
    Write-Host "  Endpoint: $($connection.EndpointUrl)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Bucket Listing
Write-Host "2. Testing Bucket Listing..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket
    Write-Host "✓ Found $($buckets.Count) buckets" -ForegroundColor Green
    if ($buckets.Count -gt 0) {
        $buckets | Select-Object -First 3 | Format-Table Name, CreationDate
    }
} catch {
    Write-Host "✗ Bucket listing failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Tests completed!" -ForegroundColor Cyan
