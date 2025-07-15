# PSMinIO Bulk Operations Example
# Demonstrates bulk file operations, batch processing, and automation scenarios

# Import the module
Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1

# Connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

try {
    # Connect to MinIO
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    "Connected to MinIO server for bulk operations demo"

    # Create a demo bucket
    $bucketName = "bulk-demo-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $bucketName
    "Created demo bucket: $bucketName"

    "=== 1. Creating Multiple Test Files ==="
    # Create a variety of files for bulk operations
    $testFiles = @()
    
    # Create document files
    for ($i = 1; $i -le 5; $i++) {
        $fileName = "document-$i.txt"
        "Document $i content - $(Get-Date)" | Out-File -FilePath $fileName -Encoding UTF8
        $testFiles += $fileName
    }
    
    # Create data files
    for ($i = 1; $i -le 3; $i++) {
        $fileName = "data-$i.csv"
        "Name,Value,Date`nItem$i,$(Get-Random -Minimum 100 -Maximum 1000),$(Get-Date)" | Out-File -FilePath $fileName -Encoding UTF8
        $testFiles += $fileName
    }
    
    # Create log files
    for ($i = 1; $i -le 4; $i++) {
        $fileName = "log-$i.log"
        "$(Get-Date) - Log entry $i`n$(Get-Date) - Another log entry" | Out-File -FilePath $fileName -Encoding UTF8
        $testFiles += $fileName
    }
    
    "Created $($testFiles.Count) test files"
    Get-ChildItem "*.txt", "*.csv", "*.log" | Format-Table Name, @{Name="Size";Expression={"$($_.Length) bytes"}} -AutoSize
    ""

    "=== 2. Bulk Upload to Different Directories ==="
    # Upload different file types to organized directories
    
    # Upload documents
    $docFiles = Get-ChildItem "document-*.txt"
    if ($docFiles) {
        "Uploading $($docFiles.Count) document files..."
        $docResults = New-MinIOObject -BucketName $bucketName -Files $docFiles.Name -BucketDirectory "documents/$(Get-Date -Format 'yyyy/MM')"
        $docResults | Format-Table Name, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    }
    ""

    # Upload data files
    $dataFiles = Get-ChildItem "data-*.csv"
    if ($dataFiles) {
        "Uploading $($dataFiles.Count) data files..."
        $dataResults = New-MinIOObject -BucketName $bucketName -Files $dataFiles.Name -BucketDirectory "data/exports"
        $dataResults | Format-Table Name, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    }
    ""

    # Upload log files
    $logFiles = Get-ChildItem "log-*.log"
    if ($logFiles) {
        "Uploading $($logFiles.Count) log files..."
        $logResults = New-MinIOObject -BucketName $bucketName -Files $logFiles.Name -BucketDirectory "logs/application/$(Get-Date -Format 'yyyy-MM-dd')"
        $logResults | Format-Table Name, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    }
    ""

    "=== 3. Bulk File Analysis ==="
    # Analyze uploaded files by type and location
    $allObjects = Get-MinIOObject -BucketName $bucketName -ObjectsOnly
    
    "File distribution by directory:"
    $allObjects | Group-Object { ($_.Name -split '/')[0] } | Format-Table Name, Count -AutoSize
    ""

    "File distribution by extension:"
    $allObjects | Group-Object { [System.IO.Path]::GetExtension($_.Name) } | Format-Table Name, Count -AutoSize
    ""

    "Largest files:"
    $allObjects | Sort-Object Size -Descending | Select-Object -First 5 | Format-Table Name, @{Name="Size";Expression={"$($_.Size) bytes"}}, LastModified -AutoSize
    ""

    "=== 4. Batch Download Operations ==="
    # Download files by pattern/criteria
    
    # Create download directory
    $downloadDir = "downloads"
    if (-not (Test-Path $downloadDir)) {
        New-Item -ItemType Directory -Path $downloadDir | Out-Null
    }
    
    # Download all CSV files
    $csvObjects = Get-MinIOObject -BucketName $bucketName | Where-Object { $_.Name -like "*.csv" }
    "Downloading $($csvObjects.Count) CSV files..."
    foreach ($csvObj in $csvObjects) {
        $localFileName = [System.IO.Path]::GetFileName($csvObj.Name)
        $localPath = Join-Path $downloadDir $localFileName
        $downloadResult = Get-MinIOObjectContent -BucketName $bucketName -ObjectName $csvObj.Name -FilePath $localPath
        "Downloaded: $($csvObj.Name) -> $localPath"
    }
    ""

    # Download files from specific directory
    $docObjects = Get-MinIOObject -BucketName $bucketName -Prefix "documents/"
    "Downloading $($docObjects.Count) files from documents directory..."
    foreach ($docObj in $docObjects) {
        if (-not $docObj.IsDirectory) {
            $localFileName = [System.IO.Path]::GetFileName($docObj.Name)
            $localPath = Join-Path $downloadDir "doc-$localFileName"
            $downloadResult = Get-MinIOObjectContent -BucketName $bucketName -ObjectName $docObj.Name -FilePath $localPath
            "Downloaded: $($docObj.Name) -> $localPath"
        }
    }
    ""

    "=== 5. Bulk Operations with Filtering ==="
    # Demonstrate advanced bulk operations
    
    # Find and process files by size
    $largeFiles = Get-MinIOObject -BucketName $bucketName -ObjectsOnly | Where-Object { $_.Size -gt 50 }
    "Processing $($largeFiles.Count) files larger than 50 bytes:"
    $largeFiles | Format-Table Name, @{Name="Size";Expression={"$($_.Size) bytes"}}, LastModified -AutoSize
    ""

    # Find files by date (last hour)
    $recentFiles = Get-MinIOObject -BucketName $bucketName -ObjectsOnly | Where-Object { $_.LastModified -gt (Get-Date).AddHours(-1) }
    "Files uploaded in the last hour: $($recentFiles.Count)"
    $recentFiles | Format-Table Name, LastModified -AutoSize
    ""

    "=== 6. Bulk Cleanup Operations ==="
    # Demonstrate selective cleanup
    
    # Remove files by pattern
    $logObjects = Get-MinIOObject -BucketName $bucketName | Where-Object { $_.Name -like "*log*" -and -not $_.IsDirectory }
    "Removing $($logObjects.Count) log files..."
    foreach ($logObj in $logObjects) {
        Remove-MinIOObject -BucketName $bucketName -ObjectName $logObj.Name -Force
        "Removed: $($logObj.Name)"
    }
    ""

    # Remove files older than a certain date (simulated)
    $cutoffDate = (Get-Date).AddMinutes(-5)  # 5 minutes ago for demo
    $oldFiles = Get-MinIOObject -BucketName $bucketName -ObjectsOnly | Where-Object { $_.LastModified -lt $cutoffDate }
    if ($oldFiles.Count -gt 0) {
        "Removing $($oldFiles.Count) files older than $cutoffDate..."
        foreach ($oldFile in $oldFiles) {
            Remove-MinIOObject -BucketName $bucketName -ObjectName $oldFile.Name -Force
            "Removed: $($oldFile.Name)"
        }
    } else {
        "No files older than $cutoffDate found"
    }
    ""

    "=== 7. Final Statistics ==="
    # Show final state
    $remainingObjects = Get-MinIOObject -BucketName $bucketName -ObjectsOnly
    "Remaining files after cleanup: $($remainingObjects.Count)"
    if ($remainingObjects.Count -gt 0) {
        $remainingObjects | Format-Table Name, @{Name="Size";Expression={"$($_.Size) bytes"}}, LastModified -AutoSize
    }
    ""

    "Downloaded files:"
    if (Test-Path $downloadDir) {
        Get-ChildItem $downloadDir | Format-Table Name, @{Name="Size";Expression={"$($_.Length) bytes"}}, LastWriteTime -AutoSize
    }

    # Clean up
    "Cleaning up demo files..."
    $allObjects = Get-MinIOObject -BucketName $bucketName
    foreach ($obj in $allObjects) {
        if (-not $obj.IsDirectory) {
            Remove-MinIOObject -BucketName $bucketName -ObjectName $obj.Name -Force
        }
    }
    Remove-MinIOBucket -BucketName $bucketName -Force
    
    # Clean up local files
    Remove-Item $testFiles -Force -ErrorAction SilentlyContinue
    if (Test-Path $downloadDir) {
        Remove-Item $downloadDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    "✅ Bulk operations demo completed"

} catch {
    "❌ Error: $($_.Exception.Message)"
}
