@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'bin\PSMinIO.dll'

    # Version number of this module.
    ModuleVersion = '2025.07.14.2318'

    # Supported PSEditions
    CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'

    # Author of this module
    Author = 'Grace Solutions'

    # Company or vendor of this module
    CompanyName = 'Grace Solutions'

    # Copyright statement for this module
    Copyright = '(c) 2025 Grace Solutions. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'A comprehensive PowerShell module for MinIO object storage operations with enterprise-grade features including multipart uploads/downloads, bucket directory management, presigned URLs, bucket policies, advanced metadata handling, and performance monitoring. Built with custom REST API implementation for optimal PowerShell compatibility.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Name of the PowerShell host required by this module
    # PowerShellHostName = ''

    # Minimum version of the PowerShell host required by this module
    # PowerShellHostVersion = ''

    # Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    DotNetFrameworkVersion = '4.7.2'

    # Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
    CLRVersion = '4.0'

    # Processor architecture (None, X86, Amd64) required by this module
    # ProcessorArchitecture = ''

    # Modules that must be imported into the global environment prior to importing this module
    # RequiredModules = @()

    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @('bin\PSMinIO.dll')

    # Script files (.ps1) that are run in the caller's environment prior to importing this module.
    # ScriptsToProcess = @()

    # Type files (.ps1xml) to be loaded when importing this module
    TypesToProcess = @('types\PSMinIO.Types.ps1xml')

    # Format files (.ps1xml) to be loaded when importing this module
    FormatsToProcess = @('types\PSMinIO.Format.ps1xml')

    # Modules to import as nested modules of the module specified in RootModule/ModuleToProcess
    # NestedModules = @()

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @()

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @(
        'Connect-MinIO',
        'Get-MinIOBucket',
        'New-MinIOBucket',
        'Test-MinIOBucketExists',
        'Get-MinIOObject',
        'New-MinIOObject',
        'Get-MinIOObjectContent',
        'Get-MinIOObjectContentMultipart',
        'New-MinIOObjectMultipart',
        'New-MinIOBucketFolder',
        'Remove-MinIOBucketFolder',
        'Get-MinIOZipArchive',
        'New-MinIOZipArchive',
        'Get-MinIOPresignedUrl',
        'New-MinIOPresignedUrl',
        'Get-MinIOBucketPolicy',
        'Set-MinIOBucketPolicy',
        'Remove-MinIOBucketPolicy'
    )

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @()

    # DSC resources to export from this module
    # DscResourcesToExport = @()

    # List of all modules packaged with this module
    # ModuleList = @()

    # List of all files packaged with this module
    FileList = @(
        'PSMinIO.psd1',
        'bin\PSMinIO.dll',
        'bin\PSMinIO.pdb',
        'bin\System.Text.Json.dll',
        'types\PSMinIO.Types.ps1xml',
        'types\PSMinIO.Format.ps1xml'
    )

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('MinIO', 'ObjectStorage', 'S3', 'Cloud', 'Storage', 'Bucket', 'Object', 'Enterprise', 'Chunked', 'Performance', 'Monitoring', 'Automation', 'Backup', 'AWS', 'Compatible')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/Grace-Solutions/PSMinIO/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/Grace-Solutions/PSMinIO'

            # A URL to an icon representing this module.
            # IconUri = ''

            # ReleaseNotes of this module
            ReleaseNotes = @'
## Version 2025.07.11.1453 - Enhanced Release

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
'@

            # Prerelease string of this module
            # Prerelease = ''

            # Flag to indicate whether the module requires explicit user acceptance for install/update/save
            # RequireLicenseAcceptance = $false

            # External dependent modules of this module
            # ExternalModuleDependencies = @()
        }
    }

    # HelpInfo URI of this module
    # HelpInfoURI = ''

    # Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
    # DefaultCommandPrefix = ''
}










