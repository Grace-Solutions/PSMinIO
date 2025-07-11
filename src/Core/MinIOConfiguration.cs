using System;
using System.Collections.Generic;

namespace PSMinIO.Core
{
    /// <summary>
    /// Configuration settings for MinIO client connections
    /// </summary>
    public class MinIOConfiguration
    {
        /// <summary>
        /// MinIO server endpoint (e.g., "minio.example.com:9000")
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Access key for authentication
        /// </summary>
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Secret key for authentication
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Whether to use SSL/TLS for connections
        /// </summary>
        public bool UseSSL { get; set; } = true;

        /// <summary>
        /// Optional region for bucket operations
        /// </summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to skip SSL certificate validation (for development/self-signed certificates)
        /// </summary>
        public bool SkipCertificateValidation { get; set; } = false;

        /// <summary>
        /// Maximum number of concurrent connections
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Default chunk size for multipart uploads (in bytes)
        /// </summary>
        public long DefaultChunkSize { get; set; } = 5 * 1024 * 1024; // 5MB

        /// <summary>
        /// Maximum retry attempts for failed operations
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Creates a new MinIOConfiguration instance
        /// </summary>
        public MinIOConfiguration()
        {
        }

        /// <summary>
        /// Creates a new MinIOConfiguration instance with specified values
        /// </summary>
        /// <param name="endpoint">MinIO server endpoint</param>
        /// <param name="accessKey">Access key</param>
        /// <param name="secretKey">Secret key</param>
        /// <param name="useSSL">Whether to use SSL</param>
        /// <param name="region">Optional region</param>
        /// <param name="timeoutSeconds">Connection timeout</param>
        /// <param name="skipCertificateValidation">Whether to skip certificate validation</param>
        public MinIOConfiguration(
            string endpoint,
            string accessKey,
            string secretKey,
            bool useSSL = true,
            string region = "us-east-1",
            int timeoutSeconds = 30,
            bool skipCertificateValidation = false)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            AccessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
            SecretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            UseSSL = useSSL;
            Region = region ?? "us-east-1";
            TimeoutSeconds = timeoutSeconds;
            SkipCertificateValidation = skipCertificateValidation;
        }

        /// <summary>
        /// Gets the base URL for the MinIO server
        /// </summary>
        public string BaseUrl => $"{(UseSSL ? "https" : "http")}://{Endpoint}";

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        public bool IsValid => 
            !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(AccessKey) &&
            !string.IsNullOrWhiteSpace(SecretKey) &&
            TimeoutSeconds > 0 &&
            MaxConnections > 0 &&
            DefaultChunkSize > 0 &&
            MaxRetries >= 0;

        /// <summary>
        /// Gets validation errors for the configuration
        /// </summary>
        /// <returns>Array of validation error messages</returns>
        public string[] GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Endpoint))
                errors.Add("Endpoint is required");

            if (string.IsNullOrWhiteSpace(AccessKey))
                errors.Add("AccessKey is required");

            if (string.IsNullOrWhiteSpace(SecretKey))
                errors.Add("SecretKey is required");

            if (TimeoutSeconds <= 0)
                errors.Add("TimeoutSeconds must be greater than 0");

            if (MaxConnections <= 0)
                errors.Add("MaxConnections must be greater than 0");

            if (DefaultChunkSize <= 0)
                errors.Add("DefaultChunkSize must be greater than 0");

            if (MaxRetries < 0)
                errors.Add("MaxRetries must be 0 or greater");

            return errors.ToArray();
        }

        /// <summary>
        /// Returns a string representation of the configuration (without sensitive data)
        /// </summary>
        public override string ToString()
        {
            return $"MinIO Configuration - Endpoint: {Endpoint}, UseSSL: {UseSSL}, Region: {Region}, Timeout: {TimeoutSeconds}s";
        }
    }
}
