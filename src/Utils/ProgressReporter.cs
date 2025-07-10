using System;
using System.Diagnostics;
using System.Management.Automation;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Utility class for reporting progress during file operations
    /// </summary>
    public class ProgressReporter
    {
        private readonly PSCmdlet _cmdlet;
        private readonly string _activity;
        private readonly string _statusDescription;
        private readonly long _totalBytes;
        private readonly Stopwatch _stopwatch;
        private long _bytesProcessed;
        private DateTime _lastUpdateTime;
        private readonly int _activityId;

        /// <summary>
        /// Creates a new ProgressReporter instance
        /// </summary>
        /// <param name="cmdlet">The cmdlet to report progress to</param>
        /// <param name="activity">Description of the activity</param>
        /// <param name="statusDescription">Status description</param>
        /// <param name="totalBytes">Total number of bytes to process</param>
        /// <param name="activityId">Unique activity ID for progress reporting</param>
        public ProgressReporter(PSCmdlet cmdlet, string activity, string statusDescription, long totalBytes, int activityId = 1)
        {
            _cmdlet = cmdlet ?? throw new ArgumentNullException(nameof(cmdlet));
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _statusDescription = statusDescription ?? throw new ArgumentNullException(nameof(statusDescription));
            _totalBytes = totalBytes;
            _activityId = activityId;
            _stopwatch = Stopwatch.StartNew();
            _lastUpdateTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the progress with the number of bytes processed
        /// </summary>
        /// <param name="bytesProcessed">Number of bytes processed so far</param>
        public void UpdateProgress(long bytesProcessed)
        {
            _bytesProcessed = bytesProcessed;

            // Only update progress every 100ms to avoid overwhelming the console
            var now = DateTime.UtcNow;
            if ((now - _lastUpdateTime).TotalMilliseconds < 100 && bytesProcessed < _totalBytes)
            {
                return;
            }

            _lastUpdateTime = now;

            var percentComplete = _totalBytes > 0 ? (int)((double)bytesProcessed / _totalBytes * 100) : 0;
            var currentStatus = FormatCurrentStatus(bytesProcessed);

            var progressRecord = new ProgressRecord(_activityId, _activity, currentStatus)
            {
                PercentComplete = Math.Min(percentComplete, 100)
            };

            // Add remaining time estimate if we have enough data
            if (_stopwatch.ElapsedMilliseconds > 1000 && bytesProcessed > 0 && bytesProcessed < _totalBytes)
            {
                var remainingTime = EstimateRemainingTime(bytesProcessed);
                if (remainingTime.HasValue)
                {
                    progressRecord.SecondsRemaining = (int)remainingTime.Value.TotalSeconds;
                }
            }

            _cmdlet.WriteProgress(progressRecord);
        }

        /// <summary>
        /// Completes the progress reporting
        /// </summary>
        public void Complete()
        {
            _stopwatch.Stop();

            var progressRecord = new ProgressRecord(_activityId, _activity, "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            };

            _cmdlet.WriteProgress(progressRecord);

            // Log completion details
            MinIOLogger.WriteVerbose(_cmdlet,
                $"Operation completed: {SizeFormatter.FormatBytes(_totalBytes)} processed in {_stopwatch.Elapsed.TotalSeconds:F1} seconds " +
                $"(Average speed: {SizeFormatter.FormatBytesPerSecond(CalculateAverageSpeed())})");
        }

        /// <summary>
        /// Formats the current status string
        /// </summary>
        /// <param name="bytesProcessed">Number of bytes processed</param>
        /// <returns>Formatted status string</returns>
        private string FormatCurrentStatus(long bytesProcessed)
        {
            var processedFormatted = SizeFormatter.FormatBytes(bytesProcessed);
            var totalFormatted = SizeFormatter.FormatBytes(_totalBytes);
            var speed = CalculateCurrentSpeed();
            var speedFormatted = SizeFormatter.FormatBytesPerSecond(speed);

            return $"{_statusDescription}: {processedFormatted} / {totalFormatted} ({speedFormatted})";
        }

        /// <summary>
        /// Calculates the current transfer speed in bytes per second
        /// </summary>
        /// <returns>Current speed in bytes per second</returns>
        private double CalculateCurrentSpeed()
        {
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            return elapsedSeconds > 0 ? _bytesProcessed / elapsedSeconds : 0;
        }

        /// <summary>
        /// Calculates the average transfer speed in bytes per second
        /// </summary>
        /// <returns>Average speed in bytes per second</returns>
        private double CalculateAverageSpeed()
        {
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            return elapsedSeconds > 0 ? _totalBytes / elapsedSeconds : 0;
        }

        /// <summary>
        /// Estimates the remaining time for the operation
        /// </summary>
        /// <param name="bytesProcessed">Number of bytes processed so far</param>
        /// <returns>Estimated remaining time, or null if cannot be calculated</returns>
        private TimeSpan? EstimateRemainingTime(long bytesProcessed)
        {
            if (bytesProcessed <= 0 || _stopwatch.ElapsedMilliseconds <= 0)
                return null;

            var remainingBytes = _totalBytes - bytesProcessed;
            var currentSpeed = CalculateCurrentSpeed();

            if (currentSpeed <= 0)
                return null;

            var remainingSeconds = remainingBytes / currentSpeed;
            return TimeSpan.FromSeconds(remainingSeconds);
        }


    }
}
