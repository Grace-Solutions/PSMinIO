using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Creates a folder (prefix) in a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class NewMinIOFolderCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to create the folder in
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the folder to create (supports nested paths like "folder1/folder2/folder3")
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Folder", "Prefix", "Path")]
        public string FolderName { get; set; } = string.Empty;



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketName(BucketName);

            // Sanitize the folder name
            var sanitizedFolderName = SanitizeFolderPath(FolderName);
            if (string.IsNullOrEmpty(sanitizedFolderName))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("Folder name cannot be empty after sanitization"),
                    "InvalidFolderName",
                    ErrorCategory.InvalidArgument,
                    FolderName));
                return;
            }

            if (ShouldProcess($"{BucketName}/{sanitizedFolderName}", "Create folder structure"))
            {
                ExecuteOperation("CreateFolder", () =>
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

                    MinIOLogger.WriteVerbose(this,
                        "Creating folder structure '{0}' in bucket '{1}'", sanitizedFolderName, BucketName);

                    // Create the complete folder structure
                    var createdFolders = CreateFolderStructure(sanitizedFolderName);

                    MinIOLogger.WriteVerbose(this,
                        "Successfully created {0} folder level(s) in bucket '{1}'", createdFolders.Count, BucketName);

                    // Return information about all created folders
                    foreach (var folder in createdFolders)
                    {
                        WriteObject(folder);
                    }

                }, $"Bucket: {BucketName}, Folder: {sanitizedFolderName}");
            }
        }

        /// <summary>
        /// Sanitizes folder path and ensures proper format
        /// </summary>
        /// <param name="folderPath">Raw folder path</param>
        /// <returns>Sanitized folder path</returns>
        private string SanitizeFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return string.Empty;

            // Remove leading and trailing slashes, then split and clean
            var cleanPath = folderPath.Trim().Trim('/', '\\');

            // Split by both forward and back slashes, then clean each part
            var parts = cleanPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // Clean each part and validate
            var cleanedParts = new System.Collections.Generic.List<string>();
            var invalidChars = new char[] { '<', '>', ':', '"', '|', '?', '*' };

            foreach (var part in parts)
            {
                var cleanPart = part.Trim();
                if (string.IsNullOrEmpty(cleanPart))
                    continue;

                // Check for invalid characters
                if (cleanPart.IndexOfAny(invalidChars) >= 0)
                {
                    WriteWarning($"Folder part '{cleanPart}' contains invalid characters and will be skipped");
                    continue;
                }

                cleanedParts.Add(cleanPart);
            }

            return string.Join("/", cleanedParts);
        }

        /// <summary>
        /// Creates the complete folder structure, including all intermediate levels
        /// </summary>
        /// <param name="folderPath">Complete folder path to create</param>
        /// <returns>List of created folder objects</returns>
        private System.Collections.Generic.List<MinIOObjectInfo> CreateFolderStructure(string folderPath)
        {
            var createdFolders = new System.Collections.Generic.List<MinIOObjectInfo>();
            var parts = folderPath.Split('/');
            var currentPath = string.Empty;

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                var fullFolderPath = $"{currentPath}/";

                try
                {
                    // Check if this folder level already exists
                    var existingObjects = Client.ListObjects(BucketName, fullFolderPath, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == fullFolderPath);

                    if (!folderExists)
                    {
                        MinIOLogger.WriteVerbose(this, "Creating folder level: {0}", fullFolderPath);

                        // Create the folder using the dedicated method
                        var etag = Client.CreateDirectory(BucketName, fullFolderPath);

                        var folderInfo = new MinIOObjectInfo(
                            fullFolderPath,
                            0,
                            DateTime.UtcNow,
                            etag,
                            BucketName)
                        {
                            ContentType = "application/x-directory"
                        };

                        createdFolders.Add(folderInfo);
                        MinIOLogger.WriteVerbose(this, "Successfully created folder level: {0}", fullFolderPath);
                    }
                    else
                    {
                        MinIOLogger.WriteVerbose(this, "Folder level already exists: {0}", fullFolderPath);
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Could not create folder level '{fullFolderPath}': {ex.Message}");
                }
            }

            return createdFolders;
        }
    }
}
