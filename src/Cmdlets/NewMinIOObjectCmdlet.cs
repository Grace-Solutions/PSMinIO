using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Uploads files or directories to a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOObject", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class NewMinIOObjectCmdlet : MinIOBaseCmdlet
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

            if (ShouldProcess(BucketName, $"Upload {Path.Length} file(s)"))
            {
                ExecuteOperation("UploadFiles", () =>
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
                        var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory);
                        if (!string.IsNullOrEmpty(sanitizedDirectory))
                        {
                            MinIOLogger.WriteVerbose(this, "Ensuring bucket directory exists: {0}", sanitizedDirectory);
                            EnsureBucketDirectoryExists(sanitizedDirectory);
                        }
                    }

                    UploadFileCollection(Path!);

                }, $"Bucket: {BucketName}, Files: {Path.Length}");
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

            if (ShouldProcess(BucketName, $"Upload directory '{Directory.Name}'"))
            {
                ExecuteOperation("UploadDirectory", () =>
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
                    UploadFileCollection(files);

                }, $"Bucket: {BucketName}, Directory: {Directory.FullName}");
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
                    var relativePath = Path.GetRelativePath(basePath, f.FullName);
                    var depth = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1;
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
                var result = filter.InvokeWithContext(null, new[] { new PSVariable("_", file) });
                return result.Count > 0 && LanguagePrimitives.IsTrue(result[0]);
            }
            catch (Exception ex)
            {
                WriteWarning($"Filter evaluation failed for {file.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uploads a collection of files
        /// </summary>
        /// <param name="files">Files to upload</param>
        private void UploadFileCollection(FileInfo[] files)
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

            MinIOLogger.WriteVerbose(this, "Uploading {0} files to bucket '{1}'", validFiles.Length, BucketName);

            // Calculate total size for overall progress
            var totalSize = validFiles.Sum(f => f.Length);
            var totalProcessed = 0L;

            // Create overall progress reporter
            var overallProgress = new ProgressReporter(
                this,
                "Uploading Files",
                $"Uploading {validFiles.Length} files",
                totalSize,
                1);

            var uploadedObjects = new System.Collections.Generic.List<MinIOObjectInfo>();

            for (int i = 0; i < validFiles.Length; i++)
            {
                var fileInfo = validFiles[i];
                var objectName = GetObjectName(fileInfo);

                try
                {
                    // Show file-level progress
                    var fileProgressRecord = new ProgressRecord(2, "Current File",
                        $"Uploading: {fileInfo.Name}")
                    {
                        PercentComplete = (int)((double)(i + 1) / validFiles.Length * 100),
                        ParentActivityId = 1
                    };
                    WriteProgress(fileProgressRecord);

                    MinIOLogger.WriteVerbose(this, "Uploading file {0}/{1}: {2} -> {3}",
                        i + 1, validFiles.Length, fileInfo.Name, objectName);

                    // Upload the file
                    var etag = Client.UploadFile(
                        BucketName,
                        objectName,
                        fileInfo.FullName,
                        null, // Auto-detect content type
                        bytesTransferred =>
                        {
                            overallProgress.UpdateProgress(totalProcessed + bytesTransferred);
                        });

                    totalProcessed += fileInfo.Length;
                    overallProgress.UpdateProgress(totalProcessed);

                    // Create object info for result
                    var uploadedObject = new MinIOObjectInfo(
                        objectName,
                        fileInfo.Length,
                        DateTime.UtcNow,
                        etag,
                        BucketName);

                    // Generate presigned URL if requested
                    if (ShowURL.IsPresent)
                    {
                        try
                        {
                            var presignedUrl = Client.GetPresignedUrl(BucketName, objectName, Expiration);
                            uploadedObject.PresignedUrl = presignedUrl;
                            uploadedObject.PresignedUrlExpiration = DateTime.UtcNow.Add(Expiration);
                        }
                        catch (Exception ex)
                        {
                            WriteWarning($"Could not generate presigned URL for {objectName}: {ex.Message}");
                        }
                    }

                    uploadedObjects.Add(uploadedObject);
                    MinIOLogger.WriteVerbose(this, "Successfully uploaded: {0}", fileInfo.Name);
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(
                        ex,
                        "FileUploadFailed",
                        ErrorCategory.WriteError,
                        fileInfo));

                    MinIOLogger.WriteVerbose(this, "Failed to upload {0}: {1}", fileInfo.Name, ex.Message);
                }
            }

            // Complete progress reporting
            overallProgress.Complete();
            WriteProgress(new ProgressRecord(2, "Current File", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed,
                ParentActivityId = 1
            });

            // Always return uploaded objects
            foreach (var obj in uploadedObjects)
            {
                WriteObject(obj);
            }

            MinIOLogger.WriteVerbose(this, "Completed uploading {0} files ({1} successful, {2} failed)",
                validFiles.Length, uploadedObjects.Count, validFiles.Length - uploadedObjects.Count);
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
                    var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory);
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
                    var relativePath = Path.GetRelativePath(Directory!.FullName, file.FullName);
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
