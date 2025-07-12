using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using PSMinIO.Core.Models;
using PSMinIO.Core.S3;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Generates presigned URLs for temporary access to MinIO objects
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOPresignedUrl")]
    [OutputType(typeof(PresignedUrlResult))]
    public class NewMinIOPresignedUrlCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// HTTP method for the presigned URL
        /// </summary>
        [Parameter(Position = 2)]
        [ValidateSet("GET", "PUT", "DELETE", "HEAD")]
        public string Method { get; set; } = "GET";

        /// <summary>
        /// Expiration time for the URL (default: 1 hour, maximum: 7 days)
        /// </summary>
        [Parameter]
        public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Additional headers to include in the presigned URL
        /// </summary>
        [Parameter]
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Show the URL in the console (for easy copying)
        /// </summary>
        [Parameter]
        public SwitchParameter ShowUrl { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate expiration time
            if (Expiration.TotalSeconds < 1)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Expiration must be at least 1 second"),
                    "InvalidExpiration",
                    ErrorCategory.InvalidArgument,
                    Expiration));
            }
            if (Expiration.TotalDays > 7)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Expiration cannot exceed 7 days"),
                    "InvalidExpiration",
                    ErrorCategory.InvalidArgument,
                    Expiration));
            }

            var result = ExecuteOperation("GeneratePresignedUrl", () =>
            {
                WriteVerboseMessage("Generating presigned URL for: {0}/{1}", BucketName, ObjectName);
                WriteVerboseMessage("Method: {0}, Expiration: {1}", Method, Expiration);

                // Get connection details
                var connection = Connection;
                
                // Create presigned URL generator
                var generator = new PresignedUrlGenerator(
                    connection.Configuration.Endpoint,
                    connection.Configuration.AccessKey,
                    connection.Configuration.SecretKey,
                    connection.Configuration.Region,
                    connection.Configuration.UseSSL);

                // Parse HTTP method
                var httpMethod = Method.ToUpperInvariant() switch
                {
                    "GET" => HttpMethod.Get,
                    "PUT" => HttpMethod.Put,
                    "DELETE" => HttpMethod.Delete,
                    "HEAD" => HttpMethod.Head,
                    _ => HttpMethod.Get
                };

                // Generate presigned URL
                var urlResult = generator.GeneratePresignedUrl(httpMethod, BucketName, ObjectName, Expiration, Headers);

                WriteVerboseMessage("Generated presigned URL successfully");
                WriteVerboseMessage("URL expires: {0}", urlResult.FormattedExpiration);

                if (ShowUrl.IsPresent)
                {
                    WriteInformation($"Presigned URL: {urlResult.Url}", new string[] { "PresignedUrl" });
                }

                return urlResult;

            }, $"Bucket: {BucketName}, Object: {ObjectName}, Method: {Method}");

            // Always return the result object
            WriteObject(result);
        }
    }

    /// <summary>
    /// Cmdlet for generating GET presigned URLs (download)
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOPresignedUrl")]
    [OutputType(typeof(PresignedUrlResult))]
    public class GetMinIOPresignedUrlCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Expiration time for the URL (default: 1 hour, maximum: 7 days)
        /// </summary>
        [Parameter]
        public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Show the URL in the console (for easy copying)
        /// </summary>
        [Parameter]
        public SwitchParameter ShowUrl { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate expiration time
            if (Expiration.TotalSeconds < 1)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Expiration must be at least 1 second"),
                    "InvalidExpiration",
                    ErrorCategory.InvalidArgument,
                    Expiration));
            }
            if (Expiration.TotalDays > 7)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Expiration cannot exceed 7 days"),
                    "InvalidExpiration",
                    ErrorCategory.InvalidArgument,
                    Expiration));
            }

            var result = ExecuteOperation("GeneratePresignedGetUrl", () =>
            {
                WriteVerboseMessage("Generating presigned GET URL for: {0}/{1}", BucketName, ObjectName);
                WriteVerboseMessage("Expiration: {0}", Expiration);

                // Get connection details
                var connection = Connection;
                
                // Create presigned URL generator
                var generator = new PresignedUrlGenerator(
                    connection.Configuration.Endpoint,
                    connection.Configuration.AccessKey,
                    connection.Configuration.SecretKey,
                    connection.Configuration.Region,
                    connection.Configuration.UseSSL);

                // Generate presigned GET URL
                var urlResult = generator.GeneratePresignedGetUrl(BucketName, ObjectName, Expiration);

                WriteVerboseMessage("Generated presigned GET URL successfully");
                WriteVerboseMessage("URL expires: {0}", urlResult.FormattedExpiration);

                if (ShowUrl.IsPresent)
                {
                    WriteInformation($"Presigned URL: {urlResult.Url}", new string[] { "PresignedUrl" });
                }

                return urlResult;

            }, $"Bucket: {BucketName}, Object: {ObjectName}");

            // Always return the result object
            WriteObject(result);
        }
    }
}
