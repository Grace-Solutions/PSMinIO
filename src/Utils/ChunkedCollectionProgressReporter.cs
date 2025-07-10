using System;
using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Manages 3-layer progress reporting for chunked file collection operations
    /// Layer 1: Collection Progress (overall files)
    /// Layer 2: Current File Progress
    /// Layer 3: Current Chunk Progress
    /// </summary>
    public class ChunkedCollectionProgressReporter
    {
        private readonly PSCmdlet _cmdlet;
        private readonly int _totalFiles;
        private readonly long _totalSize;
        private readonly DateTime _startTime;
        private readonly string _operationName;
        
        // Progress tracking
        private int _completedFiles = 0;
        private long _totalBytesTransferred = 0;
        private string _currentFileName = "";
        private long _currentFileSize = 0;
        private long _currentFileBytesTransferred = 0;
        private int _currentChunk = 0;
        private int _totalChunks = 0;
        private long _currentChunkBytesTransferred = 0;
        private long _currentChunkSize = 0;

        // Activity IDs for progress hierarchy
        private const int CollectionActivityId = 1;
        private const int FileActivityId = 2;
        private const int ChunkActivityId = 3;

        // Progress control
        private readonly long _progressUpdateInterval;
        private long _lastProgressUpdate = 0;

        /// <summary>
        /// Creates a new chunked collection progress reporter
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for progress reporting</param>
        /// <param name="totalFiles">Total number of files to process</param>
        /// <param name="totalSize">Total size of all files</param>
        /// <param name="operationName">Name of the operation (e.g., "Uploading", "Downloading")</param>
        /// <param name="progressUpdateInterval">Update progress every N bytes</param>
        public ChunkedCollectionProgressReporter(
            PSCmdlet cmdlet,
            int totalFiles,
            long totalSize,
            string operationName = "Processing",
            long progressUpdateInterval = 1024 * 1024) // 1MB default
        {
            _cmdlet = cmdlet;
            _totalFiles = totalFiles;
            _totalSize = totalSize;
            _operationName = operationName;
            _startTime = DateTime.Now;
            _progressUpdateInterval = progressUpdateInterval;
        }

        /// <summary>
        /// Starts processing a new file
        /// </summary>
        /// <param name="fileName">Name of the file being processed</param>
        /// <param name="fileSize">Size of the file</param>
        /// <param name="totalChunks">Total number of chunks for this file</param>
        public void StartNewFile(string fileName, long fileSize, int totalChunks)
        {
            _currentFileName = fileName;
            _currentFileSize = fileSize;
            _currentFileBytesTransferred = 0;
            _totalChunks = totalChunks;
            _currentChunk = 0;
            
            MinIOLogger.WriteVerbose(_cmdlet, "Starting {0} of file {1}/{2}: {3} ({4})", 
                _operationName.ToLower(), _completedFiles + 1, _totalFiles, fileName, SizeFormatter.FormatSize(fileSize));
            
            UpdateAllProgress();
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
            
            MinIOLogger.WriteVerbose(_cmdlet, "File {0}: Starting chunk {1}/{2} ({3})", 
                _currentFileName, chunkNumber, _totalChunks, SizeFormatter.FormatSize(chunkSize));
            
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
            _currentFileBytesTransferred += chunkDelta;
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
            MinIOLogger.WriteVerbose(_cmdlet, "File {0}: Completed chunk {1}/{2}{3}", 
                _currentFileName, _currentChunk, _totalChunks, 
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
        /// Completes the current file
        /// </summary>
        public void CompleteFile()
        {
            _completedFiles++;
            
            var elapsed = DateTime.Now - _startTime;
            MinIOLogger.WriteVerbose(_cmdlet, "File {0}: {1} completed in {2} - Total size: {3}", 
                _currentFileName, _operationName.ToLower(), elapsed.ToString(@"hh\:mm\:ss"), SizeFormatter.FormatSize(_currentFileSize));

            // Mark file as completed (only if progress is enabled)
            if (_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue")
                return;

            var fileProgress = new ProgressRecord(FileActivityId, "Current File", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed,
                ParentActivityId = CollectionActivityId
            };
            _cmdlet.WriteProgress(fileProgress);

            // Complete chunk progress too
            CompleteChunk();
        }

        /// <summary>
        /// Completes the entire collection operation
        /// </summary>
        public void CompleteCollection()
        {
            var elapsed = DateTime.Now - _startTime;
            MinIOLogger.WriteVerbose(_cmdlet, "Completed {0} {1} files ({2}) in {3}", 
                _operationName.ToLower(), _totalFiles, SizeFormatter.FormatSize(_totalSize), elapsed.ToString(@"hh\:mm\:ss"));

            // Complete all progress records
            var collectionProgress = new ProgressRecord(CollectionActivityId, $"{_operationName} Files", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            };
            _cmdlet.WriteProgress(collectionProgress);

            // Complete file progress if enabled
            if (!(_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                  _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue"))
            {
                CompleteFile();
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
            MinIOLogger.WriteVerbose(_cmdlet, "File {0}: Chunk {1}/{2} failed (attempt {3}/{4}): {5}", 
                _currentFileName, _currentChunk, _totalChunks, retryAttempt, maxRetries, error.Message);
        }

        /// <summary>
        /// Updates all progress layers
        /// </summary>
        private void UpdateAllProgress()
        {
            var elapsed = DateTime.Now - _startTime;
            var speed = elapsed.TotalSeconds > 0 ? _totalBytesTransferred / elapsed.TotalSeconds : 0;

            // Layer 1: Collection Progress (always shown)
            var collectionPercent = _totalSize > 0 ? (int)((_totalBytesTransferred * 100) / _totalSize) : 0;
            var collectionStatus = $"Files: {_completedFiles}/{_totalFiles} | " +
                                  $"Size: {SizeFormatter.FormatSize(_totalBytesTransferred)}/{SizeFormatter.FormatSize(_totalSize)} | " +
                                  $"Speed: {SizeFormatter.FormatSize((long)speed)}/s | " +
                                  $"Elapsed: {elapsed:hh\\:mm\\:ss}";

            var collectionProgress = new ProgressRecord(CollectionActivityId, $"{_operationName} Files", collectionStatus)
            {
                PercentComplete = collectionPercent
            };
            _cmdlet.WriteProgress(collectionProgress);

            // Check if progress is disabled
            if (_cmdlet.MyInvocation.BoundParameters.ContainsKey("ProgressAction") &&
                _cmdlet.MyInvocation.BoundParameters["ProgressAction"].ToString() == "SilentlyContinue")
                return;

            // Layer 2: Current File Progress
            if (!string.IsNullOrEmpty(_currentFileName))
            {
                var filePercent = _currentFileSize > 0 ? (int)((_currentFileBytesTransferred * 100) / _currentFileSize) : 0;
                var fileStatus = $"File: {_currentFileName} | " +
                               $"Size: {SizeFormatter.FormatSize(_currentFileBytesTransferred)}/{SizeFormatter.FormatSize(_currentFileSize)}";

                var fileProgress = new ProgressRecord(FileActivityId, "Current File", fileStatus)
                {
                    PercentComplete = filePercent,
                    ParentActivityId = CollectionActivityId
                };
                _cmdlet.WriteProgress(fileProgress);
            }

            // Layer 3: Current Chunk Progress
            if (_currentChunk > 0)
            {
                var chunkPercent = _currentChunkSize > 0 ? (int)((_currentChunkBytesTransferred * 100) / _currentChunkSize) : 0;
                var chunkStatus = $"Chunk: {_currentChunk}/{_totalChunks} | " +
                                $"Size: {SizeFormatter.FormatSize(_currentChunkBytesTransferred)}/{SizeFormatter.FormatSize(_currentChunkSize)} | " +
                                $"Speed: {SizeFormatter.FormatSize((long)speed)}/s";

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
