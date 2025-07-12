using System;
using System.IO;
using System.Management.Automation;
using PSMinIO.Core.Http;
using PSMinIO.Core.Models;
using PSMinIO.Core.S3;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Downloads objects using multipart download with resume capability
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObjectContentMultipart", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MultipartDownloadResult))]
    public class GetMinIOObjectContentMultipartCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket containing the object
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object to download
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Local file path where the object will be saved
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [ValidateNotNull]
        [Alias("File", "Path")]
        public FileInfo DestinationPath { get; set; } = null!;

        /// <summary>
        /// Size of each download chunk in bytes (default: 32MB)
        /// </summary>
        [Parameter]
        [ValidateRange(1024 * 1024, long.MaxValue)] // Minimum 1MB
        public long ChunkSize { get; set; } = 32 * 1024 * 1024; // 32MB default

        /// <summary>
        /// Maximum number of parallel download threads (default: 4)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 8)]
        public int MaxParallelDownloads { get; set; } = 4;

        /// <summary>
        /// Resume download if destination file already exists
        /// </summary>
        [Parameter]
        public SwitchParameter Resume { get; set; }

        /// <summary>
        /// Overwrite existing file without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate destination path
            if (DestinationPath.Exists && !Resume.IsPresent && !Force.IsPresent)
            {
                var errorRecord = new ErrorRecord(
                    new InvalidOperationException($"Destination file already exists: {DestinationPath.FullName}. Use -Resume to continue or -Force to overwrite."),
                    "DestinationFileExists",
                    ErrorCategory.ResourceExists,
                    DestinationPath);
                ThrowTerminatingError(errorRecord);
            }

            // Ensure destination directory exists
            if (DestinationPath.Directory != null && !DestinationPath.Directory.Exists)
            {
                DestinationPath.Directory.Create();
                WriteVerboseMessage("Created destination directory: {0}", DestinationPath.Directory.FullName);
            }

            var operationDescription = Resume.IsPresent && DestinationPath.Exists
                ? $"Resume multipart download of '{ObjectName}' from bucket '{BucketName}'"
                : $"Multipart download '{ObjectName}' from bucket '{BucketName}'";

            if (ShouldProcess(DestinationPath.FullName, operationDescription))
            {
                var result = ExecuteOperation("MultipartDownload", () =>
                {
                    WriteVerboseMessage("Starting multipart download: {0}/{1} -> {2}", 
                        BucketName, ObjectName, DestinationPath.FullName);
                    WriteVerboseMessage("Chunk size: {0}, Max parallel downloads: {1}, Resume: {2}", 
                        SizeFormatter.FormatBytes(ChunkSize), MaxParallelDownloads, Resume.IsPresent);

                    // Get connection and create download manager
                    var connection = Connection;
                    var httpClient = new MinIOHttpClient(connection.Configuration);
                    var progressCollector = new ThreadSafeProgressCollector(this);
                    var downloadManager = new MultipartDownloadManager(httpClient, progressCollector, 
                        MaxParallelDownloads, ChunkSize);

                    // Perform multipart download
                    var downloadResult = downloadManager.DownloadFile(BucketName, ObjectName, DestinationPath, 
                        ChunkSize, Resume.IsPresent);

                    if (downloadResult.IsCompleted)
                    {
                        WriteVerboseMessage("Multipart download completed successfully");
                        WriteVerboseMessage("Downloaded: {0} in {1} at {2}", 
                            SizeFormatter.FormatBytes(downloadResult.DownloadedSize),
                            SizeFormatter.FormatDuration(downloadResult.Duration),
                            SizeFormatter.FormatSpeed(downloadResult.AverageSpeed));
                    }
                    else
                    {
                        WriteWarning($"Multipart download failed: {downloadResult.Error}");
                    }

                    return downloadResult;

                }, $"Bucket: {BucketName}, Object: {ObjectName}, Destination: {DestinationPath.FullName}");

                // Always return the result object
                WriteObject(result);
            }
        }
    }
}
