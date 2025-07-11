using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using PSMinIO.Models;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Thread-safe result collector that accumulates results from background threads
    /// and allows the main thread to safely output them via WriteObject
    /// </summary>
    public class ThreadSafeResultCollector
    {
        private readonly PSCmdlet _cmdlet;
        private readonly ConcurrentQueue<object> _resultQueue = new();
        private readonly ConcurrentQueue<ErrorRecord> _errorQueue = new();
        private readonly object _lockObject = new();
        private volatile bool _isCompleted = false;

        public ThreadSafeResultCollector(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
        }

        /// <summary>
        /// Queues a successful result from a background thread
        /// </summary>
        public void QueueResult(object result)
        {
            if (_isCompleted || result == null) return;

            _resultQueue.Enqueue(result);
        }

        /// <summary>
        /// Queues an error from a background thread
        /// </summary>
        public void QueueError(ErrorRecord error)
        {
            if (_isCompleted || error == null) return;

            _errorQueue.Enqueue(error);
        }

        /// <summary>
        /// Queues an error from a background thread using exception details
        /// </summary>
        public void QueueError(Exception exception, string errorId, ErrorCategory category, object targetObject)
        {
            if (_isCompleted || exception == null) return;

            var errorRecord = new ErrorRecord(exception, errorId, category, targetObject);
            _errorQueue.Enqueue(errorRecord);
        }

        /// <summary>
        /// Processes all queued results and errors from the main thread (safe to call PowerShell methods)
        /// </summary>
        public void ProcessQueuedResults()
        {
            // Process errors first
            while (_errorQueue.TryDequeue(out var error))
            {
                _cmdlet.WriteError(error);
            }

            // Process successful results
            while (_resultQueue.TryDequeue(out var result))
            {
                _cmdlet.WriteObject(result);
            }
        }

        /// <summary>
        /// Marks the collector as completed and processes all remaining results
        /// </summary>
        public void Complete()
        {
            _isCompleted = true;
            
            // Process any remaining results
            ProcessQueuedResults();
        }

        /// <summary>
        /// Gets all queued results without processing them (for inspection)
        /// </summary>
        public List<object> GetQueuedResults()
        {
            var results = new List<object>();
            var tempQueue = new Queue<object>();

            // Dequeue all items and re-queue them
            while (_resultQueue.TryDequeue(out var result))
            {
                results.Add(result);
                tempQueue.Enqueue(result);
            }

            // Re-queue the items
            while (tempQueue.Count > 0)
            {
                _resultQueue.Enqueue(tempQueue.Dequeue());
            }

            return results;
        }

        /// <summary>
        /// Gets all queued errors without processing them (for inspection)
        /// </summary>
        public List<ErrorRecord> GetQueuedErrors()
        {
            var errors = new List<ErrorRecord>();
            var tempQueue = new Queue<ErrorRecord>();

            // Dequeue all items and re-queue them
            while (_errorQueue.TryDequeue(out var error))
            {
                errors.Add(error);
                tempQueue.Enqueue(error);
            }

            // Re-queue the items
            while (tempQueue.Count > 0)
            {
                _errorQueue.Enqueue(tempQueue.Dequeue());
            }

            return errors;
        }

        /// <summary>
        /// Gets the number of pending results
        /// </summary>
        public int PendingResults => _resultQueue.Count;

        /// <summary>
        /// Gets the number of pending errors
        /// </summary>
        public int PendingErrors => _errorQueue.Count;

        /// <summary>
        /// Gets whether the collector has any pending items
        /// </summary>
        public bool HasPendingItems => PendingResults > 0 || PendingErrors > 0;
    }
}
