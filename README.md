# PSMinIO

A comprehensive PowerShell module for MinIO object storage operations with enterprise-grade features. Built with custom REST API implementation for optimal PowerShell compatibility and performance.

## 🚀 Key Features

### **📦 Complete Object Management**
- **Single & Multipart Uploads**: Automatic chunking for large files with resume capability
- **Parallel Downloads**: High-performance multipart downloads with progress tracking
- **Bucket Directory Management**: Create, manage, and remove nested folder structures
- **Advanced Object Listing**: Filter, sort, and paginate with comprehensive metadata

### **⚡ High-Performance Operations**
- **Intelligent Progress Tracking**: Real-time 3-layer progress bars for concurrent operations
- **Optimized Multipart Uploads**: Configurable chunk sizes and parallel upload limits
- **Performance Monitoring**: Transfer speeds, timing metrics, and completion statistics
- **Memory Efficient**: Streaming operations for large files without memory bloat

### **🗂️ Bucket Directory Ecosystem**
- **Multi-Level Folder Creation**: Support for complex nested directory structures
- **Path Format Flexibility**: Handle Windows (`\Folder\Sub`) and Unix (`/Folder/Sub`) paths
- **Recursive Operations**: Create or remove entire directory trees
- **Automatic Sanitization**: Clean and normalize all path inputs

### **🔒 Enterprise Security & Management**
- **Bucket Policy Management**: Complete CRUD operations for access policies
- **Presigned URL Generation**: Secure temporary access with configurable expiration
- **Connection Management**: Secure credential handling with SSL/TLS support
- **Comprehensive Error Handling**: Detailed error reporting and recovery options

### **📊 Advanced Features**
- **ZIP Archive Integration**: Create and extract archives with progress tracking
- **Metadata Management**: Custom headers and content type handling
- **Professional Logging**: Timestamped verbose output with intelligent formatting
- **PowerShell Integration**: Full ShouldProcess support with -WhatIf and -Confirm

## Installation

### From PowerShell Gallery
```powershell
# Install from PowerShell Gallery
Install-Module -Name PSMinIO -Scope CurrentUser

# Import the module
Import-Module PSMinIO
```

### From Source
```powershell
# Clone the repository
git clone https://github.com/Grace-Solutions/PSMinIO.git
cd PSMinIO

# Build the module (automatically updates version)
.\scripts\Build.ps1

# Import the module
Import-Module .\Module\PSMinIO\PSMinIO.psd1
```

### Direct Import
```powershell
# Import from local path
Import-Module .\Module\PSMinIO\PSMinIO.psd1
```

## 🚀 Quick Start

### **Connection Setup**
```powershell
# Connect to MinIO server
Connect-MinIO -Endpoint "https://minio.example.com" -AccessKey "your-access-key" -SecretKey "your-secret-key"

# Connect with SSL verification disabled (development only)
Connect-MinIO -Endpoint "https://minio.local" -AccessKey "admin" -SecretKey "password" -SkipCertificateValidation
```

### **Bucket Management**
```powershell
# List all buckets
Get-MinIOBucket

# Create a new bucket
New-MinIOBucket -BucketName "my-bucket" -Region "us-east-1"

# Check if bucket exists
Test-MinIOBucketExists -BucketName "my-bucket"
```

### **Folder Management**
```powershell
# Create nested folder structure
New-MinIOBucketFolder -BucketName "my-bucket" -FolderPath "Documents/Projects/2024" -Recursive

# Remove folder and contents
Remove-MinIOBucketFolder -BucketName "my-bucket" -FolderPath "OldData" -Recursive -Force
```

### **File Upload Operations**
```powershell
# Simple file upload
New-MinIOObject -BucketName "my-bucket" -Path "C:\file.txt"

# Upload to specific bucket directory
New-MinIOObject -BucketName "my-bucket" -Path "C:\file.txt" -BucketDirectory "Documents/2024"

# Upload multiple files with progress
New-MinIOObject -BucketName "my-bucket" -Path @("file1.txt", "file2.txt") -Verbose

# Upload directory recursively
New-MinIOObject -BucketName "my-bucket" -Directory "C:\MyFolder" -Recursive -BucketDirectory "Backup"

# Large file multipart upload
New-MinIOObjectMultipart -BucketName "my-bucket" -FilePath "C:\large-file.zip" -ChunkSize 64MB -MaxParallelUploads 4 -BucketDirectory "Archives"
```

### **File Download Operations**
```powershell
# Simple download
Get-MinIOObjectContent -BucketName "my-bucket" -ObjectName "file.txt" -LocalPath "C:\Downloads\file.txt"

# Multipart download for large files
Get-MinIOObjectContentMultipart -BucketName "my-bucket" -ObjectName "large-file.zip" -LocalPath "C:\Downloads\" -ChunkSize 32MB
```

### **Object Listing & Management**
```powershell
# List all objects
Get-MinIOObject -BucketName "my-bucket"

# Advanced filtering and sorting
Get-MinIOObject -BucketName "my-bucket" -Prefix "documents/" -SortBy "LastModified" -Descending -MaxKeys 100 -ExcludeDirectories
```

## 📋 Available Cmdlets

### **🔌 Connection Management**
- **`Connect-MinIO`** - Establish connection to MinIO server with SSL support and certificate validation options

### **🗂️ Bucket Management**
- **`Get-MinIOBucket`** - List all buckets with filtering and metadata
- **`New-MinIOBucket`** - Create new buckets with region specification
- **`Test-MinIOBucketExists`** - Check bucket existence efficiently

### **📁 Bucket Directory Management**
- **`New-MinIOBucketFolder`** - Create nested folder structures with multi-level support
- **`Remove-MinIOBucketFolder`** - Remove folders with recursive deletion options

### **📦 Object Operations**
- **`Get-MinIOObject`** - List objects with advanced filtering, sorting, and pagination
- **`New-MinIOObject`** - Upload single/multiple files with directory support and progress tracking
- **`Get-MinIOObjectContent`** - Download objects with automatic directory creation

### **⚡ High-Performance Operations**
- **`New-MinIOObjectMultipart`** - Large file uploads with chunking, parallelization, and resume capability
- **`Get-MinIOObjectContentMultipart`** - High-speed downloads with parallel chunk processing

### **📦 Archive Management**
- **`New-MinIOZipArchive`** - Create ZIP archives with progress tracking and metadata
- **`Get-MinIOZipArchive`** - Extract and analyze ZIP archive contents

### **🔗 URL Management**
- **`New-MinIOPresignedUrl`** - Generate secure temporary URLs for object access
- **`Get-MinIOPresignedUrl`** - Retrieve presigned URLs with configurable expiration

### **🔒 Security & Policy Management**
- **`Get-MinIOBucketPolicy`** - Retrieve bucket access policies
- **`Set-MinIOBucketPolicy`** - Configure bucket access policies
- **`Remove-MinIOBucketPolicy`** - Remove bucket access policies

## 💡 Advanced Examples

### **📁 Comprehensive Bucket Directory Management**
```powershell
# Create complex nested structure
New-MinIOBucketFolder -BucketName "enterprise-data" -FolderPath "Departments/IT/Projects/2024/Q1" -Recursive -Verbose

# Upload files to specific directories
New-MinIOObject -BucketName "enterprise-data" -Path "C:\Reports\*.pdf" -BucketDirectory "Departments/Finance/Reports/2024"

# Multipart upload to nested directory
New-MinIOObjectMultipart -BucketName "enterprise-data" -FilePath "C:\Backups\database.bak" -BucketDirectory "Backups/Database/2024" -ChunkSize 128MB -MaxParallelUploads 6

# Clean up old directories
Remove-MinIOBucketFolder -BucketName "enterprise-data" -FolderPath "Departments/IT/Projects/2023" -Recursive -Force
```

### **⚡ High-Performance Multipart Operations**
```powershell
# Large file upload with optimal settings
New-MinIOObjectMultipart -BucketName "media-storage" -FilePath "C:\Videos\4K-movie.mkv" -ChunkSize 256MB -MaxParallelUploads 8 -ContentType "video/x-matroska" -Verbose

# Resume interrupted upload
$uploadResult = New-MinIOObjectMultipart -BucketName "media-storage" -FilePath "C:\Videos\4K-movie.mkv" -ResumeUploadId "abc123..." -CompletedParts $previousParts

# High-speed download with chunking
Get-MinIOObjectContentMultipart -BucketName "media-storage" -ObjectName "4K-movie.mkv" -LocalPath "C:\Downloads\" -ChunkSize 128MB -MaxParallelDownloads 6
```
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

See the [scripts/examples](./scripts/examples/) directory for comprehensive usage examples:
- **Basic Operations**: Connection, bucket management, simple uploads/downloads
- **Advanced Scenarios**: Chunked transfers, directory management, bulk operations
- **Enterprise Patterns**: Policy management, monitoring, and automation scripts

## Requirements

- **PowerShell**: 5.1+ or PowerShell 7+
- **.NET Framework**: 4.7.2+ (for PowerShell 5.1) or .NET Core/.NET 5+ (for PowerShell 7+)
- **MinIO Server**: Compatible with MinIO and Amazon S3 APIs

## 🚀 Performance Features

- **🔄 Multi-threaded Operations**: Parallel uploads and downloads for maximum throughput
- **🧠 Intelligent Progress Tracking**: Real-time 3-layer progress bars with concurrent chunk visibility
- **⚡ Optimized Multipart Operations**: Configurable chunk sizes (1MB to 5GB) with parallel processing
- **🔄 Resume Capability**: Interrupted transfers can be resumed from the last completed chunk
- **💾 Memory Efficiency**: Streaming operations minimize memory usage for large files
- **🌐 Connection Management**: Efficient HTTP connection handling with SSL/TLS support
- **📊 Performance Monitoring**: Transfer speeds, timing metrics, and completion statistics

## 🤝 Contributing

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