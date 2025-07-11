# Debug test for PSMinIO module
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== PSMinIO Debug Test ===" -ForegroundColor Cyan

# Test credentials
$endpoint = "https://api.s3.gracesolution.info"
$accessKey = "T34Wg85SAwezUa3sk3m4"
$secretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

Write-Host "1. Connecting..." -ForegroundColor Yellow
try {
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    Write-Host "SUCCESS: Connected to $($connection.EndpointUrl)" -ForegroundColor Green
    
    # Check if connection is stored in session variable
    Write-Host "2. Checking session variable..." -ForegroundColor Yellow
    $sessionConnection = Get-Variable -Name "MinIOConnection" -ErrorAction SilentlyContinue
    if ($sessionConnection) {
        Write-Host "SUCCESS: Session variable exists with value type: $($sessionConnection.Value.GetType().Name)" -ForegroundColor Green
        Write-Host "Session connection endpoint: $($sessionConnection.Value.EndpointUrl)" -ForegroundColor Gray
    } else {
        Write-Host "FAILED: Session variable not found" -ForegroundColor Red
    }
    
    # Try to manually call Get-MinIOBucket with explicit connection
    Write-Host "3. Testing with explicit connection..." -ForegroundColor Yellow
    try {
        $buckets = Get-MinIOBucket -MinIOConnection $connection
        Write-Host "SUCCESS: Found $($buckets.Count) buckets with explicit connection" -ForegroundColor Green
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Debug test completed!" -ForegroundColor Cyan
