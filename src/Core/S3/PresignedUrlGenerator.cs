using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// Generates presigned URLs for temporary access to MinIO objects
    /// </summary>
    public class PresignedUrlGenerator
    {
        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _region;
        private readonly bool _useSSL;

        public PresignedUrlGenerator(string endpoint, string accessKey, string secretKey, string region = "us-east-1", bool useSSL = true)
        {
            _endpoint = endpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(endpoint));
            _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
            _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            _region = region ?? "us-east-1";
            _useSSL = useSSL;
        }

        /// <summary>
        /// Generates a presigned URL for GET operations (download)
        /// </summary>
        public PresignedUrlResult GeneratePresignedGetUrl(string bucketName, string objectName, 
            TimeSpan expiration, Dictionary<string, string>? additionalHeaders = null)
        {
            return GeneratePresignedUrl(HttpMethod.Get, bucketName, objectName, expiration, additionalHeaders);
        }

        /// <summary>
        /// Generates a presigned URL for PUT operations (upload)
        /// </summary>
        public PresignedUrlResult GeneratePresignedPutUrl(string bucketName, string objectName, 
            TimeSpan expiration, Dictionary<string, string>? additionalHeaders = null)
        {
            return GeneratePresignedUrl(HttpMethod.Put, bucketName, objectName, expiration, additionalHeaders);
        }

        /// <summary>
        /// Generates a presigned URL for DELETE operations
        /// </summary>
        public PresignedUrlResult GeneratePresignedDeleteUrl(string bucketName, string objectName, 
            TimeSpan expiration, Dictionary<string, string>? additionalHeaders = null)
        {
            return GeneratePresignedUrl(HttpMethod.Delete, bucketName, objectName, expiration, additionalHeaders);
        }

        /// <summary>
        /// Generates a presigned URL for HEAD operations (metadata)
        /// </summary>
        public PresignedUrlResult GeneratePresignedHeadUrl(string bucketName, string objectName, 
            TimeSpan expiration, Dictionary<string, string>? additionalHeaders = null)
        {
            return GeneratePresignedUrl(HttpMethod.Head, bucketName, objectName, expiration, additionalHeaders);
        }

        /// <summary>
        /// Generates a presigned URL with custom HTTP method
        /// </summary>
        public PresignedUrlResult GeneratePresignedUrl(HttpMethod method, string bucketName, string objectName, 
            TimeSpan expiration, Dictionary<string, string>? additionalHeaders = null)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            if (expiration.TotalSeconds < 1) throw new ArgumentException("Expiration must be at least 1 second", nameof(expiration));
            if (expiration.TotalDays > 7) throw new ArgumentException("Expiration cannot exceed 7 days", nameof(expiration));

            var now = DateTime.UtcNow;
            var expirationTime = now.Add(expiration);
            var expirationUnix = ((DateTimeOffset)expirationTime).ToUnixTimeSeconds();

            // Build the canonical request
            var httpMethod = method.Method.ToUpperInvariant();
            var canonicalUri = $"/{bucketName}/{Uri.EscapeDataString(objectName)}";
            var timestamp = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            // Query parameters for AWS Signature Version 4
            var queryParams = new Dictionary<string, string>
            {
                { "X-Amz-Algorithm", "AWS4-HMAC-SHA256" },
                { "X-Amz-Credential", $"{_accessKey}/{dateStamp}/{_region}/s3/aws4_request" },
                { "X-Amz-Date", timestamp },
                { "X-Amz-Expires", expiration.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) },
                { "X-Amz-SignedHeaders", "host" }
            };

            // Add additional headers to signed headers if provided
            var signedHeaders = new List<string> { "host" };
            var canonicalHeaders = $"host:{GetHostFromEndpoint()}\n";

            if (additionalHeaders != null && additionalHeaders.Count > 0)
            {
                foreach (var header in additionalHeaders.OrderBy(h => h.Key.ToLowerInvariant()))
                {
                    var headerName = header.Key.ToLowerInvariant();
                    signedHeaders.Add(headerName);
                    canonicalHeaders += $"{headerName}:{header.Value}\n";
                }
                queryParams["X-Amz-SignedHeaders"] = string.Join(";", signedHeaders.OrderBy(h => h));
            }

            // Build canonical query string
            var canonicalQueryString = string.Join("&", 
                queryParams.OrderBy(p => p.Key).Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

            // Create canonical request
            var canonicalRequest = $"{httpMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{string.Join(";", signedHeaders.OrderBy(h => h))}\nUNSIGNED-PAYLOAD";

            // Create string to sign
            var credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{ComputeSHA256Hash(canonicalRequest)}";

            // Calculate signature
            var signature = ComputeSignature(stringToSign, dateStamp);
            queryParams["X-Amz-Signature"] = signature;

            // Build final URL
            var scheme = _useSSL ? "https" : "http";
            var finalQueryString = string.Join("&", 
                queryParams.OrderBy(p => p.Key).Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            var presignedUrl = $"{scheme}://{GetHostFromEndpoint()}{canonicalUri}?{finalQueryString}";

            return new PresignedUrlResult
            {
                Url = presignedUrl,
                Method = httpMethod,
                BucketName = bucketName,
                ObjectName = objectName,
                ExpirationTime = expirationTime,
                CreatedTime = now,
                Duration = expiration,
                AdditionalHeaders = additionalHeaders?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        /// <summary>
        /// Extracts host from endpoint
        /// </summary>
        private string GetHostFromEndpoint()
        {
            var uri = new Uri(_useSSL ? $"https://{_endpoint}" : $"http://{_endpoint}");
            return uri.Host + (uri.IsDefaultPort ? "" : $":{uri.Port}");
        }

        /// <summary>
        /// Computes SHA256 hash
        /// </summary>
        private static string ComputeSHA256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes AWS Signature Version 4 signature
        /// </summary>
        private string ComputeSignature(string stringToSign, string dateStamp)
        {
            var kDate = ComputeHMACSHA256($"AWS4{_secretKey}", dateStamp);
            var kRegion = ComputeHMACSHA256(kDate, _region);
            var kService = ComputeHMACSHA256(kRegion, "s3");
            var kSigning = ComputeHMACSHA256(kService, "aws4_request");
            var signature = ComputeHMACSHA256(kSigning, stringToSign);
            
            return BitConverter.ToString(signature).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes HMAC-SHA256
        /// </summary>
        private static byte[] ComputeHMACSHA256(string key, string data)
        {
            return ComputeHMACSHA256(Encoding.UTF8.GetBytes(key), data);
        }

        /// <summary>
        /// Computes HMAC-SHA256 with byte array key
        /// </summary>
        private static byte[] ComputeHMACSHA256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }
    }

    /// <summary>
    /// Result of presigned URL generation
    /// </summary>
    public class PresignedUrlResult
    {
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
        public DateTime CreatedTime { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, string>? AdditionalHeaders { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        public TimeSpan TimeUntilExpiration => ExpirationTime > DateTime.UtcNow ? ExpirationTime - DateTime.UtcNow : TimeSpan.Zero;
        public string FormattedExpiration => ExpirationTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }

    /// <summary>
    /// Presigned URL request configuration
    /// </summary>
    public class PresignedUrlRequest
    {
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);
        public Dictionary<string, string>? Headers { get; set; }
        public Dictionary<string, string>? QueryParameters { get; set; }
    }
}
