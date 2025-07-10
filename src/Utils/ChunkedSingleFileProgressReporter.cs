using System;
using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Manages 2-layer progress reporting for chunked single file operations
    /// Layer 1: File Progress
    /// Layer 2: Current Chunk Progress
    /// </summary>
    public class ChunkedSingleFileProgressReporter
    {
        private readonly PSCmdlet _cmdlet;
        private readonly long _totalSize;
        private readonly int _totalChunks;
        private readonly DateTime _startTime;
        private readonly string _operationName;
        
        // Progress tracking
        private long _totalBytesTransferred = 0;
        private int _currentChunk = 0;
        private long _currentChunkBytesTransferred = 0;
        private long _currentChunkSize = 0;

        // Activity IDs for progress hierarchy
        private const int FileActivityId = 1;
        private const int ChunkActivityId = 2;

        // Progress control
        private readonly long _progressUpdateInterval;
        private long _lastProgressUpdate = 0;

        /// <summary>
        /// Creates a new chunked single file progress reporter
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for progress reporting</param>
        /// <param name="totalSize">Total size of the file</param>
        /// <param name="totalChunks">Total number of chunks</param>
        /// <param name="operationName">Name of the operation (e.g., "Downloading", "Uploading")</param>
        /// <param name="progressUpdateInterval">Update progress every N bytes</param>
        public ChunkedSingleFileProgressReporter(
            PSCmdlet cmdlet,
            long totalSize,
            int totalChunks,
            string operationName = "Processing",
            long progressUpdateInterval = 1024 * 1024) // 1MB default
        {
            _cmdlet = cmdlet;
            _totalSize = totalSize;
            _totalChunks = totalChunks;
            _operationName = operationName;
            _startTime = DateTime.Now;
            _progressUpdateInterval = progressUpdateInterval;
        }

        /// <summary>
        /// Starts processing a new chunk
        /// </summary>
        /// <param name="chunkNumber">Chunk number (1-based for display)</param>
        /// <param name="chunkSize">Size of the chunk</param>
        public void StartNewChunk(int chunkNumber, long chunkSize)
        {
            _currentChunk = chunkNumber;
            _currentChunkSize = chunkSize;
            _currentChunkBytesTransferred = 0;
            
            MinIOLogger.WriteVerbose(_cmdlet, "Starting chunk {0}/{1} ({2})",
                chunkNumber, _totalChunks, SizeFormatter.FormatBytes(chunkSize));
            
            UpdateAllProgress();
        }

        /// <summary>
        /// Updates chunk progress
        /// </summary>
        /// <param name="bytesTransferred">Bytes transferred for current chunk</param>
        public void UpdateChunkProgress(long bytesTransferred)
        {
            var chunkDelta = bytesTransferred - _currentChunkBytesTransferred;
            _currentChunkBytesTransferred = bytesTransferred;
            _totalBytesTransferred += chunkDelta;
            
            // Only update progress if we've transferred enough bytes since last update
            if (_totalBytesTransferred - _lastProgressUpdate >= _progressUpdateInterval)
            {
                UpdateAllProgress();
                _lastProgressUpdate = _totalBytesTransferred;
            }
        }

        /// <summary>
        /// Completes the current chunk
        /// </summary>
        /// <param name="chunkETag">ETag of the completed chunk (optional)</param>
        public void CompleteChunk(string? chunkETag = null)
        {
            MinIOLogger.WriteVerbose(_cmdlet, "Completed chunk {0}/{1}{2}", 
                _currentChunk, _totalChunks, 
                !string.IsNullOrEmpty(chunkETag) ? $" - ETag: {chunkETag}" : "");

            // Mark chunk as completed (only if progress is enabled)
            if (_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue")
                return;

            var chunkProgress = new ProgressRecord(ChunkActivityId, "Current Chunk", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed,
                ParentActivityId = FileActivityId
            };
            _cmdlet.WriteProgress(chunkProgress);
        }

        /// <summary>
        /// Completes the entire download operation
        /// </summary>
        public void CompleteDownload()
        {
            var elapsed = DateTime.Now - _startTime;
            MinIOLogger.WriteVerbose(_cmdlet, "Completed {0} ({1}) in {2}",
                _operationName.ToLower(), SizeFormatter.FormatBytes(_totalSize), elapsed.ToString(@"hh\:mm\:ss"));

            // Complete all progress records
            var fileProgress = new ProgressRecord(FileActivityId, $"{_operationName} File", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            };
            _cmdlet.WriteProgress(fileProgress);

            // Complete chunk progress if enabled
            if (!(_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                  _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue"))
            {
                CompleteChunk();
            }
        }

        /// <summary>
        /// Reports an error for the current chunk
        /// </summary>
        /// <param name="error">Error that occurred</param>
        /// <param name="retryAttempt">Current retry attempt</param>
        /// <param name="maxRetries">Maximum retry attempts</param>
        public void ReportChunkError(Exception error, int retryAttempt, int maxRetries)
        {
            MinIOLogger.WriteVerbose(_cmdlet, "Chunk {0}/{1} failed (attempt {2}/{3}): {4}", 
                _currentChunk, _totalChunks, retryAttempt, maxRetries, error.Message);
        }

        /// <summary>
        /// Updates all progress layers
        /// </summary>
        private void UpdateAllProgress()
        {
            var elapsed = DateTime.Now - _startTime;
            var speed = elapsed.TotalSeconds > 0 ? _totalBytesTransferred / elapsed.TotalSeconds : 0;

            // Layer 1: File Progress (always shown)
            var filePercent = _totalSize > 0 ? (int)((_totalBytesTransferred * 100) / _totalSize) : 0;
            var fileStatus = $"Size: {SizeFormatter.FormatBytes(_totalBytesTransferred)}/{SizeFormatter.FormatBytes(_totalSize)} | " +
                           $"Speed: {SizeFormatter.FormatBytes((long)speed)}/s | " +
                           $"Elapsed: {elapsed:hh\\:mm\\:ss}";

            var fileProgress = new ProgressRecord(FileActivityId, $"{_operationName} File", fileStatus)
            {
                PercentComplete = filePercent
            };
            _cmdlet.WriteProgress(fileProgress);

            // Check if progress is disabled
            if (_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue")
                return;

            // Layer 2: Current Chunk Progress
            if (_currentChunk > 0)
            {
                var chunkPercent = _currentChunkSize > 0 ? (int)((_currentChunkBytesTransferred * 100) / _currentChunkSize) : 0;
                var chunkStatus = $"Chunk: {_currentChunk}/{_totalChunks} | " +
                                $"Size: {SizeFormatter.FormatBytes(_currentChunkBytesTransferred)}/{SizeFormatter.FormatBytes(_currentChunkSize)} | " +
                                $"Speed: {SizeFormatter.FormatBytes((long)speed)}/s";

                var chunkProgress = new ProgressRecord(ChunkActivityId, "Current Chunk", chunkStatus)
                {
                    PercentComplete = chunkPercent,
                    ParentActivityId = FileActivityId
                };
                _cmdlet.WriteProgress(chunkProgress);
            }
        }
    }
}
