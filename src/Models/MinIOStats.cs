using System;

namespace PSMinIO.Models
{
    /// <summary>
    /// Represents statistical information about MinIO storage
    /// </summary>
    public class MinIOStats
    {
        /// <summary>
        /// Total number of buckets
        /// </summary>
        public int TotalBuckets { get; set; }

        /// <summary>
        /// Total number of objects across all buckets
        /// </summary>
        public long TotalObjects { get; set; }

        /// <summary>
        /// Total size of all objects in bytes
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// When these statistics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// MinIO server endpoint
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Whether SSL is being used
        /// </summary>
        public bool UseSSL { get; set; }

        /// <summary>
        /// Connection status
        /// </summary>
        public string ConnectionStatus { get; set; } = "Unknown";

        /// <summary>
        /// Creates a new MinIOStats instance
        /// </summary>
        public MinIOStats()
        {
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new MinIOStats instance with specified values
        /// </summary>
        /// <param name="totalBuckets">Total number of buckets</param>
        /// <param name="totalObjects">Total number of objects</param>
        /// <param name="totalSize">Total size in bytes</param>
        /// <param name="endpoint">MinIO endpoint</param>
        /// <param name="useSSL">Whether SSL is used</param>
        public MinIOStats(int totalBuckets, long totalObjects, long totalSize, string endpoint, bool useSSL)
        {
            TotalBuckets = totalBuckets;
            TotalObjects = totalObjects;
            TotalSize = totalSize;
            Endpoint = endpoint ?? string.Empty;
            UseSSL = useSSL;
            LastUpdated = DateTime.UtcNow;
            ConnectionStatus = "Connected";
        }

        /// <summary>
        /// Gets the average object size in bytes
        /// </summary>
        public double AverageObjectSize => TotalObjects > 0 ? (double)TotalSize / TotalObjects : 0;

        /// <summary>
        /// Gets the average objects per bucket
        /// </summary>
        public double AverageObjectsPerBucket => TotalBuckets > 0 ? (double)TotalObjects / TotalBuckets : 0;

        /// <summary>
        /// Returns a string representation of the statistics
        /// </summary>
        public override string ToString()
        {
            return $"MinIO Stats: {TotalBuckets} buckets, {TotalObjects} objects, {Utils.SizeFormatter.FormatBytes(TotalSize)} total";
        }
    }
}
