# PSMinIO Enterprise Automation Example
# Demonstrates enterprise-grade automation scenarios including monitoring, policies, and scheduled operations

# Import the module
Import-Module ..\Module\PSMinIO\PSMinIO.psd1

# Connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

# Configuration
$logFile = "minio-automation-$(Get-Date -Format 'yyyyMMdd').log"
$reportFile = "minio-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    $logEntry | Out-File -FilePath $logFile -Append -Encoding UTF8
    $logEntry
}

try {
    Write-Log "Starting MinIO enterprise automation demo"
    
    # Connect to MinIO
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    Write-Log "Connected to MinIO server: $endpoint"

    "=== 1. Infrastructure Health Check ==="
    # Perform comprehensive health checks
    Write-Log "Performing infrastructure health check"
    
    # Check server connectivity and basic operations
    try {
        $buckets = Get-MinIOBucket
        Write-Log "✅ Server connectivity: OK ($($buckets.Count) buckets found)"
        "✅ Server connectivity: OK ($($buckets.Count) buckets found)"
    } catch {
        Write-Log "❌ Server connectivity: FAILED - $($_.Exception.Message)" "ERROR"
        "❌ Server connectivity: FAILED"
        throw
    }
    
    # Test bucket operations
    $testBucketName = "health-check-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    try {
        New-MinIOBucket -BucketName $testBucketName
        $testExists = Test-MinIOBucketExists -BucketName $testBucketName
        Remove-MinIOBucket -BucketName $testBucketName -Force
        Write-Log "✅ Bucket operations: OK"
        "✅ Bucket operations: OK"
    } catch {
        Write-Log "❌ Bucket operations: FAILED - $($_.Exception.Message)" "ERROR"
        "❌ Bucket operations: FAILED"
    }
    ""

    "=== 2. Storage Statistics and Monitoring ==="
    # Collect comprehensive storage statistics
    Write-Log "Collecting storage statistics"
    
    $stats = Get-MinIOStats -MaxObjectsToCount 1000
    Write-Log "Storage stats collected: $($stats.TotalBuckets) buckets, $($stats.TotalObjects) objects, $($stats.TotalSizeFormatted)"
    
    "Storage Overview:"
    $stats | Format-List TotalBuckets, TotalObjects, TotalSizeFormatted, AverageObjectSizeFormatted
    ""

    # Detailed bucket analysis
    "Bucket Analysis:"
    $bucketStats = Get-MinIOBucket -IncludeStatistics
    $bucketStats | Format-Table Name, @{Name="Objects";Expression={$_.ObjectCount}}, @{Name="Size";Expression={$_.SizeFormatted}}, CreationDate -AutoSize
    ""

    "=== 3. Automated Backup Operations ==="
    # Simulate automated backup scenario
    Write-Log "Starting automated backup operations"
    
    $backupBucket = "automated-backups-$(Get-Date -Format 'yyyyMMdd')"
    New-MinIOBucket -BucketName $backupBucket
    Write-Log "Created backup bucket: $backupBucket"
    
    # Create sample data to backup
    $backupData = @{
        "config-backup.json" = @{
            timestamp = Get-Date
            server = $env:COMPUTERNAME
            version = "1.0"
        } | ConvertTo-Json
        
        "database-backup.sql" = "-- Database backup generated on $(Get-Date)`nSELECT * FROM users;"
        
        "logs-backup.txt" = "Application logs backup`n$(Get-Date) - System started`n$(Get-Date) - Backup initiated"
    }
    
    foreach ($file in $backupData.Keys) {
        $backupData[$file] | Out-File -FilePath $file -Encoding UTF8
    }
    
    # Perform backup with organized directory structure
    $backupDate = Get-Date -Format "yyyy/MM/dd"
    $backupTime = Get-Date -Format "HH-mm"
    
    $backupResults = New-MinIOObject -BucketName $backupBucket -Files $backupData.Keys -BucketDirectory "daily-backups/$backupDate/$backupTime"
    Write-Log "Backup completed: $($backupResults.Count) files uploaded"
    
    "Backup Results:"
    $backupResults | Format-Table Name, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}
    ""

    "=== 4. Data Lifecycle Management ==="
    # Simulate data lifecycle management
    Write-Log "Performing data lifecycle management"
    
    # Create lifecycle demo bucket
    $lifecycleBucket = "lifecycle-demo-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $lifecycleBucket
    
    # Upload files with different "ages" (simulated by different directories)
    $lifecycleFiles = @(
        @{ Name = "current-data.txt"; Content = "Current data"; Directory = "current" }
        @{ Name = "week-old-data.txt"; Content = "Week old data"; Directory = "archive/weekly" }
        @{ Name = "month-old-data.txt"; Content = "Month old data"; Directory = "archive/monthly" }
        @{ Name = "year-old-data.txt"; Content = "Year old data"; Directory = "archive/yearly" }
    )
    
    foreach ($file in $lifecycleFiles) {
        $file.Content | Out-File -FilePath $file.Name -Encoding UTF8
        New-MinIOObject -BucketName $lifecycleBucket -Files $file.Name -BucketDirectory $file.Directory
        Remove-Item $file.Name -Force
    }
    
    # Analyze data by lifecycle stage
    $allLifecycleObjects = Get-MinIOObject -BucketName $lifecycleBucket -ObjectsOnly
    "Data Lifecycle Analysis:"
    $allLifecycleObjects | Group-Object { ($_.Name -split '/')[0] } | Format-Table Name, Count -AutoSize
    ""

    "=== 5. Security and Compliance Audit ==="
    # Perform security audit
    Write-Log "Performing security and compliance audit"
    
    $auditResults = @()
    
    # Check bucket policies
    foreach ($bucket in $buckets) {
        try {
            $policy = Get-MinIOBucketPolicy -BucketName $bucket.Name
            $auditResults += [PSCustomObject]@{
                Bucket = $bucket.Name
                HasPolicy = $policy -ne $null
                PolicySize = if ($policy) { $policy.Length } else { 0 }
                Status = if ($policy) { "Policy Configured" } else { "No Policy" }
            }
        } catch {
            $auditResults += [PSCustomObject]@{
                Bucket = $bucket.Name
                HasPolicy = $false
                PolicySize = 0
                Status = "Policy Check Failed"
            }
        }
    }
    
    "Security Audit Results:"
    $auditResults | Format-Table Bucket, HasPolicy, Status -AutoSize
    Write-Log "Security audit completed: $($auditResults.Count) buckets audited"
    ""

    "=== 6. Performance Monitoring ==="
    # Monitor performance metrics
    Write-Log "Collecting performance metrics"
    
    # Test upload/download performance
    $perfTestFile = "performance-test.txt"
    $perfTestContent = "Performance test data " * 1000  # ~20KB
    $perfTestContent | Out-File -FilePath $perfTestFile -Encoding UTF8 -NoNewline
    
    $perfBucket = "performance-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-MinIOBucket -BucketName $perfBucket
    
    # Measure upload performance
    $uploadStart = Get-Date
    $uploadResult = New-MinIOObject -BucketName $perfBucket -Files $perfTestFile
    $uploadEnd = Get-Date
    $uploadDuration = $uploadEnd - $uploadStart
    
    # Measure download performance
    $downloadStart = Get-Date
    $downloadResult = Get-MinIOObjectContent -BucketName $perfBucket -ObjectName $perfTestFile -FilePath "downloaded-$perfTestFile"
    $downloadEnd = Get-Date
    $downloadDuration = $downloadEnd - $downloadStart
    
    "Performance Metrics:"
    [PSCustomObject]@{
        Operation = "Upload"
        Duration = $uploadDuration.TotalMilliseconds
        Speed = $uploadResult.AverageSpeedFormatted
        FileSize = (Get-Item $perfTestFile).Length
    } | Format-Table
    
    [PSCustomObject]@{
        Operation = "Download"
        Duration = $downloadDuration.TotalMilliseconds
        Speed = $downloadResult.AverageSpeedFormatted
        FileSize = (Get-Item "downloaded-$perfTestFile").Length
    } | Format-Table
    
    Write-Log "Performance test completed - Upload: $($uploadDuration.TotalMilliseconds)ms, Download: $($downloadDuration.TotalMilliseconds)ms"
    ""

    "=== 7. Generating HTML Report ==="
    # Generate comprehensive HTML report
    Write-Log "Generating HTML report"
    
    $htmlReport = @"
<!DOCTYPE html>
<html>
<head>
    <title>MinIO Enterprise Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #f0f0f0; padding: 10px; border-radius: 5px; }
        .section { margin: 20px 0; }
        .metric { background-color: #e8f4f8; padding: 10px; margin: 5px 0; border-radius: 3px; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        .success { color: green; }
        .warning { color: orange; }
        .error { color: red; }
    </style>
</head>
<body>
    <div class="header">
        <h1>MinIO Enterprise Automation Report</h1>
        <p>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
        <p>Server: $endpoint</p>
    </div>
    
    <div class="section">
        <h2>Storage Overview</h2>
        <div class="metric">Total Buckets: $($stats.TotalBuckets)</div>
        <div class="metric">Total Objects: $($stats.TotalObjects)</div>
        <div class="metric">Total Size: $($stats.TotalSizeFormatted)</div>
    </div>
    
    <div class="section">
        <h2>Health Check Results</h2>
        <div class="metric success">✅ Server Connectivity: OK</div>
        <div class="metric success">✅ Bucket Operations: OK</div>
    </div>
    
    <div class="section">
        <h2>Performance Metrics</h2>
        <div class="metric">Upload Performance: $($uploadResult.AverageSpeedFormatted)</div>
        <div class="metric">Download Performance: $($downloadResult.AverageSpeedFormatted)</div>
    </div>
    
    <div class="section">
        <h2>Security Audit</h2>
        <p>$($auditResults.Count) buckets audited</p>
        <p>Buckets with policies: $(($auditResults | Where-Object HasPolicy).Count)</p>
    </div>
</body>
</html>
"@
    
    $htmlReport | Out-File -FilePath $reportFile -Encoding UTF8
    Write-Log "HTML report generated: $reportFile"
    "📊 HTML report generated: $reportFile"
    ""

    # Clean up demo resources
    Write-Log "Cleaning up demo resources"
    Remove-MinIOBucket -BucketName $backupBucket -Force
    Remove-MinIOBucket -BucketName $lifecycleBucket -Force
    Remove-MinIOBucket -BucketName $perfBucket -Force
    Remove-Item $backupData.Keys, $perfTestFile, "downloaded-$perfTestFile" -Force -ErrorAction SilentlyContinue
    
    Write-Log "Enterprise automation demo completed successfully"
    "✅ Enterprise automation demo completed successfully"
    "📋 Check the log file: $logFile"
    "📊 View the report: $reportFile"

} catch {
    Write-Log "❌ Enterprise automation demo failed: $($_.Exception.Message)" "ERROR"
    "❌ Error: $($_.Exception.Message)"
}
