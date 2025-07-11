using System;
using System.IO;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Downloads objects from MinIO
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObjectContent", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MinIODownloadResult))]
    public class GetMinIOObjectContentCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object to download
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Key", "Name")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Local file path where the object should be saved
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Path", "FilePath")]
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// Force overwrite if local file already exists
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Create directory structure if it doesn't exist
        /// </summary>
        [Parameter]
        public SwitchParameter CreateDirectories { get; set; }

        /// <summary>
        /// Return the download result information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(BucketName);
            ValidateObjectName(ObjectName);

            // Validate local path
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Local path cannot be null or empty"),
                    "InvalidLocalPath",
                    ErrorCategory.InvalidArgument,
                    LocalPath));
            }

            // Check if file already exists
            if (File.Exists(LocalPath) && !Force.IsPresent)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException($"File already exists: {LocalPath}. Use -Force to overwrite."),
                    "FileAlreadyExists",
                    ErrorCategory.ResourceExists,
                    LocalPath));
            }

            if (ShouldProcess($"{BucketName}/{ObjectName}", "Download object"))
            {
                var result = ExecuteOperation("DownloadObject", () =>
                {
                    WriteVerboseMessage("Downloading object '{0}' from bucket '{1}' to '{2}'", 
                        ObjectName, BucketName, LocalPath);

                    // Create download result
                    var downloadResult = new MinIODownloadResult(BucketName, ObjectName, 0)
                    {
                        TargetFilePath = LocalPath
                    };
                    downloadResult.MarkStarted();

                    try
                    {
                        // Check if bucket exists
                        if (!S3Client.BucketExists(BucketName))
                        {
                            throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                        }

                        // Create directory if needed
                        var directory = Path.GetDirectoryName(LocalPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            if (CreateDirectories.IsPresent)
                            {
                                WriteVerboseMessage("Creating directory: {0}", directory);
                                Directory.CreateDirectory(directory);
                            }
                            else
                            {
                                throw new DirectoryNotFoundException($"Directory does not exist: {directory}. Use -CreateDirectories to create it.");
                            }
                        }

                        // Track progress
                        var startTime = DateTime.UtcNow;

                        // Download the object
                        using (var fileStream = new FileStream(LocalPath, FileMode.Create, FileAccess.Write))
                        {
                            var bytesDownloaded = S3Client.GetObject(
                                BucketName,
                                ObjectName,
                                fileStream,
                                bytesTransferred =>
                                {
                                    // Only update the result object - no PowerShell calls from background thread
                                    downloadResult.BytesTransferred = bytesTransferred;
                                });

                            downloadResult.TotalSize = bytesDownloaded;
                            downloadResult.BytesTransferred = bytesDownloaded;
                        }

                        downloadResult.MarkCompleted();

                        var duration = downloadResult.Duration ?? TimeSpan.Zero;
                        var averageSpeed = downloadResult.AverageSpeed ?? 0;

                        // Report final progress from main thread
                        var percentage = downloadResult.TotalSize > 0 ?
                            (double)downloadResult.BytesTransferred / downloadResult.TotalSize * 100 : 100;
                        WriteVerboseMessage("Download progress: {0:F1}% ({1}/{2}) at {3}",
                            percentage,
                            SizeFormatter.FormatBytes(downloadResult.BytesTransferred),
                            downloadResult.TotalSize > 0 ? SizeFormatter.FormatBytes(downloadResult.TotalSize) : "Unknown",
                            SizeFormatter.FormatSpeed(averageSpeed));

                        WriteVerboseMessage("Download completed: {0} in {1} at average speed of {2}",
                            SizeFormatter.FormatBytes(downloadResult.BytesTransferred),
                            SizeFormatter.FormatDuration(duration),
                            SizeFormatter.FormatSpeed(averageSpeed));

                        // Set file timestamps if available
                        try
                        {
                            var fileInfo = new FileInfo(LocalPath);
                            if (downloadResult.LastModified.HasValue)
                            {
                                fileInfo.LastWriteTimeUtc = downloadResult.LastModified.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteVerboseMessage("Could not set file timestamps: {0}", ex.Message);
                        }

                        return downloadResult;
                    }
                    catch (Exception ex)
                    {
                        downloadResult.MarkFailed(ex.Message);
                        throw;
                    }
                }, $"Bucket: {BucketName}, Object: {ObjectName}, LocalPath: {LocalPath}");

                // Return result if requested
                if (PassThru.IsPresent)
                {
                    WriteObject(result);
                }
                else
                {
                    WriteVerboseMessage("Successfully downloaded object '{0}' from bucket '{1}' to '{2}'", 
                        ObjectName, BucketName, LocalPath);
                }
            }
        }
    }
}
