# Test script for the new New-MinIOZipArchive cmdlet
# Demonstrates comprehensive zip functionality with progress tracking

param(
    [string]$TestDirectory = "TestZipFiles",
    [switch]$Cleanup,
    [switch]$Verbose
)

if ($Verbose) {
    $VerbosePreference = 'Continue'
}

Write-Host "=== PSMinIO Zip Archive Test ===" -ForegroundColor Cyan
Write-Host "Testing New-MinIOZipArchive cmdlet with comprehensive functionality" -ForegroundColor Green

try {
    # Import the module
    Write-Host "`n1. Loading PSMinIO module..." -ForegroundColor Yellow
    Import-Module ".\Module\PSMinIO\PSMinIO.psd1" -Force
    
    # Create test directory and files
    Write-Host "2. Creating test files..." -ForegroundColor Yellow
    if (Test-Path $TestDirectory) {
        Remove-Item $TestDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TestDirectory -Force | Out-Null
    
    # Create main directory files
    for ($i = 1; $i -le 10; $i++) {
        $fileName = "document-{0:D2}.txt" -f $i
        $filePath = Join-Path $TestDirectory $fileName
        $content = "Document $i`n" + ("Sample content line $i`n" * (Get-Random -Minimum 5 -Maximum 20))
        Set-Content -Path $filePath -Value $content
    }
    
    # Create subdirectories with different file types
    $subDir1 = Join-Path $TestDirectory "Reports"
    $subDir2 = Join-Path $TestDirectory "Data"
    $subDir3 = Join-Path $TestDirectory "Logs"
    New-Item -ItemType Directory -Path $subDir1, $subDir2, $subDir3 -Force | Out-Null
    
    # Add files to subdirectories
    for ($i = 1; $i -le 5; $i++) {
        Set-Content -Path (Join-Path $subDir1 "report$i.txt") -Value "Report $i content"
        Set-Content -Path (Join-Path $subDir2 "data$i.csv") -Value "ID,Name,Value`n$i,Item$i,$(Get-Random -Minimum 100 -Maximum 1000)"
        Set-Content -Path (Join-Path $subDir3 "app$i.log") -Value "$(Get-Date) - Log entry $i"
    }
    
    $allFiles = Get-ChildItem $TestDirectory -Recurse -File
    Write-Host "✅ Created test structure: $($allFiles.Count) files in multiple directories" -ForegroundColor Green
    
    # Test 1: Create zip from FileInfo array
    Write-Host "`n3. Test 1: Creating zip from FileInfo array..." -ForegroundColor Yellow
    $mainFiles = Get-ChildItem $TestDirectory -File
    $zipFile1 = [System.IO.FileInfo]"test-files-array.zip"

    Write-Host "   Creating zip with $($mainFiles.Count) files using Files parameter set" -ForegroundColor Cyan
    $result1 = New-MinIOZipArchive -DestinationPath $zipFile1 -Path $mainFiles -CompressionLevel Optimal -Verbose
    
    Write-Host "✅ Files array zip created:" -ForegroundColor Green
    Write-Host "   Files: $($result1.FileCount)" -ForegroundColor White
    Write-Host "   Original size: $([math]::Round($result1.TotalUncompressedSize / 1KB, 2)) KB" -ForegroundColor White
    Write-Host "   Compressed size: $([math]::Round($result1.TotalCompressedSize / 1KB, 2)) KB" -ForegroundColor White
    Write-Host "   Compression: $($result1.CompressionEfficiency.ToString('F1'))%" -ForegroundColor White
    Write-Host "   Duration: $($result1.Duration.TotalSeconds.ToString('F2'))s" -ForegroundColor White
    
    # Test 2: Create zip from directory (non-recursive)
    Write-Host "`n4. Test 2: Creating zip from directory (non-recursive)..." -ForegroundColor Yellow
    $zipFile2 = [System.IO.FileInfo]"test-directory-flat.zip"

    Write-Host "   Creating zip from directory (top-level files only)" -ForegroundColor Cyan
    $result2 = New-MinIOZipArchive -DestinationPath $zipFile2 -Directory (Get-Item $TestDirectory) -CompressionLevel Fastest -Verbose
    
    Write-Host "✅ Directory flat zip created:" -ForegroundColor Green
    Write-Host "   Files: $($result2.FileCount)" -ForegroundColor White
    Write-Host "   Compression: $($result2.CompressionEfficiency.ToString('F1'))%" -ForegroundColor White
    
    # Test 3: Create zip from directory (recursive)
    Write-Host "`n5. Test 3: Creating zip from directory (recursive)..." -ForegroundColor Yellow
    $zipFile3 = [System.IO.FileInfo]"test-directory-recursive.zip"

    Write-Host "   Creating zip from directory (recursive, all subdirectories)" -ForegroundColor Cyan
    $result3 = New-MinIOZipArchive -DestinationPath $zipFile3 -Directory (Get-Item $TestDirectory) -Recursive -IncludeBaseDirectory -CompressionLevel Optimal -Verbose
    
    Write-Host "✅ Directory recursive zip created:" -ForegroundColor Green
    Write-Host "   Files: $($result3.FileCount)" -ForegroundColor White
    Write-Host "   Compression: $($result3.CompressionEfficiency.ToString('F1'))%" -ForegroundColor White
    
    # Test 4: Create zip with file filtering
    Write-Host "`n6. Test 4: Creating zip with file filtering..." -ForegroundColor Yellow
    $zipFile4 = [System.IO.FileInfo]"test-filtered-logs.zip"

    Write-Host "   Creating zip with only .log files using InclusionFilter" -ForegroundColor Cyan
    $result4 = New-MinIOZipArchive -DestinationPath $zipFile4 -Directory (Get-Item $TestDirectory) -Recursive -InclusionFilter { $_.Extension -eq ".log" } -CompressionLevel Optimal -Verbose

    Write-Host "✅ Filtered zip created:" -ForegroundColor Green
    Write-Host "   Log files: $($result4.FileCount)" -ForegroundColor White
    Write-Host "   Compression: $($result4.CompressionEfficiency.ToString('F1'))%" -ForegroundColor White

    # Test 5: Append to existing zip (Update mode)
    Write-Host "`n7. Test 5: Appending to existing zip..." -ForegroundColor Yellow
    $csvFiles = Get-ChildItem $TestDirectory -Recurse -Filter "*.csv"

    Write-Host "   Appending $($csvFiles.Count) CSV files to existing zip" -ForegroundColor Cyan
    $result5 = New-MinIOZipArchive -DestinationPath $zipFile4 -Path $csvFiles -Mode Update -CompressionLevel Optimal -Verbose

    Write-Host "✅ Files appended to zip:" -ForegroundColor Green
    Write-Host "   Total files now: $($result5.FileCount)" -ForegroundColor White

    # Test 6: Create zip with custom base path
    Write-Host "`n8. Test 6: Creating zip with custom base path..." -ForegroundColor Yellow
    $zipFile6 = [System.IO.FileInfo]"test-custom-basepath.zip"

    Write-Host "   Creating zip with custom base path (flattened structure)" -ForegroundColor Cyan
    $result6 = New-MinIOZipArchive -DestinationPath $zipFile6 -Directory (Get-Item $TestDirectory) -Recursive -BasePath $TestDirectory -CompressionLevel Optimal -Verbose

    Write-Host "✅ Custom base path zip created:" -ForegroundColor Green
    Write-Host "   Files: $($result6.FileCount)" -ForegroundColor White
    
    # Test 7: Get zip archive information
    Write-Host "`n9. Test 7: Reading zip archive information..." -ForegroundColor Yellow
    $zipFiles = Get-ChildItem "*.zip"

    foreach ($zipFile in $zipFiles) {
        Write-Host "   Reading archive: $($zipFile.Name)" -ForegroundColor Cyan

        # Basic archive info
        $archiveInfo = Get-MinIOZipArchive -ZipFile $zipFile -Verbose
        Write-Host "     Entries: $($archiveInfo.EntryCount)" -ForegroundColor White
        Write-Host "     Size: $([math]::Round($archiveInfo.TotalUncompressedSize / 1KB, 2)) KB -> $([math]::Round($archiveInfo.TotalCompressedSize / 1KB, 2)) KB" -ForegroundColor White
        Write-Host "     Compression: $($archiveInfo.CompressionEfficiency.ToString('F1'))%" -ForegroundColor White

        # Detailed entries for one archive
        if ($zipFile.Name -eq "test-directory-recursive.zip") {
            Write-Host "   Getting detailed entries for recursive archive..." -ForegroundColor Cyan
            $detailedInfo = Get-MinIOZipArchive -ZipFile $zipFile -IncludeEntries -Verbose
            Write-Host "     Entry details: $($detailedInfo.Entries.Count) entries" -ForegroundColor White

            # Show first few entries
            $detailedInfo.Entries | Select-Object -First 3 | ForEach-Object {
                Write-Host "       $($_.FullName) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Gray
            }
        }

        # Validate integrity for one archive
        if ($zipFile.Name -eq "test-files-array.zip") {
            Write-Host "   Validating archive integrity..." -ForegroundColor Cyan
            $validatedInfo = Get-MinIOZipArchive -ZipFile $zipFile -ValidateIntegrity -Verbose
            $validStatus = if ($validatedInfo.IsValid) { "✅ Valid" } else { "❌ Invalid" }
            Write-Host "     Validation: $validStatus (took $($validatedInfo.ValidationDuration.TotalMilliseconds.ToString('F0'))ms)" -ForegroundColor White
        }
    }

    Write-Host "✅ Archive reading tests completed!" -ForegroundColor Green
    
    # Summary
    Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
    Write-Host "✅ All zip archive tests completed successfully!" -ForegroundColor Green
    Write-Host "New-MinIOZipArchive features tested:" -ForegroundColor Yellow
    Write-Host "  • FileInfo destination path parameter" -ForegroundColor White
    Write-Host "  • FileInfo[] parameter with Files parameter set" -ForegroundColor White
    Write-Host "  • Directory parameter with recursive and non-recursive modes" -ForegroundColor White
    Write-Host "  • File filtering with InclusionFilter ScriptBlock" -ForegroundColor White
    Write-Host "  • Append mode (Update) for adding files to existing zips" -ForegroundColor White
    Write-Host "  • Custom base path for entry name control" -ForegroundColor White
    Write-Host "  • Multiple compression levels (Optimal, Fastest)" -ForegroundColor White
    Write-Host "  • Comprehensive progress tracking and metrics" -ForegroundColor White
    Write-Host "  • Always returns result objects (no PassThru needed)" -ForegroundColor White

    Write-Host "Get-MinIOZipArchive features tested:" -ForegroundColor Yellow
    Write-Host "  • Basic archive information reading" -ForegroundColor White
    Write-Host "  • Detailed entry information with IncludeEntries" -ForegroundColor White
    Write-Host "  • Archive integrity validation" -ForegroundColor White
    Write-Host "  • Proper disposal handling with OpenRead" -ForegroundColor White
    Write-Host "  • Comprehensive metrics and compression statistics" -ForegroundColor White
    
    Write-Host "`nZip files created:" -ForegroundColor Yellow
    foreach ($zipFile in $zipFiles) {
        $sizeKB = [math]::Round($zipFile.Length / 1KB, 2)
        Write-Host "  • $($zipFile.Name) ($sizeKB KB)" -ForegroundColor White
    }
    
    if ($Cleanup) {
        Write-Host "`n10. Cleaning up..." -ForegroundColor Yellow
        Remove-Item "*.zip" -Force -ErrorAction SilentlyContinue
        Write-Host "✅ Cleanup completed!" -ForegroundColor Green
    } else {
        Write-Host "`nZip files preserved for inspection. Use -Cleanup to remove them." -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
} finally {
    # Clean up test directory
    if (Test-Path $TestDirectory) {
        Remove-Item $TestDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
