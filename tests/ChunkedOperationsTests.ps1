# PSMinIO Chunked Operations Test Script
# This script tests the chunked upload and download functionality with resume capabilities

param(
    [string]$MinIOEndpoint = "https://localhost:9000",
    [string]$AccessKey = "minioadmin",
    [string]$SecretKey = "minioadmin",
    [string]$TestBucket = "chunked-test",
    [switch]$SkipCertValidation,
    [switch]$CleanupOnly
)

# Import the module
Import-Module "$PSScriptRoot\..\PSMinIO.psd1" -Force

Write-Host "=== PSMinIO Chunked Operations Test Suite ===" -ForegroundColor Cyan

# Cleanup function
function Cleanup-TestEnvironment {
    Write-Host "Cleaning up test environment..." -ForegroundColor Yellow
    
    try {
        # Remove test bucket if it exists
        if (Test-MinIOBucketExists -BucketName $TestBucket -ErrorAction SilentlyContinue) {
            Write-Host "Removing test bucket: $TestBucket"
            
            # Remove all objects first
            $objects = Get-MinIOObject -BucketName $TestBucket -ErrorAction SilentlyContinue
            if ($objects) {
                foreach ($obj in $objects) {
                    Remove-MinIOObject -BucketName $TestBucket -ObjectName $obj.Name -Force -ErrorAction SilentlyContinue
                }
            }
            
            Remove-MinIOBucket -BucketName $TestBucket -Force -ErrorAction SilentlyContinue
        }
        
        # Clean up test files
        $testDir = "$env:TEMP\PSMinIOChunkedTests"
        if (Test-Path $testDir) {
            Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        # Clean up resume data
        $resumeDir = "$env:LOCALAPPDATA\PSMinIO\Resume"
        if (Test-Path $resumeDir) {
            Get-ChildItem $resumeDir -Filter "*.psminioResume" | Remove-Item -Force -ErrorAction SilentlyContinue
        }
        
        Write-Host "Cleanup completed." -ForegroundColor Green
    }
    catch {
        Write-Warning "Cleanup failed: $($_.Exception.Message)"
    }
}

# If cleanup only, run cleanup and exit
if ($CleanupOnly) {
    Cleanup-TestEnvironment
    exit 0
}

try {
    # Connect to MinIO
    Write-Host "Connecting to MinIO at $MinIOEndpoint..." -ForegroundColor Yellow
    
    $connectParams = @{
        Endpoint = $MinIOEndpoint
        AccessKey = $AccessKey
        SecretKey = $SecretKey
    }
    
    if ($SkipCertValidation) {
        $connectParams.SkipCertificateValidation = $true
    }
    
    $connection = Connect-MinIO @connectParams
    Write-Host "Connected successfully!" -ForegroundColor Green
    
    # Create test directory
    $testDir = "$env:TEMP\PSMinIOChunkedTests"
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    
    # Test 1: Create test bucket
    Write-Host "`n=== Test 1: Creating test bucket ===" -ForegroundColor Cyan
    
    if (Test-MinIOBucketExists -BucketName $TestBucket) {
        Write-Host "Test bucket already exists, removing it first..."
        Cleanup-TestEnvironment
    }
    
    $bucket = New-MinIOBucket -BucketName $TestBucket
    Write-Host "Created bucket: $($bucket.Name)" -ForegroundColor Green
    
    # Test 2: Create test files of various sizes
    Write-Host "`n=== Test 2: Creating test files ===" -ForegroundColor Cyan
    
    $testFiles = @()
    
    # Small file (1MB)
    $smallFile = "$testDir\small-file.txt"
    $smallContent = "A" * (1024 * 1024)  # 1MB
    [System.IO.File]::WriteAllText($smallFile, $smallContent)
    $testFiles += Get-Item $smallFile
    Write-Host "Created small file: $($testFiles[-1].Name) ($($testFiles[-1].Length) bytes)"
    
    # Medium file (15MB)
    $mediumFile = "$testDir\medium-file.bin"
    $mediumContent = [byte[]]::new(15 * 1024 * 1024)  # 15MB
    (New-Object Random).NextBytes($mediumContent)
    [System.IO.File]::WriteAllBytes($mediumFile, $mediumContent)
    $testFiles += Get-Item $mediumFile
    Write-Host "Created medium file: $($testFiles[-1].Name) ($($testFiles[-1].Length) bytes)"
    
    # Large file (50MB)
    $largeFile = "$testDir\large-file.bin"
    $largeContent = [byte[]]::new(50 * 1024 * 1024)  # 50MB
    (New-Object Random).NextBytes($largeContent)
    [System.IO.File]::WriteAllBytes($largeFile, $largeContent)
    $testFiles += Get-Item $largeFile
    Write-Host "Created large file: $($testFiles[-1].Name) ($($testFiles[-1].Length) bytes)"
    
    Write-Host "Created $($testFiles.Count) test files totaling $([math]::Round(($testFiles | Measure-Object Length -Sum).Sum / 1MB, 2)) MB" -ForegroundColor Green
    
    # Test 3: Chunked upload - single file
    Write-Host "`n=== Test 3: Chunked upload - single file ===" -ForegroundColor Cyan
    
    $uploadResult = New-MinIOObjectChunked -BucketName $TestBucket -Path @($testFiles[2]) -ChunkSize (5 * 1024 * 1024) -ShowURL -Verbose
    Write-Host "Uploaded: $($uploadResult.Name) (Size: $($uploadResult.SizeFormatted))" -ForegroundColor Green
    if ($uploadResult.HasPresignedUrl) {
        Write-Host "Presigned URL generated successfully" -ForegroundColor Green
    }
    
    # Test 4: Chunked upload - multiple files
    Write-Host "`n=== Test 4: Chunked upload - multiple files ===" -ForegroundColor Cyan
    
    $multiUploadResults = New-MinIOObjectChunked -BucketName $TestBucket -Path $testFiles[0..1] -ChunkSize (2 * 1024 * 1024) -BucketDirectory "multi-upload" -Verbose
    Write-Host "Uploaded $($multiUploadResults.Count) files to 'multi-upload' directory" -ForegroundColor Green
    foreach ($result in $multiUploadResults) {
        Write-Host "  - $($result.Name) ($($result.SizeFormatted))" -ForegroundColor Gray
    }
    
    # Test 5: Chunked download - single file
    Write-Host "`n=== Test 5: Chunked download - single file ===" -ForegroundColor Cyan
    
    $downloadFile = [System.IO.FileInfo]"$testDir\downloaded-large-file.bin"
    $downloadResult = Get-MinIOObjectContentChunked -BucketName $TestBucket -ObjectName $uploadResult.Name -FilePath $downloadFile -ChunkSize (8 * 1024 * 1024) -ParallelDownloads 3 -Verbose
    
    # Verify download
    $originalHash = (Get-FileHash $testFiles[2].FullName -Algorithm SHA256).Hash
    $downloadedHash = (Get-FileHash $downloadResult.FullName -Algorithm SHA256).Hash
    
    if ($originalHash -eq $downloadedHash) {
        Write-Host "Download verification successful - file integrity maintained" -ForegroundColor Green
    } else {
        Write-Error "Download verification failed - file corruption detected"
    }
    
    # Test 6: Resume functionality test (simulated)
    Write-Host "`n=== Test 6: Resume functionality test ===" -ForegroundColor Cyan
    
    # Create a very large file for resume testing
    $resumeTestFile = "$testDir\resume-test-file.bin"
    $resumeContent = [byte[]]::new(30 * 1024 * 1024)  # 30MB
    (New-Object Random).NextBytes($resumeContent)
    [System.IO.File]::WriteAllBytes($resumeTestFile, $resumeContent)
    $resumeFileInfo = Get-Item $resumeTestFile
    
    Write-Host "Created resume test file: $($resumeFileInfo.Name) ($($resumeFileInfo.Length) bytes)"
    
    # Upload with resume enabled
    $resumeUploadResult = New-MinIOObjectChunked -BucketName $TestBucket -Path @($resumeFileInfo) -ChunkSize (3 * 1024 * 1024) -Resume -ResumeDataPath "$testDir\resume" -Verbose
    Write-Host "Resume upload completed: $($resumeUploadResult.Name)" -ForegroundColor Green
    
    # Test 7: Directory upload with chunking
    Write-Host "`n=== Test 7: Directory upload with chunking ===" -ForegroundColor Cyan
    
    # Create a test directory structure
    $dirTestPath = "$testDir\directory-test"
    New-Item -ItemType Directory -Path $dirTestPath -Force | Out-Null
    New-Item -ItemType Directory -Path "$dirTestPath\subdir1" -Force | Out-Null
    New-Item -ItemType Directory -Path "$dirTestPath\subdir2" -Force | Out-Null
    
    # Create files in directory
    "Content 1" | Out-File "$dirTestPath\file1.txt"
    "Content 2" | Out-File "$dirTestPath\subdir1\file2.txt"
    "Content 3" | Out-File "$dirTestPath\subdir2\file3.txt"
    
    $dirInfo = Get-Item $dirTestPath
    $dirUploadResults = New-MinIOObjectChunked -BucketName $TestBucket -Directory $dirInfo -Recursive -ChunkSize (1024 * 1024) -Verbose
    
    Write-Host "Directory upload completed: $($dirUploadResults.Count) files uploaded" -ForegroundColor Green
    foreach ($result in $dirUploadResults) {
        Write-Host "  - $($result.Name)" -ForegroundColor Gray
    }
    
    # Test 8: List all uploaded objects
    Write-Host "`n=== Test 8: Listing all uploaded objects ===" -ForegroundColor Cyan
    
    $allObjects = Get-MinIOObject -BucketName $TestBucket
    Write-Host "Total objects in bucket: $($allObjects.Count)" -ForegroundColor Green
    Write-Host "Total size: $([math]::Round(($allObjects | Measure-Object Size -Sum).Sum / 1MB, 2)) MB" -ForegroundColor Green
    
    foreach ($obj in $allObjects | Sort-Object Name) {
        Write-Host "  - $($obj.Name) ($($obj.SizeFormatted))" -ForegroundColor Gray
    }
    
    Write-Host "`n=== All tests completed successfully! ===" -ForegroundColor Green
    
    # Ask if user wants to cleanup
    $cleanup = Read-Host "`nDo you want to clean up test data? (y/N)"
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        Cleanup-TestEnvironment
    } else {
        Write-Host "Test data preserved. Run with -CleanupOnly to clean up later." -ForegroundColor Yellow
    }
}
catch {
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Host "Stack trace:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    
    # Cleanup on error
    Write-Host "`nCleaning up due to error..." -ForegroundColor Yellow
    Cleanup-TestEnvironment
    exit 1
}
finally {
    # Disconnect if connected
    if ($connection) {
        Write-Host "Disconnecting from MinIO..." -ForegroundColor Yellow
    }
}
