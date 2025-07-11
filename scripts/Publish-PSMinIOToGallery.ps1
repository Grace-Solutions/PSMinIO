# PowerShell Gallery Release Script for PSMinIO
# This script prepares and publishes the PSMinIO module to PowerShell Gallery

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$NuGetApiKey,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf,
    
    [Parameter(Mandatory = $false)]
    [switch]$Force,
    
    [Parameter(Mandatory = $false)]
    [string]$Repository = "PSGallery"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Define paths
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ModulePath = Join-Path $ProjectRoot "Module\PSMinIO"
$ManifestPath = Join-Path $ModulePath "PSMinIO.psd1"
$PublishPath = Join-Path $ProjectRoot "Publish"

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    switch ($Type) {
        "Success" { Write-Host "[$timestamp] ✅ $Message" -ForegroundColor Green }
        "Warning" { Write-Host "[$timestamp] ⚠️  $Message" -ForegroundColor Yellow }
        "Error"   { Write-Host "[$timestamp] ❌ $Message" -ForegroundColor Red }
        default   { Write-Host "[$timestamp] ℹ️  $Message" -ForegroundColor Cyan }
    }
}

function Test-ModuleStructure {
    Write-Status "Validating module structure..."
    
    # Check if manifest exists
    if (-not (Test-Path $ManifestPath)) {
        throw "Module manifest not found at: $ManifestPath"
    }
    
    # Test manifest
    try {
        $manifest = Test-ModuleManifest -Path $ManifestPath -ErrorAction Stop
        Write-Status "Module manifest is valid" "Success"
        Write-Status "Module: $($manifest.Name) v$($manifest.Version)"
        Write-Status "Author: $($manifest.Author)"
        Write-Status "Description: $($manifest.Description)"
        return $manifest
    } catch {
        throw "Module manifest validation failed: $($_.Exception.Message)"
    }
}

function Test-RequiredFiles {
    param($Manifest)
    
    Write-Status "Checking required files..."
    
    $requiredFiles = @(
        "bin\PSMinIO.dll",
        "bin\Minio.dll",
        "types\PSMinIO.Types.ps1xml",
        "types\PSMinIO.Format.ps1xml"
    )
    
    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $ModulePath $file
        if (-not (Test-Path $filePath)) {
            throw "Required file missing: $file"
        }
        Write-Status "✓ Found: $file"
    }
    
    Write-Status "All required files present" "Success"
}

function Test-ModuleFunctionality {
    Write-Status "Testing module functionality..."
    
    try {
        # Import the module
        Import-Module $ManifestPath -Force -ErrorAction Stop
        Write-Status "✓ Module imported successfully"
        
        # Test cmdlet availability
        $expectedCmdlets = @(
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
        
        $availableCmdlets = Get-Command -Module PSMinIO | Select-Object -ExpandProperty Name
        
        foreach ($cmdlet in $expectedCmdlets) {
            if ($cmdlet -in $availableCmdlets) {
                Write-Status "✓ Cmdlet available: $cmdlet"
            } else {
                throw "Expected cmdlet not found: $cmdlet"
            }
        }
        
        Write-Status "All expected cmdlets are available" "Success"
        
        # Remove module
        Remove-Module PSMinIO -Force -ErrorAction SilentlyContinue
        
    } catch {
        throw "Module functionality test failed: $($_.Exception.Message)"
    }
}

function Prepare-PublishDirectory {
    param($Manifest)
    
    Write-Status "Preparing publish directory..."
    
    # Clean and create publish directory
    if (Test-Path $PublishPath) {
        Remove-Item $PublishPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null
    
    # Create module directory in publish path
    $publishModulePath = Join-Path $PublishPath "PSMinIO"
    New-Item -ItemType Directory -Path $publishModulePath -Force | Out-Null
    
    # Copy module files
    Copy-Item -Path "$ModulePath\*" -Destination $publishModulePath -Recurse -Force
    
    Write-Status "Module prepared in: $publishModulePath" "Success"
    return $publishModulePath
}

function Test-PowerShellGalleryConnection {
    Write-Status "Testing PowerShell Gallery connection..."
    
    try {
        $repo = Get-PSRepository -Name $Repository -ErrorAction Stop
        Write-Status "✓ Repository '$Repository' is available"
        Write-Status "  Source: $($repo.SourceLocation)"
        Write-Status "  Publish: $($repo.PublishLocation)"
        
        if ($repo.InstallationPolicy -eq "Untrusted") {
            Write-Status "Repository is marked as Untrusted - this is normal for PSGallery" "Warning"
        }
        
    } catch {
        throw "Failed to connect to repository '$Repository': $($_.Exception.Message)"
    }
}

function Publish-ModuleToGallery {
    param($PublishModulePath, $Manifest)
    
    if ($WhatIf) {
        Write-Status "WhatIf: Would publish module to PowerShell Gallery" "Warning"
        Write-Status "Module: $($Manifest.Name) v$($Manifest.Version)"
        Write-Status "Path: $PublishModulePath"
        Write-Status "Repository: $Repository"
        return
    }
    
    if (-not $NuGetApiKey) {
        Write-Status "NuGetApiKey not provided. Please provide your PowerShell Gallery API key." "Warning"
        $NuGetApiKey = Read-Host -Prompt "Enter your PowerShell Gallery API Key" -AsSecureString
        $NuGetApiKey = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($NuGetApiKey))
    }
    
    Write-Status "Publishing module to PowerShell Gallery..."
    
    try {
        $publishParams = @{
            Path = $PublishModulePath
            Repository = $Repository
            NuGetApiKey = $NuGetApiKey
            Force = $Force
            Verbose = $true
        }
        
        Publish-Module @publishParams
        
        Write-Status "Module published successfully!" "Success"
        Write-Status "It may take a few minutes to appear in search results"
        
    } catch {
        throw "Failed to publish module: $($_.Exception.Message)"
    }
}

# Main execution
try {
    Write-Status "Starting PowerShell Gallery release process for PSMinIO"
    Write-Status "Repository: $Repository"
    Write-Status "WhatIf: $WhatIf"
    
    # Step 1: Validate module structure
    $manifest = Test-ModuleStructure
    
    # Step 2: Check required files
    Test-RequiredFiles -Manifest $manifest
    
    # Step 3: Test module functionality
    Test-ModuleFunctionality
    
    # Step 4: Test PowerShell Gallery connection
    Test-PowerShellGalleryConnection
    
    # Step 5: Prepare publish directory
    $publishModulePath = Prepare-PublishDirectory -Manifest $manifest
    
    # Step 6: Final validation of prepared module
    Write-Status "Final validation of prepared module..."
    $finalManifest = Test-ModuleManifest -Path (Join-Path $publishModulePath "PSMinIO.psd1")
    Write-Status "Final validation successful" "Success"
    
    # Step 7: Publish to gallery
    Publish-ModuleToGallery -PublishModulePath $publishModulePath -Manifest $finalManifest
    
    Write-Status "PowerShell Gallery release process completed successfully!" "Success"
    Write-Status "Module: PSMinIO v$($finalManifest.Version)"
    Write-Status "Check status at: https://www.powershellgallery.com/packages/PSMinIO"
    
} catch {
    Write-Status "Release process failed: $($_.Exception.Message)" "Error"
    exit 1
}
