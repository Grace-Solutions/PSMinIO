using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Threading;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Thread-safe progress data collector that accumulates progress updates from background threads
    /// and allows the main thread to safely report them to PowerShell
    ///
    /// CRITICAL THREADING RULE:
    /// PowerShell cmdlets can ONLY call Write-Progress, Write-Verbose, Write-Object, Write-Error
    /// from the main cmdlet thread - NEVER from background threads!
    ///
    /// USAGE PATTERN:
    /// - Background threads: Call QueueProgressUpdate(), QueueVerboseMessage() (thread-safe)
    /// - Main thread ONLY: Call ProcessQueuedUpdates() to display queued updates
    ///
    /// EXAMPLE:
    /// Task.Run(() => {
    ///     collector.QueueProgressUpdate(1, "Processing", "Status", 50); // ✅ Safe
    ///     // WriteProgress(...); // ❌ THREADING ERROR!
    /// });
    /// collector.ProcessQueuedUpdates(); // ✅ Only from main thread
    /// </summary>
    public class ThreadSafeProgressCollector
    {
        private readonly PSCmdlet _cmdlet;
        private readonly ConcurrentQueue<ProgressUpdate> _progressQueue = new();
        private readonly ConcurrentQueue<VerboseMessage> _verboseQueue = new();
        private readonly object _lockObject = new();
        private volatile bool _isCompleted = false;

        public ThreadSafeProgressCollector(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
        }

        /// <summary>
        /// Queues a progress update from a background thread
        /// </summary>
        public void QueueProgressUpdate(int activityId, string activity, string statusDescription, int percentComplete, int parentActivityId = -1)
        {
            if (_isCompleted) return;

            _progressQueue.Enqueue(new ProgressUpdate
            {
                ActivityId = activityId,
                Activity = activity,
                StatusDescription = statusDescription,
                PercentComplete = percentComplete,
                ParentActivityId = parentActivityId,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Queues a progress completion from a background thread
        /// </summary>
        public void QueueProgressCompletion(int activityId, string activity, int parentActivityId = -1)
        {
            if (_isCompleted) return;

            _progressQueue.Enqueue(new ProgressUpdate
            {
                ActivityId = activityId,
                Activity = activity,
                StatusDescription = "Completed",
                PercentComplete = 100,
                ParentActivityId = parentActivityId,
                IsCompleted = true,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Queues a verbose message from a background thread
        /// </summary>
        public void QueueVerboseMessage(string message, params object[] args)
        {
            if (_isCompleted) return;

            _verboseQueue.Enqueue(new VerboseMessage
            {
                Message = args.Length > 0 ? string.Format(message, args) : message,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Processes all queued updates from the main thread (safe to call PowerShell methods)
        /// </summary>
        public void ProcessQueuedUpdates()
        {
            // Process verbose messages first
            while (_verboseQueue.TryDequeue(out var verboseMessage))
            {
                MinIOLogger.WriteVerbose(_cmdlet, verboseMessage.Message);
            }

            // Process progress updates
            while (_progressQueue.TryDequeue(out var progressUpdate))
            {
                var progressRecord = new ProgressRecord(
                    progressUpdate.ActivityId,
                    progressUpdate.Activity,
                    progressUpdate.StatusDescription)
                {
                    PercentComplete = progressUpdate.PercentComplete
                };

                if (progressUpdate.ParentActivityId >= 0)
                {
                    progressRecord.ParentActivityId = progressUpdate.ParentActivityId;
                }

                if (progressUpdate.IsCompleted)
                {
                    progressRecord.RecordType = ProgressRecordType.Completed;
                }

                _cmdlet.WriteProgress(progressRecord);
            }
        }

        /// <summary>
        /// Marks the collector as completed (no more updates will be accepted)
        /// </summary>
        public void Complete()
        {
            _isCompleted = true;
            // Process any remaining updates
            ProcessQueuedUpdates();
        }

        /// <summary>
        /// Gets the number of pending progress updates
        /// </summary>
        public int PendingProgressUpdates => _progressQueue.Count;

        /// <summary>
        /// Gets the number of pending verbose messages
        /// </summary>
        public int PendingVerboseMessages => _verboseQueue.Count;

        /// <summary>
        /// Progress update data structure
        /// </summary>
        private class ProgressUpdate
        {
            public int ActivityId { get; set; }
            public string Activity { get; set; } = string.Empty;
            public string StatusDescription { get; set; } = string.Empty;
            public int PercentComplete { get; set; }
            public int ParentActivityId { get; set; } = -1;
            public bool IsCompleted { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Verbose message data structure
        /// </summary>
        private class VerboseMessage
        {
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}
