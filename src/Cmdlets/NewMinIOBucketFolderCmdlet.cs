using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Creates bucket folder structure with support for multi-level paths
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOBucketFolder", SupportsShouldProcess = true)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class NewMinIOBucketFolderCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket where the folder should be created
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Folder path to create (supports multiple formats: "/Folder", "Folder1/Folder2/Folder3", "Folder", "\Folder\Folder1\Folder3")
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Path", "Directory", "Prefix")]
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Create parent folders if they don't exist (recursive creation)
        /// </summary>
        [Parameter]
        public SwitchParameter Recursive { get; set; } = true;

        /// <summary>
        /// Overwrite existing folder without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(BucketName);

            // Sanitize the folder path
            var sanitizedPath = SanitizeBucketDirectory(FolderPath);
            if (string.IsNullOrEmpty(sanitizedPath))
            {
                var errorRecord = new ErrorRecord(
                    new ArgumentException("Invalid folder path after sanitization"),
                    "InvalidFolderPath",
                    ErrorCategory.InvalidArgument,
                    FolderPath);
                ThrowTerminatingError(errorRecord);
            }

            var folderObjectName = $"{sanitizedPath}/";

            if (ShouldProcess(folderObjectName, $"Create folder in bucket '{BucketName}'"))
            {
                var result = ExecuteOperation("CreateBucketFolder", () =>
                {
                    // Check if bucket exists first
                    if (!S3Client.BucketExists(BucketName))
                    {
                        throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                    }

                    WriteVerboseMessage("Creating bucket folder: {0} in bucket: {1}", sanitizedPath, BucketName);

                    // Check if folder already exists
                    var existingObjects = S3Client.ListObjects(BucketName, folderObjectName, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == folderObjectName);

                    if (folderExists && !Force.IsPresent)
                    {
                        throw new InvalidOperationException($"Folder '{sanitizedPath}' already exists in bucket '{BucketName}'. Use -Force to overwrite.");
                    }

                    // Create folder structure recursively if requested
                    if (Recursive.IsPresent)
                    {
                        EnsureBucketDirectoryExists(sanitizedPath);
                    }
                    else
                    {
                        // Create only the final folder
                        CreateSingleFolder(folderObjectName);
                    }

                    // Return the created folder as MinIOObjectInfo
                    var folderInfo = new MinIOObjectInfo
                    {
                        Name = folderObjectName,
                        BucketName = BucketName,
                        Size = 0,
                        LastModified = DateTime.UtcNow,
                        ContentType = "application/x-directory",
                        ETag = "",
                        StartTime = DateTime.UtcNow,
                        CompletionTime = DateTime.UtcNow
                    };

                    WriteVerboseMessage("Successfully created bucket folder: {0}", sanitizedPath);
                    return folderInfo;

                }, $"Bucket: {BucketName}, Folder: {sanitizedPath}");

                WriteObject(result);
            }
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
        /// Creates bucket directory structure recursively if it doesn't exist
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
                    // Check if this directory level already exists
                    var existingObjects = S3Client.ListObjects(BucketName, folderPath, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == folderPath);

                    if (!folderExists)
                    {
                        CreateSingleFolder(folderPath);
                    }
                }
                catch (Exception ex)
                {
                    WriteVerboseMessage("Could not check directory existence: {0} - {1}", folderPath, ex.Message);
                    // Try to create anyway
                    CreateSingleFolder(folderPath);
                }
            }
        }

        /// <summary>
        /// Creates a single folder by uploading an empty object with trailing slash
        /// </summary>
        /// <param name="folderPath">Folder path with trailing slash</param>
        private void CreateSingleFolder(string folderPath)
        {
            try
            {
                WriteVerboseMessage("Creating bucket directory: {0}", folderPath);
                using (var emptyStream = new MemoryStream())
                {
                    S3Client.PutObject(BucketName, folderPath, emptyStream, "application/x-directory", null, null);
                }
                WriteVerboseMessage("Successfully created bucket directory: {0}", folderPath);
            }
            catch (Exception createEx)
            {
                WriteVerboseMessage("Directory creation failed: {0} - {1}", folderPath, createEx.Message);
                throw new InvalidOperationException($"Failed to create folder '{folderPath}': {createEx.Message}", createEx);
            }
        }
    }
}
