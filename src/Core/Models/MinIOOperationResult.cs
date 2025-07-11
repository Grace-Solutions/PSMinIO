using System;
using PSMinIO.Utils;

namespace PSMinIO.Core.Models
{
    /// <summary>
    /// Base class for MinIO operation results with performance metrics
    /// </summary>
    public abstract class MinIOOperationResult
    {
        /// <summary>
        /// When the operation started
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// When the operation completed
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan? Duration => StartTime.HasValue && EndTime.HasValue 
            ? EndTime.Value - StartTime.Value 
            : null;

        /// <summary>
        /// Duration formatted as a human-readable string
        /// </summary>
        public string? DurationFormatted => Duration.HasValue 
            ? SizeFormatter.FormatDuration(Duration.Value) 
            : null;

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Sets the start time to the current UTC time
        /// </summary>
        public void MarkStarted()
        {
            StartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the end time to the current UTC time
        /// </summary>
        public void MarkCompleted()
        {
            EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the operation as failed with an error message
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public void MarkFailed(string errorMessage)
        {
            EndTime = DateTime.UtcNow;
            Success = false;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Result for transfer operations (upload/download) with speed metrics
    /// </summary>
    public abstract class MinIOTransferResult : MinIOOperationResult
    {
        /// <summary>
        /// Number of bytes transferred
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        /// Total size of the transfer
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Average transfer speed in bytes per second
        /// </summary>
        public double? AverageSpeed => Duration.HasValue && Duration.Value.TotalSeconds > 0 
            ? BytesTransferred / Duration.Value.TotalSeconds 
            : null;

        /// <summary>
        /// Average transfer speed formatted as a human-readable string
        /// </summary>
        public string? AverageSpeedFormatted => AverageSpeed.HasValue 
            ? SizeFormatter.FormatSpeed(AverageSpeed.Value) 
            : null;

        /// <summary>
        /// Bytes transferred formatted as a human-readable string
        /// </summary>
        public string BytesTransferredFormatted => SizeFormatter.FormatBytes(BytesTransferred);

        /// <summary>
        /// Total size formatted as a human-readable string
        /// </summary>
        public string TotalSizeFormatted => SizeFormatter.FormatBytes(TotalSize);

        /// <summary>
        /// Transfer completion percentage
        /// </summary>
        public double PercentComplete => TotalSize > 0 ? (double)BytesTransferred / TotalSize * 100 : 100;
    }

    /// <summary>
    /// Result for upload operations
    /// </summary>
    public class MinIOUploadResult : MinIOTransferResult
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the uploaded object
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// ETag of the uploaded object
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Content type of the uploaded object
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Source file path (if uploaded from file)
        /// </summary>
        public string? SourceFilePath { get; set; }

        /// <summary>
        /// Creates a new MinIOUploadResult
        /// </summary>
        public MinIOUploadResult()
        {
        }

        /// <summary>
        /// Creates a new MinIOUploadResult with specified values
        /// </summary>
        /// <param name="bucketName">Bucket name</param>
        /// <param name="objectName">Object name</param>
        /// <param name="etag">ETag</param>
        /// <param name="totalSize">Total size</param>
        public MinIOUploadResult(string bucketName, string objectName, string etag, long totalSize)
        {
            BucketName = bucketName ?? string.Empty;
            ObjectName = objectName ?? string.Empty;
            ETag = etag ?? string.Empty;
            TotalSize = totalSize;
            BytesTransferred = totalSize; // Assume complete transfer
        }

        /// <summary>
        /// Returns a string representation of the upload result
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "Success" : "Failed";
            var speed = AverageSpeedFormatted ?? "Unknown";
            var duration = DurationFormatted ?? "Unknown";
            
            return $"Upload {status}: {ObjectName} ({TotalSizeFormatted}) in {duration} at {speed}";
        }
    }

    /// <summary>
    /// Result for download operations
    /// </summary>
    public class MinIODownloadResult : MinIOTransferResult
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the downloaded object
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Target file path (if downloaded to file)
        /// </summary>
        public string? TargetFilePath { get; set; }

        /// <summary>
        /// Content type of the downloaded object
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Last modified date of the object
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Creates a new MinIODownloadResult
        /// </summary>
        public MinIODownloadResult()
        {
        }

        /// <summary>
        /// Creates a new MinIODownloadResult with specified values
        /// </summary>
        /// <param name="bucketName">Bucket name</param>
        /// <param name="objectName">Object name</param>
        /// <param name="totalSize">Total size</param>
        public MinIODownloadResult(string bucketName, string objectName, long totalSize)
        {
            BucketName = bucketName ?? string.Empty;
            ObjectName = objectName ?? string.Empty;
            TotalSize = totalSize;
            BytesTransferred = totalSize; // Assume complete transfer
        }

        /// <summary>
        /// Returns a string representation of the download result
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "Success" : "Failed";
            var speed = AverageSpeedFormatted ?? "Unknown";
            var duration = DurationFormatted ?? "Unknown";
            
            return $"Download {status}: {ObjectName} ({TotalSizeFormatted}) in {duration} at {speed}";
        }
    }

    /// <summary>
    /// Result for bucket operations
    /// </summary>
    public class MinIOBucketResult : MinIOOperationResult
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Operation that was performed
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new MinIOBucketResult
        /// </summary>
        public MinIOBucketResult()
        {
        }

        /// <summary>
        /// Creates a new MinIOBucketResult with specified values
        /// </summary>
        /// <param name="bucketName">Bucket name</param>
        /// <param name="operation">Operation performed</param>
        public MinIOBucketResult(string bucketName, string operation)
        {
            BucketName = bucketName ?? string.Empty;
            Operation = operation ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the bucket result
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "Success" : "Failed";
            var duration = DurationFormatted ?? "Unknown";
            
            return $"{Operation} {status}: {BucketName} in {duration}";
        }
    }
}
