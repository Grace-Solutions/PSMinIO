using System;
using System.IO;
using PSMinIO.Utils;

namespace PSMinIO.Models
{
    /// <summary>
    /// Represents the result of a MinIO download operation with timing and speed information
    /// </summary>
    public class MinIODownloadResult
    {
        /// <summary>
        /// The downloaded file information
        /// </summary>
        public FileInfo File { get; set; }

        /// <summary>
        /// Name of the bucket the object was downloaded from
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// Name of the object that was downloaded
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// Size of the downloaded file in bytes
        /// </summary>
        public long Size => File?.Length ?? 0;

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
        /// Full path to the downloaded file
        /// </summary>
        public string FullName => File?.FullName ?? string.Empty;

        /// <summary>
        /// Name of the downloaded file
        /// </summary>
        public string Name => File?.Name ?? string.Empty;

        /// <summary>
        /// Directory containing the downloaded file
        /// </summary>
        public string? DirectoryName => File?.DirectoryName;

        /// <summary>
        /// Creates a new MinIODownloadResult
        /// </summary>
        /// <param name="file">Downloaded file information</param>
        /// <param name="bucketName">Source bucket name</param>
        /// <param name="objectName">Source object name</param>
        /// <param name="startTime">Transfer start time</param>
        /// <param name="completionTime">Transfer completion time</param>
        public MinIODownloadResult(FileInfo file, string bucketName, string objectName, 
            DateTime? startTime = null, DateTime? completionTime = null)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            ObjectName = objectName ?? throw new ArgumentNullException(nameof(objectName));
            StartTime = startTime;
            CompletionTime = completionTime;
        }

        /// <summary>
        /// Returns a string representation of the download result
        /// </summary>
        public override string ToString()
        {
            var duration = Duration?.ToString(@"hh\:mm\:ss\.fff") ?? "Unknown";
            var speed = AverageSpeedFormatted ?? "Unknown";
            return $"{BucketName}/{ObjectName} -> {FullName} ({SizeFormatter.FormatBytes(Size)}, {duration}, {speed})";
        }
    }
}
