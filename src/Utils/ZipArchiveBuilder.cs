using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace PSMinIO.Utils
{
    /// <summary>
    /// High-performance zip archive builder with progress tracking and comprehensive metrics
    /// Built on System.IO.Compression for minimal dependencies and maximum compatibility
    /// </summary>
    public class ZipArchiveBuilder : IDisposable
    {
        private readonly Stream _outputStream;
        private readonly ZipArchive _archive;
        private readonly bool _leaveOpen;
        private bool _disposed = false;

        // Performance optimization fields
        private const long FlushThreshold = 128 * 1024 * 1024; // 128MB (optimized for performance)
        private long _bytesWrittenSinceFlush = 0;
        private DateTime _lastFlush = DateTime.UtcNow;
        private const int FlushIntervalMs = 10000; // 10 seconds (performance-optimized time-based flushing)

        // Cancellation support
        private CancellationTokenSource? _cancellationTokenSource;
        private volatile bool _isCancelled = false;

        // Progress tracking
        public event EventHandler<ZipProgressEventArgs>? ProgressChanged;
        public event EventHandler<ZipFileEventArgs>? FileAdded;
        public event EventHandler<ZipCompletedEventArgs>? Completed;

        // Metrics
        public DateTime StartTime { get; private set; }
        public DateTime? EndTime { get; private set; }
        public TimeSpan? Duration => EndTime?.Subtract(StartTime);
        public long TotalUncompressedSize { get; private set; }
        public long TotalCompressedSize { get; private set; }
        public int FileCount { get; private set; }
        public double CompressionRatio => TotalUncompressedSize > 0 ? (double)TotalCompressedSize / TotalUncompressedSize : 0;

        /// <summary>
        /// Creates a new zip archive builder
        /// </summary>
        /// <param name="outputStream">Stream to write the zip archive to</param>
        /// <param name="mode">Zip archive mode (Create, Update, Read)</param>
        /// <param name="leaveOpen">Whether to leave the output stream open when disposing</param>
        public ZipArchiveBuilder(Stream outputStream, ZipArchiveMode mode = ZipArchiveMode.Create, bool leaveOpen = false)
        {
            _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            _leaveOpen = leaveOpen;
            _archive = new ZipArchive(_outputStream, mode, _leaveOpen);
            StartTime = DateTime.UtcNow;
            _cancellationTokenSource = new CancellationTokenSource();

            // Set up Ctrl+C handling
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        /// <summary>
        /// Creates a new zip archive builder for a file
        /// </summary>
        /// <param name="zipFilePath">Path to the zip file to create</param>
        /// <param name="mode">Zip archive mode</param>
        public static ZipArchiveBuilder CreateFile(string zipFilePath, ZipArchiveMode mode = ZipArchiveMode.Create)
        {
            var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write);
            return new ZipArchiveBuilder(fileStream, mode, false);
        }

        /// <summary>
        /// Handles Ctrl+C cancellation
        /// </summary>
        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            _isCancelled = true;
            _cancellationTokenSource?.Cancel();

            // Force flush and complete the archive
            try
            {
                FlushToDisk();
                Complete();
            }
            catch
            {
                // Ignore errors during emergency cleanup
            }
        }

        /// <summary>
        /// Checks if operation has been cancelled
        /// </summary>
        private void ThrowIfCancelled()
        {
            if (_isCancelled)
            {
                throw new OperationCanceledException("Zip operation was cancelled by user");
            }
        }

        /// <summary>
        /// Forces flush of all data to disk
        /// </summary>
        private void FlushToDisk()
        {
            try
            {
                // Force flush the output stream first
                _outputStream?.Flush();

                if (_outputStream is FileStream fileStream)
                {
                    fileStream.Flush(true); // Force OS flush to disk
                }
            }
            catch
            {
                // Ignore flush errors during cleanup
            }
        }

        /// <summary>
        /// Aggressive flush that forces all buffers to disk
        /// </summary>
        private void AggressiveFlush()
        {
            try
            {
                // Try to flush the ZipArchive's internal buffers using reflection
                var archiveType = _archive.GetType();
                var streamField = archiveType.GetField("_archiveStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (streamField?.GetValue(_archive) is Stream archiveStream)
                {
                    archiveStream.Flush();
                }

                // Flush the output stream aggressively
                _outputStream?.Flush();

                if (_outputStream is FileStream fileStream)
                {
                    fileStream.Flush(true); // Force OS-level flush
                }

                // Force garbage collection to ensure all buffers are released
                GC.Collect(0, GCCollectionMode.Optimized);
            }
            catch
            {
                // Ignore flush errors - fallback to basic flush
                try
                {
                    _outputStream?.Flush();
                    if (_outputStream is FileStream fs) fs.Flush(true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Gets optimal buffer size based on file size
        /// </summary>
        private static int GetOptimalBufferSize(long fileSize)
        {
            return fileSize switch
            {
                < 64 * 1024 => 8 * 1024,           // < 64KB: 8KB buffer (small files)
                < 1024 * 1024 => 64 * 1024,       // < 1MB: 64KB buffer
                < 10 * 1024 * 1024 => 256 * 1024, // < 10MB: 256KB buffer
                < 100 * 1024 * 1024 => 1024 * 1024, // < 100MB: 1MB buffer
                _ => 4 * 1024 * 1024               // >= 100MB: 4MB buffer (large files)
            };
        }

        /// <summary>
        /// Gets optimal compression level based on file characteristics
        /// </summary>
        private static CompressionLevel GetOptimalCompressionLevel(FileInfo fileInfo, CompressionLevel? userSpecified = null)
        {
            // If user specified a compression level, always use it
            if (userSpecified.HasValue)
                return userSpecified.Value;

            var extension = fileInfo.Extension.ToLowerInvariant();

            // Already compressed formats - use fastest to avoid double compression overhead
            if (IsAlreadyCompressed(extension))
                return CompressionLevel.Fastest;

            // Small files (< 1MB) - use optimal for better compression ratio
            if (fileInfo.Length < 1024 * 1024)
                return CompressionLevel.Optimal;

            // Large files (> 100MB) - prioritize speed
            if (fileInfo.Length > 100 * 1024 * 1024)
                return CompressionLevel.Fastest;

            // Medium files - balance speed vs compression
            return CompressionLevel.Optimal;
        }

        /// <summary>
        /// Checks if file extension indicates already compressed content
        /// </summary>
        private static bool IsAlreadyCompressed(string extension)
        {
            var compressedExtensions = new HashSet<string>
            {
                // Archives
                ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz", ".tar",
                // Images
                ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif",
                // Audio
                ".mp3", ".aac", ".ogg", ".m4a", ".flac",
                // Video
                ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v",
                // Documents
                ".pdf", ".docx", ".xlsx", ".pptx"
            };

            return compressedExtensions.Contains(extension);
        }

        /// <summary>
        /// Optimizes file processing order for better performance
        /// </summary>
        private static IEnumerable<FileInfo> OptimizeFileProcessingOrder(IEnumerable<FileSystemInfo> files)
        {
            var fileInfos = files.OfType<FileInfo>().ToList();

            // Sort by directory first (improves disk access patterns)
            // Then by size (small files first for quick progress feedback)
            return fileInfos.OrderBy(f => f.DirectoryName)
                           .ThenBy(f => f.Length)
                           .ThenBy(f => f.Name); // Consistent ordering for same-size files
        }

        /// <summary>
        /// Adds a single file to the zip archive
        /// </summary>
        /// <param name="fileInfo">File to add</param>
        /// <param name="entryName">Name of the entry in the zip (optional, uses file name if null)</param>
        /// <param name="compressionLevel">Compression level to use (null for adaptive compression)</param>
        public void AddFile(FileInfo fileInfo, string? entryName = null, CompressionLevel? compressionLevel = null)
        {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
            if (!fileInfo.Exists) throw new FileNotFoundException($"File not found: {fileInfo.FullName}");

            // Check for cancellation before starting
            ThrowIfCancelled();

            entryName ??= fileInfo.Name;
            var fileStartTime = DateTime.UtcNow;

            // Get optimal compression level and buffer size
            var effectiveCompressionLevel = GetOptimalCompressionLevel(fileInfo, compressionLevel);
            var bufferSize = GetOptimalBufferSize(fileInfo.Length);

            // Create entry in zip
            var entry = _archive.CreateEntry(entryName, effectiveCompressionLevel);
            entry.LastWriteTime = fileInfo.LastWriteTime;

            long bytesProcessed = 0;
            using (var fileStream = fileInfo.OpenRead())
            using (var entryStream = entry.Open())
            {
                var buffer = new byte[bufferSize]; // Adaptive buffer size
                int bytesRead;

                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Check for cancellation during processing
                    ThrowIfCancelled();

                    entryStream.Write(buffer, 0, bytesRead);
                    bytesProcessed += bytesRead;
                    _bytesWrittenSinceFlush += bytesRead;

                    // Hybrid flushing: size-based OR time-based
                    var now = DateTime.UtcNow;
                    if (_bytesWrittenSinceFlush >= FlushThreshold ||
                        (now - _lastFlush).TotalMilliseconds > FlushIntervalMs)
                    {
                        entryStream.Flush();
                        AggressiveFlush(); // Force all data to disk immediately
                        _bytesWrittenSinceFlush = 0;
                        _lastFlush = now;
                    }

                    // Report progress
                    OnProgressChanged(new ZipProgressEventArgs
                    {
                        CurrentFileName = fileInfo.Name,
                        CurrentFileProgress = fileInfo.Length > 0 ? (double)bytesProcessed / fileInfo.Length * 100 : 100,
                        CurrentFileBytesProcessed = bytesProcessed,
                        CurrentFileSize = fileInfo.Length,
                        TotalFilesProcessed = FileCount,
                        TotalBytesProcessed = TotalUncompressedSize + bytesProcessed,
                        ElapsedTime = DateTime.UtcNow - StartTime
                    });
                }

                // Final flush for this file
                entryStream.Flush();
                AggressiveFlush(); // Force all data to disk after each file
            }

            // Update metrics (access CompressedLength after the entry stream is closed)
            TotalUncompressedSize += fileInfo.Length;

            // CompressedLength is only available after the entry is fully written and closed
            long compressedSize = 0;
            try
            {
                compressedSize = entry.CompressedLength;
                TotalCompressedSize += compressedSize;
            }
            catch (InvalidOperationException)
            {
                // CompressedLength not available yet, use uncompressed size as fallback
                compressedSize = fileInfo.Length;
                TotalCompressedSize += compressedSize;
            }

            FileCount++;

            var fileEndTime = DateTime.UtcNow;
            OnFileAdded(new ZipFileEventArgs
            {
                FileName = fileInfo.Name,
                EntryName = entryName,
                UncompressedSize = fileInfo.Length,
                CompressedSize = compressedSize,
                CompressionRatio = fileInfo.Length > 0 ? (double)compressedSize / fileInfo.Length : 0,
                ProcessingTime = fileEndTime - fileStartTime
            });
        }

        /// <summary>
        /// Adds multiple files to the zip archive with optimized processing order
        /// </summary>
        /// <param name="files">Files to add</param>
        /// <param name="basePath">Base path to remove from entry names (optional)</param>
        /// <param name="compressionLevel">Compression level to use (null for adaptive compression)</param>
        public void AddFiles(IEnumerable<FileSystemInfo> files, string? basePath = null, CompressionLevel? compressionLevel = null)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            // Separate files and directories for optimized processing
            var fileList = new List<FileInfo>();
            var directories = new List<DirectoryInfo>();

            foreach (var file in files)
            {
                if (file is FileInfo fileInfo)
                {
                    fileList.Add(fileInfo);
                }
                else if (file is DirectoryInfo dirInfo)
                {
                    directories.Add(dirInfo);
                }
            }

            // Process directories first to collect all files
            foreach (var dirInfo in directories)
            {
                var dirFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                fileList.AddRange(dirFiles);
            }

            // Optimize file processing order for better performance
            var optimizedFiles = OptimizeFileProcessingOrder(fileList);

            // Process files in optimized order
            foreach (var fileInfo in optimizedFiles)
            {
                // Check for cancellation before each file
                ThrowIfCancelled();

                var entryName = GetEntryName(fileInfo, basePath);
                AddFile(fileInfo, entryName, compressionLevel);
            }
        }

        /// <summary>
        /// Adds a directory and all its contents to the zip archive
        /// </summary>
        /// <param name="directoryInfo">Directory to add</param>
        /// <param name="includeBaseDirectory">Whether to include the base directory in entry names</param>
        /// <param name="compressionLevel">Compression level to use (null for adaptive compression)</param>
        public void AddDirectory(DirectoryInfo directoryInfo, bool includeBaseDirectory = true, CompressionLevel? compressionLevel = null)
        {
            if (directoryInfo == null) throw new ArgumentNullException(nameof(directoryInfo));
            if (!directoryInfo.Exists) throw new DirectoryNotFoundException($"Directory not found: {directoryInfo.FullName}");

            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            var basePath = includeBaseDirectory ? directoryInfo.Parent?.FullName : directoryInfo.FullName;

            AddFiles(files, basePath, compressionLevel);
        }

        /// <summary>
        /// Completes the zip archive and finalizes metrics
        /// </summary>
        public void Complete()
        {
            if (EndTime.HasValue) return; // Already completed

            EndTime = DateTime.UtcNow;
            
            OnCompleted(new ZipCompletedEventArgs
            {
                StartTime = StartTime,
                EndTime = EndTime.Value,
                Duration = Duration!.Value,
                TotalFiles = FileCount,
                TotalUncompressedSize = TotalUncompressedSize,
                TotalCompressedSize = TotalCompressedSize,
                CompressionRatio = CompressionRatio,
                AverageCompressionSpeed = Duration!.Value.TotalSeconds > 0 ? TotalUncompressedSize / Duration.Value.TotalSeconds : 0
            });
        }

        /// <summary>
        /// Gets the entry name for a file relative to a base path
        /// </summary>
        private string GetEntryName(FileInfo fileInfo, string? basePath)
        {
            if (string.IsNullOrEmpty(basePath))
                return fileInfo.Name;

            var fullPath = fileInfo.FullName;
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fullPath.Substring(basePath!.Length).TrimStart('\\', '/');
                return relativePath.Replace('\\', '/'); // Use forward slashes for zip entries
            }

            return fileInfo.Name;
        }

        /// <summary>
        /// Raises the ProgressChanged event
        /// </summary>
        protected virtual void OnProgressChanged(ZipProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the FileAdded event
        /// </summary>
        protected virtual void OnFileAdded(ZipFileEventArgs e)
        {
            FileAdded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the Completed event
        /// </summary>
        protected virtual void OnCompleted(ZipCompletedEventArgs e)
        {
            Completed?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the zip archive and underlying stream
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Remove Ctrl+C handler
                    Console.CancelKeyPress -= OnCancelKeyPress;

                    // Complete the archive if not cancelled
                    if (!_isCancelled)
                    {
                        Complete();
                    }

                    // Final flush to ensure all data is written
                    _outputStream?.Flush();
                    if (_outputStream is FileStream fileStream)
                    {
                        fileStream.Flush(true); // Force OS flush
                    }
                }
                catch
                {
                    // Ignore errors during disposal
                }
                finally
                {
                    _archive?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    if (!_leaveOpen)
                    {
                        _outputStream?.Dispose();
                    }
                    _disposed = true;
                }
            }
        }
    }
}
