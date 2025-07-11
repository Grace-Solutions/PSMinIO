# Debug session variable issue
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Session Variable Debug ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

Write-Host "1. Before connection - checking variables..." -ForegroundColor Yellow
Get-Variable -Name "*MinIO*" -Scope Global -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Found: $($_.Name) = $($_.Value)" -ForegroundColor Gray
}

Write-Host "2. Connecting..." -ForegroundColor Yellow
$connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey

Write-Host "3. After connection - checking variables..." -ForegroundColor Yellow
Get-Variable -Name "*MinIO*" -Scope Global -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Found: $($_.Name) = $($_.Value)" -ForegroundColor Gray
}

Write-Host "4. Checking specific variable..." -ForegroundColor Yellow
try {
    $var = Get-Variable -Name "MinIOConnection" -Scope Global -ErrorAction Stop
    Write-Host "SUCCESS: Variable exists with value: $($var.Value)" -ForegroundColor Green
} catch {
    Write-Host "FAILED: Variable not found: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "5. Testing Get-MinIOBucket with verbose..." -ForegroundColor Yellow
try {
    $buckets = Get-MinIOBucket -Verbose
    Write-Host "SUCCESS: Found $($buckets.Count) buckets" -ForegroundColor Green
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Debug completed!" -ForegroundColor Cyan
