# Test script for current upload functionality (single file version)
param(
    [string]$TestBucketName = "psminiotest-current-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [switch]$Cleanup,
    [switch]$Verbose
)

if ($Verbose) {
    $VerbosePreference = 'Continue'
}

Write-Host "=== PSMinIO Current Upload Test ===" -ForegroundColor Cyan
Write-Host "Testing current upload functionality" -ForegroundColor Green

try {
    # Import the module
    Write-Host "`n1. Loading PSMinIO module..." -ForegroundColor Yellow
    Import-Module ".\Module\PSMinIO\PSMinIO.psd1" -Force
    
    # Connect to MinIO
    Write-Host "2. Connecting to MinIO..." -ForegroundColor Yellow
    Connect-MinIO -Endpoint "https://api.s3.gracesolution.info" -AccessKey "T34Wg85SAwezUa3sk3m4" -SecretKey "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"
    Write-Host "✅ Connected successfully!" -ForegroundColor Green
    
    # Create test files
    Write-Host "`n3. Creating test files..." -ForegroundColor Yellow
    $testDir = "TestFilesCurrent"
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    
    # Create 10 test files
    $testFiles = @()
    for ($i = 1; $i -le 10; $i++) {
        $fileName = "testfile-{0:D3}.txt" -f $i
        $filePath = Join-Path $testDir $fileName
        $content = "Test file $i - $(Get-Date)`n" + ("Content line $i`n" * 10)
        Set-Content -Path $filePath -Value $content
        $testFiles += Get-Item $filePath
    }
    
    Write-Host "✅ Created 10 test files" -ForegroundColor Green
    
    # Create test bucket
    Write-Host "`n4. Creating test bucket: $TestBucketName" -ForegroundColor Yellow
    New-MinIOBucket -BucketName $TestBucketName
    Write-Host "✅ Bucket created successfully!" -ForegroundColor Green
    
    # Test uploading files one by one (current functionality)
    Write-Host "`n5. Uploading files individually..." -ForegroundColor Yellow
    $uploadResults = @()
    $uploadStart = Get-Date
    
    foreach ($file in $testFiles) {
        Write-Host "  Uploading: $($file.Name)" -ForegroundColor Cyan
        $result = New-MinIOObject -BucketName $TestBucketName -File $file -BucketDirectory "individual-uploads" -PassThru -Verbose
        $uploadResults += $result
    }
    
    $uploadDuration = (Get-Date) - $uploadStart
    Write-Host "✅ Upload completed in $($uploadDuration.TotalSeconds.ToString('F2')) seconds!" -ForegroundColor Green
    Write-Host "   Files uploaded: $($uploadResults.Count)" -ForegroundColor Green
    
    # Verify uploads
    Write-Host "`n6. Verifying uploads..." -ForegroundColor Yellow
    $objects = Get-MinIOObject -BucketName $TestBucketName
    Write-Host "✅ Found $($objects.Count) objects in bucket" -ForegroundColor Green
    
    # Test with different bucket directories
    Write-Host "`n7. Testing nested bucket directories..." -ForegroundColor Yellow
    $nestedResults = @()
    foreach ($file in $testFiles[0..2]) {  # Upload first 3 files to nested structure
        $result = New-MinIOObject -BucketName $TestBucketName -File $file -BucketDirectory "level1/level2/level3" -PassThru -Verbose
        $nestedResults += $result
    }
    Write-Host "✅ Nested directory upload completed: $($nestedResults.Count) files" -ForegroundColor Green
    
    # Final verification
    Write-Host "`n8. Final verification..." -ForegroundColor Yellow
    $allObjects = Get-MinIOObject -BucketName $TestBucketName
    $individualObjects = $allObjects | Where-Object { $_.Name -like "individual-uploads/*" }
    $nestedObjects = $allObjects | Where-Object { $_.Name -like "level1/level2/level3/*" }
    
    Write-Host "✅ Final verification complete:" -ForegroundColor Green
    Write-Host "   Total objects: $($allObjects.Count)" -ForegroundColor Green
    Write-Host "   Individual uploads: $($individualObjects.Count)" -ForegroundColor Green
    Write-Host "   Nested uploads: $($nestedObjects.Count)" -ForegroundColor Green
    
    # Summary
    Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
    Write-Host "✅ Current functionality test completed!" -ForegroundColor Green
    Write-Host "Features tested:" -ForegroundColor Yellow
    Write-Host "  • Single file upload with File parameter" -ForegroundColor White
    Write-Host "  • BucketDirectory parameter for nested structures" -ForegroundColor White
    Write-Host "  • PassThru parameter for upload results" -ForegroundColor White
    Write-Host "  • Multiple individual uploads" -ForegroundColor White
    Write-Host "  • Nested directory structure creation" -ForegroundColor White
    
    Write-Host "`nNote: Enhanced features (FileInfo[], Directory, Multi-layer progress) require updated DLL" -ForegroundColor Yellow
    
    if ($Cleanup) {
        Write-Host "`n9. Cleaning up..." -ForegroundColor Yellow
        Remove-MinIOBucket -BucketName $TestBucketName -Force
        Write-Host "✅ Cleanup completed!" -ForegroundColor Green
    }
    
} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
} finally {
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
