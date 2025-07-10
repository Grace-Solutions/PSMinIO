using System;
using System.Collections.Generic;

namespace PSMinIO.Models
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
        /// Creates a MinIOObjectInfo from a Minio.DataModel.Item
        /// </summary>
        /// <param name="item">Minio item object</param>
        /// <param name="bucketName">Name of the bucket containing the object</param>
        /// <returns>MinIOObjectInfo instance</returns>
        public static MinIOObjectInfo FromMinioItem(Minio.DataModel.Item item, string bucketName)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var objectInfo = new MinIOObjectInfo
            {
                Name = item.Key ?? string.Empty,
                Size = (long)(item.Size ?? 0),
                LastModified = item.LastModifiedDateTime ?? DateTime.MinValue,
                ETag = item.ETag ?? string.Empty,
                BucketName = bucketName ?? string.Empty,
                StorageClass = item.StorageClass ?? string.Empty
            };

            // Try to extract version information if available
            // Note: The MinIO .NET SDK Item object may have version properties
            try
            {
                // Use reflection to check for version properties that might exist
                var itemType = item.GetType();

                var versionIdProperty = itemType.GetProperty("VersionId");
                if (versionIdProperty != null)
                {
                    objectInfo.VersionId = versionIdProperty.GetValue(item)?.ToString();
                }

                var isLatestProperty = itemType.GetProperty("IsLatest");
                if (isLatestProperty != null && isLatestProperty.GetValue(item) is bool isLatest)
                {
                    objectInfo.IsLatestVersion = isLatest;
                }

                var isDeleteMarkerProperty = itemType.GetProperty("IsDeleteMarker");
                if (isDeleteMarkerProperty != null && isDeleteMarkerProperty.GetValue(item) is bool isDeleteMarker)
                {
                    objectInfo.IsDeleteMarker = isDeleteMarker;
                }
            }
            catch
            {
                // If reflection fails, just continue without version information
                // This ensures compatibility even if the SDK doesn't have these properties
            }

            // Copy metadata if available
            if (item.MetaData != null)
            {
                foreach (var kvp in item.MetaData)
                {
                    objectInfo.Metadata[kvp.Key] = kvp.Value;
                }
            }

            return objectInfo;
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
            return $"Object: {Name} ({Size} bytes, Modified: {LastModified:yyyy-MM-dd HH:mm:ss})";
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
            return HashCode.Combine(Name, BucketName?.ToLowerInvariant());
        }
    }
}
