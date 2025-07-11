# PSMinIO Version Configuration
# This file contains the centralized version information for the PSMinIO module

# Generate version based on current date and time
$CurrentDate = Get-Date
$Version = $CurrentDate.ToString("yyyy.MM.dd.HHmm")

# Version information
$VersionInfo = @{
    # Main version string (yyyy.MM.dd.HHMM format)
    Version = $Version
    
    # Semantic version for PowerShell Gallery (converted from date-based)
    SemanticVersion = "2.0.0"
    
    # Build date
    BuildDate = $CurrentDate
    
    # Build date string
    BuildDateString = $CurrentDate.ToString("yyyy-MM-dd HH:mm:ss")
    
    # Copyright year
    CopyrightYear = $CurrentDate.Year
    
    # Module information
    ModuleName = "PSMinIO"
    Author = "Grace Solutions"
    CompanyName = "Grace Solutions"
    Copyright = "(c) $($CurrentDate.Year) Grace Solutions. All rights reserved."
    
    # Description
    Description = "A comprehensive PowerShell module for MinIO object storage operations with enterprise-grade features including chunked transfers, advanced object listing, directory management, and performance monitoring. Built on the official Minio .NET SDK."
    
    # PowerShell Gallery metadata
    Tags = @('MinIO', 'ObjectStorage', 'S3', 'Cloud', 'Storage', 'Bucket', 'Object', 'Enterprise', 'Chunked', 'Performance', 'Monitoring', 'Automation', 'Backup', 'AWS', 'Compatible')
    
    # URLs
    ProjectUri = "https://github.com/Grace-Solutions/PSMinIO"
    LicenseUri = "https://github.com/Grace-Solutions/PSMinIO/blob/main/LICENSE"
    
    # Release notes
    ReleaseNotes = @"
## Version $Version - Enhanced Release

### Major Features
- Complete Get-MinIOObject cmdlet with advanced filtering, sorting, and pagination
- Enhanced directory management with automatic nested structure creation
- Advanced chunked operations with configurable chunk sizes and multi-layer progress tracking
- Comprehensive timing and performance metrics for all operations
- Enterprise-grade automation examples and monitoring capabilities

### Issues Fixed
- Fixed directory creation warnings (now clean verbose logging)
- Implemented missing Get-MinIOObject cmdlet with full functionality
- Resolved threading and progress reporting issues

### Documentation and Examples
- Updated README.md and comprehensive USAGE.md documentation
- Created comprehensive example scripts in scripts/examples/ directory
- Added enterprise automation patterns and best practices
- Professional logging with no Write-Host usage

### Technical Improvements
- Thread-safe operations for chunked transfers
- Enhanced error handling and resource management
- Performance optimization with intelligent defaults
- Centralized version management system

This release provides enterprise-grade functionality with professional documentation and comprehensive examples.
"@
}

# Export version information for use by other scripts
$VersionInfo

# Function to get version info
function Get-PSMinIOVersion {
    return $VersionInfo
}

# Function to update version in files
function Update-PSMinIOVersion {
    param(
        [string]$ManifestPath,
        [string]$AssemblyInfoPath
    )
    
    Write-Host "Updating version to: $($VersionInfo.Version)" -ForegroundColor Green
    Write-Host "Build date: $($VersionInfo.BuildDateString)" -ForegroundColor Green
    
    # Update module manifest if path provided
    if ($ManifestPath -and (Test-Path $ManifestPath)) {
        Write-Host "Updating module manifest: $ManifestPath" -ForegroundColor Yellow
        
        $manifestContent = Get-Content $ManifestPath -Raw
        
        # Update version
        $manifestContent = $manifestContent -replace "ModuleVersion\s*=\s*'[^']*'", "ModuleVersion = '$($VersionInfo.Version)'"
        
        # Update copyright
        $manifestContent = $manifestContent -replace "Copyright\s*=\s*'[^']*'", "Copyright = '$($VersionInfo.Copyright)'"
        
        # Update description
        $manifestContent = $manifestContent -replace "Description\s*=\s*'[^']*'", "Description = '$($VersionInfo.Description)'"
        
        $manifestContent | Set-Content $ManifestPath -Encoding UTF8
        Write-Host "✅ Module manifest updated" -ForegroundColor Green
    }
    
    # Update assembly info if path provided
    if ($AssemblyInfoPath -and (Test-Path $AssemblyInfoPath)) {
        Write-Host "Updating assembly info: $AssemblyInfoPath" -ForegroundColor Yellow
        
        $assemblyContent = Get-Content $AssemblyInfoPath -Raw
        
        # Update assembly version attributes
        $assemblyContent = $assemblyContent -replace '\[assembly:\s*AssemblyVersion\("[^"]*"\)\]', "[assembly: AssemblyVersion(`"$($VersionInfo.Version)`")]"
        $assemblyContent = $assemblyContent -replace '\[assembly:\s*AssemblyFileVersion\("[^"]*"\)\]', "[assembly: AssemblyFileVersion(`"$($VersionInfo.Version)`")]"
        $assemblyContent = $assemblyContent -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]*"\)\]', "[assembly: AssemblyInformationalVersion(`"$($VersionInfo.Version)`")]"
        
        # Update copyright
        $assemblyContent = $assemblyContent -replace '\[assembly:\s*AssemblyCopyright\("[^"]*"\)\]', "[assembly: AssemblyCopyright(`"$($VersionInfo.Copyright)`")]"
        
        $assemblyContent | Set-Content $AssemblyInfoPath -Encoding UTF8
        Write-Host "✅ Assembly info updated" -ForegroundColor Green
    }
}

# Export functions only if running as a module
if ($MyInvocation.MyCommand.CommandType -eq 'ExternalScript') {
    # Running as script - don't export
} else {
    # Running as module - export functions
    Export-ModuleMember -Function Get-PSMinIOVersion, Update-PSMinIOVersion -Variable VersionInfo
}
