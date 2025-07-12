using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PSMinIO.Core.Http;
using PSMinIO.Utils;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// Manages multipart downloads with parallel processing and resume capability
    /// </summary>
    public class MultipartDownloadManager
    {
        private readonly MinIOHttpClient _httpClient;
        private readonly ThreadSafeProgressCollector _progressCollector;
        private readonly int _maxParallelDownloads;
        private readonly long _defaultChunkSize;

        public MultipartDownloadManager(MinIOHttpClient httpClient, ThreadSafeProgressCollector progressCollector,
            int maxParallelDownloads = 4, long defaultChunkSize = 32 * 1024 * 1024) // 32MB default
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _progressCollector = progressCollector ?? throw new ArgumentNullException(nameof(progressCollector));
            _maxParallelDownloads = Math.Max(1, Math.Min(maxParallelDownloads, 8)); // Limit to 1-8
            _defaultChunkSize = Math.Max(1024 * 1024, defaultChunkSize); // Minimum 1MB
        }

        /// <summary>
        /// Downloads a file using multipart download with parallel processing
        /// </summary>
        public MultipartDownloadResult DownloadFile(string bucketName, string objectName, FileInfo destinationFile,
            long? chunkSize = null, bool resumeIfExists = true)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            if (destinationFile == null) throw new ArgumentNullException(nameof(destinationFile));

            var effectiveChunkSize = chunkSize ?? _defaultChunkSize;
            var startTime = DateTime.UtcNow;

            _progressCollector.QueueVerboseMessage("Starting multipart download: {0}/{1} -> {2}",
                bucketName, objectName, destinationFile.FullName);

            try
            {
                // Get object metadata to determine size
                var objectInfo = GetObjectInfo(bucketName, objectName);
                var totalSize = objectInfo.Size;
                var totalChunks = (int)Math.Ceiling((double)totalSize / effectiveChunkSize);

                _progressCollector.QueueVerboseMessage("Object size: {0}, Chunk size: {1}, Total chunks: {2}", 
                    SizeFormatter.FormatBytes(totalSize), SizeFormatter.FormatBytes(effectiveChunkSize), totalChunks);

                // Check for resume capability
                var resumeOffset = 0L;
                if (resumeIfExists && destinationFile.Exists)
                {
                    resumeOffset = destinationFile.Length;
                    if (resumeOffset >= totalSize)
                    {
                        _progressCollector.QueueVerboseMessage("File already completely downloaded");
                        return new MultipartDownloadResult
                        {
                            BucketName = bucketName,
                            ObjectName = objectName,
                            DestinationPath = destinationFile.FullName,
                            TotalSize = totalSize,
                            DownloadedSize = totalSize,
                            ChunkSize = effectiveChunkSize,
                            TotalChunks = totalChunks,
                            Duration = TimeSpan.Zero,
                            IsCompleted = true,
                            WasResumed = true
                        };
                    }
                    _progressCollector.QueueVerboseMessage("Resuming download from offset: {0}", 
                        SizeFormatter.FormatBytes(resumeOffset));
                }

                // Ensure destination directory exists
                if (destinationFile.Directory != null && !destinationFile.Directory.Exists)
                {
                    destinationFile.Directory.Create();
                }

                // Create or open file for writing
                using var fileStream = resumeIfExists && destinationFile.Exists 
                    ? new FileStream(destinationFile.FullName, FileMode.Append, FileAccess.Write)
                    : new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write);

                // Calculate chunks to download
                var remainingSize = totalSize - resumeOffset;
                var startChunk = (int)(resumeOffset / effectiveChunkSize);
                var chunksToDownload = (int)Math.Ceiling((double)remainingSize / effectiveChunkSize);

                var downloadedBytes = resumeOffset;
                var chunks = new ConcurrentDictionary<int, DownloadPartInfo>();
                var downloadTasks = new List<Task>();
                var semaphore = new SemaphoreSlim(_maxParallelDownloads, _maxParallelDownloads);

                // Download chunks in parallel
                for (int chunkIndex = startChunk; chunkIndex < startChunk + chunksToDownload; chunkIndex++)
                {
                    var chunkNum = chunkIndex;
                    var chunkOffset = chunkNum * effectiveChunkSize;
                    var chunkEnd = Math.Min(chunkOffset + effectiveChunkSize - 1, totalSize - 1);
                    var chunkActualSize = chunkEnd - chunkOffset + 1;

                    var downloadTask = Task.Run(() =>
                    {
                        semaphore.Wait();
                        try
                        {
                            var chunkData = DownloadChunk(bucketName, objectName, chunkOffset, chunkEnd);
                            
                            chunks.TryAdd(chunkNum, new DownloadPartInfo
                            {
                                PartNumber = chunkNum,
                                Offset = chunkOffset,
                                Size = chunkData.Length,
                                Data = chunkData
                            });

                            // Update progress
                            var currentDownloaded = Interlocked.Add(ref downloadedBytes, chunkData.Length);
                            var progress = (double)currentDownloaded / totalSize * 100;
                            var elapsed = DateTime.UtcNow - startTime;
                            var speed = elapsed.TotalSeconds > 0 ? (currentDownloaded - resumeOffset) / elapsed.TotalSeconds : 0;

                            _progressCollector.QueueProgressUpdate(2, "Multipart Download",
                                $"Part {chunkNum - startChunk + 1}/{chunksToDownload} - {SizeFormatter.FormatBytes(currentDownloaded)}/{SizeFormatter.FormatBytes(totalSize)} at {SizeFormatter.FormatSpeed(speed)}",
                                (int)progress);

                            _progressCollector.QueueVerboseMessage("Downloaded chunk {0}/{1} ({2})", 
                                chunkNum - startChunk + 1, chunksToDownload, SizeFormatter.FormatBytes(chunkData.Length));
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    downloadTasks.Add(downloadTask);
                }

                // Wait for all downloads to complete
                Task.WaitAll(downloadTasks.ToArray());

                // Write parts to file in order
                var sortedParts = chunks.Values.OrderBy(c => c.PartNumber).ToList();
                foreach (var part in sortedParts)
                {
                    fileStream.Write(part.Data, 0, part.Data.Length);
                }

                var totalDuration = DateTime.UtcNow - startTime;
                var averageSpeed = totalDuration.TotalSeconds > 0 ? remainingSize / totalDuration.TotalSeconds : 0;

                _progressCollector.QueueVerboseMessage("Multipart download completed successfully");
                _progressCollector.QueueVerboseMessage("Total time: {0}, Average speed: {1}",
                    SizeFormatter.FormatDuration(totalDuration), SizeFormatter.FormatSpeed(averageSpeed));

                _progressCollector.QueueProgressCompletion(2, "Multipart Download");

                return new MultipartDownloadResult
                {
                    BucketName = bucketName,
                    ObjectName = objectName,
                    DestinationPath = destinationFile.FullName,
                    TotalSize = totalSize,
                    DownloadedSize = totalSize,
                    ChunkSize = effectiveChunkSize,
                    TotalChunks = totalChunks,
                    CompletedParts = sortedParts,
                    Duration = totalDuration,
                    AverageSpeed = averageSpeed,
                    IsCompleted = true,
                    WasResumed = resumeOffset > 0
                };
            }
            catch (Exception ex)
            {
                _progressCollector.QueueVerboseMessage("Multipart download failed: {0}", ex.Message);

                var partialDuration = DateTime.UtcNow - startTime;

                return new MultipartDownloadResult
                {
                    BucketName = bucketName,
                    ObjectName = objectName,
                    DestinationPath = destinationFile.FullName,
                    ChunkSize = effectiveChunkSize,
                    Duration = partialDuration,
                    IsCompleted = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets object information including size
        /// </summary>
        private ObjectInfo GetObjectInfo(string bucketName, string objectName)
        {
            var response = _httpClient.ExecuteRequest(HttpMethod.Head, $"/{bucketName}/{objectName}");
            
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var lastModified = response.Content.Headers.LastModified?.DateTime ?? DateTime.MinValue;
            var etag = response.Headers.ETag?.Tag?.Trim('"') ?? "";

            return new ObjectInfo
            {
                Name = objectName,
                Size = contentLength,
                LastModified = lastModified,
                ETag = etag
            };
        }

        /// <summary>
        /// Downloads a specific part using range request
        /// </summary>
        private byte[] DownloadChunk(string bucketName, string objectName, long startByte, long endByte)
        {
            var headers = new Dictionary<string, string>
            {
                { "Range", $"bytes={startByte}-{endByte}" }
            };

            var response = _httpClient.ExecuteRequest(HttpMethod.Get, $"/{bucketName}/{objectName}",
                headers: headers);

            return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Information about a downloaded part
    /// </summary>
    public class DownloadPartInfo
    {
        public int PartNumber { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Result of multipart download operation
    /// </summary>
    public class MultipartDownloadResult
    {
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public long ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public List<DownloadPartInfo> CompletedParts { get; set; } = new List<DownloadPartInfo>();
        public TimeSpan Duration { get; set; }
        public double AverageSpeed { get; set; }
        public bool IsCompleted { get; set; }
        public bool WasResumed { get; set; }
        public string? Error { get; set; }
        public double CompletionPercentage => TotalSize > 0 ? (double)DownloadedSize / TotalSize * 100 : 0;
    }

    /// <summary>
    /// Basic object information
    /// </summary>
    public class ObjectInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string ETag { get; set; } = string.Empty;
    }
}
