using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using PSMinIO.Core.Http;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// MinIO S3 API client implementation using custom REST API calls
    /// Provides synchronous operations optimized for PowerShell with real progress reporting
    /// </summary>
    public class MinIOS3Client : IDisposable
    {
        private readonly MinIOHttpClient _httpClient;
        private readonly MinIOConfiguration _configuration;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new MinIOS3Client instance
        /// </summary>
        /// <param name="configuration">MinIO configuration</param>
        public MinIOS3Client(MinIOConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = new MinIOHttpClient(configuration);
        }

        #region Bucket Operations

        /// <summary>
        /// Lists all buckets
        /// </summary>
        /// <returns>List of bucket information</returns>
        public List<MinIOBucketInfo> ListBuckets()
        {
            try
            {
                var response = _httpClient.ExecuteRequestForString(HttpMethod.Get, "/");
                return ParseListBucketsResponse(response);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list buckets: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a bucket exists
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <returns>True if bucket exists</returns>
        public bool BucketExists(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                using var response = _httpClient.ExecuteRequest(HttpMethod.Head, $"/{bucketName}");
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to check if bucket '{bucketName}' exists: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new bucket
        /// </summary>
        /// <param name="bucketName">Name of the bucket to create</param>
        /// <param name="region">Optional region for the bucket</param>
        public void CreateBucket(string bucketName, string? region = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                HttpContent? content = null;
                
                // If region is specified and different from default, create location constraint
                if (!string.IsNullOrEmpty(region) && region != "us-east-1")
                {
                    var locationConstraint = $@"
                        <CreateBucketConfiguration>
                            <LocationConstraint>{region}</LocationConstraint>
                        </CreateBucketConfiguration>";
                    content = new StringContent(locationConstraint, System.Text.Encoding.UTF8, "application/xml");
                }

                using var response = _httpClient.ExecuteRequest(HttpMethod.Put, $"/{bucketName}", content: content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes a bucket
        /// </summary>
        /// <param name="bucketName">Name of the bucket to delete</param>
        public void DeleteBucket(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                using var response = _httpClient.ExecuteRequest(HttpMethod.Delete, $"/{bucketName}");
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets bucket policy
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <returns>Bucket policy as JSON string</returns>
        public string GetBucketPolicy(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var queryParams = new Dictionary<string, string> { { "policy", "" } };
                return _httpClient.ExecuteRequestForString(HttpMethod.Get, $"/{bucketName}", queryParams);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get policy for bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sets bucket policy
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="policy">Policy JSON string</param>
        public void SetBucketPolicy(string bucketName, string policy)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(policy))
                throw new ArgumentException("Policy cannot be null or empty", nameof(policy));

            try
            {
                var queryParams = new Dictionary<string, string> { { "policy", "" } };
                var content = new StringContent(policy, System.Text.Encoding.UTF8, "application/json");
                
                using var response = _httpClient.ExecuteRequest(HttpMethod.Put, $"/{bucketName}", queryParams, content: content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set policy for bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        #endregion

        #region Object Operations

        /// <summary>
        /// Lists objects in a bucket
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="prefix">Optional prefix to filter objects</param>
        /// <param name="recursive">Whether to list objects recursively</param>
        /// <param name="maxObjects">Maximum number of objects to return</param>
        /// <returns>List of object information</returns>
        public List<MinIOObjectInfo> ListObjects(string bucketName, string? prefix = null, bool recursive = true, int maxObjects = 1000)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    { "list-type", "2" },
                    { "max-keys", maxObjects.ToString() }
                };

                if (!string.IsNullOrEmpty(prefix))
                    queryParams["prefix"] = prefix;

                if (!recursive)
                    queryParams["delimiter"] = "/";

                var response = _httpClient.ExecuteRequestForString(HttpMethod.Get, $"/{bucketName}", queryParams);
                return ParseListObjectsResponse(response, bucketName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list objects in bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uploads an object from a stream
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="stream">Source stream</param>
        /// <param name="contentType">Content type</param>
        /// <param name="metadata">Optional metadata</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>ETag of the uploaded object</returns>
        public string PutObject(
            string bucketName,
            string objectName,
            Stream stream,
            string contentType = "application/octet-stream",
            Dictionary<string, string>? metadata = null,
            Action<long>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                var headers = new Dictionary<string, string>();
                
                // Add metadata headers
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        headers[$"x-amz-meta-{kvp.Key}"] = kvp.Value;
                    }
                }

                using var response = _httpClient.UploadFromStream(
                    HttpMethod.Put,
                    $"/{bucketName}/{objectName}",
                    stream,
                    contentType,
                    headers: headers,
                    progressCallback: progressCallback);

                response.EnsureSuccessStatusCode();
                
                // Extract ETag from response headers
                if (response.Headers.ETag != null)
                {
                    return response.Headers.ETag.Tag.Trim('"');
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload object '{objectName}' to bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Downloads an object to a stream
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="stream">Target stream</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>Number of bytes downloaded</returns>
        public long GetObject(
            string bucketName,
            string objectName,
            Stream stream,
            Action<long>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                return _httpClient.DownloadToStream($"/{bucketName}/{objectName}", stream, progressCallback: progressCallback);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download object '{objectName}' from bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes an object
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        public void DeleteObject(string bucketName, string objectName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            try
            {
                using var response = _httpClient.ExecuteRequest(HttpMethod.Delete, $"/{bucketName}/{objectName}");
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete object '{objectName}' from bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses the list buckets XML response
        /// </summary>
        private List<MinIOBucketInfo> ParseListBucketsResponse(string xmlResponse)
        {
            var buckets = new List<MinIOBucketInfo>();
            
            try
            {
                var doc = XDocument.Parse(xmlResponse);
                var ns = doc.Root?.GetDefaultNamespace();
                
                var bucketElements = doc.Descendants(ns + "Bucket");
                foreach (var bucketElement in bucketElements)
                {
                    var name = bucketElement.Element(ns + "Name")?.Value ?? string.Empty;
                    var creationDateStr = bucketElement.Element(ns + "CreationDate")?.Value ?? string.Empty;
                    
                    if (DateTime.TryParse(creationDateStr, out var creationDate))
                    {
                        buckets.Add(new MinIOBucketInfo(name, creationDate));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse list buckets response: {ex.Message}", ex);
            }

            return buckets;
        }

        /// <summary>
        /// Parses the list objects XML response
        /// </summary>
        private List<MinIOObjectInfo> ParseListObjectsResponse(string xmlResponse, string bucketName)
        {
            var objects = new List<MinIOObjectInfo>();
            
            try
            {
                var doc = XDocument.Parse(xmlResponse);
                var ns = doc.Root?.GetDefaultNamespace();
                
                var contentElements = doc.Descendants(ns + "Contents");
                foreach (var contentElement in contentElements)
                {
                    var keyElement = contentElement.Element(ns + "Key");
                    var lastModifiedElement = contentElement.Element(ns + "LastModified");
                    var etagElement = contentElement.Element(ns + "ETag");
                    var sizeElement = contentElement.Element(ns + "Size");
                    var storageClassElement = contentElement.Element(ns + "StorageClass");

                    var key = keyElement?.Value ?? string.Empty;
                    var lastModifiedStr = lastModifiedElement?.Value ?? string.Empty;
                    var etag = etagElement?.Value?.Trim('"') ?? string.Empty;
                    var sizeStr = sizeElement?.Value ?? "0";
                    var storageClass = storageClassElement?.Value ?? string.Empty;

                    if (DateTime.TryParse(lastModifiedStr, out var lastModified) &&
                        long.TryParse(sizeStr, out var size))
                    {
                        var objectInfo = new MinIOObjectInfo(key, size, lastModified, etag, bucketName)
                        {
                            StorageClass = storageClass
                        };
                        objects.Add(objectInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse list objects response: {ex.Message}", ex);
            }

            return objects;
        }

        #endregion

        /// <summary>
        /// Disposes the S3 client
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
