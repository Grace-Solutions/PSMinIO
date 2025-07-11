# PowerShell Gallery Release Guide for PSMinIO

This guide walks you through publishing PSMinIO to the PowerShell Gallery.

## Prerequisites

### 1. PowerShell Gallery Account
- Create an account at [PowerShell Gallery](https://www.powershellgallery.com/)
- Generate an API key from your account settings
- Keep your API key secure and never commit it to source control

### 2. Required PowerShell Modules
```powershell
# Install required modules if not already present
Install-Module -Name PowerShellGet -Force -AllowClobber
Install-Module -Name PackageManagement -Force -AllowClobber
```

### 3. Verify Module Build
Ensure the module has been built and all files are present:
```powershell
# Build the module (if needed)
dotnet build PSMinIO.csproj --configuration Release

# Verify module structure
Get-ChildItem .\Module\PSMinIO\ -Recurse
```

## Pre-Release Checklist

### ✅ Module Validation
- [ ] Module manifest (PSMinIO.psd1) is valid
- [ ] All required assemblies are present (PSMinIO.dll, Minio.dll)
- [ ] Type and format files are included
- [ ] Version number follows semantic versioning (2.0.0)
- [ ] Release notes are comprehensive and accurate
- [ ] All cmdlets are properly exported

### ✅ Documentation
- [ ] README.md is updated with latest features
- [ ] USAGE.md includes comprehensive examples
- [ ] Examples directory contains working scripts
- [ ] Release notes document all changes
- [ ] License file is present and correct

### ✅ Testing
- [ ] Module imports without errors
- [ ] All exported cmdlets are available
- [ ] Basic functionality tests pass
- [ ] Examples run successfully
- [ ] No Write-Host usage in production code

### ✅ Repository
- [ ] All changes are committed to Git
- [ ] Repository is pushed to GitHub
- [ ] Tags are created for the release
- [ ] GitHub release is created (optional but recommended)

## Release Process

### Step 1: Validate Everything
Run the automated validation:
```powershell
# Test the release process without actually publishing
.\Publish-PSMinIOToGallery.ps1 -WhatIf
```

### Step 2: Publish to PowerShell Gallery
```powershell
# Publish with your API key
.\Publish-PSMinIOToGallery.ps1 -NuGetApiKey "YOUR_API_KEY_HERE"

# Or let the script prompt for the API key securely
.\Publish-PSMinIOToGallery.ps1
```

### Step 3: Verify Publication
1. Check the PowerShell Gallery: https://www.powershellgallery.com/packages/PSMinIO
2. Test installation from the gallery:
   ```powershell
   Install-Module -Name PSMinIO -Force
   Import-Module PSMinIO
   Get-Command -Module PSMinIO
   ```

## Post-Release Tasks

### 1. Update Documentation
- [ ] Update README.md with installation instructions from PowerShell Gallery
- [ ] Add PowerShell Gallery badge to README
- [ ] Update any version references in documentation

### 2. GitHub Release
Create a GitHub release with:
- [ ] Tag matching the module version (v2.0.0)
- [ ] Release title: "PSMinIO v2.0.0 - Major Enhancement Release"
- [ ] Copy release notes from the module manifest
- [ ] Attach any relevant assets

### 3. Announce the Release
- [ ] Update project documentation
- [ ] Notify users through appropriate channels
- [ ] Consider creating a blog post or announcement

## Troubleshooting

### Common Issues

#### 1. Module Validation Errors
```powershell
# Check manifest syntax
Test-ModuleManifest -Path .\Module\PSMinIO\PSMinIO.psd1
```

#### 2. Missing Dependencies
Ensure all required files are in the module directory:
- `bin\PSMinIO.dll`
- `bin\Minio.dll`
- `types\PSMinIO.Types.ps1xml`
- `types\PSMinIO.Format.ps1xml`

#### 3. API Key Issues
- Verify your API key is correct
- Check that your PowerShell Gallery account has publishing permissions
- Ensure the API key hasn't expired

#### 4. Version Conflicts
If the version already exists:
- Increment the version number in PSMinIO.psd1
- Rebuild the module
- Commit and push changes

### Getting Help

#### PowerShell Gallery Support
- Documentation: https://docs.microsoft.com/en-us/powershell/gallery/
- Issues: https://github.com/PowerShell/PowerShellGallery/issues

#### Module-Specific Issues
- Check the validation output from the publish script
- Review the module manifest for syntax errors
- Ensure all dependencies are properly referenced

## Security Considerations

### API Key Management
- Never commit API keys to source control
- Use environment variables or secure prompts
- Rotate API keys regularly
- Limit API key permissions to publishing only

### Module Security
- All assemblies are signed and from trusted sources
- No malicious code or backdoors
- Dependencies are from official NuGet packages
- Code has been reviewed for security issues

## Version Management

### Semantic Versioning
PSMinIO follows semantic versioning (MAJOR.MINOR.PATCH):
- **MAJOR**: Breaking changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

### Current Release: v2.0.0
This is a major release with significant new features and improvements:
- Complete Get-MinIOObject implementation
- Enhanced directory management
- Advanced chunked operations
- Comprehensive documentation and examples

### Next Release Planning
Future releases will follow the same process:
1. Increment version number appropriately
2. Update release notes
3. Follow this release guide
4. Publish to PowerShell Gallery

## Success Metrics

After release, monitor:
- [ ] Download statistics on PowerShell Gallery
- [ ] User feedback and issues
- [ ] GitHub stars and forks
- [ ] Community adoption and usage

The PSMinIO module is now ready for enterprise use with professional-grade functionality and comprehensive documentation!
