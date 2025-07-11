using System;
using System.Management.Automation;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Centralized logging utility for PSMinIO operations
    /// Provides consistent logging with timestamps and proper PowerShell integration
    /// </summary>
    public static class MinIOLogger
    {
        /// <summary>
        /// Writes a verbose message with timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteVerbose(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            cmdlet.WriteVerbose($"[{timestamp}] {formattedMessage}");
        }

        /// <summary>
        /// Writes a warning message with timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Warning message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteWarning(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            cmdlet.WriteWarning($"[{timestamp}] {formattedMessage}");
        }

        /// <summary>
        /// Writes a debug message with timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Debug message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteDebug(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            cmdlet.WriteDebug($"[{timestamp}] {formattedMessage}");
        }

        /// <summary>
        /// Logs the start of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationStart(PSCmdlet cmdlet, string operationName, string? details = null)
        {
            var message = string.IsNullOrEmpty(details) 
                ? $"Starting {operationName}"
                : $"Starting {operationName} - {details}";
            
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs the successful completion of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="duration">Operation duration</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationComplete(PSCmdlet cmdlet, string operationName, TimeSpan duration, string? details = null)
        {
            var durationStr = SizeFormatter.FormatDuration(duration);
            var message = string.IsNullOrEmpty(details)
                ? $"Completed {operationName} in {durationStr}"
                : $"Completed {operationName} in {durationStr} - {details}";
            
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs the failure of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="exception">Exception that occurred</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationFailure(PSCmdlet cmdlet, string operationName, Exception exception, string? details = null)
        {
            var message = string.IsNullOrEmpty(details)
                ? $"Failed {operationName}: {exception.Message}"
                : $"Failed {operationName} - {details}: {exception.Message}";
            
            WriteWarning(cmdlet, message);
            WriteDebug(cmdlet, "Exception details: {0}", exception.ToString());
        }

        /// <summary>
        /// Logs transfer progress information
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operationName">Name of the transfer operation</param>
        /// <param name="bytesTransferred">Bytes transferred so far</param>
        /// <param name="totalBytes">Total bytes to transfer</param>
        /// <param name="speed">Current transfer speed in bytes per second</param>
        public static void LogTransferProgress(PSCmdlet cmdlet, string operationName, long bytesTransferred, long totalBytes, double speed)
        {
            var percentage = totalBytes > 0 ? (double)bytesTransferred / totalBytes * 100 : 0;
            var speedStr = SizeFormatter.FormatSpeed(speed);
            var transferredStr = SizeFormatter.FormatBytes(bytesTransferred);
            var totalStr = SizeFormatter.FormatBytes(totalBytes);

            WriteVerbose(cmdlet, "{0} progress: {1:F1}% ({2}/{3}) at {4}", 
                operationName, percentage, transferredStr, totalStr, speedStr);
        }

        /// <summary>
        /// Logs connection information
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="endpoint">MinIO endpoint</param>
        /// <param name="useSSL">Whether SSL is being used</param>
        /// <param name="region">Region</param>
        /// <param name="timeout">Timeout in seconds</param>
        public static void LogConnection(PSCmdlet cmdlet, string endpoint, bool useSSL, string region, int timeout)
        {
            WriteVerbose(cmdlet, "Connecting to MinIO - Endpoint: {0}, UseSSL: {1}, Region: {2}, Timeout: {3}s",
                endpoint, useSSL, region, timeout);
        }

        /// <summary>
        /// Logs retry attempt information
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operationName">Name of the operation being retried</param>
        /// <param name="attempt">Current attempt number</param>
        /// <param name="maxAttempts">Maximum number of attempts</param>
        /// <param name="delay">Delay before retry</param>
        /// <param name="reason">Reason for retry</param>
        public static void LogRetryAttempt(PSCmdlet cmdlet, string operationName, int attempt, int maxAttempts, TimeSpan delay, string reason)
        {
            WriteVerbose(cmdlet, "Retrying {0} (attempt {1}/{2}) after {3} - {4}",
                operationName, attempt, maxAttempts, SizeFormatter.FormatDuration(delay), reason);
        }

        /// <summary>
        /// Logs HTTP request information for debugging
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="method">HTTP method</param>
        /// <param name="url">Request URL</param>
        /// <param name="contentLength">Content length (if applicable)</param>
        public static void LogHttpRequest(PSCmdlet cmdlet, string method, string url, long? contentLength = null)
        {
            var message = contentLength.HasValue
                ? $"HTTP {method} {url} (Content-Length: {SizeFormatter.FormatBytes(contentLength.Value)})"
                : $"HTTP {method} {url}";
            
            WriteDebug(cmdlet, message);
        }

        /// <summary>
        /// Logs HTTP response information for debugging
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="contentLength">Response content length (if known)</param>
        /// <param name="duration">Request duration</param>
        public static void LogHttpResponse(PSCmdlet cmdlet, int statusCode, long? contentLength, TimeSpan duration)
        {
            var message = contentLength.HasValue
                ? $"HTTP {statusCode} (Content-Length: {SizeFormatter.FormatBytes(contentLength.Value)}) in {SizeFormatter.FormatDuration(duration)}"
                : $"HTTP {statusCode} in {SizeFormatter.FormatDuration(duration)}";
            
            WriteDebug(cmdlet, message);
        }
    }
}
