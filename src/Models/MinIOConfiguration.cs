using System;

namespace PSMinIO.Models
{
    /// <summary>
    /// Configuration settings for MinIO client connections
    /// </summary>
    public class MinIOConfiguration
    {

        /// <summary>
        /// MinIO server endpoint (without protocol)
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
        /// Whether to skip SSL certificate validation
        /// </summary>
        public bool SkipCertificateValidation { get; set; }

        /// <summary>
        /// Whether the configuration is valid for creating a client
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Endpoint) && 
                              !string.IsNullOrWhiteSpace(AccessKey) && 
                              !string.IsNullOrWhiteSpace(SecretKey);



        /// <summary>
        /// Creates a new MinIOConfiguration instance
        /// </summary>
        public MinIOConfiguration()
        {
        }

        /// <summary>
        /// Creates a new MinIOConfiguration instance with specified values
        /// </summary>
        /// <param name="endpoint">MinIO endpoint</param>
        /// <param name="accessKey">Access key</param>
        /// <param name="secretKey">Secret key</param>
        /// <param name="useSSL">Use SSL flag</param>
        /// <param name="region">Optional region</param>
        /// <param name="timeoutSeconds">Connection timeout</param>
        /// <param name="skipCertificateValidation">Whether to skip SSL certificate validation</param>
        public MinIOConfiguration(string endpoint, string accessKey, string secretKey,
            bool useSSL = true, string region = "us-east-1", int timeoutSeconds = 30, bool skipCertificateValidation = false)
        {
            Endpoint = endpoint?.Trim() ?? string.Empty;
            AccessKey = accessKey?.Trim() ?? string.Empty;
            SecretKey = secretKey?.Trim() ?? string.Empty;
            UseSSL = useSSL;
            Region = region?.Trim() ?? "us-east-1";
            TimeoutSeconds = timeoutSeconds;
            SkipCertificateValidation = skipCertificateValidation;
        }
    }
}
