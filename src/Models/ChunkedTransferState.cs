using System;
using System.Collections.Generic;
using System.Linq;

namespace PSMinIO.Models
{
    /// <summary>
    /// Represents the state of a chunked transfer operation for resume functionality
    /// </summary>
    public class ChunkedTransferState
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Local file path
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Total size of the file/object
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Size of each chunk
        /// </summary>
        public long ChunkSize { get; set; }

        /// <summary>
        /// List of completed chunks
        /// </summary>
        public List<ChunkInfo> CompletedChunks { get; set; } = new List<ChunkInfo>();

        /// <summary>
        /// Last modified time of the source file (for validation)
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// ETag of the object (for validation)
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Transfer operation type
        /// </summary>
        public ChunkedTransferType TransferType { get; set; }

        /// <summary>
        /// When the transfer was started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the transfer state was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Upload ID for multipart uploads (MinIO specific)
        /// </summary>
        public string? UploadId { get; set; }

        /// <summary>
        /// Gets the total number of chunks
        /// </summary>
        public int TotalChunks => (int)Math.Ceiling((double)TotalSize / ChunkSize);

        /// <summary>
        /// Gets the number of completed chunks
        /// </summary>
        public int CompletedChunkCount => CompletedChunks.Count;

        /// <summary>
        /// Gets the total bytes transferred
        /// </summary>
        public long BytesTransferred => CompletedChunks.Sum(c => c.Size);

        /// <summary>
        /// Gets the transfer progress as a percentage
        /// </summary>
        public double ProgressPercentage => TotalSize > 0 ? (double)BytesTransferred / TotalSize * 100 : 0;

        /// <summary>
        /// Checks if the transfer is complete
        /// </summary>
        public bool IsComplete => CompletedChunkCount >= TotalChunks;

        /// <summary>
        /// Gets the next chunk to transfer
        /// </summary>
        /// <returns>Next chunk info or null if complete</returns>
        public ChunkInfo? GetNextChunk()
        {
            for (int i = 0; i < TotalChunks; i++)
            {
                if (!CompletedChunks.Any(c => c.ChunkNumber == i))
                {
                    var startByte = i * ChunkSize;
                    var endByte = Math.Min(startByte + ChunkSize - 1, TotalSize - 1);
                    var size = endByte - startByte + 1;

                    return new ChunkInfo
                    {
                        ChunkNumber = i,
                        StartByte = startByte,
                        EndByte = endByte,
                        Size = size,
                        IsCompleted = false
                    };
                }
            }
            return null;
        }

        /// <summary>
        /// Marks a chunk as completed
        /// </summary>
        /// <param name="chunkInfo">Completed chunk information</param>
        public void MarkChunkCompleted(ChunkInfo chunkInfo)
        {
            chunkInfo.IsCompleted = true;
            chunkInfo.CompletedTime = DateTime.UtcNow;
            
            // Remove any existing entry for this chunk number
            CompletedChunks.RemoveAll(c => c.ChunkNumber == chunkInfo.ChunkNumber);
            
            // Add the completed chunk
            CompletedChunks.Add(chunkInfo);
            
            LastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Information about a single chunk
    /// </summary>
    public class ChunkInfo
    {
        /// <summary>
        /// Chunk number (0-based)
        /// </summary>
        public int ChunkNumber { get; set; }

        /// <summary>
        /// Starting byte position
        /// </summary>
        public long StartByte { get; set; }

        /// <summary>
        /// Ending byte position (inclusive)
        /// </summary>
        public long EndByte { get; set; }

        /// <summary>
        /// Size of the chunk in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// ETag of the uploaded chunk (for uploads)
        /// </summary>
        public string ChunkETag { get; set; } = string.Empty;

        /// <summary>
        /// Whether this chunk has been completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// When this chunk was completed
        /// </summary>
        public DateTime? CompletedTime { get; set; }

        /// <summary>
        /// Number of retry attempts for this chunk
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Last error encountered for this chunk
        /// </summary>
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Type of chunked transfer operation
    /// </summary>
    public enum ChunkedTransferType
    {
        Upload,
        Download
    }
}
