# PSMinIO

A comprehensive PowerShell module for MinIO object storage operations, built on the official [Minio .NET SDK](https://www.nuget.org/packages/Minio). Provides full-featured object storage management with enterprise-grade capabilities.

## Features

- **🚀 High Performance**: Built for .NET Standard 2.0, compatible with PowerShell 5.1+ and PowerShell 7+
- **📦 Complete Object Management**: Upload, download, list, and delete objects with advanced filtering and sorting
- **🗂️ Flexible Directory Support**: Create nested folder structures with automatic directory creation
- **⚡ Chunked Operations**: Large file uploads and downloads with resume capability and progress tracking
- **🔒 Security & Policy Management**: Comprehensive bucket policy and access control management
- **📊 Advanced Object Listing**: Filter by prefix, sort by multiple criteria, limit results, and exclude directories
- **⏱️ Timing & Performance Metrics**: Detailed timing information and transfer speed reporting
- **🔄 Progress Reporting**: Multi-layer progress tracking for file collections and chunked operations
- **📝 Professional Logging**: Clean, timestamped logging with configurable verbosity levels
- **🛡️ Robust Error Handling**: Graceful handling of network issues and edge cases

## Installation

### From Source
```powershell
# Clone the repository
git clone https://github.com/yourusername/PSMinIO.git
cd PSMinIO

# Build the module
dotnet build PSMinIO.csproj --configuration Release

# Import the module
Import-Module .\Module\PSMinIO\PSMinIO.psd1
```

### Direct Import
```powershell
# Import from local path
Import-Module .\Module\PSMinIO\PSMinIO.psd1
```

## Quick Start

```powershell
# Connect to MinIO server
$connection = Connect-MinIO -Endpoint "https://minio.example.com" -AccessKey "your-access-key" -SecretKey "your-secret-key"

# Create a bucket
New-MinIOBucket -BucketName 'my-data-bucket' -Verbose

# Upload files with automatic directory creation
New-MinIOObject -BucketName 'my-data-bucket' -Files "document.pdf" -BucketDirectory "documents/2025/january"

# List objects with advanced filtering
Get-MinIOObject -BucketName 'my-data-bucket' -Prefix "documents/" -SortBy "Size" -Descending -MaxObjects 10

# Download with timing information
Get-MinIOObjectContent -BucketName 'my-data-bucket' -ObjectName 'documents/2025/january/document.pdf' -FilePath 'C:\Downloads\document.pdf'

# Chunked upload for large files
New-MinIOObjectChunked -BucketName 'my-data-bucket' -Files "large-video.mp4" -ChunkSize 10MB -BucketDirectory "media/videos"
```

## Available Cmdlets

### Connection Management
- **`Connect-MinIO`** - Establish connection to MinIO server with SSL support and certificate validation options

### Bucket Operations
- **`Get-MinIOBucket`** - List all buckets with optional statistics
- **`New-MinIOBucket`** - Create new buckets with region support
- **`Remove-MinIOBucket`** - Delete buckets with safety confirmations
- **`Test-MinIOBucketExists`** - Check bucket existence

### Object Operations
- **`Get-MinIOObject`** - Advanced object listing with filtering, sorting, and pagination
  - Filter by prefix or exact object name
  - Sort by Name, Size, LastModified, or ETag (ascending/descending)
  - Limit results with MaxObjects
  - Exclude directories with ObjectsOnly
- **`New-MinIOObject`** - Upload files with directory support
  - Single file or multiple file uploads
  - Automatic nested directory creation
  - Progress tracking and timing information
- **`New-MinIOObjectChunked`** - Chunked uploads for large files
  - Configurable chunk sizes (1MB minimum)
  - Resume capability and multi-layer progress tracking
  - Automatic directory creation
- **`Get-MinIOObjectContent`** - Download objects with progress tracking
- **`Get-MinIOObjectContentChunked`** - Chunked downloads for large files
- **`Remove-MinIOObject`** - Delete objects with confirmation prompts
- **`New-MinIOFolder`** - Create folder structures in buckets

### Security & Policy Management
- **`Get-MinIOBucketPolicy`** - Retrieve bucket access policies
- **`Set-MinIOBucketPolicy`** - Configure bucket access policies

### Monitoring & Statistics
- **`Get-MinIOStats`** - Comprehensive server and bucket statistics with object counting limits

## Key Features in Detail

### 🗂️ Advanced Directory Support
- **Nested Folder Creation**: Automatically create multi-level directory structures (e.g., `documents/2025/january/reports`)
- **BucketDirectory Parameter**: Specify target directories for uploads without manual folder creation
- **Clean Directory Handling**: Non-critical directory creation attempts with graceful fallback

### ⚡ Chunked Operations
- **Large File Support**: Handle files of any size with configurable chunk sizes
- **Resume Capability**: Interrupted transfers can be resumed (future enhancement)
- **Multi-Layer Progress**: Track collection progress, file progress, and chunk progress simultaneously
- **Performance Optimization**: Optimal chunk sizes for different network conditions

### 📊 Enhanced Object Listing
```powershell
# Advanced filtering and sorting examples
Get-MinIOObject -BucketName "data" -Prefix "logs/" -SortBy "LastModified" -Descending -MaxObjects 50
Get-MinIOObject -BucketName "media" -ObjectsOnly -SortBy "Size" -Descending
Get-MinIOObject -BucketName "docs" -ObjectName "specific-file.pdf"
```

### ⏱️ Performance Metrics
All operations provide detailed timing information:
- **Duration**: Precise operation timing
- **Transfer Speed**: Formatted speed reporting (B/s, KB/s, MB/s, GB/s, TB/s)
- **Progress Tracking**: Real-time progress updates during transfers

## Examples

See the [examples](./examples/) directory for comprehensive usage examples:
- **Basic Operations**: Connection, bucket management, simple uploads/downloads
- **Advanced Scenarios**: Chunked transfers, directory management, bulk operations
- **Enterprise Patterns**: Policy management, monitoring, and automation scripts

## Requirements

- **PowerShell**: 5.1+ or PowerShell 7+
- **.NET Framework**: 4.7.2+ (for PowerShell 5.1) or .NET Core/.NET 5+ (for PowerShell 7+)
- **MinIO Server**: Compatible with MinIO and Amazon S3 APIs

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Support

- 📖 **Documentation**: See [docs/USAGE.md](docs/USAGE.md) for detailed usage instructions
- 🐛 **Issues**: Report bugs and request features via GitHub Issues
- 💬 **Discussions**: Join the community discussions for questions and tips