using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Zip builder with integrated progress tracking and PowerShell compatibility
    /// Provides seamless integration with PSMinIO's progress reporting system
    /// </summary>
    public class ZipBuilder : IDisposable
    {
        private readonly PSCmdlet _cmdlet;
        private readonly ZipArchiveBuilder _zipBuilder;
        private readonly ThreadSafeProgressCollector _progressCollector;
        private bool _disposed = false;
        private int _totalFiles = 0;
        private int _currentFileIndex = 0;
        private readonly List<object> _processedItems = new List<object>();

        // Activity IDs for progress tracking
        private const int ZipActivityId = 10;
        private const int FileActivityId = 11;

        /// <summary>
        /// Gets the underlying zip archive builder metrics
        /// </summary>
        public ZipArchiveBuilder ArchiveBuilder => _zipBuilder;

        /// <summary>
        /// Creates a new zip builder
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for progress reporting</param>
        /// <param name="outputStream">Stream to write zip to</param>
        /// <param name="mode">Zip archive mode</param>
        /// <param name="leaveOpen">Whether to leave output stream open</param>
        public ZipBuilder(PSCmdlet cmdlet, Stream outputStream, ZipArchiveMode mode = ZipArchiveMode.Create, bool leaveOpen = false)
        {
            _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
            _zipBuilder = new ZipArchiveBuilder(outputStream, mode, leaveOpen);
            _progressCollector = new ThreadSafeProgressCollector(cmdlet);

            // Subscribe to zip builder events
            _zipBuilder.ProgressChanged += OnProgressChanged;
            _zipBuilder.FileAdded += OnFileAdded;
            _zipBuilder.Completed += OnCompleted;
        }

        /// <summary>
        /// Creates a zip builder for a file
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for progress reporting</param>
        /// <param name="zipFilePath">Path to zip file to create</param>
        /// <param name="mode">Zip archive mode</param>
        public static ZipBuilder CreateFile(PSCmdlet cmdlet, string zipFilePath, ZipArchiveMode mode = ZipArchiveMode.Create)
        {
            var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write);
            return new ZipBuilder(cmdlet, fileStream, mode, false);
        }

        /// <summary>
        /// Adds a single file with PowerShell progress reporting
        /// </summary>
        /// <param name="fileInfo">File to add</param>
        /// <param name="entryName">Entry name in zip (optional)</param>
        /// <param name="compressionLevel">Compression level</param>
        public void AddFile(FileInfo fileInfo, string? entryName = null, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (fileInfo == null) throw new ArgumentNullException(nameof(fileInfo));

            MinIOLogger.WriteVerbose(_cmdlet, "Adding file to zip: {0} ({1})", 
                fileInfo.Name, SizeFormatter.FormatBytes(fileInfo.Length));

            _zipBuilder.AddFile(fileInfo, entryName, compressionLevel);
        }

        /// <summary>
        /// Adds multiple files with comprehensive progress tracking
        /// </summary>
        /// <param name="files">Files to add</param>
        /// <param name="basePath">Base path for entry names (optional)</param>
        /// <param name="compressionLevel">Compression level (null for adaptive compression)</param>
        public void AddFiles(IEnumerable<FileSystemInfo> files, string? basePath = null, CompressionLevel? compressionLevel = null)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            var fileList = files.ToList();
            _totalFiles = fileList.Count;
            var totalSize = fileList.OfType<FileInfo>().Sum(f => f.Length);

            // Track the processed items
            _processedItems.AddRange(fileList);

            MinIOLogger.WriteVerbose(_cmdlet, "Starting zip compression: {0} files, {1} total size",
                _totalFiles, SizeFormatter.FormatBytes(totalSize));

            // Initialize overall progress
            _progressCollector.QueueProgressUpdate(ZipActivityId, "Creating Zip Archive",
                $"Preparing to compress {_totalFiles} files", 0);

            _zipBuilder.AddFiles(files, basePath, compressionLevel);
        }

        /// <summary>
        /// Adds a directory with progress tracking
        /// </summary>
        /// <param name="directoryInfo">Directory to add</param>
        /// <param name="includeBaseDirectory">Include base directory in entry names</param>
        /// <param name="compressionLevel">Compression level (null for adaptive compression)</param>
        public void AddDirectory(DirectoryInfo directoryInfo, bool includeBaseDirectory = true, CompressionLevel? compressionLevel = null)
        {
            if (directoryInfo == null) throw new ArgumentNullException(nameof(directoryInfo));

            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            _totalFiles = files.Length;
            var totalSize = files.Sum(f => f.Length);

            // Track the processed directory and its files
            _processedItems.Add(directoryInfo);
            _processedItems.AddRange(files);

            MinIOLogger.WriteVerbose(_cmdlet, "Adding directory to zip: {0} ({1} files, {2})",
                directoryInfo.Name, _totalFiles, SizeFormatter.FormatBytes(totalSize));

            _zipBuilder.AddDirectory(directoryInfo, includeBaseDirectory, compressionLevel);
        }

        /// <summary>
        /// Completes the zip and processes final progress updates
        /// </summary>
        public void Complete()
        {
            _zipBuilder.Complete();
            _progressCollector.Complete();

            MinIOLogger.WriteVerbose(_cmdlet, "Zip compression completed: {0} files, {1} -> {2} ({3:F1}% compression)", 
                _zipBuilder.FileCount,
                SizeFormatter.FormatBytes(_zipBuilder.TotalUncompressedSize),
                SizeFormatter.FormatBytes(_zipBuilder.TotalCompressedSize),
                (1 - _zipBuilder.CompressionRatio) * 100);
        }

        /// <summary>
        /// Handles progress updates from the zip builder
        /// </summary>
        private void OnProgressChanged(object? sender, ZipProgressEventArgs e)
        {
            // Update file-level progress
            _progressCollector.QueueProgressUpdate(FileActivityId, "Compressing File",
                $"Processing: {e.CurrentFileName} ({SizeFormatter.FormatBytes(e.CurrentFileBytesProcessed)}/{SizeFormatter.FormatBytes(e.CurrentFileSize)})",
                (int)e.CurrentFileProgress, ZipActivityId);

            // Update overall progress with file count
            var currentFile = e.TotalFilesProcessed + 1; // +1 because we're currently processing this file
            var overallStatus = _totalFiles > 0
                ? $"Processing file {currentFile} of {_totalFiles}"
                : $"Compressed {e.TotalFilesProcessed} files";

            if (e.ElapsedTime.TotalSeconds > 0)
            {
                var speed = e.TotalBytesProcessed / e.ElapsedTime.TotalSeconds;
                overallStatus += $" at {SizeFormatter.FormatSpeed(speed)}";
            }

            // Calculate overall progress percentage
            var overallProgress = _totalFiles > 0 ? (int)((double)e.TotalFilesProcessed / _totalFiles * 100) : -1;
            _progressCollector.QueueProgressUpdate(ZipActivityId, "Creating Zip Archive", overallStatus, overallProgress);

            // Process queued updates
            _progressCollector.ProcessQueuedUpdates();
        }

        /// <summary>
        /// Handles file added events from the zip builder
        /// </summary>
        private void OnFileAdded(object? sender, ZipFileEventArgs e)
        {
            // Increment the current file counter
            _currentFileIndex++;

            // Log with counter and full path: "Counter of Total - FullPath"
            _progressCollector.QueueVerboseMessage("{0} of {1} - {2} -> {3} ({4:F1}% reduction, {5})",
                _currentFileIndex,
                _totalFiles,
                e.FullPath,
                SizeFormatter.FormatBytes(e.CompressedSize),
                e.CompressionEfficiency,
                SizeFormatter.FormatDuration(e.ProcessingTime));

            // Complete file progress
            _progressCollector.QueueProgressCompletion(FileActivityId, "Compressing File", ZipActivityId);
        }

        /// <summary>
        /// Handles completion events from the zip builder
        /// </summary>
        private void OnCompleted(object? sender, ZipCompletedEventArgs e)
        {
            _progressCollector.QueueVerboseMessage("Zip compression summary:");
            _progressCollector.QueueVerboseMessage("  Files: {0}", e.TotalFiles);
            _progressCollector.QueueVerboseMessage("  Original size: {0}", SizeFormatter.FormatBytes(e.TotalUncompressedSize));
            _progressCollector.QueueVerboseMessage("  Compressed size: {0}", SizeFormatter.FormatBytes(e.TotalCompressedSize));
            _progressCollector.QueueVerboseMessage("  Space saved: {0} ({1:F1}%)", 
                SizeFormatter.FormatBytes(e.SpaceSaved), e.CompressionEfficiency);
            _progressCollector.QueueVerboseMessage("  Duration: {0}", SizeFormatter.FormatDuration(e.Duration));
            _progressCollector.QueueVerboseMessage("  Average speed: {0}", SizeFormatter.FormatSpeed(e.AverageCompressionSpeed));

            // Complete overall progress
            _progressCollector.QueueProgressCompletion(ZipActivityId, "Creating Zip Archive");
        }

        /// <summary>
        /// Creates a zip result object with comprehensive metrics
        /// </summary>
        /// <param name="zipFilePath">Path to the created zip file</param>
        /// <returns>Zip creation result</returns>
        public ZipCreationResult CreateResult(string zipFilePath)
        {
            // Ensure the archive is completed before creating result
            _zipBuilder.Complete();

            var endTime = _zipBuilder.EndTime ?? DateTime.UtcNow;
            var duration = _zipBuilder.Duration ?? TimeSpan.Zero;

            return new ZipCreationResult
            {
                ZipFilePath = zipFilePath,
                StartTime = _zipBuilder.StartTime,
                EndTime = endTime,
                Duration = duration,
                FileCount = _zipBuilder.FileCount,
                TotalUncompressedSize = _zipBuilder.TotalUncompressedSize,
                TotalCompressedSize = _zipBuilder.TotalCompressedSize,
                CompressionRatio = _zipBuilder.CompressionRatio,
                SpaceSaved = _zipBuilder.TotalUncompressedSize - _zipBuilder.TotalCompressedSize,
                ProcessedItems = new List<object>(_processedItems) // Create a copy of the processed items
            };
        }

        /// <summary>
        /// Disposes the zip builder and resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Complete();
                _zipBuilder?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Result object for zip creation operations
    /// </summary>
    public class ZipCreationResult
    {
        public string ZipFilePath { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int FileCount { get; set; }
        public long TotalUncompressedSize { get; set; }
        public long TotalCompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public long SpaceSaved { get; set; }
        public List<object> ProcessedItems { get; set; } = new List<object>();
        public double CompressionEfficiency => (1 - CompressionRatio) * 100;
        public double AverageSpeed => Duration.TotalSeconds > 0 ? TotalUncompressedSize / Duration.TotalSeconds : 0;
    }
}
