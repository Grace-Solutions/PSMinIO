# PSMinIO Directory and Folder Management Example
# Demonstrates creating nested directory structures and organizing files

# Import the module
Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1

# Connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

try {
    # Connect to MinIO
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    "Connected to MinIO server for directory management demo"

    # Create a demo bucket
    $bucketName = "directory-demo-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $bucketName
    "Created demo bucket: $bucketName"

    # Create sample files for different scenarios
    "Creating sample files..."
    "Project documentation" | Out-File -FilePath "README.md" -Encoding UTF8
    "Configuration settings" | Out-File -FilePath "config.json" -Encoding UTF8
    "Application source code" | Out-File -FilePath "app.py" -Encoding UTF8
    "Test data" | Out-File -FilePath "test-data.csv" -Encoding UTF8
    "Build output" | Out-File -FilePath "app.exe" -Encoding UTF8

    "=== 1. Creating Explicit Folder Structures ==="
    # Create explicit folder structures using New-MinIOFolder
    New-MinIOFolder -BucketName $bucketName -FolderName "projects/web-app/src"
    New-MinIOFolder -BucketName $bucketName -FolderName "projects/web-app/tests"
    New-MinIOFolder -BucketName $bucketName -FolderName "projects/web-app/docs"
    New-MinIOFolder -BucketName $bucketName -FolderName "projects/api/v1"
    New-MinIOFolder -BucketName $bucketName -FolderName "projects/api/v2"
    "Created explicit folder structures"
    ""

    "=== 2. Automatic Directory Creation with BucketDirectory ==="
    # Upload files with automatic directory creation
    New-MinIOObject -BucketName $bucketName -Files "README.md" -BucketDirectory "projects/web-app/docs"
    New-MinIOObject -BucketName $bucketName -Files "config.json" -BucketDirectory "projects/web-app/config"
    New-MinIOObject -BucketName $bucketName -Files "app.py" -BucketDirectory "projects/web-app/src"
    New-MinIOObject -BucketName $bucketName -Files "test-data.csv" -BucketDirectory "projects/web-app/tests/data"
    New-MinIOObject -BucketName $bucketName -Files "app.exe" -BucketDirectory "releases/v1.0/binaries"
    "Uploaded files with automatic directory creation"
    ""

    "=== 3. Multi-Level Directory Structure ==="
    # Create a complex directory structure
    $complexStructure = @(
        "company/departments/engineering/teams/backend",
        "company/departments/engineering/teams/frontend", 
        "company/departments/engineering/teams/devops",
        "company/departments/marketing/campaigns/2025",
        "company/departments/hr/policies/remote-work"
    )

    foreach ($folder in $complexStructure) {
        New-MinIOFolder -BucketName $bucketName -FolderName $folder
    }
    "Created complex multi-level directory structure"
    ""

    "=== 4. Viewing Directory Structure ==="
    # List all objects to see the directory structure
    $allObjects = Get-MinIOObject -BucketName $bucketName
    "Complete directory and file structure:"
    $allObjects | Sort-Object Name | Format-Table Name, @{Name="Type";Expression={if($_.IsDirectory){"Directory"}else{"File"}}}, @{Name="Size";Expression={if($_.IsDirectory){"-"}else{"$($_.Size) B"}}} -AutoSize
    ""

    "=== 5. Filtering by Directory ==="
    # Show objects in specific directories
    "Files in projects/web-app/:"
    $webAppFiles = Get-MinIOObject -BucketName $bucketName -Prefix "projects/web-app/"
    $webAppFiles | Format-Table Name, @{Name="Type";Expression={if($_.IsDirectory){"Directory"}else{"File"}}}, @{Name="Size";Expression={if($_.IsDirectory){"-"}else{"$($_.Size) B"}}} -AutoSize
    ""

    "Engineering team directories:"
    $engineeringDirs = Get-MinIOObject -BucketName $bucketName -Prefix "company/departments/engineering/teams/"
    $engineeringDirs | Format-Table Name, @{Name="Type";Expression={if($_.IsDirectory){"Directory"}else{"File"}}} -AutoSize
    ""

    "=== 6. Directory-Only Listing ==="
    # Show only directories (folders)
    $directoriesOnly = Get-MinIOObject -BucketName $bucketName | Where-Object { $_.IsDirectory }
    "Directories only:"
    $directoriesOnly | Sort-Object Name | Format-Table Name -AutoSize
    ""

    "=== 7. Files-Only Listing ==="
    # Show only files (excluding directories)
    $filesOnly = Get-MinIOObject -BucketName $bucketName -ObjectsOnly
    "Files only:"
    $filesOnly | Format-Table Name, @{Name="Size";Expression={"$($_.Size) B"}}, LastModified -AutoSize
    ""

    "=== 8. Organizing Files by Date ==="
    # Create date-based directory structure
    $currentDate = Get-Date
    $yearMonth = $currentDate.ToString("yyyy/MM")
    $dailyFolder = $currentDate.ToString("yyyy/MM/dd")
    
    # Upload files to date-based directories
    "Sample log entry" | Out-File -FilePath "daily.log" -Encoding UTF8
    "Backup data" | Out-File -FilePath "backup.zip" -Encoding UTF8
    
    New-MinIOObject -BucketName $bucketName -Files "daily.log" -BucketDirectory "logs/$dailyFolder"
    New-MinIOObject -BucketName $bucketName -Files "backup.zip" -BucketDirectory "backups/$yearMonth"
    
    "Created date-based directory structure and uploaded files"
    ""

    # Show the final structure
    "=== Final Directory Structure ==="
    $finalStructure = Get-MinIOObject -BucketName $bucketName | Sort-Object Name
    $finalStructure | Format-Table Name, @{Name="Type";Expression={if($_.IsDirectory){"Directory"}else{"File"}}}, @{Name="Size";Expression={if($_.IsDirectory){"-"}else{"$($_.Size) B"}}} -AutoSize

    # Clean up
    "Cleaning up demo files..."
    $allObjects = Get-MinIOObject -BucketName $bucketName
    foreach ($obj in $allObjects) {
        if (-not $obj.IsDirectory) {
            Remove-MinIOObject -BucketName $bucketName -ObjectName $obj.Name -Force
        }
    }
    Remove-MinIOBucket -BucketName $bucketName -Force
    Remove-Item "README.md", "config.json", "app.py", "test-data.csv", "app.exe", "daily.log", "backup.zip" -Force -ErrorAction SilentlyContinue

    "✅ Directory management demo completed"

} catch {
    "❌ Error: $($_.Exception.Message)"
}
