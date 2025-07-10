using System;
using PSMinIO.Utils;

namespace PSMinIO.Models
{
    /// <summary>
    /// Represents an active MinIO connection with configuration and client wrapper
    /// </summary>
    public class MinIOConnection : IDisposable
    {
        private MinIOClientWrapper? _client;
        private bool _disposed = false;

        /// <summary>
        /// Gets the configuration for this connection
        /// </summary>
        public MinIOConfiguration Configuration { get; }

        /// <summary>
        /// Gets the MinIO client wrapper instance
        /// </summary>
        public MinIOClientWrapper Client
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MinIOConnection));

                if (_client == null)
                {
                    _client = new MinIOClientWrapper(Configuration);
                }
                return _client;
            }
        }

        /// <summary>
        /// Gets whether this connection is valid and ready to use
        /// </summary>
        public bool IsValid => Configuration.IsValid && !_disposed;

        /// <summary>
        /// Gets the connection status
        /// </summary>
        public string Status
        {
            get
            {
                if (_disposed) return "Disposed";
                if (!Configuration.IsValid) return "Invalid Configuration";
                return "Ready";
            }
        }

        /// <summary>
        /// Gets the endpoint URL for display purposes
        /// </summary>
        public string EndpointUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Configuration.Endpoint))
                    return "Not Set";

                var protocol = Configuration.UseSSL ? "https" : "http";
                return $"{protocol}://{Configuration.Endpoint}";
            }
        }

        /// <summary>
        /// Gets when this connection was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Creates a new MinIOConnection instance
        /// </summary>
        /// <param name="configuration">MinIO configuration</param>
        public MinIOConnection(MinIOConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Tests the connection by attempting to list buckets
        /// </summary>
        /// <returns>Connection test result</returns>
        public ConnectionTestResult TestConnection()
        {
            if (_disposed)
                return new ConnectionTestResult(false, "Connection is disposed");

            if (!Configuration.IsValid)
                return new ConnectionTestResult(false, "Configuration is invalid");

            try
            {
                var startTime = DateTime.UtcNow;
                var buckets = Client.ListBuckets();
                var duration = DateTime.UtcNow - startTime;

                return new ConnectionTestResult(true, "Connection successful")
                {
                    BucketCount = buckets.Count,
                    ResponseTime = duration,
                    TestedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult(false, ex.Message)
                {
                    TestedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Returns a string representation of the connection
        /// </summary>
        public override string ToString()
        {
            return $"MinIO Connection: {EndpointUrl} (Status: {Status})";
        }

        /// <summary>
        /// Disposes the connection and underlying client
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">Whether disposing from Dispose method</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _client?.Dispose();
                _client = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the result of a connection test
    /// </summary>
    public class ConnectionTestResult
    {
        /// <summary>
        /// Whether the connection test was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Message describing the test result
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Number of buckets found (if successful)
        /// </summary>
        public int? BucketCount { get; set; }

        /// <summary>
        /// Response time for the test
        /// </summary>
        public TimeSpan? ResponseTime { get; set; }

        /// <summary>
        /// When the test was performed
        /// </summary>
        public DateTime? TestedAt { get; set; }

        /// <summary>
        /// Creates a new ConnectionTestResult
        /// </summary>
        /// <param name="success">Whether the test was successful</param>
        /// <param name="message">Test result message</param>
        public ConnectionTestResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the test result
        /// </summary>
        public override string ToString()
        {
            var result = Success ? "SUCCESS" : "FAILED";
            var details = ResponseTime.HasValue ? $" ({ResponseTime.Value.TotalMilliseconds:F0}ms)" : "";
            return $"{result}: {Message}{details}";
        }
    }
}
