using System;
using System.Management.Automation;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Multi-layer progress reporter for 3-level progress tracking:
    /// Collection progress (overall files) > File progress (current file) > Transfer progress (bytes)
    /// </summary>
    public class MultiLayerProgressReporter
    {
        private readonly PSCmdlet _cmdlet;
        private readonly ThreadSafeProgressCollector _progressCollector;
        
        // Activity IDs for the three layers
        private const int CollectionActivityId = 1;
        private const int FileActivityId = 2;
        private const int TransferActivityId = 3;

        // Collection-level tracking
        private int _totalFiles;
        private int _currentFileIndex;
        private string _collectionOperation = "Processing Files";

        // File-level tracking
        private string _currentFileName = string.Empty;
        private long _currentFileSize;
        private long _currentFileBytesTransferred;

        // Transfer-level tracking
        private long _totalBytesTransferred;
        private DateTime _startTime;

        public MultiLayerProgressReporter(PSCmdlet cmdlet, int totalFiles, string operation = "Processing Files")
        {
            _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
            _progressCollector = new ThreadSafeProgressCollector(cmdlet);
            _totalFiles = totalFiles;
            _collectionOperation = operation;
            _startTime = DateTime.UtcNow;

            // Initialize collection progress
            UpdateCollectionProgress();
        }

        /// <summary>
        /// Starts processing a new file
        /// </summary>
        /// <param name="fileName">Name of the file being processed</param>
        /// <param name="fileSize">Size of the file in bytes</param>
        public void StartNewFile(string fileName, long fileSize)
        {
            _currentFileIndex++;
            _currentFileName = fileName;
            _currentFileSize = fileSize;
            _currentFileBytesTransferred = 0;

            // Update collection progress
            UpdateCollectionProgress();

            // Initialize file progress
            UpdateFileProgress();

            // Initialize transfer progress
            UpdateTransferProgress();

            // Process any queued updates
            _progressCollector.ProcessQueuedUpdates();
        }

        /// <summary>
        /// Updates transfer progress for the current file
        /// </summary>
        /// <param name="bytesTransferred">Bytes transferred for current file</param>
        public void UpdateTransferProgress(long bytesTransferred)
        {
            _currentFileBytesTransferred = bytesTransferred;
            UpdateTransferProgress();
        }

        /// <summary>
        /// Completes the current file and updates overall progress
        /// </summary>
        public void CompleteCurrentFile()
        {
            _totalBytesTransferred += _currentFileSize;

            // Complete transfer progress
            _progressCollector.QueueProgressCompletion(TransferActivityId, "Transfer Progress", FileActivityId);

            // Complete file progress
            _progressCollector.QueueProgressCompletion(FileActivityId, "File Progress", CollectionActivityId);

            // Update collection progress
            UpdateCollectionProgress();

            // Process updates
            _progressCollector.ProcessQueuedUpdates();
        }

        /// <summary>
        /// Completes all progress reporting
        /// </summary>
        public void Complete()
        {
            // Complete collection progress
            _progressCollector.QueueProgressCompletion(CollectionActivityId, _collectionOperation);

            // Process final updates and complete
            _progressCollector.Complete();
        }

        /// <summary>
        /// Processes any queued progress updates
        /// </summary>
        public void ProcessQueuedUpdates()
        {
            _progressCollector.ProcessQueuedUpdates();
        }

        /// <summary>
        /// Updates collection-level progress
        /// </summary>
        private void UpdateCollectionProgress()
        {
            var percentage = _totalFiles > 0 ? (int)((double)_currentFileIndex / _totalFiles * 100) : 0;
            var status = $"Processing file {_currentFileIndex} of {_totalFiles}";

            if (_currentFileIndex > 0)
            {
                var elapsed = DateTime.UtcNow - _startTime;
                var avgTimePerFile = elapsed.TotalSeconds / _currentFileIndex;
                var remainingFiles = _totalFiles - _currentFileIndex;
                var estimatedTimeRemaining = TimeSpan.FromSeconds(avgTimePerFile * remainingFiles);

                status += $" (ETA: {SizeFormatter.FormatDuration(estimatedTimeRemaining)})";
            }

            _progressCollector.QueueProgressUpdate(CollectionActivityId, _collectionOperation, status, percentage);
        }

        /// <summary>
        /// Updates file-level progress
        /// </summary>
        private void UpdateFileProgress()
        {
            var percentage = _currentFileSize > 0 ? (int)((double)_currentFileBytesTransferred / _currentFileSize * 100) : 0;
            var status = $"Processing: {_currentFileName} ({SizeFormatter.FormatBytes(_currentFileBytesTransferred)}/{SizeFormatter.FormatBytes(_currentFileSize)})";

            _progressCollector.QueueProgressUpdate(FileActivityId, "File Progress", status, percentage, CollectionActivityId);
        }

        /// <summary>
        /// Updates transfer-level progress
        /// </summary>
        private void UpdateTransferProgress()
        {
            var percentage = _currentFileSize > 0 ? (int)((double)_currentFileBytesTransferred / _currentFileSize * 100) : 0;
            var status = $"Transferring: {SizeFormatter.FormatBytes(_currentFileBytesTransferred)}/{SizeFormatter.FormatBytes(_currentFileSize)}";

            if (_currentFileBytesTransferred > 0)
            {
                var elapsed = DateTime.UtcNow - _startTime;
                if (elapsed.TotalSeconds > 0)
                {
                    var speed = _currentFileBytesTransferred / elapsed.TotalSeconds;
                    status += $" at {SizeFormatter.FormatSpeed(speed)}";
                }
            }

            _progressCollector.QueueProgressUpdate(TransferActivityId, "Transfer Progress", status, percentage, FileActivityId);
        }

        /// <summary>
        /// Queues a verbose message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="args">Message arguments</param>
        public void QueueVerboseMessage(string message, params object[] args)
        {
            _progressCollector.QueueVerboseMessage(message, args);
        }

        /// <summary>
        /// Gets the current collection progress percentage
        /// </summary>
        public int CollectionProgressPercentage => _totalFiles > 0 ? (int)((double)_currentFileIndex / _totalFiles * 100) : 0;

        /// <summary>
        /// Gets the current file progress percentage
        /// </summary>
        public int FileProgressPercentage => _currentFileSize > 0 ? (int)((double)_currentFileBytesTransferred / _currentFileSize * 100) : 0;

        /// <summary>
        /// Gets the total bytes transferred across all files
        /// </summary>
        public long TotalBytesTransferred => _totalBytesTransferred + _currentFileBytesTransferred;
    }
}
