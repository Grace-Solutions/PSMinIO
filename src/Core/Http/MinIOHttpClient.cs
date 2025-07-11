using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PSMinIO.Utils;

namespace PSMinIO.Core.Http
{
    /// <summary>
    /// Custom HTTP client for MinIO REST API operations with AWS S3 signature support
    /// Provides synchronous operations optimized for PowerShell with progress reporting
    /// </summary>
    public class MinIOHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly MinIOConfiguration _configuration;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new MinIOHttpClient instance
        /// </summary>
        /// <param name="configuration">MinIO configuration</param>
        public MinIOHttpClient(MinIOConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (!_configuration.IsValid)
            {
                var errors = string.Join(", ", _configuration.GetValidationErrors());
                throw new ArgumentException($"Invalid MinIO configuration: {errors}", nameof(configuration));
            }

            // Create HTTP client with custom configuration
            var handler = new HttpClientHandler();
            
            // Configure certificate validation if needed
            if (_configuration.SkipCertificateValidation)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds),
                BaseAddress = new Uri(_configuration.BaseUrl)
            };

            // Set default headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PSMinIO/2.0.0");
        }

        /// <summary>
        /// Executes a synchronous HTTP request with AWS S3 signature
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="path">Request path</param>
        /// <param name="queryParameters">Query parameters</param>
        /// <param name="headers">Additional headers</param>
        /// <param name="content">Request content</param>
        /// <param name="progressCallback">Progress callback for uploads/downloads</param>
        /// <returns>HTTP response</returns>
        public HttpResponseMessage ExecuteRequest(
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParameters = null,
            Dictionary<string, string>? headers = null,
            HttpContent? content = null,
            Action<long>? progressCallback = null)
        {
            var request = CreateRequest(method, path, queryParameters, headers, content);

            // Sign the request with AWS S3 signature
            SignRequest(request);

            try
            {
                // Execute the request synchronously
                var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
                return response;
            }
            catch (Exception ex)
            {
                // Add more detailed error information
                var innerMessage = ex.InnerException?.Message ?? "No inner exception";
                throw new InvalidOperationException($"HTTP request failed: {ex.Message}. Inner: {innerMessage}. URL: {request.RequestUri}", ex);
            }
        }

        /// <summary>
        /// Executes a synchronous HTTP request and returns the response content as string
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="path">Request path</param>
        /// <param name="queryParameters">Query parameters</param>
        /// <param name="headers">Additional headers</param>
        /// <param name="content">Request content</param>
        /// <returns>Response content as string</returns>
        public string ExecuteRequestForString(
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParameters = null,
            Dictionary<string, string>? headers = null,
            HttpContent? content = null)
        {
            using var response = ExecuteRequest(method, path, queryParameters, headers, content);
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes a synchronous HTTP request and returns the response content as byte array
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="path">Request path</param>
        /// <param name="queryParameters">Query parameters</param>
        /// <param name="headers">Additional headers</param>
        /// <param name="content">Request content</param>
        /// <returns>Response content as byte array</returns>
        public byte[] ExecuteRequestForBytes(
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParameters = null,
            Dictionary<string, string>? headers = null,
            HttpContent? content = null)
        {
            using var response = ExecuteRequest(method, path, queryParameters, headers, content);
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Downloads content to a stream with progress reporting
        /// </summary>
        /// <param name="path">Request path</param>
        /// <param name="outputStream">Output stream</param>
        /// <param name="queryParameters">Query parameters</param>
        /// <param name="headers">Additional headers</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>Total bytes downloaded</returns>
        public long DownloadToStream(
            string path,
            Stream outputStream,
            Dictionary<string, string>? queryParameters = null,
            Dictionary<string, string>? headers = null,
            Action<long>? progressCallback = null)
        {
            using var response = ExecuteRequest(HttpMethod.Get, path, queryParameters, headers);
            response.EnsureSuccessStatusCode();

            using var contentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            return CopyStreamWithProgress(contentStream, outputStream, progressCallback);
        }

        /// <summary>
        /// Uploads content from a stream with progress reporting
        /// </summary>
        /// <param name="method">HTTP method (PUT or POST)</param>
        /// <param name="path">Request path</param>
        /// <param name="inputStream">Input stream</param>
        /// <param name="contentType">Content type</param>
        /// <param name="queryParameters">Query parameters</param>
        /// <param name="headers">Additional headers</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>HTTP response</returns>
        public HttpResponseMessage UploadFromStream(
            HttpMethod method,
            string path,
            Stream inputStream,
            string contentType = "application/octet-stream",
            Dictionary<string, string>? queryParameters = null,
            Dictionary<string, string>? headers = null,
            Action<long>? progressCallback = null)
        {
            var content = new ProgressStreamContent(inputStream, contentType, progressCallback);
            return ExecuteRequest(method, path, queryParameters, headers, content);
        }

        /// <summary>
        /// Creates an HTTP request message
        /// </summary>
        private HttpRequestMessage CreateRequest(
            HttpMethod method,
            string path,
            Dictionary<string, string>? queryParameters,
            Dictionary<string, string>? headers,
            HttpContent? content)
        {
            // Build the request URI
            var uriBuilder = new UriBuilder(_configuration.BaseUrl)
            {
                Path = path
            };

            if (queryParameters != null && queryParameters.Count > 0)
            {
                var query = new StringBuilder();
                foreach (var kvp in queryParameters)
                {
                    if (query.Length > 0) query.Append("&");
                    query.Append($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                }
                uriBuilder.Query = query.ToString();
            }

            var request = new HttpRequestMessage(method, uriBuilder.Uri)
            {
                Content = content
            };

            // Add custom headers
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            return request;
        }

        /// <summary>
        /// Signs the HTTP request with AWS S3 signature v4
        /// </summary>
        private void SignRequest(HttpRequestMessage request)
        {
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString("yyyyMMdd");
            var timeStamp = now.ToString("yyyyMMddTHHmmssZ");

            // Add required headers
            request.Headers.TryAddWithoutValidation("x-amz-date", timeStamp);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", "UNSIGNED-PAYLOAD");

            // Create authorization header
            var authHeader = CreateAuthorizationHeader(request, dateStamp, timeStamp);
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }

        /// <summary>
        /// Creates the AWS S3 authorization header
        /// </summary>
        private string CreateAuthorizationHeader(HttpRequestMessage request, string dateStamp, string timeStamp)
        {
            var algorithm = "AWS4-HMAC-SHA256";
            var credentialScope = $"{dateStamp}/{_configuration.Region}/s3/aws4_request";
            var credential = $"{_configuration.AccessKey}/{credentialScope}";

            // Create canonical request
            var canonicalRequest = CreateCanonicalRequest(request);
            var canonicalRequestHash = ComputeSHA256Hash(canonicalRequest);

            // Create string to sign
            var stringToSign = $"{algorithm}\n{timeStamp}\n{credentialScope}\n{canonicalRequestHash}";

            // Calculate signature
            var signature = CalculateSignature(stringToSign, dateStamp);

            return $"{algorithm} Credential={credential}, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature={signature}";
        }

        /// <summary>
        /// Creates the canonical request for AWS S3 signature
        /// </summary>
        private string CreateCanonicalRequest(HttpRequestMessage request)
        {
            var method = request.Method.Method;
            var path = request.RequestUri?.AbsolutePath ?? "/";
            var query = request.RequestUri?.Query?.TrimStart('?') ?? "";

            // Canonical headers (must be sorted)
            var canonicalHeaders = "host:" + request.RequestUri?.Host + "\n" +
                                 "x-amz-content-sha256:UNSIGNED-PAYLOAD\n" +
                                 "x-amz-date:" + request.Headers.GetValues("x-amz-date").FirstOrDefault() + "\n";

            var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            var payloadHash = "UNSIGNED-PAYLOAD";

            return $"{method}\n{path}\n{query}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        }

        /// <summary>
        /// Calculates the AWS S3 signature
        /// </summary>
        private string CalculateSignature(string stringToSign, string dateStamp)
        {
            var kSecret = Encoding.UTF8.GetBytes($"AWS4{_configuration.SecretKey}");
            var kDate = ComputeHMACSHA256(kSecret, dateStamp);
            var kRegion = ComputeHMACSHA256(kDate, _configuration.Region);
            var kService = ComputeHMACSHA256(kRegion, "s3");
            var kSigning = ComputeHMACSHA256(kService, "aws4_request");
            var signature = ComputeHMACSHA256(kSigning, stringToSign);

            return BitConverter.ToString(signature).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes SHA256 hash
        /// </summary>
        private string ComputeSHA256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Computes HMAC-SHA256
        /// </summary>
        private byte[] ComputeHMACSHA256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Copies stream with progress reporting
        /// </summary>
        private long CopyStreamWithProgress(Stream source, Stream destination, Action<long>? progressCallback)
        {
            var buffer = new byte[8192];
            long totalBytes = 0;
            int bytesRead;

            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, bytesRead);
                totalBytes += bytesRead;
                progressCallback?.Invoke(totalBytes);
            }

            return totalBytes;
        }

        /// <summary>
        /// Disposes the HTTP client
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
