using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Http;
using PSMinIO.Core.Models;
using PSMinIO.Core.S3;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Uploads objects using multipart upload with parallel processing and resume capability
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOObjectMultipart", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MultipartUploadResult))]
    public class NewMinIOObjectMultipartCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to upload to
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Local file to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        [Alias("File", "Path")]
        public FileInfo FilePath { get; set; } = null!;

        /// <summary>
        /// Name of the object in the bucket (optional, uses filename if not specified)
        /// </summary>
        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string? ObjectName { get; set; }

        /// <summary>
        /// Size of each upload chunk in bytes (default: 64MB, minimum: 5MB for S3 compatibility)
        /// </summary>
        [Parameter]
        [ValidateRange(5 * 1024 * 1024, long.MaxValue)] // Minimum 5MB for S3 compatibility
        public long ChunkSize { get; set; } = 64 * 1024 * 1024; // 64MB default

        /// <summary>
        /// Maximum number of parallel upload threads (default: 4)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 10)]
        public int MaxParallelUploads { get; set; } = 4;

        /// <summary>
        /// Content type of the object (auto-detected if not specified)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ContentType { get; set; }

        /// <summary>
        /// Custom metadata for the object
        /// </summary>
        [Parameter]
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Resume upload using existing upload ID
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? ResumeUploadId { get; set; }

        /// <summary>
        /// Completed parts for resume (used with ResumeUploadId)
        /// </summary>
        [Parameter]
        public PartInfo[]? CompletedParts { get; set; }

        /// <summary>
        /// Overwrite existing object without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate file exists
            if (!FilePath.Exists)
            {
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"File not found: {FilePath.FullName}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    FilePath);
                ThrowTerminatingError(errorRecord);
            }

            // Determine object name
            var effectiveObjectName = ObjectName ?? FilePath.Name;

            // Auto-detect content type if not specified
            var effectiveContentType = ContentType ?? GetContentType(FilePath.Extension);

            // Add content type to metadata
            var effectiveMetadata = Metadata ?? new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(effectiveContentType))
            {
                effectiveMetadata["Content-Type"] = effectiveContentType;
            }

            var operationDescription = !string.IsNullOrEmpty(ResumeUploadId)
                ? $"Resume multipart upload of '{FilePath.Name}' to bucket '{BucketName}'"
                : $"Multipart upload '{FilePath.Name}' to bucket '{BucketName}'";

            if (ShouldProcess(effectiveObjectName, operationDescription))
            {
                var result = ExecuteOperation("MultipartUpload", () =>
                {
                    WriteVerboseMessage("Starting multipart upload: {0} -> {1}/{2}", 
                        FilePath.FullName, BucketName, effectiveObjectName);
                    WriteVerboseMessage("File size: {0}, Chunk size: {1}, Max parallel uploads: {2}", 
                        SizeFormatter.FormatBytes(FilePath.Length), SizeFormatter.FormatBytes(ChunkSize), MaxParallelUploads);

                    if (!string.IsNullOrEmpty(ResumeUploadId))
                    {
                        WriteVerboseMessage("Resuming upload with ID: {0}", ResumeUploadId!);
                        if (CompletedParts != null && CompletedParts.Length > 0)
                        {
                            WriteVerboseMessage("Found {0} completed parts", CompletedParts.Length);
                        }
                    }

                    // Get connection and create upload manager
                    var connection = Connection;
                    var httpClient = new MinIOHttpClient(connection.Configuration);
                    var progressCollector = new ThreadSafeProgressCollector(this);
                    var uploadManager = new MultipartUploadManager(httpClient, progressCollector, 
                        MaxParallelUploads, ChunkSize);

                    // Perform multipart upload
                    var uploadResult = uploadManager.UploadFile(BucketName, effectiveObjectName, FilePath, 
                        effectiveMetadata, ChunkSize, ResumeUploadId, CompletedParts?.ToList());

                    if (uploadResult.IsCompleted)
                    {
                        WriteVerboseMessage("Multipart upload completed successfully");
                        WriteVerboseMessage("Uploaded: {0} in {1} at {2}", 
                            SizeFormatter.FormatBytes(uploadResult.TotalSize),
                            SizeFormatter.FormatDuration(uploadResult.Duration),
                            SizeFormatter.FormatSpeed(uploadResult.AverageSpeed));
                        WriteVerboseMessage("ETag: {0}", uploadResult.ETag);
                    }
                    else
                    {
                        WriteWarning($"Multipart upload failed: {uploadResult.Error}");
                        if (uploadResult.CompletedParts.Count > 0)
                        {
                            WriteWarning($"Upload can be resumed using UploadId: {uploadResult.UploadId}");
                            WriteWarning($"Completed parts: {uploadResult.CompletedParts.Count}/{uploadResult.TotalParts}");
                        }
                    }

                    return uploadResult;

                }, $"File: {FilePath.FullName}, Bucket: {BucketName}, Object: {effectiveObjectName}");

                // Always return the result object
                WriteObject(result);
            }
        }

        /// <summary>
        /// Gets content type based on file extension
        /// </summary>
        private string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
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
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/octet-stream"
            };
        }
    }
}
