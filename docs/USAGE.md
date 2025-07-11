# PSMinIO Usage Guide

This comprehensive guide covers all aspects of using the PSMinIO PowerShell module for MinIO object storage operations.

## Table of Contents

- [Installation](#installation)
- [Connection Management](#connection-management)
- [Bucket Operations](#bucket-operations)
- [Object Operations](#object-operations)
- [Advanced Object Listing](#advanced-object-listing)
- [Directory and Folder Management](#directory-and-folder-management)
- [Chunked Operations](#chunked-operations)
- [Security and Policies](#security-and-policies)
- [Performance and Monitoring](#performance-and-monitoring)
- [Advanced Usage Patterns](#advanced-usage-patterns)
- [Troubleshooting](#troubleshooting)

## Installation

### Prerequisites

- PowerShell 5.1+ or PowerShell 7+
- .NET Framework 4.7.2+ (for PowerShell 5.1) or .NET Core/.NET 5+ (for PowerShell 7+)

### Import the Module

```powershell
# Import the module from the Module directory
Import-Module .\Module\PSMinIO\PSMinIO.psd1

# Verify the module is loaded and check available cmdlets
Get-Module PSMinIO
Get-Command -Module PSMinIO
```

## Connection Management

### Establishing Connections

PSMinIO uses a modern connection-based approach with the `Connect-MinIO` cmdlet:

```powershell
# Connect to MinIO server with SSL
$connection = Connect-MinIO -Endpoint "https://minio.example.com" -AccessKey "your-access-key" -SecretKey "your-secret-key"

# Connect to local MinIO (no SSL)
$connection = Connect-MinIO -Endpoint "http://localhost:9000" -AccessKey "minioadmin" -SecretKey "minioadmin"

# Connect with custom port
$connection = Connect-MinIO -Endpoint "https://minio.example.com:9443" -AccessKey "your-access-key" -SecretKey "your-secret-key"
```

### Connection Options

```powershell
# Skip SSL certificate validation (for self-signed certificates)
$connection = Connect-MinIO -Endpoint "https://minio.internal.com" -AccessKey "key" -SecretKey "secret" -SkipCertificateValidation

# Connection with region specification
$connection = Connect-MinIO -Endpoint "https://s3.amazonaws.com" -AccessKey "key" -SecretKey "secret" -Region "us-west-2"
```

### Advanced Configuration

```powershell
# Set configuration with custom region and timeout
Set-MinIOConfig -Endpoint "minio.example.com:9000" `
                -AccessKey "your-access-key" `
                -SecretKey "your-secret-key" `
                -UseSSL `
                -Region "us-west-2" `
                -TimeoutSeconds 60 `
                -SaveToDisk `
                -TestConnection
```

### View Current Configuration

```powershell
# Basic configuration view
Get-MinIOConfig

# Detailed configuration with connection test
Get-MinIOConfig -Detailed -TestConnection

# Show sensitive information (use with caution)
Get-MinIOConfig -ShowSensitive
```

## Bucket Operations

### Create Buckets

```powershell
# Create a simple bucket
New-MinIOBucket -BucketName "my-data-bucket"

# Create bucket with specific region
New-MinIOBucket -BucketName "eu-data-bucket" -Region "eu-west-1"

# Create bucket and return information
New-MinIOBucket -BucketName "logs-bucket" -PassThru

# Force creation (no error if exists)
New-MinIOBucket -BucketName "existing-bucket" -Force
```

### List Buckets

```powershell
# List all buckets
Get-MinIOBucket

# Get specific bucket information
Get-MinIOBucket -BucketName "my-data-bucket"

# Include statistics (object count and size)
Get-MinIOBucket -IncludeStatistics

# Get bucket with statistics
Get-MinIOBucket -BucketName "my-data-bucket" -IncludeStatistics
```

### Check Bucket Existence

```powershell
# Simple existence check
Test-MinIOBucketExists -BucketName "my-data-bucket"

# Detailed existence information
Test-MinIOBucketExists -BucketName "my-data-bucket" -Detailed
```

### Remove Buckets

```powershell
# Remove empty bucket
Remove-MinIOBucket -BucketName "old-bucket"

# Remove bucket and all its objects
Remove-MinIOBucket -BucketName "temp-bucket" -RemoveObjects

# Force removal without confirmation
Remove-MinIOBucket -BucketName "test-bucket" -Force
```

## Object Operations

### Upload Objects

```powershell
# Upload a single file
New-MinIOObject -BucketName "my-data-bucket" `
                -ObjectName "documents/report.pdf" `
                -FilePath "C:\Reports\monthly-report.pdf"

# Upload with custom content type
New-MinIOObject -BucketName "web-assets" `
                -ObjectName "images/logo.png" `
                -FilePath "C:\Assets\logo.png" `
                -ContentType "image/png"

# Upload and return object information
New-MinIOObject -BucketName "uploads" `
                -ObjectName "data.csv" `
                -FilePath "C:\Data\export.csv" `
                -PassThru

# Force overwrite existing object
New-MinIOObject -BucketName "backups" `
                -ObjectName "backup.zip" `
                -FilePath "C:\Backups\latest.zip" `
                -Force
```

### List Objects

```powershell
# List all objects in a bucket
Get-MinIOObject -BucketName "my-data-bucket"

# List objects with prefix
Get-MinIOObject -BucketName "logs" -Prefix "2025/01/"

# List specific object
Get-MinIOObject -BucketName "documents" -ObjectName "report.pdf"

# List with filtering and sorting
Get-MinIOObject -BucketName "media" `
                -Prefix "images/" `
                -ObjectsOnly `
                -SortBy "Size" `
                -Descending `
                -MaxObjects 50
```

### Download Objects

```powershell
# Download a single object
Get-MinIOObjectContent -BucketName "documents" `
                       -ObjectName "report.pdf" `
                       -FilePath "C:\Downloads\report.pdf"

# Download with overwrite
Get-MinIOObjectContent -BucketName "backups" `
                       -ObjectName "backup.zip" `
                       -FilePath "C:\Restore\backup.zip" `
                       -Force

# Download and return file information
Get-MinIOObjectContent -BucketName "data" `
                       -ObjectName "export.csv" `
                       -FilePath "C:\Import\data.csv" `
                       -PassThru
```

### Remove Objects

```powershell
# Remove a single object
Remove-MinIOObject -BucketName "temp" -ObjectName "old-file.txt"

# Remove all objects with prefix
Remove-MinIOObject -BucketName "logs" `
                   -ObjectName "2024/" `
                   -RemovePrefix

# Force removal without confirmation
Remove-MinIOObject -BucketName "cache" `
                   -ObjectName "temp-data.json" `
                   -Force
```

## Advanced Object Listing

The `Get-MinIOObject` cmdlet provides powerful filtering, sorting, and pagination capabilities:

### Basic Object Listing

```powershell
# List all objects in a bucket
Get-MinIOObject -BucketName "my-bucket"

# List objects with a specific prefix
Get-MinIOObject -BucketName "my-bucket" -Prefix "documents/"

# List objects recursively (default behavior)
Get-MinIOObject -BucketName "my-bucket" -Prefix "logs/" -Recursive

# List objects non-recursively (current level only)
Get-MinIOObject -BucketName "my-bucket" -Prefix "logs/" -Recursive:$false
```

### Advanced Filtering

```powershell
# Get a specific object by exact name
Get-MinIOObject -BucketName "my-bucket" -ObjectName "documents/report.pdf"

# List only files (exclude directory markers)
Get-MinIOObject -BucketName "my-bucket" -ObjectsOnly

# Include object versions (for versioned buckets)
Get-MinIOObject -BucketName "my-bucket" -IncludeVersions

# Limit the number of results
Get-MinIOObject -BucketName "my-bucket" -MaxObjects 50
```

### Sorting Options

```powershell
# Sort by name (ascending - default)
Get-MinIOObject -BucketName "my-bucket" -SortBy "Name"

# Sort by name (descending)
Get-MinIOObject -BucketName "my-bucket" -SortBy "Name" -Descending

# Sort by file size (largest first)
Get-MinIOObject -BucketName "my-bucket" -SortBy "Size" -Descending

# Sort by last modified date (newest first)
Get-MinIOObject -BucketName "my-bucket" -SortBy "LastModified" -Descending

# Sort by ETag
Get-MinIOObject -BucketName "my-bucket" -SortBy "ETag"
```

### Complex Queries

```powershell
# Find large files in a specific directory
Get-MinIOObject -BucketName "media" -Prefix "videos/" -SortBy "Size" -Descending -MaxObjects 10

# Get recent files only
$recentFiles = Get-MinIOObject -BucketName "logs" -SortBy "LastModified" -Descending -MaxObjects 20

# Find files by pattern using PowerShell filtering
Get-MinIOObject -BucketName "documents" | Where-Object { $_.Name -like "*.pdf" -and $_.Size -gt 1MB }

# Get directory structure overview
Get-MinIOObject -BucketName "my-bucket" | Group-Object { ($_.Name -split '/')[0] } | Format-Table Name, Count
```

## Directory and Folder Management

### Creating Directory Structures

```powershell
# Create explicit folder structures
New-MinIOFolder -BucketName "my-bucket" -FolderName "projects/web-app/src"
New-MinIOFolder -BucketName "my-bucket" -FolderName "projects/web-app/docs"

# Create multiple folder levels at once
New-MinIOFolder -BucketName "my-bucket" -FolderName "company/departments/engineering/teams/backend"
```

### Automatic Directory Creation

```powershell
# Upload files with automatic directory creation using BucketDirectory
New-MinIOObject -BucketName "my-bucket" -Files "report.pdf" -BucketDirectory "documents/2025/january"

# Create nested directory structures automatically
New-MinIOObject -BucketName "my-bucket" -Files "config.json" -BucketDirectory "projects/web-app/config/production"
```

### Directory-Based Operations

```powershell
# List all directories (folder markers)
Get-MinIOObject -BucketName "my-bucket" | Where-Object { $_.IsDirectory }

# List files in a specific directory
Get-MinIOObject -BucketName "my-bucket" -Prefix "documents/2025/" -ObjectsOnly

# Organize files by date-based directories
$today = Get-Date -Format "yyyy/MM/dd"
New-MinIOObject -BucketName "logs" -Files "app.log" -BucketDirectory "daily-logs/$today"
```

## Chunked Operations

### Chunked Uploads

```powershell
# Upload large files with chunked transfer
New-MinIOObjectChunked -BucketName "media" -Files "large-video.mp4" -ChunkSize 10MB

# Upload with custom chunk size and directory
New-MinIOObjectChunked -BucketName "backups" -Files "database-backup.sql" -ChunkSize 5MB -BucketDirectory "daily-backups/$(Get-Date -Format 'yyyy-MM-dd')"

# Upload multiple large files
New-MinIOObjectChunked -BucketName "media" -Files @("video1.mp4", "video2.mp4") -ChunkSize 10MB -BucketDirectory "videos/uploads"
```

### Chunked Downloads

```powershell
# Download large files with chunked transfer
Get-MinIOObjectContentChunked -BucketName "media" -ObjectName "large-video.mp4" -FilePath "C:\Downloads\video.mp4" -ChunkSize 10MB

# Download with progress tracking
Get-MinIOObjectContentChunked -BucketName "backups" -ObjectName "large-backup.zip" -FilePath "C:\Restore\backup.zip" -ChunkSize 5MB
```

### Chunk Size Guidelines

```powershell
# Recommended chunk sizes based on file size:
# Files < 10MB: Use regular upload/download
# Files 10MB - 100MB: Use 1-5MB chunks
# Files 100MB - 1GB: Use 5-10MB chunks
# Files > 1GB: Use 10-50MB chunks

# Example with optimal chunk size selection
$fileSize = (Get-Item "large-file.zip").Length
$chunkSize = if ($fileSize -lt 10MB) { 1MB }
             elseif ($fileSize -lt 100MB) { 5MB }
             elseif ($fileSize -lt 1GB) { 10MB }
             else { 25MB }

New-MinIOObjectChunked -BucketName "uploads" -Files "large-file.zip" -ChunkSize $chunkSize
```

## Security and Policies

### Get Bucket Policies

```powershell
# Get policy as JSON
Get-MinIOBucketPolicy -BucketName "public-bucket"

# Get policy as structured object
Get-MinIOBucketPolicy -BucketName "public-bucket" -AsObject

# Get pretty-printed JSON
Get-MinIOBucketPolicy -BucketName "public-bucket" -PrettyPrint
```

### Set Bucket Policies

```powershell
# Set policy from JSON string
$policy = @"
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {"AWS": "*"},
      "Action": ["s3:GetObject"],
      "Resource": ["arn:aws:s3:::public-bucket/*"]
    }
  ]
}
"@

Set-MinIOBucketPolicy -BucketName "public-bucket" -PolicyJson $policy

# Set policy from file
Set-MinIOBucketPolicy -BucketName "secure-bucket" `
                      -PolicyFilePath "C:\Policies\secure-policy.json"

# Use canned policies
Set-MinIOBucketPolicy -BucketName "readonly-bucket" `
                      -CannedPolicy "ReadOnly"

Set-MinIOBucketPolicy -BucketName "upload-bucket" `
                      -CannedPolicy "WriteOnly" `
                      -Prefix "uploads/*"

# Validate policy without setting
Set-MinIOBucketPolicy -BucketName "test-bucket" `
                      -PolicyJson $policy `
                      -ValidateOnly
```

## Statistics and Monitoring

### Basic Statistics

```powershell
# Get basic statistics
Get-MinIOStats

# Include object counts (may be slow)
Get-MinIOStats -IncludeObjectCounts

# Detailed statistics with per-bucket information
Get-MinIOStats -IncludeBucketDetails -IncludeObjectCounts

# Limit object counting for performance
Get-MinIOStats -IncludeObjectCounts -MaxObjectsToCount 1000
```

## Advanced Usage

### Batch Operations

```powershell
# Upload multiple files
$files = Get-ChildItem "C:\Data\*.csv"
foreach ($file in $files) {
    New-MinIOObject -BucketName "data-lake" `
                    -ObjectName "csv-files/$($file.Name)" `
                    -FilePath $file.FullName `
                    -Verbose
}

# Download all objects with specific prefix
$objects = Get-MinIOObject -BucketName "backups" -Prefix "2025/01/"
foreach ($obj in $objects) {
    $localPath = "C:\Restore\$($obj.Name)"
    $localDir = Split-Path $localPath -Parent
    if (!(Test-Path $localDir)) { New-Item -ItemType Directory -Path $localDir -Force }
    
    Get-MinIOObjectContent -BucketName "backups" `
                           -ObjectName $obj.Name `
                           -FilePath $localPath `
                           -Verbose
}
```

### Pipeline Usage

```powershell
# Pipeline bucket operations
Get-MinIOBucket | Where-Object { $_.Name -like "temp-*" } | Remove-MinIOBucket -Force

# Pipeline object operations
Get-MinIOObject -BucketName "logs" -Prefix "old/" | 
    ForEach-Object { Remove-MinIOObject -BucketName $_.BucketName -ObjectName $_.Name -Force }
```

### Error Handling

```powershell
try {
    New-MinIOBucket -BucketName "test-bucket" -ErrorAction Stop
    Write-Host "Bucket created successfully"
} catch {
    Write-Error "Failed to create bucket: $($_.Exception.Message)"
}

# Using -WhatIf for testing
New-MinIOObject -BucketName "test" `
                -ObjectName "test.txt" `
                -FilePath "C:\test.txt" `
                -WhatIf
```

## Troubleshooting

### Common Issues

1. **Configuration Problems**
   ```powershell
   # Test configuration
   Get-MinIOConfig -TestConnection
   
   # Reset configuration
   Set-MinIOConfig -Endpoint "localhost:9000" `
                   -AccessKey "minioadmin" `
                   -SecretKey "minioadmin" `
                   -NoSSL `
                   -TestConnection
   ```

2. **Connection Issues**
   ```powershell
   # Check endpoint accessibility
   Test-NetConnection -ComputerName "minio.example.com" -Port 9000
   
   # Verify SSL settings
   Get-MinIOConfig -Detailed
   ```

3. **Permission Issues**
   ```powershell
   # Check bucket policy
   Get-MinIOBucketPolicy -BucketName "problem-bucket" -AsObject
   
   # Test with different credentials
   Set-MinIOConfig -AccessKey "admin" -SecretKey "admin-password"
   ```

### Verbose Logging

```powershell
# Enable verbose output for troubleshooting
Get-MinIOBucket -Verbose
New-MinIOObject -BucketName "test" -ObjectName "test.txt" -FilePath "C:\test.txt" -Verbose
```

### Performance Tips

1. **Use appropriate batch sizes for large operations**
2. **Limit object counting with `-MaxObjectsToCount` for large buckets**
3. **Use `-Force` parameter to avoid confirmation prompts in scripts**
4. **Test operations with `-WhatIf` before execution**

For more information, see the [API Reference](API.md) and [Examples](EXAMPLES.md).
