# PSMinIO Project Structure

This document describes the reorganized project structure and centralized version management system.

## Directory Structure

```
PSMinIO/
├── README.md                          # Main project documentation
├── LICENSE                           # Project license
├── PSMinIO.csproj                    # Main project file
├── Version.ps1                       # Centralized version configuration
│
├── src/                              # Source code
│   ├── Properties/
│   │   └── AssemblyInfo.cs          # Assembly version information
│   ├── Cmdlets/                     # PowerShell cmdlet implementations
│   ├── Models/                      # Data models and result objects
│   └── Utils/                       # Utility classes and helpers
│
├── Module/                          # Built module directory
│   └── PSMinIO/
│       ├── PSMinIO.psd1            # Module manifest
│       ├── bin/                    # Compiled assemblies
│       └── types/                  # PowerShell type and format files
│
├── scripts/                        # All PowerShell scripts
│   ├── Build.ps1                   # Main build script
│   ├── Quick-Build.ps1             # Quick build without file locking issues
│   ├── Update-Version.ps1          # Version update script
│   ├── Publish-PSMinIOToGallery.ps1 # PowerShell Gallery publishing
│   └── examples/                   # Usage examples
│       ├── README.md               # Examples documentation
│       ├── 01-Basic-Operations.ps1
│       ├── 02-Advanced-Object-Listing.ps1
│       ├── 03-Directory-Management.ps1
│       ├── 04-Chunked-Operations.ps1
│       ├── 05-Bulk-Operations.ps1
│       └── 06-Enterprise-Automation.ps1
│
├── docs/                           # Documentation
│   ├── USAGE.md                    # Comprehensive usage guide
│   ├── RELEASE-NOTES.md            # Release notes
│   ├── POWERSHELL-GALLERY-RELEASE.md # Gallery release guide
│   └── PROJECT-STRUCTURE.md        # This file
│
├── Artifacts/                      # Build artifacts
├── Publish/                        # Publishing staging area
└── bin/                           # Build output
```

## Version Management System

### Centralized Version Configuration

The project uses a centralized version management system based on `Version.ps1`:

- **Version Format**: `yyyy.MM.dd.HHmm` (e.g., `2025.07.11.1151`)
- **Automatic Generation**: Version is generated based on current date/time
- **Centralized Updates**: Single script updates all version references

### Version Files

1. **Version.ps1** - Master version configuration
2. **src/Properties/AssemblyInfo.cs** - Assembly version information
3. **Module/PSMinIO/PSMinIO.psd1** - PowerShell module manifest

### Version Update Process

```powershell
# Update all version information
.\scripts\Update-Version.ps1

# Build with updated version
.\scripts\Quick-Build.ps1
```

## Build System

### Build Scripts

1. **scripts/Build.ps1** - Full build with validation and packaging
2. **scripts/Quick-Build.ps1** - Fast build for development (handles file locking)
3. **scripts/Update-Version.ps1** - Version management

### Build Process

1. Update version information across all files
2. Clean previous build artifacts
3. Compile .NET project
4. Copy assemblies to module directory
5. Validate module manifest
6. Optional: Run tests and create packages

### Handling File Locking

The Quick-Build script handles PowerShell file locking issues:
- Removes automatic copy from project file
- Manual copy with retry logic
- Graceful handling of locked files

## Scripts Organization

### Location
All scripts are now in the `scripts/` directory:
- Build and deployment scripts in `scripts/`
- Usage examples in `scripts/examples/`

### Import Paths
Example scripts use relative imports:
```powershell
Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1
```

## Documentation Structure

### Location
All documentation is in the `docs/` directory except README.md:
- `README.md` - Main project overview (root directory)
- `docs/USAGE.md` - Comprehensive usage guide
- `docs/RELEASE-NOTES.md` - Version history and changes
- `docs/POWERSHELL-GALLERY-RELEASE.md` - Publishing guide
- `docs/PROJECT-STRUCTURE.md` - This structure guide

### Content Organization
- **README.md**: Overview, installation, quick start
- **USAGE.md**: Detailed usage patterns and examples
- **Examples**: Practical scripts for common scenarios
- **Release Notes**: Version history and changes

## Development Workflow

### 1. Making Changes
```powershell
# Make code changes in src/
# Update documentation if needed
```

### 2. Building
```powershell
# Quick build for testing
.\scripts\Quick-Build.ps1

# Full build with validation
.\scripts\Build.ps1
```

### 3. Testing
```powershell
# Import and test module
Import-Module .\Module\PSMinIO\PSMinIO.psd1

# Run example scripts
.\scripts\examples\01-Basic-Operations.ps1
```

### 4. Committing
```powershell
# Version is automatically updated during build
git add .
git commit -m "Description of changes"
git push
```

### 5. Publishing
```powershell
# Publish to PowerShell Gallery
.\scripts\Publish-PSMinIOToGallery.ps1
```

## Key Benefits

### Centralized Version Management
- Single source of truth for version information
- Automatic timestamp-based versioning
- Consistent version across all files

### Organized Structure
- Clear separation of concerns
- All scripts in dedicated directory
- Documentation properly organized

### Improved Build Process
- Handles file locking issues
- Automatic version updates
- Validation and testing integration

### Professional Documentation
- Comprehensive usage examples
- Clear project structure
- Publishing guidelines

## Migration Notes

### From Previous Structure
- Examples moved from `examples/` to `scripts/examples/`
- Documentation moved to `docs/` (except README.md)
- Version management centralized in `Version.ps1`
- Build process improved with Quick-Build option

### Import Path Updates
All example scripts updated to use:
```powershell
Import-Module ..\..\Module\PSMinIO\PSMinIO.psd1
```

### Version Format Change
- Previous: Manual semantic versioning
- Current: Automatic date-based versioning (yyyy.MM.dd.HHmm)
- PowerShell Gallery: Uses semantic version for compatibility

This structure provides a professional, maintainable, and scalable foundation for the PSMinIO project.
