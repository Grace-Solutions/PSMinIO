# PSMinIO Individual Test Commands
# Copy and paste these commands one by one to test functionality

# Test Configuration
$S3Url = "https://api.s3.gracesolution.info"
$AccessKey = "T34Wg85SAwezUa3sk3m4"
$SecretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"
$TestBucket = "psminiotest-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Output "Test Configuration:"
Write-Output "S3 Endpoint: $S3Url"
Write-Output "Test Bucket: $TestBucket"
Write-Output ""

# ===== STEP 1: IMPORT MODULE =====
Write-Output "1. Import PSMinIO Module:"
Import-Module "..\Module\PSMinIO\PSMinIO.psd1" -Force -Verbose

# ===== STEP 2: CONNECT TO S3 =====
Write-Output "`n2. Connect to S3:"
$connection = Connect-MinIO -Url $S3Url -AccessKey $AccessKey -SecretKey $SecretKey -Verbose
$connection

# ===== STEP 3: LIST EXISTING BUCKETS =====
Write-Output "`n3. List existing buckets:"
$buckets = Get-MinIOBucket -Verbose
$buckets | Format-Table Name, CreationDate, Region -AutoSize

# ===== STEP 4: CREATE TEST BUCKET =====
Write-Output "`n4. Create test bucket:"
$newBucket = New-MinIOBucket -BucketName $TestBucket -Verbose
$newBucket

# ===== STEP 5: VERIFY BUCKET EXISTS =====
Write-Output "`n5. Verify bucket exists:"
$bucketExists = Test-MinIOBucketExists -BucketName $TestBucket -Verbose
Write-Output "Bucket exists: $bucketExists"

# ===== STEP 6: CREATE TEST FILES =====
Write-Output "`n6. Create test files:"
$TestDirectory = "C:\Temp\PSMinIOTest"
if (Test-Path $TestDirectory) { Remove-Item $TestDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null

# Small file (< 1KB)
$SmallFile = Join-Path $TestDirectory "small-test.txt"
"This is a small test file for PSMinIO testing." | Out-File $SmallFile -Encoding UTF8

# Medium file (~50KB)
$MediumFile = Join-Path $TestDirectory "medium-test.txt"
1..1000 | ForEach-Object { "Line $_ - Medium test file content for PSMinIO testing with more data." } | Out-File $MediumFile -Encoding UTF8

# Large file (~500KB)
$LargeFile = Join-Path $TestDirectory "large-test.txt"
1..10000 | ForEach-Object { "Line $_ - Large test file content for PSMinIO multipart testing with substantial data to trigger chunked operations." } | Out-File $LargeFile -Encoding UTF8

Get-ChildItem $TestDirectory | Select-Object Name, @{Name="Size(KB)";Expression={[math]::Round($_.Length/1KB,2)}}

# ===== STEP 7: UPLOAD SMALL FILE (SINGLE PART) =====
Write-Output "`n7. Upload small file (single part):"
$uploadResult1 = New-MinIOObject -BucketName $TestBucket -Files (Get-Item $SmallFile) -Verbose
$uploadResult1

# ===== STEP 8: UPLOAD MEDIUM FILE (SINGLE PART) =====
Write-Output "`n8. Upload medium file (single part):"
$uploadResult2 = New-MinIOObject -BucketName $TestBucket -Files (Get-Item $MediumFile) -Verbose
$uploadResult2

# ===== STEP 9: UPLOAD LARGE FILE (MULTIPART) =====
Write-Output "`n9. Upload large file (multipart):"
$multipartResult = New-MinIOObjectMultipart -BucketName $TestBucket -FilePath (Get-Item $LargeFile) -Verbose
$multipartResult

# ===== STEP 10: LIST OBJECTS IN BUCKET =====
Write-Output "`n10. List objects in bucket:"
$objects = Get-MinIOObject -BucketName $TestBucket -Verbose
$objects | Format-Table Key, @{Name="Size(KB)";Expression={[math]::Round($_.Size/1KB,2)}}, LastModified -AutoSize

# ===== STEP 11: DOWNLOAD SMALL FILE =====
Write-Output "`n11. Download small file:"
$downloadPath1 = Join-Path $TestDirectory "downloaded-small.txt"
$downloadResult1 = Get-MinIOObjectContent -BucketName $TestBucket -ObjectName "small-test.txt" -LocalPath $downloadPath1 -Verbose
$downloadResult1

# ===== STEP 12: DOWNLOAD LARGE FILE (MULTIPART) =====
Write-Output "`n12. Download large file (multipart):"
$downloadPath2 = Join-Path $TestDirectory "downloaded-large.txt"
$multipartDownload = Get-MinIOObjectContentMultipart -BucketName $TestBucket -ObjectName "large-test.txt" -DestinationPath (New-Object System.IO.FileInfo $downloadPath2) -Verbose
$multipartDownload

# ===== STEP 13: GENERATE PRESIGNED URL =====
Write-Output "`n13. Generate presigned URL:"
$presignedUrl = Get-MinIOPresignedUrl -BucketName $TestBucket -ObjectName "small-test.txt" -Expiration (New-TimeSpan -Hours 1) -Verbose
$presignedUrl

# ===== STEP 14: VERIFY DOWNLOADED FILES =====
Write-Output "`n14. Verify downloaded files:"
Write-Output "Original files:"
Get-ChildItem $TestDirectory -Filter "*test.txt" | Select-Object Name, @{Name="Size(KB)";Expression={[math]::Round($_.Length/1KB,2)}}

Write-Output "`nDownloaded files:"
Get-ChildItem $TestDirectory -Filter "downloaded-*" | Select-Object Name, @{Name="Size(KB)";Expression={[math]::Round($_.Length/1KB,2)}}

# ===== STEP 15: COMPARE FILE CONTENTS =====
Write-Output "`n15. Compare file contents:"
$originalSmall = Get-Content $SmallFile
$downloadedSmall = Get-Content $downloadPath1
Write-Output "Small file content match: $(($originalSmall -join '') -eq ($downloadedSmall -join ''))"

$originalLarge = Get-Content $LargeFile
$downloadedLarge = Get-Content $downloadPath2
Write-Output "Large file content match: $(($originalLarge -join '') -eq ($downloadedLarge -join ''))"

# ===== CLEANUP INSTRUCTIONS =====
Write-Output "`n16. Cleanup (manual):"
Write-Output "Test bucket created: $TestBucket"
Write-Output "Local test directory: $TestDirectory"
Write-Output ""
Write-Output "To clean up:"
Write-Output "1. Remove local directory: Remove-Item '$TestDirectory' -Recurse -Force"
Write-Output "2. Remove S3 bucket objects and bucket manually from S3 console"
Write-Output ""
Write-Output "=== ALL TESTS COMPLETED ==="
