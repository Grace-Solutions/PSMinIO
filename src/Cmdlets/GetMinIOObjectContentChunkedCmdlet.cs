using System;
using System.IO;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Downloads objects from a MinIO bucket using chunked transfer with resume capability
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObjectContentChunked", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(FileInfo))]
    public class GetMinIOObjectContentChunkedCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to download from
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object to download
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// FileInfo object representing where the file should be saved
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [ValidateNotNull]
        [Alias("File", "Path")]
        public FileInfo FilePath { get; set; } = null!;

        /// <summary>
        /// Overwrite existing files without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Size of each chunk for download (default: 10MB)
        /// </summary>
        [Parameter]
        [ValidateRange(1024 * 1024, 100 * 1024 * 1024)] // 1MB to 100MB
        public long ChunkSize { get; set; } = 10 * 1024 * 1024; // 10MB default

        /// <summary>
        /// Enable resume functionality for interrupted downloads
        /// </summary>
        [Parameter]
        public SwitchParameter Resume { get; set; }

        /// <summary>
        /// Maximum number of retry attempts for failed chunks
        /// </summary>
        [Parameter]
        [ValidateRange(1, 10)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Number of parallel chunk downloads (default: 3)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 10)]
        public int ParallelDownloads { get; set; } = 3;

        /// <summary>
        /// Custom path for storing resume data (default: %LOCALAPPDATA%\PSMinIO\Resume)
        /// </summary>
        [Parameter]
        public string? ResumeDataPath { get; set; }



        /// <summary>
        /// Update progress every N bytes transferred (default: 1MB)
        /// </summary>
        [Parameter]
        [ValidateRange(1024, 10 * 1024 * 1024)] // 1KB to 10MB
        public long ProgressUpdateInterval { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketName(BucketName);
            ValidateObjectName(ObjectName);
            ValidateAndPrepareFilePath();

            if (ShouldProcess($"{BucketName}/{ObjectName}", $"Download using chunked transfer to '{FilePath.FullName}'"))
            {
                ExecuteOperation("DownloadObjectChunked", () =>
                {
                    // Check if bucket exists
                    var bucketExists = Client.BucketExists(BucketName);
                    if (!bucketExists)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Bucket '{BucketName}' does not exist"),
                            "BucketNotFound",
                            ErrorCategory.ObjectNotFound,
                            BucketName));
                        return;
                    }

                    // Get object information
                    var objectInfo = GetObjectInfo();
                    if (objectInfo == null)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Object '{ObjectName}' not found in bucket '{BucketName}'"),
                            "ObjectNotFound",
                            ErrorCategory.ObjectNotFound,
                            ObjectName));
                        return;
                    }

                    MinIOLogger.WriteVerbose(this, 
                        "Starting chunked download of object '{0}' from bucket '{1}' (Size: {2}, ChunkSize: {3})", 
                        ObjectName, BucketName, SizeFormatter.FormatSize(objectInfo.Size), SizeFormatter.FormatSize(ChunkSize));

                    // Download using chunked transfer
                    var downloadedFile = DownloadObjectChunked(objectInfo);
                    
                    if (downloadedFile != null)
                    {
                        MinIOLogger.WriteVerbose(this, 
                            "Successfully downloaded object '{0}' from bucket '{1}' to '{2}'", 
                            ObjectName, BucketName, FilePath.FullName);

                        // Always return file information
                        FilePath.Refresh(); // Refresh to get updated file info
                        WriteObject(FilePath);
                    }

                }, $"Bucket: {BucketName}, Object: {ObjectName}, File: {FilePath.FullName}, ChunkSize: {SizeFormatter.FormatSize(ChunkSize)}");
            }
        }

        /// <summary>
        /// Gets information about the object to download
        /// </summary>
        /// <returns>Object information or null if not found</returns>
        private MinIOObjectInfo? GetObjectInfo()
        {
            try
            {
                var objects = Client.ListObjects(BucketName, ObjectName, false);
                return objects.Find(obj => string.Equals(obj.Name, ObjectName, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                WriteWarning($"Could not get object information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads an object using chunked transfer with resume capability
        /// </summary>
        /// <param name="objectInfo">Information about the object to download</param>
        /// <returns>Downloaded file info or null if failed</returns>
        private FileInfo? DownloadObjectChunked(MinIOObjectInfo objectInfo)
        {
            ChunkedTransferState? transferState = null;

            // Try to load existing transfer state for resume
            if (Resume.IsPresent)
            {
                transferState = ChunkedTransferResumeManager.LoadTransferState(
                    BucketName, ObjectName, FilePath.FullName, ChunkedTransferType.Download, ResumeDataPath);

                if (transferState != null && !ChunkedTransferResumeManager.IsResumeDataValid(transferState))
                {
                    MinIOLogger.WriteVerbose(this, "Resume data for {0} is invalid, starting fresh download", ObjectName);
                    transferState = null;
                }
            }

            // Create new transfer state if none exists or resume is disabled
            if (transferState == null)
            {
                transferState = new ChunkedTransferState
                {
                    BucketName = BucketName,
                    ObjectName = ObjectName,
                    FilePath = FilePath.FullName,
                    TotalSize = objectInfo.Size,
                    ChunkSize = ChunkSize,
                    ETag = objectInfo.ETag,
                    TransferType = ChunkedTransferType.Download,
                    StartTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
            }
            else
            {
                MinIOLogger.WriteVerbose(this, "Resuming download of {0} from {1:P1} complete", 
                    ObjectName, transferState.ProgressPercentage / 100);
            }

            // Calculate total chunks for progress reporting
            var totalChunks = (int)Math.Ceiling((double)objectInfo.Size / ChunkSize);

            // Create progress reporter (single file, so no collection progress)
            var progressReporter = new ChunkedSingleFileProgressReporter(
                this,
                objectInfo.Size,
                totalChunks,
                "Downloading",
                ProgressUpdateInterval);

            try
            {
                // Download file using chunked transfer
                var result = Client.DownloadFileChunked(transferState, progressReporter, MaxRetries, ParallelDownloads);
                
                if (result)
                {
                    // Clean up resume data on successful completion
                    if (Resume.IsPresent)
                    {
                        ChunkedTransferResumeManager.CleanupResumeData(transferState, ResumeDataPath);
                    }

                    progressReporter.CompleteDownload();
                    return FilePath;
                }
            }
            catch (Exception ex)
            {
                // Save resume data on failure if resume is enabled
                if (Resume.IsPresent)
                {
                    try
                    {
                        ChunkedTransferResumeManager.SaveTransferState(transferState, ResumeDataPath);
                        MinIOLogger.WriteVerbose(this, "Saved resume data for {0}", ObjectName);
                    }
                    catch (Exception saveEx)
                    {
                        WriteWarning($"Could not save resume data for {ObjectName}: {saveEx.Message}");
                    }
                }

                throw;
            }

            return null;
        }

        /// <summary>
        /// Validates and prepares the file path for download
        /// </summary>
        private void ValidateAndPrepareFilePath()
        {
            if (FilePath == null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("FilePath cannot be null"),
                    "InvalidFilePath",
                    ErrorCategory.InvalidArgument,
                    FilePath));
            }

            // Check if file already exists
            if (FilePath.Exists && !Force.IsPresent)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"File '{FilePath.FullName}' already exists. Use -Force to overwrite."),
                    "FileAlreadyExists",
                    ErrorCategory.ResourceExists,
                    FilePath));
                return;
            }

            // Ensure the directory exists
            var directory = FilePath.Directory;
            if (directory != null && !directory.Exists)
            {
                try
                {
                    directory.Create();
                    MinIOLogger.WriteVerbose(this, "Created directory: {0}", directory.FullName);
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException($"Cannot create directory '{directory.FullName}': {ex.Message}", ex),
                        "DirectoryCreationFailed",
                        ErrorCategory.WriteError,
                        directory));
                }
            }

            // Check if we can write to the target location
            try
            {
                var testFile = Path.Combine(FilePath.DirectoryName ?? ".", $".psminiotest_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException($"Cannot write to location '{FilePath.FullName}': {ex.Message}", ex),
                    "LocationNotWritable",
                    ErrorCategory.PermissionDenied,
                    FilePath));
            }
        }
    }
}
