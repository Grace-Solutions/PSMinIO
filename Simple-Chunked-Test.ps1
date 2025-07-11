# Simple chunked upload test
Import-Module ./Module/PSMinIO/PSMinIO.psd1 -Force

Write-Host "=== Simple Chunked Test ===" -ForegroundColor Cyan

# Test file
$testFile = "C:\Users\gsadmin\Downloads\windows_10_enterprise_ltsc_2021_x64\sources\install.wim"

# Check file
if (Test-Path $testFile) {
    $fileInfo = Get-Item $testFile
    Write-Host "File found: $($fileInfo.Name)" -ForegroundColor Green
    Write-Host "Size: $([math]::Round($fileInfo.Length / 1GB, 2)) GB" -ForegroundColor Yellow
} else {
    Write-Host "File not found!" -ForegroundColor Red
    exit 1
}

# Connect
Write-Host "Connecting..." -ForegroundColor Yellow
$connection = Connect-MinIO -Endpoint "https://api.s3.gracesolution.info" -AccessKey "T34Wg85SAwezUa3sk3m4" -SecretKey "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

# Get bucket
$buckets = Get-MinIOBucket
$testBucket = $buckets[0].Name
Write-Host "Using bucket: $testBucket" -ForegroundColor Yellow

# Test chunked upload with 50MB chunks
Write-Host "Starting chunked upload with 50MB chunks..." -ForegroundColor Yellow
try {
    $startTime = Get-Date
    $result = New-MinIOObjectChunked -BucketName $testBucket -Files $testFile -ChunkSize 50MB -Verbose
    $endTime = Get-Date
    
    $duration = $endTime - $startTime
    $speedMBps = [math]::Round(($fileInfo.Length / 1MB) / $duration.TotalSeconds, 2)
    
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "Duration: $($duration.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
    Write-Host "Speed: $speedMBps MB/s" -ForegroundColor Gray
    
    $result | Format-Table ObjectName, BucketName, @{Name="SizeGB";Expression={[math]::Round($_.Size/1GB,2)}}
    
} catch {
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Test completed!" -ForegroundColor Cyan
