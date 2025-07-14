# PSMinIO Comprehensive Test Script
# Tests all major functionality against Grace Solution S3 instance

param(
    [string]$TestDirectory = "C:\Temp\PSMinIOTest",
    [string]$TestBucket = "psminiotest-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
)

# Test configuration
$S3Url = "https://api.s3.gracesolution.info"
$AccessKey = "T34Wg85SAwezUa3sk3m4"
$SecretKey = "PxEmnbQoQJTJsDocSEV6mSSscDpJMiJCayPv93xe"

Write-Verbose "=== PSMinIO Comprehensive Test Suite ==="
Write-Verbose "S3 Endpoint: $S3Url"
Write-Verbose "Test Bucket: $TestBucket"
Write-Verbose "Test Directory: $TestDirectory"

# Create test directory and files
Write-Verbose "Setting up test environment..."
if (Test-Path $TestDirectory) {
    Remove-Item $TestDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null

# Create test files of various sizes
$SmallFile = Join-Path $TestDirectory "small-test.txt"
$MediumFile = Join-Path $TestDirectory "medium-test.txt"
$LargeFile = Join-Path $TestDirectory "large-test.txt"

"This is a small test file for PSMinIO testing." | Out-File $SmallFile -Encoding UTF8
1..1000 | ForEach-Object { "Line $_ - Medium test file content for PSMinIO testing with more data." } | Out-File $MediumFile -Encoding UTF8
1..10000 | ForEach-Object { "Line $_ - Large test file content for PSMinIO multipart testing with substantial data to trigger chunked operations." } | Out-File $LargeFile -Encoding UTF8

$testFiles = Get-ChildItem $TestDirectory
Write-Verbose "Created test files: $($testFiles.Count) files, Total size: $([math]::Round(($testFiles | Measure-Object Length -Sum).Sum / 1KB, 2)) KB"

$testResults = @()

try {
    # Test 1: Connection
    Write-Verbose "Testing connection..."
    $connection = Connect-MinIO -Url $S3Url -AccessKey $AccessKey -SecretKey $SecretKey -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Connection"
        Status = "Success"
        Details = "Connected to $S3Url"
        Result = $connection
    }

    # Test 2: List existing buckets
    Write-Verbose "Testing bucket listing..."
    $buckets = Get-MinIOBucket -Verbose
    $testResults += [PSCustomObject]@{
        Test = "List Buckets"
        Status = "Success"
        Details = "Found $($buckets.Count) existing buckets"
        Result = $buckets
    }

    # Test 3: Create test bucket
    Write-Verbose "Testing bucket creation..."
    $newBucket = New-MinIOBucket -BucketName $TestBucket -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Create Bucket"
        Status = "Success"
        Details = "Created bucket: $($newBucket.Name)"
        Result = $newBucket
    }

    # Test 4: Verify bucket exists
    Write-Verbose "Testing bucket existence check..."
    $bucketExists = Test-MinIOBucketExists -BucketName $TestBucket -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Bucket Exists"
        Status = "Success"
        Details = "Bucket exists: $bucketExists"
        Result = $bucketExists
    }

    # Test 5: Upload small file (single part)
    Write-Verbose "Testing single file upload..."
    $uploadResult = New-MinIOObject -BucketName $TestBucket -Files (Get-Item $SmallFile) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Single File Upload"
        Status = "Success"
        Details = "Uploaded small file: $($uploadResult.Count) objects"
        Result = $uploadResult
    }

    # Test 6: Upload medium file (single part)
    Write-Verbose "Testing medium file upload..."
    $uploadResult2 = New-MinIOObject -BucketName $TestBucket -Files (Get-Item $MediumFile) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Medium File Upload"
        Status = "Success"
        Details = "Uploaded medium file: $($uploadResult2.Count) objects"
        Result = $uploadResult2
    }

    # Test 7: Upload large file (multipart)
    Write-Verbose "Testing multipart upload..."
    $multipartResult = New-MinIOObjectMultipart -BucketName $TestBucket -Files (Get-Item $LargeFile) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Multipart Upload"
        Status = "Success"
        Details = "Multipart upload completed: $($multipartResult.Count) objects"
        Result = $multipartResult
    }

    # Test 8: List objects in bucket
    Write-Verbose "Testing object listing..."
    $objects = Get-MinIOObject -BucketName $TestBucket -Verbose
    $testResults += [PSCustomObject]@{
        Test = "List Objects"
        Status = "Success"
        Details = "Found $($objects.Count) objects in bucket"
        Result = $objects
    }

    # Test 9: Download small file
    Write-Verbose "Testing single file download..."
    $downloadPath = Join-Path $TestDirectory "downloaded-small.txt"
    $downloadResult = Get-MinIOObjectContent -BucketName $TestBucket -ObjectKey "small-test.txt" -FilePath (New-Object System.IO.FileInfo $downloadPath) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Single File Download"
        Status = "Success"
        Details = "Downloaded file: $($downloadResult.FilePath)"
        Result = $downloadResult
    }

    # Test 10: Download large file (multipart)
    Write-Verbose "Testing multipart download..."
    $downloadPath2 = Join-Path $TestDirectory "downloaded-large.txt"
    $multipartDownload = Get-MinIOObjectContentMultipart -BucketName $TestBucket -ObjectKey "large-test.txt" -FilePath (New-Object System.IO.FileInfo $downloadPath2) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Multipart Download"
        Status = "Success"
        Details = "Multipart download completed: $($multipartDownload.FilePath)"
        Result = $multipartDownload
    }

    # Test 11: Generate presigned URL
    Write-Verbose "Testing presigned URL generation..."
    $presignedUrl = Get-MinIOPresignedUrl -BucketName $TestBucket -ObjectKey "small-test.txt" -Expiration (New-TimeSpan -Hours 1) -Verbose
    $testResults += [PSCustomObject]@{
        Test = "Presigned URL"
        Status = "Success"
        Details = "Generated presigned URL"
        Result = $presignedUrl
    }

} catch {
    $testResults += [PSCustomObject]@{
        Test = "ERROR"
        Status = "Failed"
        Details = $_.Exception.Message
        Result = $_.ScriptStackTrace
    }
} finally {
    # Cleanup
    Write-Verbose "Cleaning up test resources..."

    try {
        # Remove local test directory
        if (Test-Path $TestDirectory) {
            Remove-Item $TestDirectory -Recurse -Force
            Write-Verbose "Removed local test directory"
        }
    } catch {
        Write-Warning "Cleanup warning: $($_.Exception.Message)"
    }
}

# Output test results
Write-Output "PSMinIO Comprehensive Test Results:"
Write-Output "=================================="
$testResults | Format-Table Test, Status, Details -AutoSize
Write-Output ""
Write-Output "Test bucket '$TestBucket' left for manual cleanup"
Write-Output "Total tests: $($testResults.Count)"
Write-Output "Successful: $(($testResults | Where-Object Status -eq 'Success').Count)"
Write-Output "Failed: $(($testResults | Where-Object Status -eq 'Failed').Count)"

# Return results for further processing
return $testResults
