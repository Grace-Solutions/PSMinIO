using System;
using System.Collections.Generic;

namespace PSMinIO.Core.Models
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
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Region where the bucket is located
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Bucket policy (if retrieved)
        /// </summary>
        public string? Policy { get; set; }

        /// <summary>
        /// Bucket versioning status
        /// </summary>
        public BucketVersioningStatus VersioningStatus { get; set; } = BucketVersioningStatus.Unversioned;

        /// <summary>
        /// Bucket encryption configuration
        /// </summary>
        public BucketEncryptionInfo? Encryption { get; set; }

        /// <summary>
        /// Bucket tags
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Number of objects in the bucket (if counted)
        /// </summary>
        public long? ObjectCount { get; set; }

        /// <summary>
        /// Total size of all objects in the bucket (if calculated)
        /// </summary>
        public long? TotalSize { get; set; }

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
        /// <param name="creationDate">Creation date</param>
        /// <param name="region">Bucket region</param>
        public MinIOBucketInfo(string name, DateTime creationDate, string region = "")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            CreationDate = creationDate;
            Region = region ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the bucket info
        /// </summary>
        public override string ToString()
        {
            return $"Bucket: {Name} (Created: {CreationDate:yyyy-MM-dd HH:mm:ss}, Region: {Region})";
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
            return Name?.ToLowerInvariant()?.GetHashCode() ?? 0;
        }
    }

    /// <summary>
    /// Bucket versioning status
    /// </summary>
    public enum BucketVersioningStatus
    {
        /// <summary>
        /// Versioning is not enabled
        /// </summary>
        Unversioned,

        /// <summary>
        /// Versioning is enabled
        /// </summary>
        Enabled,

        /// <summary>
        /// Versioning is suspended
        /// </summary>
        Suspended
    }

    /// <summary>
    /// Bucket encryption information
    /// </summary>
    public class BucketEncryptionInfo
    {
        /// <summary>
        /// Encryption algorithm (e.g., "AES256", "aws:kms")
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;

        /// <summary>
        /// KMS key ID (for KMS encryption)
        /// </summary>
        public string? KmsKeyId { get; set; }

        /// <summary>
        /// Whether encryption is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Creates a new BucketEncryptionInfo instance
        /// </summary>
        public BucketEncryptionInfo()
        {
        }

        /// <summary>
        /// Creates a new BucketEncryptionInfo instance with specified values
        /// </summary>
        /// <param name="algorithm">Encryption algorithm</param>
        /// <param name="isEnabled">Whether encryption is enabled</param>
        /// <param name="kmsKeyId">Optional KMS key ID</param>
        public BucketEncryptionInfo(string algorithm, bool isEnabled, string? kmsKeyId = null)
        {
            Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            IsEnabled = isEnabled;
            KmsKeyId = kmsKeyId;
        }

        /// <summary>
        /// Returns a string representation of the encryption info
        /// </summary>
        public override string ToString()
        {
            return $"Encryption: {Algorithm} (Enabled: {IsEnabled})";
        }
    }
}
