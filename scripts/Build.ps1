#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for PSMinIO PowerShell module

.DESCRIPTION
    This script builds the PSMinIO module, copies required files, and prepares the module for distribution.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER OutputPath
    Output path for the built module. Default: Module/PSMinIO

.PARAMETER Clean
    Clean the output directory before building

.PARAMETER Test
    Run tests after building (if test files exist)

.PARAMETER Package
    Create a distributable package after building

.EXAMPLE
    .\Build.ps1
    
.EXAMPLE
    .\Build.ps1 -Configuration Debug -Clean
    
.EXAMPLE
    .\Build.ps1 -Package
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [string]$OutputPath = 'Artifacts\PSMinIO',
    
    [switch]$Clean,
    
    [switch]$Test,
    
    [switch]$Package
)

# Set error action preference
$ErrorActionPreference = 'Stop'

# Get script directory and project root
$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent
$ProjectFile = Join-Path $ProjectRoot 'PSMinIO.csproj'
$ModuleManifest = Join-Path $ProjectRoot 'Module\PSMinIO\PSMinIO.psd1'

Write-Host "=== PSMinIO Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Output Path: $OutputPath" -ForegroundColor Gray
Write-Host "Script Root: $ScriptRoot" -ForegroundColor Gray
Write-Host ""

# Function to write status messages
function Write-Status {
    param([string]$Message, [string]$Color = 'Yellow')
    Write-Host ">>> $Message" -ForegroundColor $Color
}

# Function to check if command exists
function Test-Command {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

try {
    # Update version information first
    Write-Status "Updating version information..."
    $updateVersionScript = Join-Path $ScriptRoot "Update-Version.ps1"
    if (Test-Path $updateVersionScript) {
        & $updateVersionScript
        Write-Host "  ✓ Version information updated" -ForegroundColor Green
    } else {
        Write-Warning "Update-Version.ps1 not found, skipping version update"
    }
    Write-Host ""

    # Check prerequisites
    Write-Status "Checking prerequisites..."

    if (!(Test-Command 'dotnet')) {
        throw ".NET SDK is required but not found. Please install .NET SDK 6.0 or later."
    }

    $dotnetVersion = dotnet --version
    Write-Host "  .NET SDK Version: $dotnetVersion" -ForegroundColor Green

    if (!(Test-Path $ProjectFile)) {
        throw "Project file not found: $ProjectFile"
    }

    if (!(Test-Path $ModuleManifest)) {
        throw "Module manifest not found: $ModuleManifest"
    }

    Write-Host "  ✓ Prerequisites check passed" -ForegroundColor Green
    Write-Host ""
    
    # Clean output directory if requested
    if ($Clean -and (Test-Path $OutputPath)) {
        Write-Status "Cleaning output directory..."
        Remove-Item -Path $OutputPath -Recurse -Force
        Write-Host "  ✓ Output directory cleaned" -ForegroundColor Green
        Write-Host ""
    }
    
    # Ensure output directory exists
    if (!(Test-Path $OutputPath)) {
        Write-Status "Creating output directory..."
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        Write-Host "  ✓ Output directory created" -ForegroundColor Green
        Write-Host ""
    }
    
    # Build the project
    Write-Status "Building project..."
    $buildArgs = @(
        'build'
        $ProjectFile
        '--configuration', $Configuration
        '--verbosity', 'minimal'
        '--nologo'
    )
    
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "  ✓ Build completed successfully" -ForegroundColor Green
    Write-Host ""
    
    # Copy built files to module directory
    Write-Status "Copying module files..."
    
    $binPath = Join-Path $OutputPath 'bin'
    if (!(Test-Path $binPath)) {
        New-Item -ItemType Directory -Path $binPath -Force | Out-Null
    }
    
    # Copy main assembly and dependencies
    $buildOutputPath = "bin\$Configuration\netstandard2.0"
    $filesToCopy = @(
        'PSMinIO.dll',
        'PSMinIO.pdb',
        'Minio.dll',
        'System.Text.Json.dll'
    )
    
    foreach ($file in $filesToCopy) {
        $sourcePath = Join-Path $buildOutputPath $file
        $destPath = Join-Path $binPath $file
        
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $destPath -Force
            Write-Host "  ✓ Copied: $file" -ForegroundColor Green
        } else {
            Write-Warning "  File not found: $file"
        }
    }
    
    # Copy module manifest
    $manifestDest = Join-Path $OutputPath 'PSMinIO.psd1'
    Copy-Item -Path $ModuleManifest -Destination $manifestDest -Force
    Write-Host "  ✓ Copied: PSMinIO.psd1" -ForegroundColor Green
    
    # Copy type and format files
    $typesPath = Join-Path $OutputPath 'types'
    if (!(Test-Path $typesPath)) {
        New-Item -ItemType Directory -Path $typesPath -Force | Out-Null
    }
    
    $typeFiles = Get-ChildItem -Path 'Module\PSMinIO\types\*.ps1xml' -ErrorAction SilentlyContinue
    foreach ($typeFile in $typeFiles) {
        $destPath = Join-Path $typesPath $typeFile.Name
        Copy-Item -Path $typeFile.FullName -Destination $destPath -Force
        Write-Host "  ✓ Copied: $($typeFile.Name)" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # Validate module
    Write-Status "Validating module..."
    
    try {
        $moduleInfo = Test-ModuleManifest -Path $manifestDest -ErrorAction Stop
        Write-Host "  ✓ Module manifest is valid" -ForegroundColor Green
        Write-Host "    Name: $($moduleInfo.Name)" -ForegroundColor Gray
        Write-Host "    Version: $($moduleInfo.Version)" -ForegroundColor Gray
        Write-Host "    Author: $($moduleInfo.Author)" -ForegroundColor Gray
        Write-Host "    Cmdlets: $($moduleInfo.ExportedCmdlets.Count)" -ForegroundColor Gray
    } catch {
        Write-Warning "Module manifest validation failed: $($_.Exception.Message)"
    }
    
    Write-Host ""
    
    # Run tests if requested
    if ($Test) {
        Write-Status "Running tests..."
        
        # Check if Pester is available
        if (Test-Command 'Invoke-Pester') {
            $testPath = Join-Path $ScriptRoot 'Tests'
            if (Test-Path $testPath) {
                try {
                    Invoke-Pester -Path $testPath -PassThru
                    Write-Host "  ✓ Tests completed" -ForegroundColor Green
                } catch {
                    Write-Warning "Tests failed: $($_.Exception.Message)"
                }
            } else {
                Write-Warning "Test directory not found: $testPath"
            }
        } else {
            Write-Warning "Pester module not found. Install with: Install-Module -Name Pester"
        }
        
        Write-Host ""
    }
    
    # Create package if requested
    if ($Package) {
        Write-Status "Creating package..."
        
        $timestamp = Get-Date -Format 'yyyy.MM.dd.HHmm'
        $packageDir = Join-Path 'Artifacts' "Release-$timestamp"
        
        if (!(Test-Path $packageDir)) {
            New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
        }
        
        # Create ZIP package
        $packagePath = Join-Path $packageDir "PSMinIO-$timestamp.zip"
        Compress-Archive -Path $OutputPath -DestinationPath $packagePath -Force
        
        Write-Host "  ✓ Package created: $packagePath" -ForegroundColor Green
        
        # Copy documentation
        $docsSource = Join-Path $ScriptRoot 'docs'
        if (Test-Path $docsSource) {
            $docsDest = Join-Path $packageDir 'docs'
            Copy-Item -Path $docsSource -Destination $docsDest -Recurse -Force
            Write-Host "  ✓ Documentation copied" -ForegroundColor Green
        }
        
        # Copy README and LICENSE
        $readmePath = Join-Path $ScriptRoot 'README.md'
        $licensePath = Join-Path $ScriptRoot 'LICENSE'
        
        if (Test-Path $readmePath) {
            Copy-Item -Path $readmePath -Destination $packageDir -Force
            Write-Host "  ✓ README copied" -ForegroundColor Green
        }
        
        if (Test-Path $licensePath) {
            Copy-Item -Path $licensePath -Destination $packageDir -Force
            Write-Host "  ✓ LICENSE copied" -ForegroundColor Green
        }
        
        Write-Host ""
    }
    
    # Build summary
    Write-Status "Build Summary" "Green"
    Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
    Write-Host "  Output Path: $OutputPath" -ForegroundColor Gray
    Write-Host "  Module Files: $(Get-ChildItem -Path $OutputPath -Recurse -File | Measure-Object | Select-Object -ExpandProperty Count)" -ForegroundColor Gray
    
    if ($Package) {
        Write-Host "  Package: $packagePath" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "=== Build Completed Successfully ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "To import the module, run:" -ForegroundColor Cyan
    Write-Host "  Import-Module '$OutputPath\PSMinIO.psd1'" -ForegroundColor White
    
} catch {
    Write-Host ""
    Write-Host "=== Build Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}
