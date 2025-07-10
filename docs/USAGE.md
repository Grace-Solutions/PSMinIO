# PSMinIO Usage Guide

This guide provides comprehensive examples and usage patterns for the PSMinIO PowerShell module.

## Table of Contents

- [Installation](#installation)
- [Configuration](#configuration)
- [Bucket Operations](#bucket-operations)
- [Object Operations](#object-operations)
- [Security and Policies](#security-and-policies)
- [Statistics and Monitoring](#statistics-and-monitoring)
- [Advanced Usage](#advanced-usage)
- [Troubleshooting](#troubleshooting)

## Installation

### Prerequisites

- PowerShell 5.1+ or PowerShell 7+
- .NET Framework 4.7.2+ (for PowerShell 5.1) or .NET Core/.NET 5+ (for PowerShell 7+)

### Import the Module

```powershell
# Import the module
Import-Module .\PSMinIO.psd1

# Verify the module is loaded
Get-Module PSMinIO
```

## Configuration

### Basic Configuration

```powershell
# Set up MinIO connection
Set-MinIOConfig -Endpoint "minio.example.com:9000" `
                -AccessKey "your-access-key" `
                -SecretKey "your-secret-key" `
                -UseSSL

# For local development (no SSL)
Set-MinIOConfig -Endpoint "localhost:9000" `
                -AccessKey "minioadmin" `
                -SecretKey "minioadmin" `
                -NoSSL
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
