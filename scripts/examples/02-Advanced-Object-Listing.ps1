# PSMinIO Advanced Object Listing Example
# Demonstrates the powerful Get-MinIOObject cmdlet with filtering, sorting, and pagination

# Import the module
Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1

# Connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

try {
    # Connect to MinIO
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    "Connected to MinIO server for advanced object listing demo"

    # Create a demo bucket
    $bucketName = "demo-listing-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $bucketName
    "Created demo bucket: $bucketName"

    # Create sample files with different sizes and organize them in directories
    "Creating sample files..."
    
    # Documents directory
    "Small document content" | Out-File -FilePath "small-doc.txt" -Encoding UTF8
    "Medium document content " * 50 | Out-File -FilePath "medium-doc.txt" -Encoding UTF8
    "Large document content " * 200 | Out-File -FilePath "large-doc.txt" -Encoding UTF8
    
    # Images directory (simulated)
    "Image data " * 100 | Out-File -FilePath "image1.jpg" -Encoding UTF8
    "Image data " * 300 | Out-File -FilePath "image2.png" -Encoding UTF8
    
    # Logs directory
    "Log entry " * 20 | Out-File -FilePath "app.log" -Encoding UTF8
    "Error log " * 80 | Out-File -FilePath "error.log" -Encoding UTF8

    # Upload files to different directories
    New-MinIOObject -BucketName $bucketName -Files "small-doc.txt" -BucketDirectory "documents/2025"
    New-MinIOObject -BucketName $bucketName -Files "medium-doc.txt" -BucketDirectory "documents/2025"
    New-MinIOObject -BucketName $bucketName -Files "large-doc.txt" -BucketDirectory "documents/archive"
    New-MinIOObject -BucketName $bucketName -Files "image1.jpg" -BucketDirectory "media/images"
    New-MinIOObject -BucketName $bucketName -Files "image2.png" -BucketDirectory "media/images"
    New-MinIOObject -BucketName $bucketName -Files "app.log" -BucketDirectory "logs/application"
    New-MinIOObject -BucketName $bucketName -Files "error.log" -BucketDirectory "logs/system"

    "Sample files uploaded to various directories"
    ""

    # Demonstrate different listing capabilities
    "=== 1. List All Objects ==="
    $allObjects = Get-MinIOObject -BucketName $bucketName
    $allObjects | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 2. Filter by Prefix ==="
    $documentsObjects = Get-MinIOObject -BucketName $bucketName -Prefix "documents/"
    $documentsObjects | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 3. Sort by Size (Descending) ==="
    $sortedBySize = Get-MinIOObject -BucketName $bucketName -SortBy "Size" -Descending
    $sortedBySize | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 4. Sort by Name (Ascending) ==="
    $sortedByName = Get-MinIOObject -BucketName $bucketName -SortBy "Name"
    $sortedByName | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 5. Limit Results ==="
    $limitedResults = Get-MinIOObject -BucketName $bucketName -MaxObjects 3 -SortBy "Size" -Descending
    $limitedResults | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 6. Files Only (Exclude Directories) ==="
    $filesOnly = Get-MinIOObject -BucketName $bucketName -ObjectsOnly
    $filesOnly | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 7. Specific Object Lookup ==="
    $specificObject = Get-MinIOObject -BucketName $bucketName -ObjectName "documents/2025/small-doc.txt"
    if ($specificObject) {
        $specificObject | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    } else {
        "Object not found"
    }
    ""

    "=== 8. Complex Filtering Example ==="
    "Large files in media directory:"
    $largeMediaFiles = Get-MinIOObject -BucketName $bucketName -Prefix "media/" -SortBy "Size" -Descending | Where-Object { $_.Size -gt 1000 }
    $largeMediaFiles | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize

    # Clean up
    "Cleaning up demo files..."
    $allObjects = Get-MinIOObject -BucketName $bucketName
    foreach ($obj in $allObjects) {
        Remove-MinIOObject -BucketName $bucketName -ObjectName $obj.Name -Force
    }
    Remove-MinIOBucket -BucketName $bucketName -Force
    Remove-Item "small-doc.txt", "medium-doc.txt", "large-doc.txt", "image1.jpg", "image2.png", "app.log", "error.log" -Force -ErrorAction SilentlyContinue

    "✅ Advanced object listing demo completed"

} catch {
    "❌ Error: $($_.Exception.Message)"
}
