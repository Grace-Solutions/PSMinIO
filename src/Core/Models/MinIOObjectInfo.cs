using System;
using System.Collections.Generic;
using PSMinIO.Utils;

namespace PSMinIO.Core.Models
{
    /// <summary>
    /// Represents information about a MinIO object
    /// </summary>
    public class MinIOObjectInfo
    {
        /// <summary>
        /// Name/key of the object
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Size of the object in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last modified date of the object
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// ETag of the object
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Content type of the object
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Bucket name containing this object
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a directory/prefix (ends with /)
        /// </summary>
        public bool IsDirectory => Name.EndsWith("/");

        /// <summary>
        /// Object metadata/user-defined metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Storage class of the object
        /// </summary>
        public string StorageClass { get; set; } = string.Empty;

        /// <summary>
        /// Version ID of the object (for versioned buckets)
        /// </summary>
        public string? VersionId { get; set; }

        /// <summary>
        /// Whether this object is the latest version
        /// </summary>
        public bool IsLatestVersion { get; set; } = true;

        /// <summary>
        /// Whether this object is a delete marker
        /// </summary>
        public bool IsDeleteMarker { get; set; } = false;

        /// <summary>
        /// Presigned URL for accessing this object (if generated)
        /// </summary>
        public string? PresignedUrl { get; set; }

        /// <summary>
        /// Expiration time for the presigned URL (if generated)
        /// </summary>
        public DateTime? PresignedUrlExpiration { get; set; }

        /// <summary>
        /// Transfer start time
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Transfer completion time
        /// </summary>
        public DateTime? CompletionTime { get; set; }

        /// <summary>
        /// Transfer duration
        /// </summary>
        public TimeSpan? Duration => StartTime.HasValue && CompletionTime.HasValue ?
            CompletionTime.Value - StartTime.Value : null;

        /// <summary>
        /// Average transfer speed in bytes per second
        /// </summary>
        public double? AverageSpeed => Duration.HasValue && Duration.Value.TotalSeconds > 0 ?
            Size / Duration.Value.TotalSeconds : null;

        /// <summary>
        /// Average transfer speed formatted as string (e.g., "15.2 MB/s")
        /// </summary>
        public string? AverageSpeedFormatted => AverageSpeed.HasValue ?
            $"{SizeFormatter.FormatBytes((long)AverageSpeed.Value)}/s" : null;

        /// <summary>
        /// Creates a new MinIOObjectInfo instance
        /// </summary>
        public MinIOObjectInfo()
        {
        }

        /// <summary>
        /// Creates a new MinIOObjectInfo instance with specified values
        /// </summary>
        /// <param name="name">Object name</param>
        /// <param name="size">Object size</param>
        /// <param name="lastModified">Last modified date</param>
        /// <param name="etag">ETag</param>
        /// <param name="bucketName">Bucket name</param>
        public MinIOObjectInfo(string name, long size, DateTime lastModified, string etag, string bucketName)
        {
            Name = name ?? string.Empty;
            Size = size;
            LastModified = lastModified;
            ETag = etag ?? string.Empty;
            BucketName = bucketName ?? string.Empty;
        }

        /// <summary>
        /// Gets the file extension of the object
        /// </summary>
        public string GetFileExtension()
        {
            if (IsDirectory || string.IsNullOrEmpty(Name))
                return string.Empty;

            var lastDotIndex = Name.LastIndexOf('.');
            if (lastDotIndex >= 0 && lastDotIndex < Name.Length - 1)
            {
                return Name.Substring(lastDotIndex);
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the directory path of the object (everything before the last /)
        /// </summary>
        public string GetDirectoryPath()
        {
            if (string.IsNullOrEmpty(Name))
                return string.Empty;

            var lastSlashIndex = Name.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                return Name.Substring(0, lastSlashIndex + 1);
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the file name without the directory path
        /// </summary>
        public string GetFileName()
        {
            if (string.IsNullOrEmpty(Name) || IsDirectory)
                return Name;

            var lastSlashIndex = Name.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < Name.Length - 1)
            {
                return Name.Substring(lastSlashIndex + 1);
            }

            return Name;
        }

        /// <summary>
        /// Returns a string representation of the object info
        /// </summary>
        public override string ToString()
        {
            return $"Object: {Name} ({SizeFormatter.FormatBytes(Size)}, Modified: {LastModified:yyyy-MM-dd HH:mm:ss})";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is MinIOObjectInfo other)
            {
                return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                       string.Equals(BucketName, other.BucketName, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for the current object
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                hash = hash * 23 + (BucketName?.ToLowerInvariant()?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
