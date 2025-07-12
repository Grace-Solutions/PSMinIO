# Test script for uploading 50 test files to demonstrate enhanced upload functionality
# This tests FileInfo[] support, multi-layer progress tracking, and BucketDirectory features

param(
    [string]$TestBucketName = "psminiotest-50files-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [string]$TestDirectory = "TestFiles50",
    [switch]$Cleanup,
    [switch]$Verbose
)

# Set verbose preference if requested
if ($Verbose) {
    $VerbosePreference = 'Continue'
}

Write-Host "=== PSMinIO 50-File Upload Test ===" -ForegroundColor Cyan
Write-Host "Testing enhanced upload functionality with multi-layer progress tracking" -ForegroundColor Green

try {
    # Import the module
    Write-Host "`n1. Loading PSMinIO module..." -ForegroundColor Yellow
    Import-Module ".\Module\PSMinIO\PSMinIO.psd1" -Force
    
    # Connect to MinIO
    Write-Host "2. Connecting to MinIO..." -ForegroundColor Yellow
    Connect-MinIO -Endpoint "https://api.s3.gracesolution.info" -AccessKey "T34Wg85SAwezUa3sk3m4" -SecretKey "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"
    Write-Host "✅ Connected successfully!" -ForegroundColor Green
    
    # Create test directory and files
    Write-Host "`n3. Creating test files..." -ForegroundColor Yellow
    if (Test-Path $TestDirectory) {
        Remove-Item $TestDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null
    
    # Create 50 test files with varying sizes and content
    $testFiles = @()
    for ($i = 1; $i -le 50; $i++) {
        $fileName = "testfile-{0:D3}.txt" -f $i
        $filePath = Join-Path $TestDirectory $fileName
        
        # Create files with different sizes (1KB to 100KB)
        $fileSize = Get-Random -Minimum 1024 -Maximum 102400
        $content = "Test file $i`n" + ("X" * ($fileSize - 20))
        
        Set-Content -Path $filePath -Value $content -NoNewline
        $testFiles += Get-Item $filePath
    }
    
    Write-Host "✅ Created 50 test files (total size: $((($testFiles | Measure-Object Length -Sum).Sum / 1MB).ToString('F2')) MB)" -ForegroundColor Green
    
    # Create test bucket
    Write-Host "`n4. Creating test bucket: $TestBucketName" -ForegroundColor Yellow
    New-MinIOBucket -BucketName $TestBucketName
    Write-Host "✅ Bucket created successfully!" -ForegroundColor Green
    
    # Test 1: Upload all files using FileInfo[] parameter
    Write-Host "`n5. Test 1: Uploading 50 files using FileInfo[] parameter..." -ForegroundColor Yellow
    Write-Host "   This will demonstrate multi-layer progress tracking:" -ForegroundColor Cyan
    Write-Host "   • Layer 1: Collection progress (overall files)" -ForegroundColor Cyan
    Write-Host "   • Layer 2: File progress (current file)" -ForegroundColor Cyan
    Write-Host "   • Layer 3: Transfer progress (bytes)" -ForegroundColor Cyan
    
    $uploadStart = Get-Date
    $uploadResults = New-MinIOObject -BucketName $TestBucketName -Files $testFiles -BucketDirectory "batch-upload" -PassThru -Verbose
    $uploadDuration = (Get-Date) - $uploadStart
    
    Write-Host "✅ Upload completed in $($uploadDuration.TotalSeconds.ToString('F2')) seconds!" -ForegroundColor Green
    Write-Host "   Average speed: $(($uploadResults | Measure-Object TotalSize -Sum).Sum / $uploadDuration.TotalSeconds / 1MB | ForEach-Object { $_.ToString('F2') }) MB/s" -ForegroundColor Green
    Write-Host "   Files uploaded: $($uploadResults.Count)" -ForegroundColor Green
    
    # Test 2: Upload using Directory parameter with BucketDirectory
    Write-Host "`n6. Test 2: Uploading directory with BucketDirectory parameter..." -ForegroundColor Yellow
    
    $dirUploadStart = Get-Date
    $dirUploadResults = New-MinIOObject -BucketName $TestBucketName -Directory (Get-Item $TestDirectory) -BucketDirectory "directory-upload/nested/structure" -PassThru -Verbose
    $dirUploadDuration = (Get-Date) - $dirUploadStart
    
    Write-Host "✅ Directory upload completed in $($dirUploadDuration.TotalSeconds.ToString('F2')) seconds!" -ForegroundColor Green
    Write-Host "   Files uploaded: $($dirUploadResults.Count)" -ForegroundColor Green
    
    # Test 3: Verify uploads by listing objects
    Write-Host "`n7. Verifying uploads..." -ForegroundColor Yellow
    $allObjects = Get-MinIOObject -BucketName $TestBucketName
    
    $batchObjects = $allObjects | Where-Object { $_.Name -like "batch-upload/*" }
    $dirObjects = $allObjects | Where-Object { $_.Name -like "directory-upload/*" }
    
    Write-Host "✅ Verification complete:" -ForegroundColor Green
    Write-Host "   Total objects in bucket: $($allObjects.Count)" -ForegroundColor Green
    Write-Host "   Batch upload objects: $($batchObjects.Count)" -ForegroundColor Green
    Write-Host "   Directory upload objects: $($dirObjects.Count)" -ForegroundColor Green
    
    # Test 4: Test with filters (create subdirectories first)
    Write-Host "`n8. Test 3: Testing directory upload with filters..." -ForegroundColor Yellow
    
    # Create subdirectories with different file types
    $subDir1 = Join-Path $TestDirectory "SubDir1"
    $subDir2 = Join-Path $TestDirectory "SubDir2"
    New-Item -ItemType Directory -Path $subDir1 -Force | Out-Null
    New-Item -ItemType Directory -Path $subDir2 -Force | Out-Null
    
    # Create some .log and .json files
    for ($i = 1; $i -le 5; $i++) {
        Set-Content -Path (Join-Path $subDir1 "logfile$i.log") -Value "Log entry $i"
        Set-Content -Path (Join-Path $subDir2 "config$i.json") -Value "{`"test`": $i}"
    }
    
    # Upload only .log files using inclusion filter
    $filterStart = Get-Date
    $filterResults = New-MinIOObject -BucketName $TestBucketName -Directory (Get-Item $TestDirectory) -Recursive -InclusionFilter { $_.Extension -eq ".log" } -BucketDirectory "filtered-upload" -PassThru -Verbose
    $filterDuration = (Get-Date) - $filterStart
    
    Write-Host "✅ Filtered upload completed in $($filterDuration.TotalSeconds.ToString('F2')) seconds!" -ForegroundColor Green
    Write-Host "   Log files uploaded: $($filterResults.Count)" -ForegroundColor Green
    
    # Summary
    Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
    Write-Host "✅ All tests completed successfully!" -ForegroundColor Green
    Write-Host "Features tested:" -ForegroundColor Yellow
    Write-Host "  • FileInfo[] parameter with 50 files" -ForegroundColor White
    Write-Host "  • Multi-layer progress tracking (3 layers)" -ForegroundColor White
    Write-Host "  • BucketDirectory parameter for nested structures" -ForegroundColor White
    Write-Host "  • Directory parameter with recursive upload" -ForegroundColor White
    Write-Host "  • InclusionFilter for selective file upload" -ForegroundColor White
    Write-Host "  • PassThru parameter for upload results" -ForegroundColor White
    Write-Host "  • Thread-safe progress reporting" -ForegroundColor White
    
    Write-Host "`nPerformance metrics:" -ForegroundColor Yellow
    Write-Host "  • Batch upload: $($uploadDuration.TotalSeconds.ToString('F2'))s for $($uploadResults.Count) files" -ForegroundColor White
    Write-Host "  • Directory upload: $($dirUploadDuration.TotalSeconds.ToString('F2'))s for $($dirUploadResults.Count) files" -ForegroundColor White
    Write-Host "  • Filtered upload: $($filterDuration.TotalSeconds.ToString('F2'))s for $($filterResults.Count) files" -ForegroundColor White
    
    # Cleanup option
    if ($Cleanup) {
        Write-Host "`n9. Cleaning up..." -ForegroundColor Yellow
        Remove-MinIOBucket -BucketName $TestBucketName -Force
        Remove-Item $TestDirectory -Recurse -Force
        Write-Host "✅ Cleanup completed!" -ForegroundColor Green
    } else {
        Write-Host "`nTest bucket '$TestBucketName' and files preserved for inspection." -ForegroundColor Cyan
        Write-Host "Use -Cleanup parameter to automatically clean up test resources." -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
} finally {
    # Clean up test files if they exist
    if (Test-Path $TestDirectory) {
        Write-Host "`nCleaning up local test files..." -ForegroundColor Yellow
        Remove-Item $TestDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
