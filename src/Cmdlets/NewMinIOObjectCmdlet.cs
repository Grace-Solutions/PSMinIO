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
        /// Single file to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "SingleFile")]
        [ValidateNotNull]
        public FileInfo? File { get; set; }

        /// <summary>
        /// Array of FileInfo objects to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Files")]
        [ValidateNotNull]
        [Alias("Files")]
        public FileInfo[]? Path { get; set; }

        /// <summary>
        /// Directory to upload
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Directory")]
        [ValidateNotNull]
        [Alias("Dir")]
        public DirectoryInfo? Directory { get; set; }

        /// <summary>
        /// Object name in the bucket (defaults to filename) - SingleFile parameter set only
        /// </summary>
        [Parameter(Position = 2, ParameterSetName = "SingleFile")]
        [ValidateNotNullOrEmpty]
        public string? ObjectName { get; set; }

        /// <summary>
        /// Optional bucket directory path where files should be uploaded (Files parameter set only)
        /// </summary>
        [Parameter(ParameterSetName = "Files")]
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

            switch (ParameterSetName)
            {
                case "SingleFile":
                    ProcessSingleFile();
                    break;
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
