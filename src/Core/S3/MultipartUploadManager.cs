using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PSMinIO.Core.Http;
using PSMinIO.Utils;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// Manages multipart uploads with parallel processing and resume capability
    /// </summary>
    public class MultipartUploadManager
    {
        private readonly MinIOHttpClient _httpClient;
        private readonly ThreadSafeProgressCollector _progressCollector;
        private readonly int _maxParallelUploads;
        private readonly long _defaultChunkSize;

        public MultipartUploadManager(MinIOHttpClient httpClient, ThreadSafeProgressCollector progressCollector, 
            int maxParallelUploads = 4, long defaultChunkSize = 64 * 1024 * 1024) // 64MB default
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _progressCollector = progressCollector ?? throw new ArgumentNullException(nameof(progressCollector));
            _maxParallelUploads = Math.Max(1, Math.Min(maxParallelUploads, 10)); // Limit to 1-10
            _defaultChunkSize = Math.Max(5 * 1024 * 1024, defaultChunkSize); // Minimum 5MB for S3 compatibility
        }

        /// <summary>
        /// Uploads a file using multipart upload with parallel processing
        /// </summary>
        public MultipartUploadResult UploadFile(string bucketName, string objectName, FileInfo fileInfo, 
            Dictionary<string, string>? metadata = null, long? chunkSize = null, 
            string? resumeUploadId = null, List<PartInfo>? completedParts = null)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            if (string.IsNullOrEmpty(objectName)) throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            if (fileInfo == null || !fileInfo.Exists) throw new ArgumentException("File must exist", nameof(fileInfo));

            var effectiveChunkSize = chunkSize ?? _defaultChunkSize;
            var totalSize = fileInfo.Length;
            var totalParts = (int)Math.Ceiling((double)totalSize / effectiveChunkSize);

            _progressCollector.QueueVerboseMessage("Starting multipart upload: {0} -> {1}/{2}",
                fileInfo.Name, bucketName, objectName);
            _progressCollector.QueueVerboseMessage("File size: {0}, Chunk size: {1}, Total parts: {2}",
                SizeFormatter.FormatBytes(totalSize), SizeFormatter.FormatBytes(effectiveChunkSize), totalParts);

            // Initialize 3-layer progress tracking
            // Layer 1: Collection Progress (if multiple files)
            _progressCollector.QueueProgressUpdate(1, "Multipart Upload Collection",
                $"Processing {fileInfo.Name}", 0);

            // Layer 2: File Progress
            _progressCollector.QueueProgressUpdate(2, "File Upload",
                $"Uploading {fileInfo.Name} - {SizeFormatter.FormatBytesIntelligent(0, totalSize)}", 0, 1);

            // Process initial progress updates
            _progressCollector.ProcessQueuedUpdates();

            // Log chunk generation
            _progressCollector.QueueVerboseMessage("Generating {0} chunks for upload - File size: {1}, Chunk size: {2}",
                totalParts, SizeFormatter.FormatBytes(totalSize), SizeFormatter.FormatBytes(effectiveChunkSize));

            var uploadId = resumeUploadId;
            var parts = new ConcurrentDictionary<int, PartInfo>();
            var operationStartTime = DateTime.UtcNow;

            // Add completed parts if resuming
            if (completedParts != null)
            {
                foreach (var part in completedParts)
                {
                    parts.TryAdd(part.PartNumber, part);
                }
                _progressCollector.QueueVerboseMessage("Resuming upload with {0} completed parts", completedParts.Count);
            }

            try
            {
                // Initialize multipart upload if not resuming
                if (string.IsNullOrEmpty(uploadId))
                {
                    uploadId = InitiateMultipartUpload(bucketName, objectName, metadata);
                    _progressCollector.QueueVerboseMessage("Initiated multipart upload - Upload ID: {0}", uploadId!);
                }
                else
                {
                    _progressCollector.QueueVerboseMessage("Resuming multipart upload - Upload ID: {0}", uploadId!);
                }

                // Log upload configuration
                _progressCollector.QueueVerboseMessage("Upload configuration - Chunk size: {0}, Total parts: {1}, Max parallel: {2}",
                    SizeFormatter.FormatBytes(effectiveChunkSize), totalParts, _maxParallelUploads);

                // Process verbose messages and progress updates
                _progressCollector.ProcessQueuedUpdates();

                // Upload parts in parallel
                var uploadTasks = new List<Task>();
                var semaphore = new SemaphoreSlim(_maxParallelUploads, _maxParallelUploads);
                var uploadedBytes = completedParts?.Sum(p => p.Size) ?? 0;

                for (int partNumber = 1; partNumber <= totalParts; partNumber++)
                {
                    // Skip already completed parts
                    if (parts.ContainsKey(partNumber))
                    {
                        continue;
                    }

                    var partNum = partNumber;
                    var partOffset = (partNum - 1) * effectiveChunkSize;
                    var partSize = Math.Min(effectiveChunkSize, totalSize - partOffset);

                    var uploadTask = Task.Run(() =>
                    {
                        semaphore.Wait();
                        try
                        {
                            // Create part info with initial status
                            var partInfo = new PartInfo
                            {
                                PartNumber = partNum,
                                Size = partSize,
                                Offset = partOffset,
                                Status = PartStatus.Queued,
                                StartTime = DateTime.UtcNow
                            };
                            parts.TryAdd(partNum, partInfo);

                            // Log chunk start for all chunks (provides better visibility)
                            _progressCollector.QueueVerboseMessage("Starting upload of part {0}/{1} ({2})",
                                partNum, totalParts, SizeFormatter.FormatBytes(partSize));

                            // Update status to transferring
                            partInfo.Status = PartStatus.Transferring;

                            var uploadedPart = UploadPart(bucketName, objectName, uploadId!, fileInfo,
                                partNum, partOffset, partSize, totalParts);

                            // Update with completed info
                            partInfo.ETag = uploadedPart.ETag;
                            partInfo.MD5Hash = uploadedPart.MD5Hash;
                            partInfo.Status = PartStatus.Completed;
                            partInfo.CompletionTime = DateTime.UtcNow;
                            
                            // Update progress
                            var currentUploaded = Interlocked.Add(ref uploadedBytes, partSize);
                            var fileProgress = (double)currentUploaded / totalSize * 100;
                            var elapsed = DateTime.UtcNow - operationStartTime;
                            var speed = elapsed.TotalSeconds > 0 ? currentUploaded / elapsed.TotalSeconds : 0;

                            // Update file progress (Layer 2) with intelligent size formatting
                            var sizeDisplay = SizeFormatter.FormatBytesIntelligent(currentUploaded, totalSize);
                            _progressCollector.QueueProgressUpdate(2, "File Upload",
                                $"Uploading {fileInfo.Name} - {sizeDisplay} at {SizeFormatter.FormatSpeed(speed)}",
                                (int)fileProgress, 1);

                            // Log completion for larger chunks or milestone parts
                            if (partSize >= 32 * 1024 * 1024 || partNum % 10 == 0 || partNum == totalParts)
                            {
                                _progressCollector.QueueVerboseMessage("Completed part {0}/{1} ({2}) in {3}",
                                    partNum, totalParts, SizeFormatter.FormatBytes(partSize),
                                    SizeFormatter.FormatDuration(partInfo.Duration ?? TimeSpan.Zero));
                            }

                            // Don't call ProcessQueuedUpdates from background thread - PowerShell threading issue
                        }
                        catch (Exception ex)
                        {
                            // Update part status to failed
                            if (parts.TryGetValue(partNum, out var failedPart))
                            {
                                failedPart.Status = PartStatus.Failed;
                                failedPart.ErrorMessage = ex.Message;
                                failedPart.CompletionTime = DateTime.UtcNow;
                            }

                            _progressCollector.QueueVerboseMessage("Failed to upload part {0}/{1}: {2}",
                                partNum, totalParts, ex.Message);

                            throw new InvalidOperationException($"Failed to upload part {partNum}: {ex.Message}", ex);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    uploadTasks.Add(uploadTask);
                }

                // Process initial "starting upload" messages immediately
                _progressCollector.ProcessQueuedUpdates();

                // Wait for all uploads to complete with periodic progress updates
                var allTasks = uploadTasks.ToArray();
                while (!Task.WaitAll(allTasks, 1000)) // Wait 1 second at a time
                {
                    // Process progress updates from main thread every second
                    _progressCollector.ProcessQueuedUpdates();
                }

                // Process final updates
                _progressCollector.ProcessQueuedUpdates();

                // Complete multipart upload
                var sortedParts = parts.Values.OrderBy(p => p.PartNumber).ToList();
                var result = CompleteMultipartUpload(bucketName, objectName, uploadId!, sortedParts);

                var totalDuration = DateTime.UtcNow - operationStartTime;
                var averageSpeed = totalDuration.TotalSeconds > 0 ? totalSize / totalDuration.TotalSeconds : 0;

                _progressCollector.QueueVerboseMessage("Multipart upload completed successfully");
                _progressCollector.QueueVerboseMessage("Total time: {0}, Average speed: {1}",
                    SizeFormatter.FormatDuration(totalDuration), SizeFormatter.FormatSpeed(averageSpeed));

                // Complete all progress layers
                _progressCollector.QueueProgressCompletion(2, "File Upload", 1);
                _progressCollector.QueueProgressCompletion(1, "Multipart Upload Collection");

                // Process final progress updates
                _progressCollector.ProcessQueuedUpdates();

                return new MultipartUploadResult
                {
                    BucketName = bucketName,
                    ObjectName = objectName,
                    UploadId = uploadId!,
                    ETag = result.ETag,
                    TotalSize = totalSize,
                    ChunkSize = effectiveChunkSize,
                    TotalParts = totalParts,
                    CompletedParts = sortedParts,
                    Duration = totalDuration,
                    AverageSpeed = averageSpeed,
                    IsCompleted = true
                };
            }
            catch (Exception ex)
            {
                _progressCollector.QueueVerboseMessage("Multipart upload failed: {0}", ex.Message);
                
                // Return partial result for resume capability
                var sortedParts = parts.Values.OrderBy(p => p.PartNumber).ToList();
                var partialDuration = DateTime.UtcNow - operationStartTime;
                
                return new MultipartUploadResult
                {
                    BucketName = bucketName,
                    ObjectName = objectName,
                    UploadId = uploadId!,
                    TotalSize = totalSize,
                    ChunkSize = effectiveChunkSize,
                    TotalParts = totalParts,
                    CompletedParts = sortedParts,
                    Duration = partialDuration,
                    IsCompleted = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Initiates a multipart upload
        /// </summary>
        private string InitiateMultipartUpload(string bucketName, string objectName, Dictionary<string, string>? metadata)
        {
            var queryParams = new Dictionary<string, string> { { "uploads", "" } };
            var headers = new Dictionary<string, string>();

            // Add metadata as headers
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    headers[$"x-amz-meta-{kvp.Key}"] = kvp.Value;
                }
            }

            var response = _httpClient.ExecuteRequestForString(HttpMethod.Post, $"/{bucketName}/{objectName}",
                queryParams, headers);

            var doc = XDocument.Parse(response);

            // Handle XML namespace properly - use LocalName approach for compatibility
            var uploadId = doc.Descendants()
                .Where(e => e.Name.LocalName == "UploadId")
                .FirstOrDefault()?.Value;

            if (string.IsNullOrEmpty(uploadId))
            {
                throw new InvalidOperationException($"Failed to initiate multipart upload - no upload ID returned. Response: {response}");
            }

            return uploadId!;
        }

        /// <summary>
        /// Uploads a single part
        /// </summary>
        private PartInfo UploadPart(string bucketName, string objectName, string uploadId,
            FileInfo fileInfo, int partNumber, long offset, long size, int totalParts)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "partNumber", partNumber.ToString() },
                { "uploadId", uploadId }
            };

            using var fileStream = fileInfo.OpenRead();
            fileStream.Seek(offset, SeekOrigin.Begin);

            // Create progress-tracking stream for chunk upload (Layer 3)
            // Use unique activity ID for each chunk: 100 + partNumber (allows up to 900 concurrent chunks)
            var chunkActivityId = 100 + partNumber;
            var progressStream = new ProgressTrackingStream(fileStream, size,
                (bytesRead, totalBytes) =>
                {
                    var chunkProgress = (double)bytesRead / totalBytes * 100;
                    _progressCollector.QueueProgressUpdate(chunkActivityId, "Uploading Chunk",
                        $"Part {partNumber} of {totalParts} - {SizeFormatter.FormatBytes(bytesRead)}/{SizeFormatter.FormatBytes(totalBytes)}",
                        (int)chunkProgress, 2); // Parent: File Upload (Layer 2)
                });

            // Read data for MD5 calculation (still need to buffer for MD5)
            var buffer = new byte[size];
            var totalRead = 0;
            var originalPosition = fileStream.Position;
            while (totalRead < size)
            {
                var bytesRead = fileStream.Read(buffer, totalRead, (int)(size - totalRead));
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }

            // Calculate MD5 for integrity
            using var md5 = MD5.Create();
            var md5Hash = md5.ComputeHash(buffer, 0, totalRead);
            var md5String = Convert.ToBase64String(md5Hash);

            // Reset stream position for actual upload
            fileStream.Seek(originalPosition, SeekOrigin.Begin);

            using var content = new StreamContent(progressStream);
            content.Headers.ContentLength = size;
            content.Headers.Add("Content-MD5", md5String);

            var response = _httpClient.ExecuteRequest(HttpMethod.Put, $"/{bucketName}/{objectName}",
                queryParams, content: content);

            var etag = response.Headers.ETag?.Tag?.Trim('"') ?? "";

            // Complete chunk progress (Layer 3) - use same unique activity ID
            _progressCollector.QueueProgressCompletion(chunkActivityId, "Uploading Chunk", 2);

            return new PartInfo
            {
                PartNumber = partNumber,
                ETag = etag,
                Size = totalRead,
                MD5Hash = md5String
            };
        }

        /// <summary>
        /// Completes the multipart upload
        /// </summary>
        private CompleteMultipartUploadResult CompleteMultipartUpload(string bucketName, string objectName, 
            string uploadId, List<PartInfo> parts)
        {
            var queryParams = new Dictionary<string, string> { { "uploadId", uploadId } };

            var xml = new XElement("CompleteMultipartUpload",
                parts.Select(p => new XElement("Part",
                    new XElement("PartNumber", p.PartNumber),
                    new XElement("ETag", p.ETag)
                ))
            );

            var xmlContent = xml.ToString();
            using var content = new StringContent(xmlContent, Encoding.UTF8, "application/xml");

            var response = _httpClient.ExecuteRequestForString(HttpMethod.Post, $"/{bucketName}/{objectName}", 
                queryParams, content: content);

            var doc = XDocument.Parse(response);

            // Handle XML namespace properly - use LocalName approach for compatibility
            var etag = doc.Descendants()
                .Where(e => e.Name.LocalName == "ETag")
                .FirstOrDefault()?.Value?.Trim('"') ?? "";

            return new CompleteMultipartUploadResult
            {
                ETag = etag,
                Location = doc.Descendants("Location").FirstOrDefault()?.Value ?? ""
            };
        }
    }

    /// <summary>
    /// Status of a multipart upload part
    /// </summary>
    public enum PartStatus
    {
        Queued,
        Transferring,
        Completed,
        Failed
    }

    /// <summary>
    /// Information about an uploaded part
    /// </summary>
    public class PartInfo
    {
        public int PartNumber { get; set; }
        public string ETag { get; set; } = string.Empty;
        public long Size { get; set; }
        public string MD5Hash { get; set; } = string.Empty;
        public PartStatus Status { get; set; } = PartStatus.Queued;
        public string? ErrorMessage { get; set; }
        public long Offset { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public TimeSpan? Duration => StartTime.HasValue && CompletionTime.HasValue ?
            CompletionTime.Value - StartTime.Value : null;
    }

    /// <summary>
    /// Result of multipart upload operation
    /// </summary>
    public class MultipartUploadResult
    {
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string UploadId { get; set; } = string.Empty;
        public string ETag { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long ChunkSize { get; set; }
        public int TotalParts { get; set; }
        public List<PartInfo> CompletedParts { get; set; } = new List<PartInfo>();
        public TimeSpan Duration { get; set; }
        public double AverageSpeed { get; set; }
        public bool IsCompleted { get; set; }
        public string? Error { get; set; }
        public double CompletionPercentage => TotalParts > 0 ? (double)CompletedParts.Count / TotalParts * 100 : 0;
    }

    /// <summary>
    /// Result of completing multipart upload
    /// </summary>
    public class CompleteMultipartUploadResult
    {
        public string ETag { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
