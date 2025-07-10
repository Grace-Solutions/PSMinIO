using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Synchronous wrapper for MinIO client operations
    /// Converts async MinIO operations to synchronous calls for PowerShell compatibility
    /// </summary>
    public class MinIOClientWrapper : IDisposable
    {
        private readonly IMinioClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed = false;

        /// <summary>
        /// Creates a new MinIOClientWrapper instance
        /// </summary>
        /// <param name="configuration">MinIO configuration</param>
        public MinIOClientWrapper(MinIOConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (!configuration.IsValid)
                throw new ArgumentException("Invalid MinIO configuration", nameof(configuration));

            _cancellationTokenSource = new CancellationTokenSource();
            
            // Create MinIO client with configuration
            var clientBuilder = new MinioClient()
                .WithEndpoint(configuration.Endpoint)
                .WithCredentials(configuration.AccessKey, configuration.SecretKey);

            if (configuration.UseSSL)
            {
                clientBuilder = clientBuilder.WithSSL();

                // Configure custom HttpClient for certificate validation if needed
                if (configuration.SkipCertificateValidation)
                {
                    var httpClientHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    var httpClient = new HttpClient(httpClientHandler);
                    clientBuilder = clientBuilder.WithHttpClient(httpClient);
                }
            }

            if (configuration.TimeoutSeconds > 0)
            {
                clientBuilder = clientBuilder.WithTimeout(configuration.TimeoutSeconds * 1000);
            }

            _client = clientBuilder.Build();
        }

        /// <summary>
        /// Gets the cancellation token for operations
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        /// <summary>
        /// Lists all buckets synchronously
        /// </summary>
        /// <returns>List of bucket information</returns>
        public List<MinIOBucketInfo> ListBuckets()
        {
            try
            {
                var bucketsResult = Task.Run(async () => 
                    await _client.ListBucketsAsync(CancellationToken)).GetAwaiter().GetResult();

                return bucketsResult.Buckets
                    .Select(MinIOBucketInfo.FromMinioBucket)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list buckets: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a bucket exists synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <returns>True if bucket exists, false otherwise</returns>
        public bool BucketExists(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var args = new BucketExistsArgs().WithBucket(bucketName);
                return Task.Run(async () => 
                    await _client.BucketExistsAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to check if bucket '{bucketName}' exists: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a bucket synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket to create</param>
        /// <param name="region">Optional region for the bucket</param>
        public void CreateBucket(string bucketName, string? region = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var args = new MakeBucketArgs().WithBucket(bucketName);
                
                if (!string.IsNullOrWhiteSpace(region))
                {
                    args = args.WithLocation(region);
                }

                Task.Run(async () => 
                    await _client.MakeBucketAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes a bucket synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket to delete</param>
        public void DeleteBucket(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var args = new RemoveBucketArgs().WithBucket(bucketName);
                Task.Run(async () => 
                    await _client.RemoveBucketAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lists objects in a bucket synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="prefix">Optional prefix to filter objects</param>
        /// <param name="recursive">Whether to list objects recursively</param>
        /// <param name="includeVersions">Whether to include all versions of objects</param>
        /// <returns>List of object information</returns>
        public List<MinIOObjectInfo> ListObjects(string bucketName, string? prefix = null, bool recursive = true, bool includeVersions = false)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var args = new ListObjectsArgs()
                    .WithBucket(bucketName)
                    .WithRecursive(recursive);

                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    args = args.WithPrefix(prefix);
                }

                // Add version support if requested
                if (includeVersions)
                {
                    args = args.WithVersions(true);
                }

                var objects = new List<MinIOObjectInfo>();
                var observable = _client.ListObjectsAsync(args, CancellationToken);

                // Convert async enumerable to synchronous list
                var task = Task.Run(async () =>
                {
                    await foreach (var item in observable.WithCancellation(CancellationToken))
                    {
                        objects.Add(MinIOObjectInfo.FromMinioItem(item, bucketName));
                    }
                });

                task.GetAwaiter().GetResult();
                return objects;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list objects in bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets bucket policy synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <returns>Bucket policy as JSON string</returns>
        public string GetBucketPolicy(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            try
            {
                var args = new GetPolicyArgs().WithBucket(bucketName);
                return Task.Run(async () => 
                    await _client.GetPolicyAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get policy for bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sets bucket policy synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="policy">Policy JSON string</param>
        public void SetBucketPolicy(string bucketName, string policy)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(policy))
                throw new ArgumentException("Policy cannot be null or empty", nameof(policy));

            try
            {
                var args = new SetPolicyArgs()
                    .WithBucket(bucketName)
                    .WithPolicy(policy);

                Task.Run(async () =>
                    await _client.SetPolicyAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set policy for bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes an object synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object to delete</param>
        public void DeleteObject(string bucketName, string objectName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            try
            {
                var args = new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName);

                Task.Run(async () =>
                    await _client.RemoveObjectAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete object '{objectName}' from bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes multiple objects synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectNames">List of object names to delete</param>
        public void DeleteObjects(string bucketName, IEnumerable<string> objectNames)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (objectNames == null)
                throw new ArgumentNullException(nameof(objectNames));

            var objectList = objectNames.ToList();
            if (objectList.Count == 0)
                return;

            try
            {
                var deleteObjectsArgs = new RemoveObjectsArgs()
                    .WithBucket(bucketName)
                    .WithObjects(objectList);

                var observable = _client.RemoveObjectsAsync(deleteObjectsArgs, CancellationToken);

                // Convert async enumerable to synchronous operation
                var task = Task.Run(async () =>
                {
                    await foreach (var deleteError in observable.WithCancellation(CancellationToken))
                    {
                        if (deleteError.Exception != null)
                        {
                            throw new InvalidOperationException($"Failed to delete object '{deleteError.Key}': {deleteError.Exception.Message}", deleteError.Exception);
                        }
                    }
                });

                task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete objects from bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uploads a file to MinIO synchronously with progress reporting
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="filePath">Path to the file to upload</param>
        /// <param name="contentType">Content type of the file (optional)</param>
        /// <param name="progressCallback">Progress callback for reporting upload progress</param>
        /// <returns>ETag of the uploaded object</returns>
        public string UploadFile(string bucketName, string objectName, string filePath,
            string? contentType = null, Action<long>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSize = fileInfo.Length;

                // Determine content type if not provided
                if (string.IsNullOrWhiteSpace(contentType))
                {
                    contentType = GetContentType(filePath);
                }

                var args = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithFileName(filePath)
                    .WithContentType(contentType);

                // Add progress callback if provided
                if (progressCallback != null)
                {
                    args = args.WithProgress(new Progress<ProgressReport>(report =>
                    {
                        progressCallback(report.TotalBytesTransferred);
                    }));
                }

                var result = Task.Run(async () =>
                    await _client.PutObjectAsync(args, CancellationToken)).GetAwaiter().GetResult();

                return result.Etag ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload file '{filePath}' to bucket '{bucketName}' as '{objectName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Downloads an object from MinIO synchronously with progress reporting
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="filePath">Path where the file should be saved</param>
        /// <param name="progressCallback">Progress callback for reporting download progress</param>
        public void DownloadFile(string bucketName, string objectName, string filePath,
            Action<long>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            try
            {
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var args = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithFile(filePath);

                // Add progress callback if provided
                if (progressCallback != null)
                {
                    args = args.WithProgress(new Progress<ProgressReport>(report =>
                    {
                        progressCallback(report.TotalBytesTransferred);
                    }));
                }

                Task.Run(async () =>
                    await _client.GetObjectAsync(args, CancellationToken)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download object '{objectName}' from bucket '{bucketName}' to '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uploads a stream to MinIO synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="data">Stream containing the data to upload</param>
        /// <param name="contentType">Content type of the data</param>
        /// <param name="progressCallback">Progress callback for reporting upload progress</param>
        /// <returns>ETag of the uploaded object</returns>
        public string UploadStream(string bucketName, string objectName, Stream data,
            string contentType = "application/octet-stream", Action<long>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                var args = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(data)
                    .WithObjectSize(data.Length)
                    .WithContentType(contentType);

                // Add progress callback if provided
                if (progressCallback != null)
                {
                    args = args.WithProgress(new Progress<ProgressReport>(report =>
                    {
                        progressCallback(report.TotalBytesTransferred);
                    }));
                }

                var result = Task.Run(async () =>
                    await _client.PutObjectAsync(args, CancellationToken)).GetAwaiter().GetResult();

                return result.Etag ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload stream to bucket '{bucketName}' as '{objectName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the content type for a file based on its extension
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Content type string</returns>
        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Lists object versions in a bucket synchronously
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="prefix">Optional prefix to filter objects</param>
        /// <param name="recursive">Whether to list objects recursively</param>
        /// <param name="maxObjects">Maximum number of objects to return (0 = unlimited)</param>
        /// <returns>List of object information including versions</returns>
        private List<MinIOObjectInfo> ListObjectVersions(string bucketName, string? prefix, bool recursive, int maxObjects)
        {
            try
            {
                // For now, fall back to regular object listing since version listing
                // may not be available in all MinIO SDK versions
                // This can be enhanced when the SDK supports it
                return ListObjects(bucketName, prefix, recursive, maxObjects, false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to list object versions in bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates a presigned URL for an object
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="objectName">Name of the object</param>
        /// <param name="expiry">URL expiry time</param>
        /// <returns>Presigned URL</returns>
        public string GetPresignedUrl(string bucketName, string objectName, TimeSpan expiry)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

            try
            {
                var args = new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithExpiry((int)expiry.TotalSeconds);

                var result = Task.Run(async () =>
                    await _client.PresignedGetObjectAsync(args)).GetAwaiter().GetResult();

                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate presigned URL for object '{objectName}' in bucket '{bucketName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uploads a file using chunked transfer with resume capability
        /// </summary>
        /// <param name="transferState">Transfer state for resume functionality</param>
        /// <param name="progressReporter">Progress reporter for updates</param>
        /// <param name="maxRetries">Maximum retry attempts per chunk</param>
        /// <returns>MinIOObjectInfo of uploaded object or null if failed</returns>
        public MinIOObjectInfo? UploadFileChunked(
            ChunkedTransferState transferState,
            ChunkedCollectionProgressReporter progressReporter,
            int maxRetries = 3)
        {
            if (transferState == null)
                throw new ArgumentNullException(nameof(transferState));

            try
            {
                // Start multipart upload if not already started
                if (string.IsNullOrEmpty(transferState.UploadId))
                {
                    var initiateArgs = new NewMultipartUploadArgs()
                        .WithBucket(transferState.BucketName)
                        .WithObject(transferState.ObjectName);

                    var initiateResult = Task.Run(async () =>
                        await _client.NewMultipartUploadAsync(initiateArgs, CancellationToken)).GetAwaiter().GetResult();

                    transferState.UploadId = initiateResult.UploadId;
                }

                var completedParts = new List<UploadPartResponse>();

                // Process each chunk
                while (!transferState.IsComplete)
                {
                    var nextChunk = transferState.GetNextChunk();
                    if (nextChunk == null)
                        break;

                    progressReporter.StartNewChunk(nextChunk.ChunkNumber + 1, nextChunk.Size);

                    var uploadResult = UploadChunkWithRetry(transferState, nextChunk, progressReporter, maxRetries);
                    if (uploadResult != null)
                    {
                        completedParts.Add(uploadResult);
                        transferState.MarkChunkCompleted(nextChunk);
                        progressReporter.CompleteChunk(uploadResult.ETag);

                        // Save progress for resume
                        ChunkedTransferResumeManager.SaveTransferState(transferState);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to upload chunk {nextChunk.ChunkNumber} after {maxRetries} attempts");
                    }
                }

                // Complete multipart upload
                var completeArgs = new CompleteMultipartUploadArgs()
                    .WithBucket(transferState.BucketName)
                    .WithObject(transferState.ObjectName)
                    .WithUploadId(transferState.UploadId)
                    .WithETags(completedParts.OrderBy(p => p.PartNumber).Select(p => new Tuple<int, string>(p.PartNumber, p.ETag)));

                var completeResult = Task.Run(async () =>
                    await _client.CompleteMultipartUploadAsync(completeArgs, CancellationToken)).GetAwaiter().GetResult();

                // Return object information
                return new MinIOObjectInfo(
                    transferState.ObjectName,
                    transferState.TotalSize,
                    DateTime.UtcNow,
                    completeResult.ETag,
                    transferState.BucketName);
            }
            catch (Exception ex)
            {
                // Abort multipart upload on failure
                if (!string.IsNullOrEmpty(transferState.UploadId))
                {
                    try
                    {
                        var abortArgs = new AbortMultipartUploadArgs()
                            .WithBucket(transferState.BucketName)
                            .WithObject(transferState.ObjectName)
                            .WithUploadId(transferState.UploadId);

                        Task.Run(async () =>
                            await _client.AbortMultipartUploadAsync(abortArgs, CancellationToken)).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignore abort errors
                    }
                }

                throw new InvalidOperationException($"Chunked upload failed for object '{transferState.ObjectName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uploads a single chunk with retry logic
        /// </summary>
        /// <param name="transferState">Transfer state</param>
        /// <param name="chunk">Chunk to upload</param>
        /// <param name="progressReporter">Progress reporter</param>
        /// <param name="maxRetries">Maximum retry attempts</param>
        /// <returns>Upload part response or null if failed</returns>
        private UploadPartResponse? UploadChunkWithRetry(
            ChunkedTransferState transferState,
            ChunkInfo chunk,
            ChunkedCollectionProgressReporter progressReporter,
            int maxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var fileStream = new FileStream(transferState.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    fileStream.Seek(chunk.StartByte, SeekOrigin.Begin);

                    var chunkData = new byte[chunk.Size];
                    var bytesRead = fileStream.Read(chunkData, 0, (int)chunk.Size);

                    using var chunkStream = new MemoryStream(chunkData, 0, bytesRead);

                    var uploadArgs = new UploadPartArgs()
                        .WithBucket(transferState.BucketName)
                        .WithObject(transferState.ObjectName)
                        .WithUploadId(transferState.UploadId)
                        .WithPartNumber(chunk.ChunkNumber + 1) // MinIO uses 1-based part numbers
                        .WithPartSize(bytesRead)
                        .WithStreamData(chunkStream);

                    // Add progress callback
                    uploadArgs = uploadArgs.WithProgress(new Progress<ProgressReport>(report =>
                    {
                        progressReporter.UpdateChunkProgress(report.TotalBytesTransferred);
                    }));

                    var result = Task.Run(async () =>
                        await _client.UploadPartAsync(uploadArgs, CancellationToken)).GetAwaiter().GetResult();

                    chunk.ChunkETag = result.ETag;
                    return result;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    progressReporter.ReportChunkError(ex, attempt, maxRetries);

                    // Exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Task.Delay(delay, CancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    progressReporter.ReportChunkError(ex, attempt, maxRetries);
                    chunk.LastError = ex.Message;
                    chunk.RetryCount = attempt;
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Downloads a file using chunked transfer with resume capability
        /// </summary>
        /// <param name="transferState">Transfer state for resume functionality</param>
        /// <param name="progressReporter">Progress reporter for updates</param>
        /// <param name="maxRetries">Maximum retry attempts per chunk</param>
        /// <param name="parallelDownloads">Number of parallel chunk downloads</param>
        /// <returns>True if download succeeded, false otherwise</returns>
        public bool DownloadFileChunked(
            ChunkedTransferState transferState,
            ChunkedSingleFileProgressReporter progressReporter,
            int maxRetries = 3,
            int parallelDownloads = 3)
        {
            if (transferState == null)
                throw new ArgumentNullException(nameof(transferState));

            try
            {
                // Create or open the target file
                using var fileStream = new FileStream(transferState.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                fileStream.SetLength(transferState.TotalSize);

                // Get list of chunks to download
                var chunksToDownload = new List<ChunkInfo>();
                while (!transferState.IsComplete)
                {
                    var nextChunk = transferState.GetNextChunk();
                    if (nextChunk == null)
                        break;
                    chunksToDownload.Add(nextChunk);
                }

                if (chunksToDownload.Count == 0)
                {
                    return true; // Already complete
                }

                // Download chunks (with limited parallelism)
                var semaphore = new SemaphoreSlim(parallelDownloads, parallelDownloads);
                var downloadTasks = chunksToDownload.Select(chunk =>
                    DownloadChunkAsync(transferState, chunk, fileStream, progressReporter, maxRetries, semaphore)).ToArray();

                var results = Task.WhenAll(downloadTasks).GetAwaiter().GetResult();

                // Check if all chunks downloaded successfully
                var allSucceeded = results.All(r => r);
                if (allSucceeded)
                {
                    // Mark all chunks as completed
                    foreach (var chunk in chunksToDownload)
                    {
                        transferState.MarkChunkCompleted(chunk);
                    }
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Chunked download failed for object '{transferState.ObjectName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Downloads a single chunk asynchronously with retry logic
        /// </summary>
        /// <param name="transferState">Transfer state</param>
        /// <param name="chunk">Chunk to download</param>
        /// <param name="fileStream">Target file stream</param>
        /// <param name="progressReporter">Progress reporter</param>
        /// <param name="maxRetries">Maximum retry attempts</param>
        /// <param name="semaphore">Semaphore for controlling parallelism</param>
        /// <returns>True if chunk downloaded successfully</returns>
        private async Task<bool> DownloadChunkAsync(
            ChunkedTransferState transferState,
            ChunkInfo chunk,
            FileStream fileStream,
            ChunkedSingleFileProgressReporter progressReporter,
            int maxRetries,
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync(CancellationToken);

            try
            {
                progressReporter.StartNewChunk(chunk.ChunkNumber + 1, chunk.Size);

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var getArgs = new GetObjectArgs()
                            .WithBucket(transferState.BucketName)
                            .WithObject(transferState.ObjectName)
                            .WithOffsetAndLength(chunk.StartByte, chunk.Size);

                        using var chunkStream = new MemoryStream();

                        await _client.GetObjectAsync(getArgs, (stream) =>
                        {
                            stream.CopyTo(chunkStream);
                        }, CancellationToken);

                        // Write chunk to file at correct position
                        lock (fileStream)
                        {
                            fileStream.Seek(chunk.StartByte, SeekOrigin.Begin);
                            chunkStream.Seek(0, SeekOrigin.Begin);
                            chunkStream.CopyTo(fileStream);
                            fileStream.Flush();
                        }

                        progressReporter.UpdateChunkProgress(chunk.Size);
                        progressReporter.CompleteChunk();

                        chunk.IsCompleted = true;
                        return true;
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        progressReporter.ReportChunkError(ex, attempt, maxRetries);

                        // Exponential backoff
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        await Task.Delay(delay, CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        progressReporter.ReportChunkError(ex, attempt, maxRetries);
                        chunk.LastError = ex.Message;
                        chunk.RetryCount = attempt;
                        return false;
                    }
                }

                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Cancels all ongoing operations
        /// </summary>
        public void CancelOperations()
        {
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Disposes the wrapper and underlying client
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">Whether disposing from Dispose method</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
}
