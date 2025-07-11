# PSMinIO Chunked Operations Example
# Demonstrates chunked uploads and downloads for large files with progress tracking

# Import the module
Import-Module ..\Module\PSMinIO\PSMinIO.psd1

# Connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

try {
    # Connect to MinIO
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    "Connected to MinIO server for chunked operations demo"

    # Create a demo bucket
    $bucketName = "chunked-demo-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $bucketName
    "Created demo bucket: $bucketName"

    "=== 1. Creating Large Test Files ==="
    # Create files of different sizes for testing
    
    # Small file (under chunk size)
    $smallContent = "Small file content " * 100  # ~2KB
    $smallContent | Out-File -FilePath "small-file.txt" -Encoding UTF8 -NoNewline
    
    # Medium file (1-2 chunks)
    $mediumContent = "Medium file content " * 30000  # ~600KB
    $mediumContent | Out-File -FilePath "medium-file.txt" -Encoding UTF8 -NoNewline
    
    # Large file (multiple chunks)
    $largeContent = "Large file content " * 100000  # ~2MB
    $largeContent | Out-File -FilePath "large-file.txt" -Encoding UTF8 -NoNewline
    
    # Very large file (many chunks)
    $veryLargeContent = "Very large file content " * 250000  # ~6MB
    $veryLargeContent | Out-File -FilePath "very-large-file.txt" -Encoding UTF8 -NoNewline
    
    "Created test files of various sizes"
    Get-ChildItem "*.txt" | Format-Table Name, @{Name="Size";Expression={"$([math]::Round($_.Length/1KB,2)) KB"}}
    ""

    "=== 2. Chunked Upload with Different Chunk Sizes ==="
    
    # Upload with 1MB chunks (default)
    "Uploading medium file with 1MB chunks..."
    $result1 = New-MinIOObjectChunked -BucketName $bucketName -Files "medium-file.txt" -ChunkSize 1MB -BucketDirectory "uploads/1mb-chunks"
    $result1 | Format-Table Name, @{Name="SizeMB";Expression={[math]::Round($_.Size/1MB,2)}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    # Upload with 512KB chunks
    "Uploading large file with 512KB chunks..."
    $result2 = New-MinIOObjectChunked -BucketName $bucketName -Files "large-file.txt" -ChunkSize 512KB -BucketDirectory "uploads/512kb-chunks"
    $result2 | Format-Table Name, @{Name="SizeMB";Expression={[math]::Round($_.Size/1MB,2)}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    # Upload with 2MB chunks
    "Uploading very large file with 2MB chunks..."
    $result3 = New-MinIOObjectChunked -BucketName $bucketName -Files "very-large-file.txt" -ChunkSize 2MB -BucketDirectory "uploads/2mb-chunks"
    $result3 | Format-Table Name, @{Name="SizeMB";Expression={[math]::Round($_.Size/1MB,2)}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    "=== 3. Multiple File Chunked Upload ==="
    # Upload multiple files in one operation
    "Uploading multiple files with chunked transfer..."
    $multiResult = New-MinIOObjectChunked -BucketName $bucketName -Files @("small-file.txt", "medium-file.txt") -ChunkSize 1MB -BucketDirectory "uploads/multi-file"
    $multiResult | Format-Table Name, @{Name="SizeMB";Expression={[math]::Round($_.Size/1MB,2)}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    "=== 4. Chunked Download Operations ==="
    
    # Download with chunked transfer
    "Downloading large file with chunked transfer..."
    $downloadResult = Get-MinIOObjectContentChunked -BucketName $bucketName -ObjectName "uploads/2mb-chunks/very-large-file.txt" -FilePath "downloaded-very-large.txt" -ChunkSize 1MB
    $downloadResult | Format-Table @{Name="LocalFile";Expression={$_.FilePath.Name}}, @{Name="SizeMB";Expression={[math]::Round($_.FilePath.Length/1MB,2)}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    # Verify download integrity
    if (Test-Path "downloaded-very-large.txt") {
        $originalSize = (Get-Item "very-large-file.txt").Length
        $downloadedSize = (Get-Item "downloaded-very-large.txt").Length
        if ($originalSize -eq $downloadedSize) {
            "✅ Download integrity verified - sizes match ($originalSize bytes)"
        } else {
            "❌ Download integrity check failed - size mismatch"
        }
    }
    ""

    "=== 5. Performance Comparison ==="
    # Compare regular vs chunked upload for the same file
    "Comparing regular vs chunked upload performance..."
    
    # Regular upload
    $regularStart = Get-Date
    $regularResult = New-MinIOObject -BucketName $bucketName -Files "large-file.txt" -BucketDirectory "comparison/regular"
    $regularEnd = Get-Date
    $regularDuration = $regularEnd - $regularStart
    
    # Chunked upload
    $chunkedStart = Get-Date
    $chunkedResult = New-MinIOObjectChunked -BucketName $bucketName -Files "large-file.txt" -ChunkSize 1MB -BucketDirectory "comparison/chunked"
    $chunkedEnd = Get-Date
    $chunkedDuration = $chunkedEnd - $chunkedStart
    
    "Performance Comparison for large-file.txt:"
    [PSCustomObject]@{
        Method = "Regular"
        Duration = $regularDuration.ToString("mm\:ss\.fff")
        Speed = $regularResult.AverageSpeedFormatted
    } | Format-Table
    
    [PSCustomObject]@{
        Method = "Chunked"
        Duration = $chunkedDuration.ToString("mm\:ss\.fff")
        Speed = $chunkedResult.AverageSpeedFormatted
    } | Format-Table
    ""

    "=== 6. Listing Uploaded Files ==="
    # Show all uploaded files with their details
    $allUploads = Get-MinIOObject -BucketName $bucketName -Prefix "uploads/" -ObjectsOnly -SortBy "Size" -Descending
    "All uploaded files (sorted by size):"
    $allUploads | Format-Table Name, @{Name="SizeMB";Expression={[math]::Round($_.Size/1MB,2)}}, LastModified -AutoSize
    ""

    "=== 7. Chunk Size Recommendations ==="
    "Chunk Size Recommendations:"
    "• Files < 10MB: Use regular upload (New-MinIOObject)"
    "• Files 10MB - 100MB: Use 1-5MB chunks"
    "• Files 100MB - 1GB: Use 5-10MB chunks"
    "• Files > 1GB: Use 10-50MB chunks"
    "• Network considerations: Smaller chunks for unstable connections"
    ""

    # Clean up
    "Cleaning up demo files..."
    $allObjects = Get-MinIOObject -BucketName $bucketName
    foreach ($obj in $allObjects) {
        if (-not $obj.IsDirectory) {
            Remove-MinIOObject -BucketName $bucketName -ObjectName $obj.Name -Force
        }
    }
    Remove-MinIOBucket -BucketName $bucketName -Force
    Remove-Item "small-file.txt", "medium-file.txt", "large-file.txt", "very-large-file.txt", "downloaded-very-large.txt" -Force -ErrorAction SilentlyContinue

    "✅ Chunked operations demo completed"

} catch {
    "❌ Error: $($_.Exception.Message)"
}
