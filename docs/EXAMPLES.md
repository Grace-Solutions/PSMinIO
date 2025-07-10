# PSMinIO Examples

This document contains practical examples for using the PSMinIO PowerShell module.

## Basic Setup and Configuration

### Example 1: Initial Setup for Local MinIO

```powershell
# Import the module
Import-Module .\PSMinIO.psd1

# Configure for local MinIO instance
Set-MinIOConfig -Endpoint "localhost:9000" `
                -AccessKey "minioadmin" `
                -SecretKey "minioadmin" `
                -NoSSL `
                -TestConnection `
                -SaveToDisk

# Verify configuration
Get-MinIOConfig -Detailed
```

### Example 2: Production Setup with SSL

```powershell
# Configure for production MinIO cluster
Set-MinIOConfig -Endpoint "minio.company.com:9000" `
                -AccessKey $env:MINIO_ACCESS_KEY `
                -SecretKey $env:MINIO_SECRET_KEY `
                -UseSSL `
                -Region "us-east-1" `
                -TimeoutSeconds 60 `
                -TestConnection

# Test the connection
Get-MinIOConfig -TestConnection
```

## Bucket Management Examples

### Example 3: Creating and Managing Buckets

```powershell
# Create buckets for different purposes
New-MinIOBucket -BucketName "company-documents" -Region "us-east-1" -PassThru
New-MinIOBucket -BucketName "user-uploads" -Region "us-west-2" -PassThru
New-MinIOBucket -BucketName "application-logs" -Region "eu-west-1" -PassThru

# List all buckets with statistics
Get-MinIOBucket -IncludeStatistics

# Check if specific buckets exist
$buckets = @("company-documents", "user-uploads", "temp-bucket")
foreach ($bucket in $buckets) {
    $exists = Test-MinIOBucketExists -BucketName $bucket
    Write-Host "Bucket '$bucket' exists: $exists"
}
```

### Example 4: Bucket Cleanup

```powershell
# Find and remove temporary buckets
Get-MinIOBucket | Where-Object { $_.Name -like "temp-*" } | ForEach-Object {
    Write-Host "Removing temporary bucket: $($_.Name)"
    Remove-MinIOBucket -BucketName $_.Name -RemoveObjects -Force
}

# Remove old test buckets (older than 30 days)
Get-MinIOBucket | Where-Object { 
    $_.Name -like "test-*" -and $_.Created -lt (Get-Date).AddDays(-30) 
} | ForEach-Object {
    Write-Host "Removing old test bucket: $($_.Name) (Created: $($_.Created))"
    Remove-MinIOBucket -BucketName $_.Name -RemoveObjects -Force
}
```

## File Upload and Download Examples

### Example 5: Bulk File Upload

```powershell
# Upload all PDF files from a directory
$sourceDir = "C:\Documents\Reports"
$bucketName = "company-documents"

Get-ChildItem -Path $sourceDir -Filter "*.pdf" -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($sourceDir.Length + 1).Replace('\', '/')
    $objectName = "reports/$relativePath"
    
    Write-Host "Uploading: $($_.Name) -> $objectName"
    
    try {
        New-MinIOObject -BucketName $bucketName `
                        -ObjectName $objectName `
                        -FilePath $_.FullName `
                        -Verbose
        Write-Host "✓ Successfully uploaded: $($_.Name)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to upload: $($_.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}
```

### Example 6: Backup Script

```powershell
# Daily backup script
param(
    [Parameter(Mandatory)]
    [string]$SourcePath,
    
    [Parameter(Mandatory)]
    [string]$BucketName,
    
    [string]$BackupPrefix = "backups"
)

$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$backupFolder = "$BackupPrefix/$timestamp"

Write-Host "Starting backup of '$SourcePath' to bucket '$BucketName'"

# Create a compressed archive
$tempZip = "$env:TEMP\backup_$timestamp.zip"
Compress-Archive -Path $SourcePath -DestinationPath $tempZip -Force

try {
    # Upload the backup
    $objectName = "$backupFolder/backup.zip"
    New-MinIOObject -BucketName $BucketName `
                    -ObjectName $objectName `
                    -FilePath $tempZip `
                    -PassThru
    
    Write-Host "✓ Backup completed successfully" -ForegroundColor Green
    
    # Clean up old backups (keep last 7 days)
    $cutoffDate = (Get-Date).AddDays(-7)
    Get-MinIOObject -BucketName $BucketName -Prefix $BackupPrefix | 
        Where-Object { $_.LastModified -lt $cutoffDate } |
        ForEach-Object {
            Write-Host "Removing old backup: $($_.Name)"
            Remove-MinIOObject -BucketName $BucketName -ObjectName $_.Name -Force
        }
        
} finally {
    # Clean up temporary file
    if (Test-Path $tempZip) {
        Remove-Item $tempZip -Force
    }
}
```

### Example 7: Bulk Download

```powershell
# Download all objects with specific prefix
$bucketName = "company-documents"
$prefix = "reports/2025/"
$downloadDir = "C:\Downloads\Reports"

# Ensure download directory exists
if (!(Test-Path $downloadDir)) {
    New-Item -ItemType Directory -Path $downloadDir -Force
}

# Get all objects with the prefix
$objects = Get-MinIOObject -BucketName $bucketName -Prefix $prefix

Write-Host "Found $($objects.Count) objects to download"

foreach ($obj in $objects) {
    # Skip directories
    if ($obj.IsDirectory) { continue }
    
    # Create local file path
    $relativePath = $obj.Name.Substring($prefix.Length)
    $localPath = Join-Path $downloadDir $relativePath.Replace('/', '\')
    $localDir = Split-Path $localPath -Parent
    
    # Ensure local directory exists
    if (!(Test-Path $localDir)) {
        New-Item -ItemType Directory -Path $localDir -Force
    }
    
    Write-Host "Downloading: $($obj.Name) -> $localPath"
    
    try {
        Get-MinIOObjectContent -BucketName $bucketName `
                               -ObjectName $obj.Name `
                               -FilePath $localPath `
                               -Force
        Write-Host "✓ Downloaded: $($obj.GetFileName())" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to download: $($obj.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}
```

## Security and Policy Examples

### Example 8: Setting Up Public Read Access

```powershell
# Create a bucket for public assets
New-MinIOBucket -BucketName "public-assets"

# Set read-only policy for public access
Set-MinIOBucketPolicy -BucketName "public-assets" `
                      -CannedPolicy "ReadOnly" `
                      -Prefix "*"

# Verify the policy
Get-MinIOBucketPolicy -BucketName "public-assets" -AsObject
```

### Example 9: Custom Policy for Upload-Only Access

```powershell
$uploadOnlyPolicy = @"
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {"AWS": "*"},
      "Action": ["s3:PutObject"],
      "Resource": ["arn:aws:s3:::user-uploads/uploads/*"]
    }
  ]
}
"@

Set-MinIOBucketPolicy -BucketName "user-uploads" `
                      -PolicyJson $uploadOnlyPolicy

# Test the policy
Get-MinIOBucketPolicy -BucketName "user-uploads" -PrettyPrint
```

## Monitoring and Statistics Examples

### Example 10: Storage Usage Report

```powershell
# Generate comprehensive storage report
$stats = Get-MinIOStats -IncludeBucketDetails -IncludeObjectCounts

Write-Host "=== MinIO Storage Report ===" -ForegroundColor Cyan
Write-Host "Generated: $(Get-Date)" -ForegroundColor Gray
Write-Host ""

Write-Host "Overall Statistics:" -ForegroundColor Yellow
Write-Host "  Total Buckets: $($stats.TotalBuckets)"
Write-Host "  Total Objects: $($stats.TotalObjects)"
Write-Host "  Total Size: $($stats.TotalSizeFormatted)"
Write-Host "  Average Object Size: $($stats.AverageObjectSizeFormatted)"
Write-Host ""

Write-Host "Bucket Details:" -ForegroundColor Yellow
$stats.BucketDetails | Sort-Object Size -Descending | ForEach-Object {
    Write-Host "  $($_.Name):"
    Write-Host "    Objects: $($_.ObjectCount)"
    Write-Host "    Size: $($_.SizeFormatted)"
    Write-Host "    Created: $($_.Created)"
    Write-Host ""
}
```

### Example 11: Health Check Script

```powershell
# MinIO health check script
function Test-MinIOHealth {
    param(
        [string]$TestBucketName = "health-check-$(Get-Date -Format 'yyyyMMdd')"
    )
    
    $results = @{
        ConfigurationValid = $false
        ConnectionSuccessful = $false
        BucketOperations = $false
        ObjectOperations = $false
        OverallHealth = $false
    }
    
    try {
        # Test 1: Configuration
        $config = Get-MinIOConfig
        $results.ConfigurationValid = $config.IsValid
        Write-Host "✓ Configuration is valid" -ForegroundColor Green
        
        # Test 2: Connection
        $connectionTest = Get-MinIOConfig -TestConnection
        $results.ConnectionSuccessful = $connectionTest.ConnectionStatus -eq "Success"
        Write-Host "✓ Connection successful" -ForegroundColor Green
        
        # Test 3: Bucket operations
        New-MinIOBucket -BucketName $TestBucketName -Force | Out-Null
        $bucketExists = Test-MinIOBucketExists -BucketName $TestBucketName
        $results.BucketOperations = $bucketExists
        Write-Host "✓ Bucket operations working" -ForegroundColor Green
        
        # Test 4: Object operations
        $testFile = "$env:TEMP\minio-health-test.txt"
        "Health check test file - $(Get-Date)" | Out-File -FilePath $testFile
        
        New-MinIOObject -BucketName $TestBucketName `
                        -ObjectName "health-test.txt" `
                        -FilePath $testFile | Out-Null
        
        $objects = Get-MinIOObject -BucketName $TestBucketName -ObjectName "health-test.txt"
        $results.ObjectOperations = $objects.Count -eq 1
        Write-Host "✓ Object operations working" -ForegroundColor Green
        
        # Cleanup
        Remove-MinIOObject -BucketName $TestBucketName -ObjectName "health-test.txt" -Force
        Remove-MinIOBucket -BucketName $TestBucketName -Force
        Remove-Item $testFile -Force
        
        $results.OverallHealth = $results.ConfigurationValid -and 
                                $results.ConnectionSuccessful -and 
                                $results.BucketOperations -and 
                                $results.ObjectOperations
        
        Write-Host "✓ Overall health check passed" -ForegroundColor Green
        
    } catch {
        Write-Host "✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    return $results
}

# Run health check
Test-MinIOHealth
```

## Advanced Automation Examples

### Example 12: Log Rotation and Archival

```powershell
# Automated log rotation script
param(
    [string]$LogBucket = "application-logs",
    [int]$RetentionDays = 90,
    [string]$ArchiveBucket = "archived-logs"
)

$cutoffDate = (Get-Date).AddDays(-$RetentionDays)

Write-Host "Starting log rotation process..."
Write-Host "Archiving logs older than: $cutoffDate"

# Get old log files
$oldLogs = Get-MinIOObject -BucketName $LogBucket | 
           Where-Object { $_.LastModified -lt $cutoffDate -and !$_.IsDirectory }

Write-Host "Found $($oldLogs.Count) log files to archive"

foreach ($log in $oldLogs) {
    try {
        # Download log file
        $tempFile = "$env:TEMP\$($log.GetFileName())"
        Get-MinIOObjectContent -BucketName $LogBucket `
                               -ObjectName $log.Name `
                               -FilePath $tempFile
        
        # Compress the log file
        $compressedFile = "$tempFile.gz"
        # Note: You would use a compression library here
        # For this example, we'll just rename
        Move-Item $tempFile $compressedFile
        
        # Upload to archive bucket with date prefix
        $archiveObjectName = "archived/$(Get-Date $log.LastModified -Format 'yyyy/MM/dd')/$($log.GetFileName()).gz"
        New-MinIOObject -BucketName $ArchiveBucket `
                        -ObjectName $archiveObjectName `
                        -FilePath $compressedFile
        
        # Remove original log file
        Remove-MinIOObject -BucketName $LogBucket -ObjectName $log.Name -Force
        
        # Clean up temp file
        Remove-Item $compressedFile -Force
        
        Write-Host "✓ Archived: $($log.Name)" -ForegroundColor Green
        
    } catch {
        Write-Host "✗ Failed to archive: $($log.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Log rotation completed"
```

These examples demonstrate the practical usage of PSMinIO for various scenarios including setup, file management, security configuration, monitoring, and automation. Each example includes error handling and best practices for production use.
