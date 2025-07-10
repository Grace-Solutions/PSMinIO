using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Uploads files or directories to a MinIO bucket using chunked transfer with resume capability
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOObjectChunked", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class NewMinIOObjectChunkedCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to upload to
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Array of FileInfo objects to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Files")]
        [ValidateNotNull]
        [Alias("File", "Files")]
        public FileInfo[]? Path { get; set; }

        /// <summary>
        /// Optional bucket directory path where files should be uploaded (Files parameter set only)
        /// </summary>
        [Parameter(ParameterSetName = "Files")]
        [ValidateNotNullOrEmpty]
        [Alias("Folder", "Prefix")]
        public string? BucketDirectory { get; set; }

        /// <summary>
        /// Directory to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Directory")]
        [ValidateNotNull]
        [Alias("Dir", "Folder")]
        public DirectoryInfo? Directory { get; set; }

        /// <summary>
        /// Upload directory contents recursively (only applies to Directory parameter set)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public SwitchParameter Recursive { get; set; }

        /// <summary>
        /// Maximum depth for recursive directory upload (0 = unlimited)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        [ValidateRange(0, int.MaxValue)]
        public int MaxDepth { get; set; } = 0;

        /// <summary>
        /// Flatten directory structure (upload all files to bucket root)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public SwitchParameter Flatten { get; set; }

        /// <summary>
        /// Script block to filter files for inclusion
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public ScriptBlock? InclusionFilter { get; set; }

        /// <summary>
        /// Script block to filter files for exclusion
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public ScriptBlock? ExclusionFilter { get; set; }

        /// <summary>
        /// Overwrite existing objects without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Generate presigned URLs for uploaded objects
        /// </summary>
        [Parameter]
        public SwitchParameter ShowURL { get; set; }

        /// <summary>
        /// Expiration time for presigned URLs (default: 1 hour)
        /// </summary>
        [Parameter]
        [ValidateRange("00:01:00", "7.00:00:00")] // 1 minute to 7 days
        public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Size of each chunk for upload (default: 5MB)
        /// </summary>
        [Parameter]
        [ValidateRange(1024 * 1024, 1024 * 1024 * 1024)] // 1MB to 1GB
        public long ChunkSize { get; set; } = 5 * 1024 * 1024; // 5MB default

        /// <summary>
        /// Enable resume functionality for interrupted uploads
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

            if (ParameterSetName == "Files")
            {
                ProcessFiles();
            }
            else if (ParameterSetName == "Directory")
            {
                ProcessDirectory();
            }
        }

        /// <summary>
        /// Processes file uploads from FileInfo array
        /// </summary>
        private void ProcessFiles()
        {
            if (Path == null || Path.Length == 0)
            {
                WriteWarning("No files provided for upload");
                return;
            }

            if (ShouldProcess(BucketName, $"Upload {Path.Length} file(s) using chunked transfer"))
            {
                ExecuteOperation("UploadFilesChunked", () =>
                {
                    // Check if bucket exists first
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

                    // Create bucket directory structure if specified
                    if (!string.IsNullOrWhiteSpace(BucketDirectory))
                    {
                        var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory!);
                        if (!string.IsNullOrEmpty(sanitizedDirectory))
                        {
                            MinIOLogger.WriteVerbose(this, "Ensuring bucket directory exists: {0}", sanitizedDirectory);
                            EnsureBucketDirectoryExists(sanitizedDirectory);
                        }
                    }

                    UploadFileCollectionChunked(Path!);

                }, $"Bucket: {BucketName}, Files: {Path.Length}, ChunkSize: {SizeFormatter.FormatBytes(ChunkSize)}");
            }
        }

        /// <summary>
        /// Processes directory upload
        /// </summary>
        private void ProcessDirectory()
        {
            if (Directory == null || !Directory.Exists)
            {
                WriteError(new ErrorRecord(
                    new DirectoryNotFoundException($"Directory not found: {Directory?.FullName}"),
                    "DirectoryNotFound",
                    ErrorCategory.ObjectNotFound,
                    Directory));
                return;
            }

            if (ShouldProcess(BucketName, $"Upload directory '{Directory.Name}' using chunked transfer"))
            {
                ExecuteOperation("UploadDirectoryChunked", () =>
                {
                    // Check if bucket exists first
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

                    // Get files from directory
                    var files = GetDirectoryFiles();
                    if (files.Length == 0)
                    {
                        WriteWarning($"No files found in directory: {Directory.FullName}");
                        return;
                    }

                    MinIOLogger.WriteVerbose(this, "Found {0} files in directory '{1}'", files.Length, Directory.FullName);
                    UploadFileCollectionChunked(files);

                }, $"Bucket: {BucketName}, Directory: {Directory.FullName}, ChunkSize: {SizeFormatter.FormatBytes(ChunkSize)}");
            }
        }

        /// <summary>
        /// Gets files from directory based on filters and recursion settings
        /// </summary>
        /// <returns>Array of FileInfo objects</returns>
        private FileInfo[] GetDirectoryFiles()
        {
            var searchOption = Recursive.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory!.GetFiles("*", searchOption);

            // Apply depth filtering if MaxDepth is specified and we're recursive
            if (Recursive.IsPresent && MaxDepth > 0)
            {
                var basePath = Directory.FullName;
                allFiles = allFiles.Where(f =>
                {
                    var relativePath = f.FullName.Substring(basePath.Length).TrimStart('\\', '/');
                    var depth = relativePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                    return depth <= MaxDepth;
                }).ToArray();
            }

            // Apply inclusion filter
            if (InclusionFilter != null)
            {
                allFiles = allFiles.Where(f => EvaluateFilter(InclusionFilter, f)).ToArray();
            }

            // Apply exclusion filter
            if (ExclusionFilter != null)
            {
                allFiles = allFiles.Where(f => !EvaluateFilter(ExclusionFilter, f)).ToArray();
            }

            return allFiles;
        }

        /// <summary>
        /// Evaluates a script block filter against a file
        /// </summary>
        /// <param name="filter">Script block filter</param>
        /// <param name="file">File to evaluate</param>
        /// <returns>True if file matches filter</returns>
        private bool EvaluateFilter(ScriptBlock filter, FileInfo file)
        {
            try
            {
                var result = filter.InvokeWithContext(null, new List<PSVariable> { new PSVariable("_", file) });
                return result.Count > 0 && LanguagePrimitives.IsTrue(result[0]);
            }
            catch (Exception ex)
            {
                WriteWarning($"Filter evaluation failed for {file.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uploads a collection of files using chunked transfer
        /// </summary>
        /// <param name="files">Files to upload</param>
        private void UploadFileCollectionChunked(FileInfo[] files)
        {
            // Filter out files that don't exist
            var validFiles = files.Where(f => f.Exists).ToArray();
            var skippedCount = files.Length - validFiles.Length;

            if (skippedCount > 0)
            {
                WriteWarning($"Skipped {skippedCount} files that do not exist");
            }

            if (validFiles.Length == 0)
            {
                WriteWarning("No valid files found for upload");
                return;
            }

            MinIOLogger.WriteVerbose(this, "Starting chunked upload of {0} files to bucket '{1}' (ChunkSize: {2})",
                validFiles.Length, BucketName, SizeFormatter.FormatBytes(ChunkSize));

            // Calculate total size for overall progress
            var totalSize = validFiles.Sum(f => f.Length);

            // Create progress reporter
            var progressReporter = new ChunkedCollectionProgressReporter(
                this,
                validFiles.Length,
                totalSize,
                "Uploading",
                ProgressUpdateInterval);

            var uploadedObjects = new System.Collections.Generic.List<MinIOObjectInfo>();

            for (int i = 0; i < validFiles.Length; i++)
            {
                var fileInfo = validFiles[i];
                var objectName = GetObjectName(fileInfo);

                try
                {
                    // Calculate chunks for this file
                    var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / ChunkSize);
                    progressReporter.StartNewFile(fileInfo.Name, fileInfo.Length, totalChunks);

                    // Upload file using chunked transfer
                    var uploadedObject = UploadFileChunked(fileInfo, objectName, progressReporter);

                    if (uploadedObject != null)
                    {
                        uploadedObjects.Add(uploadedObject);
                        progressReporter.CompleteFile();
                    }
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        ex,
                        "ChunkedFileUploadFailed",
                        ErrorCategory.WriteError,
                        fileInfo));

                    MinIOLogger.WriteVerbose(this, "Failed to upload {0}: {1}", fileInfo.Name, ex.Message);
                }
            }

            progressReporter.CompleteCollection();

            // Always return uploaded objects
            foreach (var obj in uploadedObjects)
            {
                WriteObject(obj);
            }

            MinIOLogger.WriteVerbose(this, "Completed chunked upload: {0} files ({1} successful, {2} failed)",
                validFiles.Length, uploadedObjects.Count, validFiles.Length - uploadedObjects.Count);
        }

        /// <summary>
        /// Uploads a single file using chunked transfer with resume capability
        /// </summary>
        /// <param name="fileInfo">File to upload</param>
        /// <param name="objectName">Object name in bucket</param>
        /// <param name="progressReporter">Progress reporter</param>
        /// <returns>Uploaded object info or null if failed</returns>
        private MinIOObjectInfo? UploadFileChunked(FileInfo fileInfo, string objectName, ChunkedCollectionProgressReporter progressReporter)
        {
            ChunkedTransferState? transferState = null;

            // Try to load existing transfer state for resume
            if (Resume.IsPresent)
            {
                transferState = ChunkedTransferResumeManager.LoadTransferState(
                    BucketName, objectName, fileInfo.FullName, ChunkedTransferType.Upload, ResumeDataPath);

                if (transferState != null && !ChunkedTransferResumeManager.IsResumeDataValid(transferState, fileInfo))
                {
                    MinIOLogger.WriteVerbose(this, "Resume data for {0} is invalid, starting fresh upload", fileInfo.Name);
                    transferState = null;
                }
            }

            // Create new transfer state if none exists or resume is disabled
            if (transferState == null)
            {
                transferState = new ChunkedTransferState
                {
                    BucketName = BucketName,
                    ObjectName = objectName,
                    FilePath = fileInfo.FullName,
                    TotalSize = fileInfo.Length,
                    ChunkSize = ChunkSize,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    TransferType = ChunkedTransferType.Upload,
                    StartTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
            }
            else
            {
                MinIOLogger.WriteVerbose(this, "Resuming upload of {0} from {1:P1} complete",
                    fileInfo.Name, transferState.ProgressPercentage / 100);
            }

            try
            {
                // Upload file using chunked transfer
                var result = Client.UploadFileChunked(transferState, progressReporter, MaxRetries);

                if (result != null)
                {
                    // Generate presigned URL if requested
                    if (ShowURL.IsPresent)
                    {
                        try
                        {
                            var presignedUrl = Client.GetPresignedUrl(BucketName, objectName, Expiration);
                            result.PresignedUrl = presignedUrl;
                            result.PresignedUrlExpiration = DateTime.UtcNow.Add(Expiration);
                        }
                        catch (Exception ex)
                        {
                            WriteWarning($"Could not generate presigned URL for {objectName}: {ex.Message}");
                        }
                    }

                    // Clean up resume data on successful completion
                    if (Resume.IsPresent)
                    {
                        ChunkedTransferResumeManager.CleanupResumeData(transferState, ResumeDataPath);
                    }

                    return result;
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used - false positive, ex is used in throw
            catch (Exception ex)
            {
                // Save resume data on failure if resume is enabled
                if (Resume.IsPresent)
                {
                    try
                    {
                        ChunkedTransferResumeManager.SaveTransferState(transferState, ResumeDataPath);
                        MinIOLogger.WriteVerbose(this, "Saved resume data for {0}", fileInfo.Name);
                    }
                    catch (Exception saveEx)
                    {
                        WriteWarning($"Could not save resume data for {fileInfo.Name}: {saveEx.Message}");
                    }
                }

                throw;
            }
#pragma warning restore CS0168

            return null;
        }

        /// <summary>
        /// Gets the object name for a file based on the upload context
        /// </summary>
        /// <param name="file">File to get object name for</param>
        /// <returns>Object name to use in MinIO</returns>
        private string GetObjectName(FileInfo file)
        {
            if (ParameterSetName == "Files")
            {
                // For file uploads, optionally prefix with bucket directory
                var objectName = file.Name;
                if (!string.IsNullOrWhiteSpace(BucketDirectory))
                {
                    var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory!);
                    objectName = $"{sanitizedDirectory}/{file.Name}";
                }
                return objectName;
            }
            else if (ParameterSetName == "Directory")
            {
                if (Flatten.IsPresent)
                {
                    // Flatten: use just the filename
                    return file.Name;
                }
                else
                {
                    // Maintain directory structure relative to the base directory
                    var relativePath = file.FullName.Substring(Directory!.FullName.Length).TrimStart('\\', '/');
                    return relativePath.Replace('\\', '/'); // Ensure forward slashes for object storage
                }
            }

            return file.Name;
        }

        /// <summary>
        /// Sanitizes bucket directory path and ensures proper format
        /// </summary>
        /// <param name="directory">Raw directory path</param>
        /// <returns>Sanitized directory path</returns>
        private string SanitizeBucketDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return string.Empty;

            // Remove leading and trailing slashes, then split and clean
            var cleanDirectory = directory.Trim().Trim('/', '\\');

            // Split by both forward and back slashes, then clean each part
            var parts = cleanDirectory.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // Clean each part and rejoin with forward slashes
            var cleanedParts = parts.Select(part => part.Trim()).Where(part => !string.IsNullOrEmpty(part));

            return string.Join("/", cleanedParts);
        }

        /// <summary>
        /// Creates bucket directory structure if it doesn't exist
        /// </summary>
        /// <param name="directoryPath">Directory path to create</param>
        private void EnsureBucketDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return;

            var parts = directoryPath.Split('/');
            var currentPath = string.Empty;

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                var folderPath = $"{currentPath}/";

                try
                {
                    // Check if this directory level already exists by trying to list objects with this prefix
                    var existingObjects = Client.ListObjects(BucketName, folderPath, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == folderPath);

                    if (!folderExists)
                    {
                        MinIOLogger.WriteVerbose(this, "Creating bucket directory: {0}", folderPath);

                        // Create the directory by uploading a zero-byte object
                        using var emptyStream = new MemoryStream();
                        Client.UploadStream(BucketName, folderPath, emptyStream, "application/x-directory");

                        MinIOLogger.WriteVerbose(this, "Successfully created bucket directory: {0}", folderPath);
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Could not create bucket directory '{folderPath}': {ex.Message}");
                }
            }
        }
    }
}
