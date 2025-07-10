# PSMinIO Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2025.07.10.1200] - 2025-07-10

### Added

#### Core Infrastructure
- **MinIOConfiguration**: Singleton configuration management with JSON persistence
- **MinIOLogger**: Centralized logging utility with timestamp formatting (yyyy/MM/dd HH:mm:ss.fff)
- **MinIOClientWrapper**: Synchronous wrapper for async MinIO operations using `.GetAwaiter().GetResult()`
- **MinIOBaseCmdlet**: Base class for all cmdlets with common functionality
- **ProgressReporter**: Upload/download progress tracking with percentages and time estimates

#### Bucket Operations
- **Get-MinIOBucket**: List buckets with optional statistics gathering
- **New-MinIOBucket**: Create buckets with region support and validation
- **Remove-MinIOBucket**: Delete buckets with optional object removal
- **Test-MinIOBucketExists**: Check bucket existence with detailed information

#### Object Operations
- **Get-MinIOObject**: List objects with filtering, sorting, and pagination
- **New-MinIOObject**: Upload files with progress reporting and content type detection
- **Get-MinIOObjectContent**: Download objects with progress reporting
- **Remove-MinIOObject**: Delete objects with prefix support for batch operations

#### Security & Policy Management
- **Get-MinIOBucketPolicy**: Retrieve bucket policies as JSON or structured objects
- **Set-MinIOBucketPolicy**: Set policies from JSON, files, or predefined canned policies

#### Configuration & Utilities
- **Set-MinIOConfig**: Configure MinIO connection with validation and testing
- **Get-MinIOConfig**: View configuration with optional sensitive data masking
- **Get-MinIOStats**: Comprehensive statistics with per-bucket details

#### PowerShell Integration
- **Type Definitions**: Custom .ps1xml files for formatted output
- **Format Definitions**: Table views for all major object types
- **Parameter Validation**: Comprehensive input validation and error handling
- **ShouldProcess Support**: All destructive operations support -WhatIf and -Confirm

### Design Decisions

#### .NET Standard 2.0 Compatibility
- **Target Framework**: .NET Standard 2.0 for maximum compatibility
- **PowerShell Support**: Compatible with PowerShell 5.1 (.NET Framework 4.7.2) and PowerShell 7+
- **Dependency Management**: Uses PowerShellStandard.Library 5.1.1 for cmdlet base classes

#### Synchronous Operations Only
- **No Async/Await**: All operations are synchronous for PowerShell compatibility
- **Wrapper Strategy**: Uses `Task.Run().GetAwaiter().GetResult()` pattern
- **Cancellation Support**: Implements CancellationToken for operation cancellation

#### Logging Strategy
- **Conditional Logging**: Only logs when `-Verbose` is specified
- **Timestamp Format**: Consistent yyyy/MM/dd HH:mm:ss.fff format
- **Centralized Utility**: Single MinIOLogger class for all logging operations
- **Error Categorization**: Proper PowerShell ErrorCategory assignment

#### Progress Reporting
- **Upload/Download Progress**: Real-time progress with bytes transferred
- **Time Estimates**: Calculates remaining time based on current speed
- **Throttled Updates**: Updates every 100ms to avoid console flooding
- **Formatted Display**: Human-readable size formatting (B, KB, MB, GB, etc.)

#### Configuration Management
- **Singleton Pattern**: Single configuration instance across the module
- **Persistent Storage**: JSON configuration file in user's AppData
- **Validation**: Comprehensive validation before client creation
- **Security**: Sensitive data masking in display output

#### Error Handling
- **Comprehensive Validation**: Input validation at multiple levels
- **Proper Error Categories**: Uses appropriate PowerShell ErrorCategory values
- **Graceful Degradation**: Operations continue when possible, warn on failures
- **Detailed Error Messages**: Includes context and suggestions for resolution

#### Performance Considerations
- **Lazy Client Creation**: MinIO client created only when needed
- **Resource Disposal**: Proper disposal of clients and resources
- **Batch Operations**: Support for bulk operations with progress reporting
- **Configurable Limits**: MaxObjects parameters to prevent performance issues

#### PowerShell Best Practices
- **Parameter Sets**: Logical grouping of related parameters
- **Pipeline Support**: ValueFromPipeline and ValueFromPipelineByPropertyName
- **Aliases**: Common aliases for frequently used parameters
- **Help Integration**: Comprehensive parameter documentation
- **Output Types**: Strongly typed output objects

### Technical Implementation

#### Synchronous Wrapper Pattern
```csharp
public bool BucketExists(string bucketName)
{
    var args = new BucketExistsArgs().WithBucket(bucketName);
    return Task.Run(async () => 
        await _client.BucketExistsAsync(args, CancellationToken))
        .GetAwaiter().GetResult();
}
```

#### Progress Reporting Implementation
```csharp
var progressReporter = new ProgressReporter(
    this, "Uploading Object", $"Uploading {fileInfo.Name}", fileSize, 1);

var etag = Client.UploadFile(BucketName, ObjectName, FilePath, ContentType,
    bytesTransferred => progressReporter.UpdateProgress(bytesTransferred));
```

#### Logging Pattern
```csharp
MinIOLogger.WriteVerbose(this, "Operation started: {0}", operationName);
// ... operation code ...
MinIOLogger.WriteVerbose(this, "Operation completed: {0}", operationName);
```

### Dependencies

- **Minio**: 5.0.0 - Core MinIO .NET SDK
- **PowerShellStandard.Library**: 5.1.1 - PowerShell cmdlet base classes
- **System.Text.Json**: 6.0.0 - JSON serialization for configuration and policies

### Breaking Changes

None - Initial release.

### Security Considerations

- **Credential Storage**: Configuration file stored in user's AppData directory
- **Sensitive Data Masking**: Access keys and secret keys masked in output by default
- **SSL by Default**: UseSSL defaults to true for secure connections
- **Input Validation**: Comprehensive validation to prevent injection attacks

### Known Limitations

- **Large Bucket Performance**: Object counting can be slow for buckets with many objects
- **Synchronous Only**: No async operations available (by design)
- **Windows Paths**: File path handling optimized for Windows (cross-platform compatible)

### Future Enhancements

- **Multipart Upload Support**: For large files
- **Presigned URL Generation**: For temporary access
- **Server-Side Encryption**: Configuration and management
- **Lifecycle Policies**: Bucket lifecycle management
- **Notification Configuration**: Event notification setup

---

## Version Numbering

This project uses a date-based versioning scheme: `YYYY.MM.DD.HHMM`

- **YYYY**: Year (2025)
- **MM**: Month (07)
- **DD**: Day (10)
- **HHMM**: Hour and minute (1200 = 12:00 PM)

This ensures chronological ordering and makes it easy to identify when a version was released.
