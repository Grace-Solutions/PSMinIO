using System;
using PSMinIO.Core.S3;

namespace PSMinIO.Core
{
    /// <summary>
    /// Represents an active MinIO connection with configuration and S3 client
    /// </summary>
    public class MinIOConnection : IDisposable
    {
        private MinIOS3Client? _s3Client;
        private bool _disposed = false;

        /// <summary>
        /// Gets the configuration for this connection
        /// </summary>
        public MinIOConfiguration Configuration { get; }

        /// <summary>
        /// Gets the connection status
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// Gets the time when the connection was established
        /// </summary>
        public DateTime? ConnectedAt { get; private set; }

        /// <summary>
        /// Gets the last activity time
        /// </summary>
        public DateTime? LastActivity { get; private set; }

        /// <summary>
        /// Gets the MinIO S3 client instance
        /// </summary>
        public MinIOS3Client S3Client
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MinIOConnection));

                if (_s3Client == null)
                {
                    _s3Client = new MinIOS3Client(Configuration);
                    Status = ConnectionStatus.Connected;
                    ConnectedAt = DateTime.UtcNow;
                }

                LastActivity = DateTime.UtcNow;
                return _s3Client;
            }
        }

        /// <summary>
        /// Gets whether the connection is valid and ready to use
        /// </summary>
        public bool IsValid => !_disposed && Configuration.IsValid && Status != ConnectionStatus.Failed;

        /// <summary>
        /// Creates a new MinIOConnection instance
        /// </summary>
        /// <param name="configuration">MinIO configuration</param>
        public MinIOConnection(MinIOConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (!Configuration.IsValid)
            {
                var errors = string.Join(", ", Configuration.GetValidationErrors());
                throw new ArgumentException($"Invalid MinIO configuration: {errors}", nameof(configuration));
            }
        }

        /// <summary>
        /// Tests the connection by attempting to list buckets
        /// </summary>
        /// <returns>True if connection test succeeds</returns>
        public bool TestConnection()
        {
            if (_disposed)
                return false;

            try
            {
                // Attempt to list buckets as a connection test
                var buckets = S3Client.ListBuckets();
                Status = ConnectionStatus.Connected;
                return true;
            }
            catch (Exception)
            {
                Status = ConnectionStatus.Failed;
                return false;
            }
        }

        /// <summary>
        /// Gets connection statistics
        /// </summary>
        /// <returns>Connection statistics</returns>
        public ConnectionStats GetStats()
        {
            return new ConnectionStats
            {
                Status = Status,
                ConnectedAt = ConnectedAt,
                LastActivity = LastActivity,
                Uptime = ConnectedAt.HasValue ? DateTime.UtcNow - ConnectedAt.Value : null,
                Configuration = Configuration
            };
        }

        /// <summary>
        /// Returns a string representation of the connection
        /// </summary>
        public override string ToString()
        {
            var statusStr = Status switch
            {
                ConnectionStatus.Connected => "Connected",
                ConnectionStatus.Disconnected => "Disconnected",
                ConnectionStatus.Failed => "Failed",
                _ => "Unknown"
            };

            return $"MinIO Connection - {Configuration.Endpoint} ({statusStr})";
        }

        /// <summary>
        /// Disposes the connection and underlying resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _s3Client?.Dispose();
                Status = ConnectionStatus.Disconnected;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Connection status enumeration
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Connection is not established
        /// </summary>
        Disconnected,

        /// <summary>
        /// Connection is active and working
        /// </summary>
        Connected,

        /// <summary>
        /// Connection failed or encountered an error
        /// </summary>
        Failed
    }

    /// <summary>
    /// Connection statistics
    /// </summary>
    public class ConnectionStats
    {
        /// <summary>
        /// Current connection status
        /// </summary>
        public ConnectionStatus Status { get; set; }

        /// <summary>
        /// When the connection was established
        /// </summary>
        public DateTime? ConnectedAt { get; set; }

        /// <summary>
        /// Last activity time
        /// </summary>
        public DateTime? LastActivity { get; set; }

        /// <summary>
        /// Connection uptime
        /// </summary>
        public TimeSpan? Uptime { get; set; }

        /// <summary>
        /// Connection configuration (without sensitive data)
        /// </summary>
        public MinIOConfiguration Configuration { get; set; } = null!;

        /// <summary>
        /// Returns a string representation of the stats
        /// </summary>
        public override string ToString()
        {
            var uptimeStr = Uptime.HasValue ? $", Uptime: {Uptime.Value:hh\\:mm\\:ss}" : "";
            return $"Status: {Status}, Endpoint: {Configuration.Endpoint}{uptimeStr}";
        }
    }
}
