using System;

namespace PSMinIO.Models
{
    /// <summary>
    /// Represents information about a MinIO bucket
    /// </summary>
    public class MinIOBucketInfo
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Creation date of the bucket
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Region where the bucket is located
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Total size of all objects in the bucket (optional, calculated separately)
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// Number of objects in the bucket (optional, calculated separately)
        /// </summary>
        public long? ObjectCount { get; set; }

        /// <summary>
        /// Creates a new MinIOBucketInfo instance
        /// </summary>
        public MinIOBucketInfo()
        {
        }

        /// <summary>
        /// Creates a new MinIOBucketInfo instance with specified values
        /// </summary>
        /// <param name="name">Bucket name</param>
        /// <param name="created">Creation date</param>
        /// <param name="region">Bucket region</param>
        public MinIOBucketInfo(string name, DateTime created, string region = "")
        {
            Name = name ?? string.Empty;
            Created = created;
            Region = region ?? string.Empty;
        }

        /// <summary>
        /// Creates a MinIOBucketInfo from a Minio.DataModel.Bucket
        /// </summary>
        /// <param name="bucket">Minio bucket object</param>
        /// <returns>MinIOBucketInfo instance</returns>
        public static MinIOBucketInfo FromMinioBucket(Minio.DataModel.Bucket bucket)
        {
            if (bucket == null)
                throw new ArgumentNullException(nameof(bucket));

            return new MinIOBucketInfo
            {
                Name = bucket.Name ?? string.Empty,
                Created = bucket.CreationDate,
                Region = string.Empty // Region is not available in basic bucket info
            };
        }

        /// <summary>
        /// Returns a string representation of the bucket info
        /// </summary>
        public override string ToString()
        {
            return $"Bucket: {Name} (Created: {Created:yyyy-MM-dd HH:mm:ss})";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is MinIOBucketInfo other)
            {
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for the current object
        /// </summary>
        public override int GetHashCode()
        {
            return Name?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
}
