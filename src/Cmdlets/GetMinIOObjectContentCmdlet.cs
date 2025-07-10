using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Downloads an object from a MinIO bucket to a local file
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObjectContent", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(FileInfo))]
    public class GetMinIOObjectContentCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket containing the object
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
        /// Overwrite the file if it already exists
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConfiguration();
            ValidateBucketName(BucketName);
            ValidateObjectName(ObjectName);
            ValidateAndPrepareFilePath();

            if (ShouldProcess($"{BucketName}/{ObjectName}", $"Download to '{FilePath}'"))
            {
                ExecuteOperation("DownloadObject", () =>
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

                    // Check if object exists and get its information
                    var objectInfo = GetObjectInfo();
                    if (objectInfo == null)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Object '{ObjectName}' does not exist in bucket '{BucketName}'"),
                            "ObjectNotFound",
                            ErrorCategory.ObjectNotFound,
                            ObjectName));
                        return;
                    }

                    MinIOLogger.WriteVerbose(this, 
                        "Downloading object '{0}' from bucket '{1}' ({2} bytes) to '{3}'", 
                        ObjectName, BucketName, objectInfo.Size, FilePath);

                    // Create progress reporter
                    var progressReporter = new ProgressReporter(
                        this, 
                        "Downloading Object", 
                        $"Downloading {objectInfo.GetFileName()}", 
                        objectInfo.Size, 
                        1);

                    try
                    {
                        // Download the file with progress reporting
                        Client.DownloadFile(
                            BucketName,
                            ObjectName,
                            FilePath.FullName,
                            bytesTransferred => progressReporter.UpdateProgress(bytesTransferred));

                        // Complete progress reporting
                        progressReporter.Complete();

                        MinIOLogger.WriteVerbose(this,
                            "Successfully downloaded object '{0}' from bucket '{1}' to '{2}'",
                            ObjectName, BucketName, FilePath.FullName);

                        // Always return file information
                        FilePath.Refresh(); // Refresh to get updated file info
                        WriteObject(FilePath);
                    }
                    catch (Exception ex)
                    {
                        progressReporter.Complete();
                        throw;
                    }

                }, $"Bucket: {BucketName}, Object: {ObjectName}, File: {FilePath.FullName}");
            }
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

        /// <summary>
        /// Gets information about the object to be downloaded
        /// </summary>
        /// <returns>Object information or null if not found</returns>
        private Models.MinIOObjectInfo? GetObjectInfo()
        {
            try
            {
                var objects = Client.ListObjects(BucketName, ObjectName, false);
                return objects.FirstOrDefault(obj => 
                    string.Equals(obj.Name, ObjectName, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Could not retrieve object information for '{0}': {1}", ObjectName, ex.Message);
                return null;
            }
        }
    }
}
