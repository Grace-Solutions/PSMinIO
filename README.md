# PSMinIO

A fully-fledged C# PowerShell binary module built on top of the [Minio](https://www.nuget.org/packages/Minio) .NET SDK for managing MinIO object storage operations.

## Features

- **Cross-Platform Compatibility**: Built for .NET Standard 2.0, compatible with PowerShell 5.1+ and PowerShell 7+
- **Comprehensive Bucket Operations**: Create, list, delete, and check bucket existence
- **Object Management**: Upload, download, list, and delete objects with progress tracking
- **Security & Policy Management**: Manage bucket policies and access controls
- **Synchronous Operations**: All operations are synchronous for PowerShell compatibility
- **Detailed Logging**: Centralized logging with timestamps when `-Verbose` is specified
- **Progress Reporting**: Upload/download progress with percentages and time estimates

## Installation

```powershell
# Import the module
Import-Module .\PSMinIO.psd1
```

## Quick Start

```powershell
# Configure connection
Set-MinIOConfig -Endpoint 'https://minio.myorg.com' -AccessKey 'AKIA...' -SecretKey 'abc123' -UseSSL

# Create a bucket
New-MinIOBucket -BucketName 'my-bucket' -Verbose

# Upload a file
New-MinIOObject -BucketName 'my-bucket' -ObjectName 'data.txt' -FilePath 'C:\data.txt'

# List objects
Get-MinIOObject -BucketName 'my-bucket' -Prefix '2025/'

# Download an object
Get-MinIOObjectContent -BucketName 'my-bucket' -ObjectName 'data.txt' -FilePath 'C:\downloaded-data.txt'
```

## Cmdlets

### Bucket Operations
- `Get-MinIOBucket` - Lists all buckets
- `New-MinIOBucket` - Creates a new bucket
- `Remove-MinIOBucket` - Deletes a bucket
- `Test-MinIOBucketExists` - Checks if a bucket exists

### Object Operations
- `Get-MinIOObject` - Lists objects in a bucket
- `New-MinIOObject` - Uploads a file to a bucket
- `Get-MinIOObjectContent` - Downloads an object
- `Remove-MinIOObject` - Deletes an object

### Security & Policy
- `Get-MinIOBucketPolicy` - Retrieves bucket policy
- `Set-MinIOBucketPolicy` - Sets bucket policy

### Utility
- `Get-MinIOConfig` - Shows current configuration
- `Set-MinIOConfig` - Sets connection configuration
- `Get-MinIOStats` - Displays statistics and metrics

## Requirements

- PowerShell 5.1+ or PowerShell 7+
- .NET Framework 4.7.2+ (for PowerShell 5.1) or .NET Core/.NET 5+ (for PowerShell 7+)

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.