using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

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
        /// Adds a single file to the zip archive
        /// </summary>
        /// <param name="fileInfo">File to add</param>
        /// <param name="entryName">Name of the entry in the zip (optional, uses file name if null)</param>
        /// <param name="compressionLevel">Compression level to use</param>
        public void AddFile(FileInfo fileInfo, string? entryName = null, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));
            if (!fileInfo.Exists) throw new FileNotFoundException($"File not found: {fileInfo.FullName}");

            entryName ??= fileInfo.Name;
            var fileStartTime = DateTime.UtcNow;

            // Create entry in zip
            var entry = _archive.CreateEntry(entryName, compressionLevel);
            entry.LastWriteTime = fileInfo.LastWriteTime;

            long bytesProcessed = 0;
            using (var fileStream = fileInfo.OpenRead())
            using (var entryStream = entry.Open())
            {
                var buffer = new byte[81920]; // 80KB buffer for good performance
                int bytesRead;

                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    entryStream.Write(buffer, 0, bytesRead);
                    bytesProcessed += bytesRead;

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
            }

            // Update metrics
            TotalUncompressedSize += fileInfo.Length;
            TotalCompressedSize += entry.CompressedLength;
            FileCount++;

            var fileEndTime = DateTime.UtcNow;
            OnFileAdded(new ZipFileEventArgs
            {
                FileName = fileInfo.Name,
                EntryName = entryName,
                UncompressedSize = fileInfo.Length,
                CompressedSize = entry.CompressedLength,
                CompressionRatio = fileInfo.Length > 0 ? (double)entry.CompressedLength / fileInfo.Length : 0,
                ProcessingTime = fileEndTime - fileStartTime
            });
        }

        /// <summary>
        /// Adds multiple files to the zip archive
        /// </summary>
        /// <param name="files">Files to add</param>
        /// <param name="basePath">Base path to remove from entry names (optional)</param>
        /// <param name="compressionLevel">Compression level to use</param>
        public void AddFiles(IEnumerable<FileSystemInfo> files, string? basePath = null, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            foreach (var file in files)
            {
                if (file is FileInfo fileInfo)
                {
                    var entryName = GetEntryName(fileInfo, basePath);
                    AddFile(fileInfo, entryName, compressionLevel);
                }
                else if (file is DirectoryInfo dirInfo)
                {
                    // Add directory files recursively
                    var dirFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    var dirBasePath = basePath ?? dirInfo.FullName;
                    AddFiles(dirFiles, dirBasePath, compressionLevel);
                }
            }
        }

        /// <summary>
        /// Adds a directory and all its contents to the zip archive
        /// </summary>
        /// <param name="directoryInfo">Directory to add</param>
        /// <param name="includeBaseDirectory">Whether to include the base directory in entry names</param>
        /// <param name="compressionLevel">Compression level to use</param>
        public void AddDirectory(DirectoryInfo directoryInfo, bool includeBaseDirectory = true, CompressionLevel compressionLevel = CompressionLevel.Optimal)
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
                var relativePath = fullPath.Substring(basePath.Length).TrimStart('\\', '/');
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
                Complete();
                _archive?.Dispose();
                if (!_leaveOpen)
                {
                    _outputStream?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
