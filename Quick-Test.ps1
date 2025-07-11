# Quick test for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Quick PSMinIO Test ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

Write-Host "1. Connecting..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    Write-Host "SUCCESS: Connected to $($connection.EndpointUrl)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "2. Testing bucket listing..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket
    Write-Host "SUCCESS: Found $($buckets.Count) buckets" -ForegroundColor Green
    if ($buckets.Count -gt 0) {
        $buckets | Format-Table Name, CreationDate
    }
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
}

Write-Host "Test completed!" -ForegroundColor Cyan
