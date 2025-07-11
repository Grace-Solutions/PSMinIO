using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Uploads objects to MinIO
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOObject", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MinIOUploadResult))]
    public class NewMinIOObjectCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object (if not specified, uses filename)
        /// </summary>
        [Parameter(Position = 1)]
        [ValidateNotNullOrEmpty]
        public string? ObjectName { get; set; }

        /// <summary>
        /// File to upload
        /// </summary>
        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        public FileInfo File { get; set; } = null!;

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
        /// Directory prefix in the bucket (creates nested structure)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        [Alias("Folder", "Directory")]
        public string? BucketDirectory { get; set; }

        /// <summary>
        /// Force overwrite if object already exists
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

            // Validate file exists
            if (!File.Exists)
            {
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"File not found: {File.FullName}"),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    File);
                ThrowTerminatingError(errorRecord);
            }

            // Determine object name
            var finalObjectName = ObjectName ?? File?.Name ?? "unknown";
            
            // Add bucket directory prefix if specified
            if (!string.IsNullOrEmpty(BucketDirectory))
            {
                var cleanDirectory = BucketDirectory.Trim('/');
                if (!string.IsNullOrEmpty(cleanDirectory))
                {
                    finalObjectName = $"{cleanDirectory}/{finalObjectName}";
                }
            }

            ValidateObjectName(finalObjectName);

            if (ShouldProcess($"{BucketName}/{finalObjectName}", "Upload file"))
            {
                var result = ExecuteOperation("UploadObject", () =>
                {
                    WriteVerboseMessage("Uploading file '{0}' to bucket '{1}' as object '{2}'",
                        File.FullName, BucketName, finalObjectName);

                    // Create upload result
                    var uploadResult = new MinIOUploadResult(BucketName, finalObjectName, string.Empty, File.Length)
                    {
                        SourceFilePath = File.FullName
                    };
                    uploadResult.MarkStarted();

                    try
                    {
                        // Check if bucket exists
                        if (!S3Client.BucketExists(BucketName))
                        {
                            throw new InvalidOperationException($"Bucket '{BucketName}' does not exist");
                        }

                        // Determine content type
                        var fileContentType = ContentType ?? GetContentType(File.Extension);
                        uploadResult.ContentType = fileContentType;
                        WriteVerboseMessage("Using content type: {0}", fileContentType);

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
                            WriteVerboseMessage("Added {0} metadata entries", metadata.Count);
                        }

                        // Track progress
                        var fileSize = File.Length;
                        var startTime = DateTime.UtcNow;

                        WriteVerboseMessage("Starting upload of {0}", SizeFormatter.FormatBytes(fileSize));

                        // Upload the file
                        string etag;
                        using (var fileStream = File.OpenRead())
                        {
                            etag = S3Client.PutObject(
                                BucketName,
                                finalObjectName,
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
                        var percentage = fileSize > 0 ? (double)uploadResult.BytesTransferred / fileSize * 100 : 100;
                        WriteVerboseMessage("Upload progress: {0:F1}% ({1}/{2}) at {3}",
                            percentage,
                            SizeFormatter.FormatBytes(uploadResult.BytesTransferred),
                            SizeFormatter.FormatBytes(fileSize),
                            SizeFormatter.FormatSpeed(averageSpeed));

                        WriteVerboseMessage("Upload completed in {0} at average speed of {1}",
                            SizeFormatter.FormatDuration(duration),
                            SizeFormatter.FormatSpeed(averageSpeed));

                        return uploadResult;
                    }
                    catch (Exception ex)
                    {
                        uploadResult.MarkFailed(ex.Message);
                        throw;
                    }

                }, $"File: {File.FullName}, Bucket: {BucketName}, Object: {finalObjectName}");

                // Return result if requested
                if (PassThru.IsPresent)
                {
                    WriteObject(result);
                }
                else
                {
                    WriteVerboseMessage("Successfully uploaded object '{0}' to bucket '{1}'", finalObjectName, BucketName);
                }
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
    }
}
