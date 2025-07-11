using System;
using System.Management.Automation;
using System.Threading;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Thread-safe wrapper for chunked progress reporting that can be safely called from background threads
    /// </summary>
    public class ThreadSafeChunkedProgressReporter
    {
        private readonly ThreadSafeProgressCollector _progressCollector;
        private readonly string _operationName;
        private readonly long _totalSize;
        private readonly int _totalChunks;
        private readonly DateTime _startTime;

        // Activity IDs for progress hierarchy
        private const int FileActivityId = 1;
        private const int ChunkActivityId = 2;

        // Current state (using Interlocked for long values since volatile doesn't support long)
        private volatile int _currentChunk = 0;
        private long _currentChunkSize = 0;
        private long _totalBytesTransferred = 0;
        private volatile string _currentFileName = string.Empty;

        public ThreadSafeChunkedProgressReporter(
            PSCmdlet cmdlet,
            long totalSize,
            int totalChunks,
            string operationName)
        {
            _progressCollector = new ThreadSafeProgressCollector(cmdlet);
            _totalSize = totalSize;
            _totalChunks = totalChunks;
            _operationName = operationName;
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Starts a new file operation (thread-safe)
        /// </summary>
        public void StartNewFile(string fileName, long fileSize, int totalChunks)
        {
            _currentFileName = fileName;
            Interlocked.Exchange(ref _totalBytesTransferred, 0);
            _currentChunk = 0;

            _progressCollector.QueueVerboseMessage("Starting {0} of file: {1} ({2})",
                _operationName.ToLower(), fileName, SizeFormatter.FormatBytes(fileSize));

            _progressCollector.QueueProgressUpdate(
                FileActivityId,
                $"{_operationName} File",
                $"{_operationName}: {fileName}",
                0);
        }

        /// <summary>
        /// Starts a new chunk operation (thread-safe)
        /// </summary>
        public void StartNewChunk(int chunkNumber, long chunkSize)
        {
            _currentChunk = chunkNumber;
            Interlocked.Exchange(ref _currentChunkSize, chunkSize);

            _progressCollector.QueueVerboseMessage("File {0}: Starting chunk {1}/{2} ({3})",
                _currentFileName, chunkNumber, _totalChunks, SizeFormatter.FormatBytes(chunkSize));

            _progressCollector.QueueProgressUpdate(
                ChunkActivityId,
                "Current Chunk",
                $"Chunk {chunkNumber}/{_totalChunks}",
                0,
                FileActivityId);
        }

        /// <summary>
        /// Updates chunk progress (thread-safe)
        /// </summary>
        public void UpdateChunkProgress(long bytesTransferred)
        {
            var newTotal = Interlocked.Add(ref _totalBytesTransferred, bytesTransferred);
            var currentChunkSize = Interlocked.Read(ref _currentChunkSize);

            var chunkPercent = currentChunkSize > 0 ?
                (int)((double)bytesTransferred / currentChunkSize * 100) : 100;
            var filePercent = _totalSize > 0 ?
                (int)((double)newTotal / _totalSize * 100) : 100;

            // Update chunk progress
            _progressCollector.QueueProgressUpdate(
                ChunkActivityId,
                "Current Chunk",
                $"Chunk {_currentChunk}/{_totalChunks} - {SizeFormatter.FormatBytes(bytesTransferred)}/{SizeFormatter.FormatBytes(_currentChunkSize)}",
                Math.Min(chunkPercent, 100),
                FileActivityId);

            // Update file progress
            _progressCollector.QueueProgressUpdate(
                FileActivityId,
                $"{_operationName} File",
                $"{_operationName}: {_currentFileName} - {SizeFormatter.FormatBytes(newTotal)}/{SizeFormatter.FormatBytes(_totalSize)}",
                Math.Min(filePercent, 100));
        }

        /// <summary>
        /// Completes the current chunk (thread-safe)
        /// </summary>
        public void CompleteChunk(string? chunkETag = null)
        {
            _progressCollector.QueueVerboseMessage("File {0}: Completed chunk {1}/{2}{3}",
                _currentFileName, _currentChunk, _totalChunks,
                !string.IsNullOrEmpty(chunkETag) ? $" - ETag: {chunkETag}" : "");

            _progressCollector.QueueProgressCompletion(ChunkActivityId, "Current Chunk", FileActivityId);
        }

        /// <summary>
        /// Completes the current file (thread-safe)
        /// </summary>
        public void CompleteFile()
        {
            var elapsed = DateTime.UtcNow - _startTime;
            _progressCollector.QueueVerboseMessage("File {0}: {1} completed in {2} - Total size: {3}",
                _currentFileName, _operationName.ToLower(), elapsed.ToString(@"hh\:mm\:ss"), 
                SizeFormatter.FormatBytes(_totalSize));

            _progressCollector.QueueProgressCompletion(FileActivityId, $"{_operationName} File");
        }

        /// <summary>
        /// Reports a chunk error (thread-safe)
        /// </summary>
        public void ReportChunkError(Exception error, int retryAttempt, int maxRetries)
        {
            _progressCollector.QueueVerboseMessage("File {0}: Chunk {1}/{2} failed (attempt {3}/{4}): {5}",
                _currentFileName, _currentChunk, _totalChunks, retryAttempt, maxRetries, error.Message);
        }

        /// <summary>
        /// Processes all queued updates from the main thread (must be called from main thread)
        /// </summary>
        public void ProcessQueuedUpdates()
        {
            _progressCollector.ProcessQueuedUpdates();
        }

        /// <summary>
        /// Completes all operations and processes final updates (must be called from main thread)
        /// </summary>
        public void Complete()
        {
            _progressCollector.Complete();
        }

        /// <summary>
        /// Gets the number of pending updates
        /// </summary>
        public int PendingUpdates => _progressCollector.PendingProgressUpdates + _progressCollector.PendingVerboseMessages;
    }
}
