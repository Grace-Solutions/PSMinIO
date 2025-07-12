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
                    _progressCollector.QueueVerboseMessage("Initiated multipart upload with ID: {0}", uploadId);
                }

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
                            var partInfo = UploadPart(bucketName, objectName, uploadId!, fileInfo,
                                partNum, partOffset, partSize);
                            
                            parts.TryAdd(partNum, partInfo);
                            
                            // Update progress
                            var currentUploaded = Interlocked.Add(ref uploadedBytes, partSize);
                            var progress = (double)currentUploaded / totalSize * 100;
                            var elapsed = DateTime.UtcNow - operationStartTime;
                            var speed = elapsed.TotalSeconds > 0 ? currentUploaded / elapsed.TotalSeconds : 0;

                            _progressCollector.QueueProgressUpdate(1, "Multipart Upload", 
                                $"Part {partNum}/{totalParts} - {SizeFormatter.FormatBytes(currentUploaded)}/{SizeFormatter.FormatBytes(totalSize)} at {SizeFormatter.FormatSpeed(speed)}", 
                                (int)progress);

                            _progressCollector.QueueVerboseMessage("Completed part {0}/{1} ({2})", 
                                partNum, totalParts, SizeFormatter.FormatBytes(partSize));
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    uploadTasks.Add(uploadTask);
                }

                // Wait for all uploads to complete
                Task.WaitAll(uploadTasks.ToArray());

                // Complete multipart upload
                var sortedParts = parts.Values.OrderBy(p => p.PartNumber).ToList();
                var result = CompleteMultipartUpload(bucketName, objectName, uploadId!, sortedParts);

                var totalDuration = DateTime.UtcNow - operationStartTime;
                var averageSpeed = totalDuration.TotalSeconds > 0 ? totalSize / totalDuration.TotalSeconds : 0;

                _progressCollector.QueueVerboseMessage("Multipart upload completed successfully");
                _progressCollector.QueueVerboseMessage("Total time: {0}, Average speed: {1}", 
                    SizeFormatter.FormatDuration(totalDuration), SizeFormatter.FormatSpeed(averageSpeed));

                _progressCollector.QueueProgressCompletion(1, "Multipart Upload");

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
            var uploadId = doc.Descendants("UploadId").FirstOrDefault()?.Value;
            
            if (string.IsNullOrEmpty(uploadId))
            {
                throw new InvalidOperationException("Failed to initiate multipart upload - no upload ID returned");
            }

            return uploadId;
        }

        /// <summary>
        /// Uploads a single part
        /// </summary>
        private PartInfo UploadPart(string bucketName, string objectName, string uploadId,
            FileInfo fileInfo, int partNumber, long offset, long size)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "partNumber", partNumber.ToString() },
                { "uploadId", uploadId }
            };

            using var fileStream = fileInfo.OpenRead();
            fileStream.Seek(offset, SeekOrigin.Begin);
            
            var buffer = new byte[size];
            var totalRead = 0;
            while (totalRead < size)
            {
                var bytesRead = fileStream.Read(buffer, totalRead, (int)(size - totalRead));
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }

            using var content = new ByteArrayContent(buffer, 0, totalRead);
            content.Headers.ContentLength = totalRead;

            // Calculate MD5 for integrity
            using var md5 = MD5.Create();
            var md5Hash = md5.ComputeHash(buffer, 0, totalRead);
            var md5String = Convert.ToBase64String(md5Hash);
            content.Headers.Add("Content-MD5", md5String);

            var response = _httpClient.ExecuteRequest(HttpMethod.Put, $"/{bucketName}/{objectName}",
                queryParams, content: content);

            var etag = response.Headers.ETag?.Tag?.Trim('"') ?? "";
            
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
            var etag = doc.Descendants("ETag").FirstOrDefault()?.Value?.Trim('"') ?? "";

            return new CompleteMultipartUploadResult
            {
                ETag = etag,
                Location = doc.Descendants("Location").FirstOrDefault()?.Value ?? ""
            };
        }
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
