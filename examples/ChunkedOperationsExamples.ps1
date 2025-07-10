# PSMinIO Chunked Operations Examples
# This script demonstrates various chunked upload and download scenarios

# Import the module
Import-Module PSMinIO

# Connect to MinIO (adjust endpoint and credentials as needed)
$connection = Connect-MinIO -Endpoint "https://minio.example.com:9000" -AccessKey "your-access-key" -SecretKey "your-secret-key"

Write-Host "=== PSMinIO Chunked Operations Examples ===" -ForegroundColor Cyan

# Example 1: Basic chunked upload of large files
Write-Host "`n--- Example 1: Basic chunked upload ---" -ForegroundColor Yellow

# Upload large files with 10MB chunks
$largeFiles = Get-ChildItem "C:\LargeFiles\*.zip" | Select-Object -First 3
$uploadResults = New-MinIOObjectChunked -BucketName "backup" -Path $largeFiles -ChunkSize (10 * 1024 * 1024)

foreach ($result in $uploadResults) {
    Write-Host "Uploaded: $($result.Name) - Size: $($result.SizeFormatted)" -ForegroundColor Green
}

# Example 2: Chunked upload with resume capability
Write-Host "`n--- Example 2: Chunked upload with resume ---" -ForegroundColor Yellow

# Upload with resume enabled - if interrupted, can be resumed later
$videoFiles = Get-ChildItem "C:\Videos\*.mp4"
$resumeResults = New-MinIOObjectChunked -BucketName "media" -Path $videoFiles -ChunkSize (20 * 1024 * 1024) -Resume -MaxRetries 5

Write-Host "Uploaded $($resumeResults.Count) video files with resume capability" -ForegroundColor Green

# Example 3: Directory upload with chunking and filtering
Write-Host "`n--- Example 3: Directory upload with filtering ---" -ForegroundColor Yellow

# Upload entire directory with filtering
$projectDir = Get-Item "C:\Projects\MyProject"
$filteredResults = New-MinIOObjectChunked -BucketName "projects" -Directory $projectDir -Recursive -MaxDepth 3 `
    -InclusionFilter { $_.Extension -in @(".cs", ".js", ".json", ".md") } `
    -ExclusionFilter { $_.Name -like "*temp*" -or $_.Name -like "*.tmp" } `
    -ChunkSize (5 * 1024 * 1024) -Resume

Write-Host "Uploaded $($filteredResults.Count) project files" -ForegroundColor Green

# Example 4: Upload to specific bucket directory with presigned URLs
Write-Host "`n--- Example 4: Upload to bucket directory with URLs ---" -ForegroundColor Yellow

# Upload files to specific bucket directory and generate presigned URLs
$documents = Get-ChildItem "C:\Documents\Reports\*.pdf"
$urlResults = New-MinIOObjectChunked -BucketName "storage" -Path $documents -BucketDirectory "reports/2024/Q1" `
    -ShowURL -Expiration (New-TimeSpan -Hours 24) -ChunkSize (8 * 1024 * 1024)

foreach ($result in $urlResults) {
    Write-Host "Uploaded: $($result.Name)" -ForegroundColor Green
    if ($result.HasPresignedUrl) {
        Write-Host "  URL: $($result.PresignedUrl)" -ForegroundColor Cyan
        Write-Host "  Expires: $($result.PresignedUrlExpiration)" -ForegroundColor Gray
    }
}

# Example 5: Basic chunked download
Write-Host "`n--- Example 5: Basic chunked download ---" -ForegroundColor Yellow

# Download large file with chunking
$downloadFile = [System.IO.FileInfo]"C:\Downloads\large-dataset.zip"
$downloadResult = Get-MinIOObjectContentChunked -BucketName "data" -ObjectName "datasets/large-dataset.zip" `
    -FilePath $downloadFile -ChunkSize (15 * 1024 * 1024) -ParallelDownloads 4

Write-Host "Downloaded: $($downloadResult.Name) - Size: $($downloadResult.Length) bytes" -ForegroundColor Green

# Example 6: Chunked download with resume
Write-Host "`n--- Example 6: Chunked download with resume ---" -ForegroundColor Yellow

# Download with resume capability - can be interrupted and resumed
$resumeDownloadFile = [System.IO.FileInfo]"C:\Downloads\huge-backup.tar.gz"
$resumeDownloadResult = Get-MinIOObjectContentChunked -BucketName "backups" -ObjectName "system-backup.tar.gz" `
    -FilePath $resumeDownloadFile -Resume -ChunkSize (25 * 1024 * 1024) -ParallelDownloads 5 -MaxRetries 3

Write-Host "Downloaded with resume: $($resumeDownloadResult.Name)" -ForegroundColor Green

# Example 7: Progress control examples
Write-Host "`n--- Example 7: Progress control options ---" -ForegroundColor Yellow

# Upload with detailed progress (default)
New-MinIOObjectChunked -BucketName "test" -Path $largeFiles -ShowDetailedProgress

# Upload with collection-level progress only
New-MinIOObjectChunked -BucketName "test" -Path $largeFiles -ShowCollectionProgressOnly

# Upload with no progress bars (only verbose logging)
New-MinIOObjectChunked -BucketName "test" -Path $largeFiles -ProgressAction SilentlyContinue -Verbose

# Example 8: Custom chunk sizes for different scenarios
Write-Host "`n--- Example 8: Custom chunk sizes ---" -ForegroundColor Yellow

# Small chunks for slow connections
$smallChunkResults = New-MinIOObjectChunked -BucketName "mobile" -Path $documents -ChunkSize (1 * 1024 * 1024)  # 1MB chunks

# Large chunks for fast connections
$largeChunkResults = New-MinIOObjectChunked -BucketName "datacenter" -Path $videoFiles -ChunkSize (100 * 1024 * 1024)  # 100MB chunks

# Adaptive chunk size based on file size
foreach ($file in $largeFiles) {
    $chunkSize = if ($file.Length -lt 50MB) { 2MB } 
                elseif ($file.Length -lt 500MB) { 10MB } 
                else { 50MB }
    
    $adaptiveResult = New-MinIOObjectChunked -BucketName "adaptive" -Path @($file) -ChunkSize $chunkSize
    Write-Host "Uploaded $($file.Name) with $($chunkSize / 1MB)MB chunks" -ForegroundColor Green
}

# Example 9: Resume data management
Write-Host "`n--- Example 9: Resume data management ---" -ForegroundColor Yellow

# Upload with custom resume data location
$customResumeResults = New-MinIOObjectChunked -BucketName "important" -Path $videoFiles `
    -Resume -ResumeDataPath "D:\ResumeData" -ChunkSize (30 * 1024 * 1024)

# Check for existing resume files
$resumeFiles = [PSMinIO.Utils.ChunkedTransferResumeManager]::GetResumeFiles()
Write-Host "Found $($resumeFiles.Count) resume files" -ForegroundColor Cyan

# Clean up old resume files (older than 7 days)
$cleanedCount = [PSMinIO.Utils.ChunkedTransferResumeManager]::CleanupOldResumeFiles(7)
Write-Host "Cleaned up $cleanedCount old resume files" -ForegroundColor Green

# Example 10: Error handling and retry strategies
Write-Host "`n--- Example 10: Error handling and retry ---" -ForegroundColor Yellow

try {
    # Upload with aggressive retry settings for unreliable connections
    $retryResults = New-MinIOObjectChunked -BucketName "unreliable" -Path $largeFiles `
        -ChunkSize (5 * 1024 * 1024) -MaxRetries 10 -Resume -Verbose
    
    Write-Host "Upload completed despite network issues" -ForegroundColor Green
}
catch {
    Write-Warning "Upload failed after all retries: $($_.Exception.Message)"
    
    # Resume data is automatically saved, so you can retry later:
    # New-MinIOObjectChunked -BucketName "unreliable" -Path $largeFiles -Resume
}

# Example 11: Monitoring transfer state
Write-Host "`n--- Example 11: Transfer state monitoring ---" -ForegroundColor Yellow

# For advanced scenarios, you can access transfer state information
# This would typically be done in a custom progress handler or monitoring script

# Example of what transfer state information is available:
Write-Host "Transfer State Properties:" -ForegroundColor Cyan
Write-Host "  - ProgressPercentage: Shows completion percentage" -ForegroundColor Gray
Write-Host "  - BytesTransferred: Shows bytes completed" -ForegroundColor Gray
Write-Host "  - TotalChunks: Total number of chunks" -ForegroundColor Gray
Write-Host "  - CompletedChunkCount: Number of completed chunks" -ForegroundColor Gray
Write-Host "  - TransferType: Upload or Download" -ForegroundColor Gray
Write-Host "  - ElapsedTime: Time since transfer started" -ForegroundColor Gray

Write-Host "`n=== Examples completed! ===" -ForegroundColor Green

# Best Practices Summary
Write-Host "`n--- Best Practices Summary ---" -ForegroundColor Yellow
Write-Host "1. Use appropriate chunk sizes:" -ForegroundColor Cyan
Write-Host "   - 1-5MB for slow/mobile connections" -ForegroundColor Gray
Write-Host "   - 10-25MB for typical broadband" -ForegroundColor Gray
Write-Host "   - 50-100MB for high-speed datacenter connections" -ForegroundColor Gray

Write-Host "2. Enable resume for large transfers:" -ForegroundColor Cyan
Write-Host "   - Always use -Resume for files > 100MB" -ForegroundColor Gray
Write-Host "   - Consider custom -ResumeDataPath for important transfers" -ForegroundColor Gray

Write-Host "3. Optimize parallel downloads:" -ForegroundColor Cyan
Write-Host "   - Use 3-5 parallel downloads for most scenarios" -ForegroundColor Gray
Write-Host "   - Increase for very fast connections, decrease for slow ones" -ForegroundColor Gray

Write-Host "4. Handle errors gracefully:" -ForegroundColor Cyan
Write-Host "   - Set appropriate -MaxRetries based on connection reliability" -ForegroundColor Gray
Write-Host "   - Use try/catch blocks for critical transfers" -ForegroundColor Gray

Write-Host "5. Monitor progress appropriately:" -ForegroundColor Cyan
Write-Host "   - Use -ShowDetailedProgress for interactive sessions" -ForegroundColor Gray
Write-Host "   - Use -ShowCollectionProgressOnly for batch operations" -ForegroundColor Gray
Write-Host "   - Use -ProgressAction SilentlyContinue with -Verbose for logging" -ForegroundColor Gray
