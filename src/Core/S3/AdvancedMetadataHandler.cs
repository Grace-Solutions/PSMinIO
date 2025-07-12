using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// Handles advanced object metadata with S3-compatible headers and custom metadata
    /// </summary>
    public class AdvancedMetadataHandler
    {
        /// <summary>
        /// Creates headers from metadata configuration
        /// </summary>
        public Dictionary<string, string> CreateHeaders(ObjectMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            var headers = new Dictionary<string, string>();

            // Content-Type
            if (!string.IsNullOrEmpty(metadata.ContentType))
            {
                headers["Content-Type"] = metadata.ContentType;
            }

            // Content-Encoding
            if (!string.IsNullOrEmpty(metadata.ContentEncoding))
            {
                headers["Content-Encoding"] = metadata.ContentEncoding;
            }

            // Content-Language
            if (!string.IsNullOrEmpty(metadata.ContentLanguage))
            {
                headers["Content-Language"] = metadata.ContentLanguage;
            }

            // Content-Disposition
            if (!string.IsNullOrEmpty(metadata.ContentDisposition))
            {
                headers["Content-Disposition"] = metadata.ContentDisposition;
            }

            // Cache-Control
            if (!string.IsNullOrEmpty(metadata.CacheControl))
            {
                headers["Cache-Control"] = metadata.CacheControl;
            }

            // Expires
            if (metadata.Expires.HasValue)
            {
                headers["Expires"] = metadata.Expires.Value.ToString("R", CultureInfo.InvariantCulture);
            }

            // Server-Side Encryption
            if (!string.IsNullOrEmpty(metadata.ServerSideEncryption))
            {
                headers["x-amz-server-side-encryption"] = metadata.ServerSideEncryption;
            }

            // SSE-KMS Key ID
            if (!string.IsNullOrEmpty(metadata.SSEKMSKeyId))
            {
                headers["x-amz-server-side-encryption-aws-kms-key-id"] = metadata.SSEKMSKeyId;
            }

            // SSE-C Algorithm
            if (!string.IsNullOrEmpty(metadata.SSECustomerAlgorithm))
            {
                headers["x-amz-server-side-encryption-customer-algorithm"] = metadata.SSECustomerAlgorithm;
            }

            // SSE-C Key
            if (!string.IsNullOrEmpty(metadata.SSECustomerKey))
            {
                headers["x-amz-server-side-encryption-customer-key"] = metadata.SSECustomerKey;
            }

            // SSE-C Key MD5
            if (!string.IsNullOrEmpty(metadata.SSECustomerKeyMD5))
            {
                headers["x-amz-server-side-encryption-customer-key-MD5"] = metadata.SSECustomerKeyMD5;
            }

            // Storage Class
            if (!string.IsNullOrEmpty(metadata.StorageClass))
            {
                headers["x-amz-storage-class"] = metadata.StorageClass;
            }

            // Website Redirect Location
            if (!string.IsNullOrEmpty(metadata.WebsiteRedirectLocation))
            {
                headers["x-amz-website-redirect-location"] = metadata.WebsiteRedirectLocation;
            }

            // Object Lock Mode
            if (!string.IsNullOrEmpty(metadata.ObjectLockMode))
            {
                headers["x-amz-object-lock-mode"] = metadata.ObjectLockMode;
            }

            // Object Lock Retain Until Date
            if (metadata.ObjectLockRetainUntilDate.HasValue)
            {
                headers["x-amz-object-lock-retain-until-date"] = 
                    metadata.ObjectLockRetainUntilDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            }

            // Object Lock Legal Hold
            if (metadata.ObjectLockLegalHoldStatus.HasValue)
            {
                headers["x-amz-object-lock-legal-hold"] = metadata.ObjectLockLegalHoldStatus.Value ? "ON" : "OFF";
            }

            // Tagging
            if (metadata.Tags != null && metadata.Tags.Count > 0)
            {
                var tagString = string.Join("&", metadata.Tags.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                headers["x-amz-tagging"] = tagString;
            }

            // Custom metadata (x-amz-meta-*)
            if (metadata.UserMetadata != null && metadata.UserMetadata.Count > 0)
            {
                foreach (var kvp in metadata.UserMetadata)
                {
                    var key = kvp.Key.StartsWith("x-amz-meta-") ? kvp.Key : $"x-amz-meta-{kvp.Key}";
                    headers[key] = kvp.Value;
                }
            }

            // Custom headers
            if (metadata.CustomHeaders != null && metadata.CustomHeaders.Count > 0)
            {
                foreach (var kvp in metadata.CustomHeaders)
                {
                    headers[kvp.Key] = kvp.Value;
                }
            }

            return headers;
        }

        /// <summary>
        /// Parses metadata from response headers
        /// </summary>
        public ObjectMetadata ParseMetadata(HttpResponseMessage response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var metadata = new ObjectMetadata();

            // Process response headers
            foreach (var header in response.Headers)
            {
                var key = header.Key.ToLowerInvariant();
                var value = string.Join(", ", header.Value);

                ProcessHeader(metadata, key, value, header.Key);
            }

            // Process content headers
            foreach (var header in response.Content.Headers)
            {
                var key = header.Key.ToLowerInvariant();
                var value = string.Join(", ", header.Value);
                ProcessHeader(metadata, key, value, header.Key);
            }

            return metadata;
        }

        /// <summary>
        /// Processes a single header into metadata
        /// </summary>
        private void ProcessHeader(ObjectMetadata metadata, string key, string value, string originalKey)
        {
            switch (key)
            {
                case "content-type":
                    metadata.ContentType = value;
                    break;
                case "content-encoding":
                    metadata.ContentEncoding = value;
                    break;
                case "content-language":
                    metadata.ContentLanguage = value;
                    break;
                case "content-disposition":
                    metadata.ContentDisposition = value;
                    break;
                case "cache-control":
                    metadata.CacheControl = value;
                    break;
                case "expires":
                    if (DateTime.TryParse(value, out var expires))
                        metadata.Expires = expires;
                    break;
                case "x-amz-server-side-encryption":
                    metadata.ServerSideEncryption = value;
                    break;
                case "x-amz-server-side-encryption-aws-kms-key-id":
                    metadata.SSEKMSKeyId = value;
                    break;
                case "x-amz-server-side-encryption-customer-algorithm":
                    metadata.SSECustomerAlgorithm = value;
                    break;
                case "x-amz-storage-class":
                    metadata.StorageClass = value;
                    break;
                case "x-amz-website-redirect-location":
                    metadata.WebsiteRedirectLocation = value;
                    break;
                case "x-amz-object-lock-mode":
                    metadata.ObjectLockMode = value;
                    break;
                case "x-amz-object-lock-retain-until-date":
                    if (DateTime.TryParse(value, out var retainUntil))
                        metadata.ObjectLockRetainUntilDate = retainUntil;
                    break;
                case "x-amz-object-lock-legal-hold":
                    metadata.ObjectLockLegalHoldStatus = value.Equals("ON", StringComparison.OrdinalIgnoreCase);
                    break;
                case "x-amz-tagging":
                    metadata.Tags = ParseTagString(value);
                    break;
                default:
                    if (key.StartsWith("x-amz-meta-"))
                    {
                        metadata.UserMetadata ??= new Dictionary<string, string>();
                        metadata.UserMetadata[key] = value;
                    }
                    else
                    {
                        metadata.CustomHeaders ??= new Dictionary<string, string>();
                        metadata.CustomHeaders[originalKey] = value;
                    }
                    break;
            }
        }

        /// <summary>
        /// Creates cache control header value
        /// </summary>
        public string CreateCacheControlHeader(CacheControlSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var directives = new List<string>();

            if (settings.NoCache) directives.Add("no-cache");
            if (settings.NoStore) directives.Add("no-store");
            if (settings.MustRevalidate) directives.Add("must-revalidate");
            if (settings.Public) directives.Add("public");
            if (settings.Private) directives.Add("private");

            if (settings.MaxAge.HasValue)
                directives.Add($"max-age={settings.MaxAge.Value.TotalSeconds:F0}");

            if (settings.SMaxAge.HasValue)
                directives.Add($"s-maxage={settings.SMaxAge.Value.TotalSeconds:F0}");

            return string.Join(", ", directives);
        }

        /// <summary>
        /// Creates content disposition header value
        /// </summary>
        public string CreateContentDispositionHeader(string disposition, string? filename = null)
        {
            if (string.IsNullOrEmpty(disposition)) throw new ArgumentException("Disposition cannot be null or empty", nameof(disposition));

            var result = disposition;
            if (!string.IsNullOrEmpty(filename))
            {
                result += $"; filename=\"{filename}\"";
            }

            return result;
        }

        /// <summary>
        /// Parses tag string into dictionary
        /// </summary>
        private Dictionary<string, string> ParseTagString(string tagString)
        {
            var tags = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(tagString)) return tags;

            var pairs = tagString.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split(new char[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var key = Uri.UnescapeDataString(parts[0]);
                    var value = Uri.UnescapeDataString(parts[1]);
                    tags[key] = value;
                }
            }

            return tags;
        }
    }

    /// <summary>
    /// Comprehensive object metadata configuration
    /// </summary>
    public class ObjectMetadata
    {
        // Standard HTTP headers
        public string? ContentType { get; set; }
        public string? ContentEncoding { get; set; }
        public string? ContentLanguage { get; set; }
        public string? ContentDisposition { get; set; }
        public string? CacheControl { get; set; }
        public DateTime? Expires { get; set; }

        // S3-specific headers
        public string? ServerSideEncryption { get; set; }
        public string? SSEKMSKeyId { get; set; }
        public string? SSECustomerAlgorithm { get; set; }
        public string? SSECustomerKey { get; set; }
        public string? SSECustomerKeyMD5 { get; set; }
        public string? StorageClass { get; set; }
        public string? WebsiteRedirectLocation { get; set; }

        // Object Lock
        public string? ObjectLockMode { get; set; }
        public DateTime? ObjectLockRetainUntilDate { get; set; }
        public bool? ObjectLockLegalHoldStatus { get; set; }

        // Tags and custom metadata
        public Dictionary<string, string>? Tags { get; set; }
        public Dictionary<string, string>? UserMetadata { get; set; }
        public Dictionary<string, string>? CustomHeaders { get; set; }
    }

    /// <summary>
    /// Cache control settings
    /// </summary>
    public class CacheControlSettings
    {
        public bool NoCache { get; set; }
        public bool NoStore { get; set; }
        public bool MustRevalidate { get; set; }
        public bool Public { get; set; }
        public bool Private { get; set; }
        public TimeSpan? MaxAge { get; set; }
        public TimeSpan? SMaxAge { get; set; }
    }
}
