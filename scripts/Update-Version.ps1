# Update-Version.ps1
# Updates version information across all PSMinIO project files

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

# Import version configuration
. "$PSScriptRoot\..\Version.ps1"

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$VersionInfo = Get-PSMinIOVersion

Write-Host "PSMinIO Version Update Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Current Version: $($VersionInfo.Version)" -ForegroundColor Green
Write-Host "Build Date: $($VersionInfo.BuildDateString)" -ForegroundColor Green
Write-Host "Semantic Version: $($VersionInfo.SemanticVersion)" -ForegroundColor Green
Write-Host ""

# Define file paths
$FilesToUpdate = @{
    "Module Manifest" = @{
        Path = Join-Path $ProjectRoot "Module\PSMinIO\PSMinIO.psd1"
        Type = "ModuleManifest"
    }
    "Assembly Info" = @{
        Path = Join-Path $ProjectRoot "src\Properties\AssemblyInfo.cs"
        Type = "AssemblyInfo"
    }
    "Project File" = @{
        Path = Join-Path $ProjectRoot "PSMinIO.csproj"
        Type = "ProjectFile"
    }
}

function Update-ModuleManifest {
    param($FilePath, $VersionInfo)
    
    if (-not (Test-Path $FilePath)) {
        Write-Warning "Module manifest not found: $FilePath"
        return
    }
    
    Write-Host "Updating Module Manifest: $FilePath" -ForegroundColor Yellow
    
    if ($WhatIf) {
        Write-Host "  [WhatIf] Would update ModuleVersion to: $($VersionInfo.Version)" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update Copyright to: $($VersionInfo.Copyright)" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update Description" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update ReleaseNotes" -ForegroundColor Magenta
        return
    }
    
    $content = Get-Content $FilePath -Raw
    
    # Update version
    $content = $content -replace "ModuleVersion\s*=\s*'[^']*'", "ModuleVersion = '$($VersionInfo.Version)'"
    
    # Update copyright
    $content = $content -replace "Copyright\s*=\s*'[^']*'", "Copyright = '$($VersionInfo.Copyright)'"
    
    # Update description
    $escapedDescription = $VersionInfo.Description -replace "'", "''"
    $content = $content -replace "Description\s*=\s*'[^']*'", "Description = '$escapedDescription'"
    
    # Update author and company
    $content = $content -replace "Author\s*=\s*'[^']*'", "Author = '$($VersionInfo.Author)'"
    $content = $content -replace "CompanyName\s*=\s*'[^']*'", "CompanyName = '$($VersionInfo.CompanyName)'"
    
    # Update URLs
    $content = $content -replace "ProjectUri\s*=\s*'[^']*'", "ProjectUri = '$($VersionInfo.ProjectUri)'"
    $content = $content -replace "LicenseUri\s*=\s*'[^']*'", "LicenseUri = '$($VersionInfo.LicenseUri)'"
    
    # Update release notes (this is more complex due to multiline)
    $escapedReleaseNotes = $VersionInfo.ReleaseNotes -replace "'", "''" -replace "`r`n", "`n" -replace "`n", "``n"
    $content = $content -replace "ReleaseNotes\s*=\s*@'[^']*'@", "ReleaseNotes = @'`n$($VersionInfo.ReleaseNotes)`n'@"
    
    $content | Set-Content $FilePath -Encoding UTF8
    Write-Host "  ✅ Module manifest updated" -ForegroundColor Green
}

function Update-AssemblyInfo {
    param($FilePath, $VersionInfo)
    
    if (-not (Test-Path $FilePath)) {
        Write-Warning "Assembly info not found: $FilePath"
        return
    }
    
    Write-Host "Updating Assembly Info: $FilePath" -ForegroundColor Yellow
    
    if ($WhatIf) {
        Write-Host "  [WhatIf] Would update AssemblyVersion to: $($VersionInfo.Version)" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update AssemblyFileVersion to: $($VersionInfo.Version)" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update AssemblyInformationalVersion to: $($VersionInfo.Version)" -ForegroundColor Magenta
        Write-Host "  [WhatIf] Would update AssemblyCopyright to: $($VersionInfo.Copyright)" -ForegroundColor Magenta
        return
    }
    
    $content = Get-Content $FilePath -Raw
    
    # Update version attributes
    $content = $content -replace '\[assembly:\s*AssemblyVersion\("[^"]*"\)\]', "[assembly: AssemblyVersion(`"$($VersionInfo.Version)`")]"
    $content = $content -replace '\[assembly:\s*AssemblyFileVersion\("[^"]*"\)\]', "[assembly: AssemblyFileVersion(`"$($VersionInfo.Version)`")]"
    $content = $content -replace '\[assembly:\s*AssemblyInformationalVersion\("[^"]*"\)\]', "[assembly: AssemblyInformationalVersion(`"$($VersionInfo.Version)`")]"
    
    # Update other attributes
    $content = $content -replace '\[assembly:\s*AssemblyCopyright\("[^"]*"\)\]', "[assembly: AssemblyCopyright(`"$($VersionInfo.Copyright)`")]"
    $content = $content -replace '\[assembly:\s*AssemblyCompany\("[^"]*"\)\]', "[assembly: AssemblyCompany(`"$($VersionInfo.CompanyName)`")]"
    $content = $content -replace '\[assembly:\s*AssemblyDescription\("[^"]*"\)\]', "[assembly: AssemblyDescription(`"$($VersionInfo.Description)`")]"
    
    $content | Set-Content $FilePath -Encoding UTF8
    Write-Host "  ✅ Assembly info updated" -ForegroundColor Green
}

function Update-ProjectFile {
    param($FilePath, $VersionInfo)
    
    if (-not (Test-Path $FilePath)) {
        Write-Warning "Project file not found: $FilePath"
        return
    }
    
    Write-Host "Updating Project File: $FilePath" -ForegroundColor Yellow
    
    if ($WhatIf) {
        Write-Host "  [WhatIf] Project file uses AssemblyInfo.cs for version information" -ForegroundColor Magenta
        return
    }
    
    # Project file now uses AssemblyInfo.cs, so no direct updates needed
    Write-Host "  ✅ Project file uses AssemblyInfo.cs for version information" -ForegroundColor Green
}

# Process each file
foreach ($fileInfo in $FilesToUpdate.GetEnumerator()) {
    $fileName = $fileInfo.Key
    $fileData = $fileInfo.Value
    
    Write-Host ""
    Write-Host "Processing: $fileName" -ForegroundColor Cyan
    
    switch ($fileData.Type) {
        "ModuleManifest" {
            Update-ModuleManifest -FilePath $fileData.Path -VersionInfo $VersionInfo
        }
        "AssemblyInfo" {
            Update-AssemblyInfo -FilePath $fileData.Path -VersionInfo $VersionInfo
        }
        "ProjectFile" {
            Update-ProjectFile -FilePath $fileData.Path -VersionInfo $VersionInfo
        }
    }
}

Write-Host ""
Write-Host "Version Update Summary" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host "Version: $($VersionInfo.Version)" -ForegroundColor Green
Write-Host "Build Date: $($VersionInfo.BuildDateString)" -ForegroundColor Green
Write-Host "Author: $($VersionInfo.Author)" -ForegroundColor Green
Write-Host "Company: $($VersionInfo.CompanyName)" -ForegroundColor Green

if ($WhatIf) {
    Write-Host ""
    Write-Host "This was a WhatIf run - no files were actually modified" -ForegroundColor Yellow
    Write-Host "Run without -WhatIf to apply changes" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "✅ Version update completed successfully!" -ForegroundColor Green
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Build the project: dotnet build --configuration Release" -ForegroundColor White
    Write-Host "  2. Test the module: Import-Module .\Module\PSMinIO\PSMinIO.psd1" -ForegroundColor White
    Write-Host "  3. Commit changes: git add . && git commit -m 'Update version to $($VersionInfo.Version)'" -ForegroundColor White
}
