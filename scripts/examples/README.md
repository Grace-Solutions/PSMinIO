# PSMinIO Examples

This directory contains comprehensive examples demonstrating various PSMinIO capabilities and usage patterns.

## Prerequisites

Before running these examples:

1. **Install PSMinIO**: Import the module using `Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1`
2. **MinIO Server**: Have access to a MinIO server or compatible S3 service
3. **Credentials**: Update the connection details in each script with your actual values:
   ```powershell
   $endpoint = "https://your-minio-server.com"
   $accessKey = "your-access-key"
   $secretKey = "your-secret-key"
   ```

## Example Scripts

### 01-Basic-Operations.ps1
**Fundamental MinIO operations for beginners**

Demonstrates:
- Connecting to MinIO server
- Creating and managing buckets
- Basic file upload and download
- Object listing and verification
- Clean resource management

**Best for**: First-time users, basic automation scripts

---

### 02-Advanced-Object-Listing.ps1
**Comprehensive object listing and filtering capabilities**

Demonstrates:
- Advanced filtering with prefixes
- Sorting by multiple criteria (Name, Size, LastModified, ETag)
- Result pagination with MaxObjects
- Files-only filtering (excluding directories)
- Complex query combinations

**Best for**: Data discovery, content management, reporting

---

### 03-Directory-Management.ps1
**Creating and managing directory structures**

Demonstrates:
- Explicit folder creation with New-MinIOFolder
- Automatic directory creation with BucketDirectory
- Multi-level nested directory structures
- Directory-based file organization
- Date-based directory patterns

**Best for**: Organized file storage, hierarchical data management

---

### 04-Chunked-Operations.ps1
**Large file handling with chunked transfers**

Demonstrates:
- Chunked uploads with configurable chunk sizes
- Chunked downloads for large files
- Multi-layer progress tracking
- Performance comparison (regular vs chunked)
- Chunk size optimization guidelines

**Best for**: Large file transfers, bandwidth optimization, resume capability

---

### 05-Bulk-Operations.ps1
**Batch processing and bulk file operations**

Demonstrates:
- Multiple file uploads to organized directories
- Bulk download operations with filtering
- File analysis and statistics
- Pattern-based file processing
- Selective cleanup operations

**Best for**: Data migration, batch processing, automated workflows

---

### 06-Enterprise-Automation.ps1
**Enterprise-grade automation and monitoring**

Demonstrates:
- Infrastructure health checks
- Storage statistics and monitoring
- Automated backup operations
- Data lifecycle management
- Security and compliance auditing
- Performance monitoring
- HTML report generation

**Best for**: Enterprise environments, monitoring systems, compliance reporting

## Usage Patterns

### Quick Start
```powershell
# Run a basic example
.\01-Basic-Operations.ps1
```

### Customization
Each script includes configuration variables at the top. Modify these for your environment:

```powershell
# Connection details
$endpoint = "https://your-minio-server.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

# Optional: Bucket naming
$bucketName = "your-custom-bucket-name"
```

### Integration
These examples can be integrated into larger automation workflows:

```powershell
# Source common functions
. .\examples\06-Enterprise-Automation.ps1

# Use specific functions in your scripts
Write-Log "Starting custom automation"
$stats = Get-MinIOStats
```

## Common Scenarios

### Development Environment Setup
```powershell
# Local MinIO setup
$endpoint = "http://localhost:9000"
$accessKey = "minioadmin"
$secretKey = "minioadmin"
```

### Production Environment
```powershell
# Production setup with SSL
$endpoint = "https://minio.company.com"
$accessKey = $env:MINIO_ACCESS_KEY
$secretKey = $env:MINIO_SECRET_KEY
```

### AWS S3 Compatibility
```powershell
# AWS S3 setup
$endpoint = "https://s3.amazonaws.com"
$accessKey = $env:AWS_ACCESS_KEY_ID
$secretKey = $env:AWS_SECRET_ACCESS_KEY
```

## Best Practices

### Error Handling
All examples include comprehensive error handling:
```powershell
try {
    # MinIO operations
} catch {
    "❌ Error: $($_.Exception.Message)"
} finally {
    # Cleanup operations
}
```

### Resource Cleanup
Examples automatically clean up created resources:
- Remove test buckets
- Delete uploaded files
- Clean local temporary files

### Logging
Enterprise examples include structured logging:
```powershell
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$timestamp] [$Level] $Message"
}
```

### Performance Considerations
- Use chunked operations for files > 10MB
- Implement appropriate chunk sizes based on network conditions
- Monitor transfer speeds and adjust accordingly

## Troubleshooting

### Connection Issues
- Verify endpoint URL and port
- Check SSL/TLS configuration
- Validate access credentials
- Test network connectivity

### Permission Issues
- Ensure proper bucket permissions
- Verify access key has required privileges
- Check bucket policies if applicable

### Performance Issues
- Adjust chunk sizes for large files
- Monitor network bandwidth
- Consider parallel operations for bulk transfers

## Contributing

When adding new examples:
1. Follow the existing naming convention
2. Include comprehensive error handling
3. Add resource cleanup
4. Document the example purpose and usage
5. Update this README with the new example

## Support

For issues with these examples:
- Check the main [PSMinIO documentation](../docs/USAGE.md)
- Review error messages and logs
- Verify your MinIO server configuration
- Test with basic operations first
