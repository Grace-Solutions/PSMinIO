# PSMinIO Basic Operations Example
# This script demonstrates fundamental MinIO operations using PSMinIO

# Import the module
Import-Module ..\Module\PSMinIO\PSMinIO.psd1

# Example connection details (replace with your actual values)
$endpoint = "https://minio.example.com"
$accessKey = "your-access-key"
$secretKey = "your-secret-key"

try {
    # Connect to MinIO server
    $connection = Connect-MinIO -Endpoint $endpoint -AccessKey $accessKey -SecretKey $secretKey
    "Connected to MinIO server: $endpoint"

    # List all buckets
    "Listing all buckets..."
    $buckets = Get-MinIOBucket
    $buckets | Format-Table Name, CreationDate, @{Name="Objects";Expression={"N/A"}}

    # Create a new bucket
    $bucketName = "example-bucket-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    "Creating bucket: $bucketName"
    New-MinIOBucket -BucketName $bucketName

    # Verify bucket creation
    if (Test-MinIOBucketExists -BucketName $bucketName) {
        "✅ Bucket '$bucketName' created successfully"
    }

    # Create a sample file for upload
    $sampleFile = "sample-document.txt"
    "This is a sample document created on $(Get-Date)" | Out-File -FilePath $sampleFile -Encoding UTF8

    # Upload the file
    "Uploading file: $sampleFile"
    $uploadResult = New-MinIOObject -BucketName $bucketName -Files $sampleFile
    $uploadResult | Format-Table Name, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}

    # List objects in the bucket
    "Listing objects in bucket '$bucketName':"
    $objects = Get-MinIOObject -BucketName $bucketName
    $objects | Format-Table Name, @{Name="Size";Expression={"$($_.Size) bytes"}}, LastModified

    # Download the file
    $downloadPath = "downloaded-$sampleFile"
    "Downloading file to: $downloadPath"
    $downloadResult = Get-MinIOObjectContent -BucketName $bucketName -ObjectName $sampleFile -FilePath $downloadPath
    $downloadResult | Format-Table @{Name="LocalFile";Expression={$_.FilePath.Name}}, @{Name="Duration";Expression={$_.Duration}}, @{Name="Speed";Expression={$_.AverageSpeedFormatted}}

    # Verify download
    if (Test-Path $downloadPath) {
        "✅ File downloaded successfully"
        "Content: $(Get-Content $downloadPath)"
    }

    # Clean up
    "Cleaning up..."
    Remove-MinIOObject -BucketName $bucketName -ObjectName $sampleFile -Force
    Remove-MinIOBucket -BucketName $bucketName -Force
    Remove-Item $sampleFile, $downloadPath -Force -ErrorAction SilentlyContinue

    "✅ Basic operations completed successfully"

} catch {
    "❌ Error: $($_.Exception.Message)"
} finally {
    # Clean up any remaining files
    Remove-Item "sample-document.txt", "downloaded-sample-document.txt" -Force -ErrorAction SilentlyContinue
}
