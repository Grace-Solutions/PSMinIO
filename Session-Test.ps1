# Session variable test for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Session Variable Test ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

Write-Host "1. Before connection - checking existing variables..." -ForegroundColor Yellow
Get-Variable -Name "*MinIO*" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Found variable: $($_.Name) = $($_.Value)" -ForegroundColor Gray
}

Write-Host "2. Connecting with verbose output..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey -Verbose
    Write-Host "SUCCESS: Connected" -ForegroundColor Green
    
    Write-Host "3. After connection - checking variables..." -ForegroundColor Yellow
    Get-Variable -Name "*MinIO*" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Found variable: $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
    
    # Try to manually set the session variable
    Write-Host "4. Manually setting session variable..." -ForegroundColor Yellow
    Set-Variable -Name "MinIOConnection" -Value $connection -Scope Global
    
    Write-Host "5. After manual set - checking variables..." -ForegroundColor Yellow
    Get-Variable -Name "*MinIO*" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Found variable: $($_.Name) = $($_.Value)" -ForegroundColor Gray
    }
    
    # Now try Get-MinIOBucket without explicit connection
    Write-Host "6. Testing Get-MinIOBucket without explicit connection..." -ForegroundColor Yellow
    try {
        $buckets = Get-MinIOBucket -Verbose
        Write-Host "SUCCESS: Found $($buckets.Count) buckets" -ForegroundColor Green
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Session test completed!" -ForegroundColor Cyan
