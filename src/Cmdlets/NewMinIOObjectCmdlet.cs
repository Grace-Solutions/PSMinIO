using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Uploads files or directories to a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOObject", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MinIOUploadResult))]
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
        /// Array of FileInfo objects to upload (supports single or multiple files)
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Files")]
        [ValidateNotNull]
        [Alias("File", "Files")]
        public FileInfo[]? Path { get; set; }

        /// <summary>
        /// Directory to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Directory")]
        [ValidateNotNull]
        [Alias("Dir")]
        public DirectoryInfo? Directory { get; set; }

        /// <summary>
        /// Optional bucket directory path where files should be uploaded (available for both Files and Directory parameter sets)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        [Alias("Prefix", "Folder")]
        public string? BucketDirectory { get; set; }

        /// <summary>
        /// Upload directory contents recursively (Directory parameter set only)
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
        /// Content type of the file (auto-detected if not specified)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ContentType { get; set; }

        /// <summary>
        /// Custom metadata for the object
        /// </summary>
        [Parameter]
        public Hashtable? Metadata { get; set; }

        /// <summary>
        /// Overwrite existing objects without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Return the upload result information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(BucketName);

            switch (ParameterSetName)
            {
                case "Files":
                    ProcessFiles();
                    break;
                case "Directory":
                    ProcessDirectory();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown parameter set: {ParameterSetName}");
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
                    if (!S3Client.BucketExists(BucketName))
                    {
                        throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                    }

                    // Create bucket directory structure if specified
                    if (!string.IsNullOrWhiteSpace(BucketDirectory))
                    {
                        var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory!);
                        if (!string.IsNullOrEmpty(sanitizedDirectory))
                        {
                            WriteVerboseMessage("Ensuring bucket directory exists: {0}", sanitizedDirectory);
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
                var errorRecord = new ErrorRecord(
                    new DirectoryNotFoundException($"Directory not found: {Directory?.FullName}"),
                    "DirectoryNotFound",
                    ErrorCategory.ObjectNotFound,
                    Directory);
                ThrowTerminatingError(errorRecord);
                return;
            }

            if (ShouldProcess(BucketName, $"Upload directory '{Directory.Name}'"))
            {
                ExecuteOperation("UploadDirectory", () =>
                {
                    // Check if bucket exists first
                    if (!S3Client.BucketExists(BucketName))
                    {
                        throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                    }

                    // Create bucket directory structure if specified
                    if (!string.IsNullOrWhiteSpace(BucketDirectory))
                    {
                        var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory!);
                        if (!string.IsNullOrEmpty(sanitizedDirectory))
                        {
                            WriteVerboseMessage("Ensuring bucket directory exists: {0}", sanitizedDirectory);
                            EnsureBucketDirectoryExists(sanitizedDirectory);
                        }
                    }

                    // Get files from directory
                    var files = GetDirectoryFiles();
                    if (files.Length == 0)
                    {
                        WriteWarning($"No files found in directory: {Directory.FullName}");
                        return;
                    }

                    WriteVerboseMessage("Found {0} files in directory '{1}'", files.Length, Directory.FullName);
                    UploadFileCollection(files);
                }, $"Bucket: {BucketName}, Directory: {Directory.FullName}");
            }
        }

        /// <summary>
        /// Gets the content type for a file based on its extension
        /// </summary>
        /// <param name="extension">File extension</param>
        /// <returns>Content type string</returns>
        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".csv" => "text/csv",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
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

            WriteVerboseMessage("Uploading {0} files to bucket '{1}'", validFiles.Length, BucketName);

            // Calculate total size for overall progress
            var totalSize = validFiles.Sum(f => f.Length);
            var totalProcessed = 0L;

            var uploadedObjects = new List<MinIOUploadResult>();

            for (int i = 0; i < validFiles.Length; i++)
            {
                var fileInfo = validFiles[i];
                var objectName = GetObjectName(fileInfo);

                try
                {
                    WriteVerboseMessage("Uploading file {0}/{1}: {2} -> {3}",
                        i + 1, validFiles.Length, fileInfo.Name, objectName);

                    var uploadResult = UploadSingleFile(fileInfo, objectName);
                    if (uploadResult != null)
                    {
                        uploadedObjects.Add(uploadResult);
                        totalProcessed += fileInfo.Length;
                    }

                    WriteVerboseMessage("Successfully uploaded: {0}", fileInfo.Name);
                }
                catch (Exception ex)
                {
                    var errorRecord = new ErrorRecord(ex, "FileUploadFailed", ErrorCategory.WriteError, fileInfo);
                    WriteError(errorRecord);
                    WriteVerboseMessage("Failed to upload {0}: {1}", fileInfo.Name, ex.Message);
                }
            }

            // Always return uploaded objects if PassThru is specified
            if (PassThru.IsPresent)
            {
                foreach (var obj in uploadedObjects)
                {
                    WriteObject(obj);
                }
            }

            WriteVerboseMessage("Completed uploading {0} files ({1} successful, {2} failed)",
                validFiles.Length, uploadedObjects.Count, validFiles.Length - uploadedObjects.Count);
        }

        /// <summary>
        /// Gets the object name for a file based on the upload context
        /// </summary>
        /// <param name="file">File to get object name for</param>
        /// <returns>Object name to use in MinIO</returns>
        private string GetObjectName(FileInfo file)
        {
            string objectName;

            if (ParameterSetName == "Files")
            {
                // For file uploads, use just the filename
                objectName = file.Name;
            }
            else if (ParameterSetName == "Directory")
            {
                if (Flatten.IsPresent)
                {
                    // Flatten: use just the filename
                    objectName = file.Name;
                }
                else
                {
                    // Maintain directory structure relative to the base directory
                    var relativePath = file.FullName.Substring(Directory!.FullName.Length).TrimStart('\\', '/');
                    objectName = relativePath.Replace('\\', '/'); // Ensure forward slashes for object storage
                }
            }
            else
            {
                objectName = file.Name;
            }

            // Apply bucket directory prefix if specified (for both parameter sets)
            if (!string.IsNullOrWhiteSpace(BucketDirectory))
            {
                var sanitizedDirectory = SanitizeBucketDirectory(BucketDirectory!);
                if (!string.IsNullOrEmpty(sanitizedDirectory))
                {
                    objectName = $"{sanitizedDirectory}/{objectName}";
                }
            }

            return objectName;
        }

        /// <summary>
        /// Sanitizes bucket directory path and ensures proper format
        /// Handles paths like "/Folder1/Folder2/Folder3" or "Folder1\Folder2\Folder3"
        /// </summary>
        /// <param name="directory">Raw directory path</param>
        /// <returns>Sanitized directory path with forward slashes</returns>
        private string SanitizeBucketDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return string.Empty;

            // Remove leading and trailing whitespace and slashes
            var cleanDirectory = directory.Trim().Trim('/', '\\');

            if (string.IsNullOrEmpty(cleanDirectory))
                return string.Empty;

            // Split by both forward and back slashes, then clean each part
            var parts = cleanDirectory.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // Clean each part: trim whitespace, remove invalid characters, ensure not empty
            var cleanedParts = parts
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrEmpty(part))
                .Select(part => part.Replace("\\", "").Replace("/", "")) // Remove any remaining slashes
                .Where(part => !string.IsNullOrEmpty(part)); // Filter again after cleaning

            // Join with forward slashes (S3/MinIO standard)
            var result = string.Join("/", cleanedParts);

            WriteVerboseMessage("Sanitized bucket directory: '{0}' -> '{1}'", directory, result);
            return result;
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
                    var existingObjects = S3Client.ListObjects(BucketName, folderPath, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == folderPath);

                    if (!folderExists)
                    {
                        WriteVerboseMessage("Creating bucket directory: {0}", folderPath);
                        try
                        {
                            // Create the directory by uploading an empty object with trailing slash
                            using (var emptyStream = new MemoryStream())
                            {
                                S3Client.PutObject(BucketName, folderPath, emptyStream, "application/x-directory", null, null);
                            }
                            WriteVerboseMessage("Successfully created bucket directory: {0}", folderPath);
                        }
                        catch (Exception createEx)
                        {
                            // Directory creation failed, but this is not critical since MinIO creates directories implicitly
                            WriteVerboseMessage("Directory creation failed (non-critical): {0} - {1}", folderPath, createEx.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Listing failed, but this is not critical for the upload operation
                    WriteVerboseMessage("Could not check directory existence (non-critical): {0} - {1}", folderPath, ex.Message);
                }
            }
        }

        /// <summary>
        /// Uploads a single file and returns the result
        /// </summary>
        /// <param name="fileInfo">File to upload</param>
        /// <param name="objectName">Object name in bucket</param>
        /// <returns>Upload result</returns>
        private MinIOUploadResult? UploadSingleFile(FileInfo fileInfo, string objectName)
        {
            // Create upload result
            var uploadResult = new MinIOUploadResult(BucketName, objectName, string.Empty, fileInfo.Length)
            {
                SourceFilePath = fileInfo.FullName
            };
            uploadResult.MarkStarted();

            try
            {
                // Determine content type
                var fileContentType = ContentType ?? GetContentType(fileInfo.Extension);
                uploadResult.ContentType = fileContentType;

                // Convert metadata
                Dictionary<string, string>? metadata = null;
                if (Metadata != null && Metadata.Count > 0)
                {
                    metadata = new Dictionary<string, string>();
                    foreach (var key in Metadata.Keys)
                    {
                        if (key != null && Metadata[key] != null)
                        {
                            metadata[key.ToString()!] = Metadata[key]!.ToString()!;
                        }
                    }
                }

                // Upload the file
                string etag;
                using (var fileStream = fileInfo.OpenRead())
                {
                    etag = S3Client.PutObject(
                        BucketName,
                        objectName,
                        fileStream,
                        fileContentType,
                        metadata,
                        bytesTransferred =>
                        {
                            // Only update the result object - no PowerShell calls from background thread
                            uploadResult.BytesTransferred = bytesTransferred;
                        });
                }

                uploadResult.ETag = etag;
                uploadResult.MarkCompleted();

                var duration = uploadResult.Duration ?? TimeSpan.Zero;
                var averageSpeed = uploadResult.AverageSpeed ?? 0;

                // Report final progress from main thread
                var percentage = fileInfo.Length > 0 ? (double)uploadResult.BytesTransferred / fileInfo.Length * 100 : 100;
                WriteVerboseMessage("Upload progress: {0:F1}% ({1}/{2}) at {3}",
                    percentage,
                    SizeFormatter.FormatBytes(uploadResult.BytesTransferred),
                    SizeFormatter.FormatBytes(fileInfo.Length),
                    SizeFormatter.FormatSpeed(averageSpeed));

                return uploadResult;
            }
            catch (Exception ex)
            {
                uploadResult.MarkFailed(ex.Message);
                throw;
            }
        }
    }
}
