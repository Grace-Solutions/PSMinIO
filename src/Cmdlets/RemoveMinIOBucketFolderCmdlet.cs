using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Removes bucket folder structure with support for recursive deletion
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "MinIOBucketFolder", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class RemoveMinIOBucketFolderCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket containing the folder to remove
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Folder path to remove (supports multiple formats: "/Folder", "Folder1/Folder2/Folder3", "Folder", "\Folder\Folder1\Folder3")
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Path", "Directory", "Prefix")]
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Remove folder and all its contents recursively
        /// </summary>
        [Parameter]
        public SwitchParameter Recursive { get; set; }

        /// <summary>
        /// Remove folder without prompting for confirmation
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

            if (ShouldProcess(folderObjectName, $"Remove folder from bucket '{BucketName}'"))
            {
                var result = ExecuteOperation("RemoveBucketFolder", () =>
                {
                    // Check if bucket exists first
                    if (!S3Client.BucketExists(BucketName))
                    {
                        throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                    }

                    WriteVerboseMessage("Removing bucket folder: {0} from bucket: {1}", sanitizedPath, BucketName);

                    // Check if folder exists
                    var existingObjects = S3Client.ListObjects(BucketName, folderObjectName, false);
                    var folderExists = existingObjects.Any(obj => obj.Name == folderObjectName);

                    if (!folderExists)
                    {
                        WriteVerboseMessage("Folder '{0}' does not exist in bucket '{1}'", sanitizedPath, BucketName);
                        return null;
                    }

                    // Get folder info before deletion
                    var folderInfo = new MinIOObjectInfo
                    {
                        Name = folderObjectName,
                        BucketName = BucketName,
                        Size = 0,
                        LastModified = DateTime.UtcNow,
                        ContentType = "application/x-directory",
                        ETag = "",
                        StartTime = DateTime.UtcNow
                    };

                    if (Recursive.IsPresent)
                    {
                        // Remove folder and all contents recursively
                        RemoveFolderRecursively(sanitizedPath);
                    }
                    else
                    {
                        // Check if folder has contents
                        var folderContents = S3Client.ListObjects(BucketName, sanitizedPath + "/", true);
                        var hasContents = folderContents.Any(obj => obj.Name != folderObjectName);

                        if (hasContents && !Force.IsPresent)
                        {
                            throw new InvalidOperationException($"Folder '{sanitizedPath}' is not empty. Use -Recursive to remove contents or -Force to ignore this check.");
                        }

                        // Remove only the folder marker
                        RemoveSingleFolder(folderObjectName);
                    }

                    folderInfo.CompletionTime = DateTime.UtcNow;
                    WriteVerboseMessage("Successfully removed bucket folder: {0}", sanitizedPath);
                    return folderInfo;

                }, $"Bucket: {BucketName}, Folder: {sanitizedPath}");

                if (result != null)
                {
                    WriteObject(result);
                }
            }
        }

        /// <summary>
        /// Sanitizes bucket directory path and ensures proper format
        /// </summary>
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
        /// Removes folder and all its contents recursively
        /// </summary>
        private void RemoveFolderRecursively(string folderPath)
        {
            WriteVerboseMessage("Removing folder recursively: {0}", folderPath);

            // List all objects with the folder prefix
            var objectsToDelete = S3Client.ListObjects(BucketName, folderPath + "/", true);

            foreach (var obj in objectsToDelete)
            {
                try
                {
                    WriteVerboseMessage("Removing object: {0}", obj.Name);
                    S3Client.DeleteObject(BucketName, obj.Name);
                }
                catch (Exception ex)
                {
                    WriteVerboseMessage("Failed to remove object '{0}': {1}", obj.Name, ex.Message);
                    throw new InvalidOperationException($"Failed to remove object '{obj.Name}': {ex.Message}", ex);
                }
            }

            WriteVerboseMessage("Successfully removed folder recursively: {0}", folderPath);
        }

        /// <summary>
        /// Removes a single folder marker object
        /// </summary>
        private void RemoveSingleFolder(string folderObjectName)
        {
            try
            {
                WriteVerboseMessage("Removing folder marker: {0}", folderObjectName);
                S3Client.DeleteObject(BucketName, folderObjectName);
                WriteVerboseMessage("Successfully removed folder marker: {0}", folderObjectName);
            }
            catch (Exception ex)
            {
                WriteVerboseMessage("Failed to remove folder marker '{0}': {1}", folderObjectName, ex.Message);
                throw new InvalidOperationException($"Failed to remove folder '{folderObjectName}': {ex.Message}", ex);
            }
        }
    }
}
