@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'bin\PSMinIO.dll'

    # Version number of this module.
    ModuleVersion = '2025.07.10.1200'

    # Supported PSEditions
    CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'

    # Author of this module
    Author = 'PSMinIO Team'

    # Company or vendor of this module
    CompanyName = 'PSMinIO'

    # Copyright statement for this module
    Copyright = '(c) 2025 PSMinIO Team. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'A PowerShell module for MinIO object storage operations built on the Minio .NET SDK'

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
    RequiredAssemblies = @('bin\PSMinIO.dll', 'bin\Minio.dll')

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
        'Remove-MinIOBucket',
        'Test-MinIOBucketExists',
        'Get-MinIOObject',
        'New-MinIOObject',
        'New-MinIOObjectChunked',
        'New-MinIOFolder',
        'Get-MinIOObjectContent',
        'Get-MinIOObjectContentChunked',
        'Remove-MinIOObject',
        'Get-MinIOBucketPolicy',
        'Set-MinIOBucketPolicy',
        'Get-MinIOStats'
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
        'bin\Minio.dll',
        'types\PSMinIO.Types.ps1xml',
        'types\PSMinIO.Format.ps1xml'
    )

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('MinIO', 'ObjectStorage', 'S3', 'Cloud', 'Storage', 'Bucket', 'Object')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/PSMinIO/PSMinIO/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/PSMinIO/PSMinIO'

            # A URL to an icon representing this module.
            # IconUri = ''

            # ReleaseNotes of this module
            ReleaseNotes = 'Initial release of PSMinIO module with comprehensive MinIO object storage operations support.'

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
