using System;
using System.Management.Automation;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Centralized logging utility for PSMinIO module
    /// </summary>
    public static class MinIOLogger
    {
        /// <summary>
        /// Log levels for different types of messages
        /// </summary>
        public enum LogLevel
        {
            Verbose,
            Information,
            Warning,
            Error
        }

        /// <summary>
        /// Writes a verbose message if verbose preference allows it
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteVerbose(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var formattedMessage = FormatLogMessage(LogLevel.Verbose, message, args);
            cmdlet.WriteVerbose(formattedMessage);
        }

        /// <summary>
        /// Writes an information message
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteInformation(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var formattedMessage = FormatLogMessage(LogLevel.Information, message, args);
            
            // Use WriteInformation if available (PowerShell 5.0+), otherwise WriteVerbose
            try
            {
                var infoRecord = new InformationRecord(formattedMessage, "PSMinIO");
                cmdlet.WriteInformation(infoRecord);
            }
            catch
            {
                // Fallback to WriteVerbose for older PowerShell versions
                cmdlet.WriteVerbose(formattedMessage);
            }
        }

        /// <summary>
        /// Writes a warning message
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteWarning(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var formattedMessage = FormatLogMessage(LogLevel.Warning, message, args);
            cmdlet.WriteWarning(formattedMessage);
        }

        /// <summary>
        /// Writes an error message
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="message">Message to log</param>
        /// <param name="args">Optional format arguments</param>
        public static void WriteError(PSCmdlet cmdlet, string message, params object[] args)
        {
            if (cmdlet == null) return;

            var formattedMessage = FormatLogMessage(LogLevel.Error, message, args);
            var errorRecord = new ErrorRecord(
                new InvalidOperationException(formattedMessage),
                "PSMinIOError",
                ErrorCategory.InvalidOperation,
                null);
            
            cmdlet.WriteError(errorRecord);
        }

        /// <summary>
        /// Writes an error from an exception
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="exception">Exception to log</param>
        /// <param name="errorId">Error identifier</param>
        /// <param name="category">Error category</param>
        /// <param name="targetObject">Target object that caused the error</param>
        public static void WriteError(PSCmdlet cmdlet, Exception exception, string errorId, 
            ErrorCategory category = ErrorCategory.InvalidOperation, object? targetObject = null)
        {
            if (cmdlet == null) return;

            var errorRecord = new ErrorRecord(exception, errorId, category, targetObject);
            cmdlet.WriteError(errorRecord);
        }

        /// <summary>
        /// Logs the start of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operation">Name of the operation</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationStart(PSCmdlet cmdlet, string operation, string? details = null)
        {
            var message = string.IsNullOrEmpty(details) 
                ? $"Starting operation: {operation}"
                : $"Starting operation: {operation} - {details}";
            
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs the completion of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operation">Name of the operation</param>
        /// <param name="duration">Optional operation duration</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationComplete(PSCmdlet cmdlet, string operation, TimeSpan? duration = null, string? details = null)
        {
            var message = $"Completed operation: {operation}";
            
            if (duration.HasValue)
            {
                message += $" (Duration: {duration.Value.TotalMilliseconds:F0}ms)";
            }
            
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs an operation failure
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for context</param>
        /// <param name="operation">Name of the operation</param>
        /// <param name="exception">Exception that occurred</param>
        /// <param name="details">Optional operation details</param>
        public static void LogOperationFailure(PSCmdlet cmdlet, string operation, Exception exception, string? details = null)
        {
            var message = $"Operation failed: {operation} - {exception.Message}";
            
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            
            WriteError(cmdlet, message);
            WriteVerbose(cmdlet, $"Exception details: {exception}");
        }

        /// <summary>
        /// Formats a log message with timestamp and level, automatically formatting byte sizes
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="message">Message to format</param>
        /// <param name="args">Optional format arguments</param>
        /// <returns>Formatted log message</returns>
        private static string FormatLogMessage(LogLevel level, string message, params object[] args)
        {
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");

            // Process arguments to format byte sizes intelligently
            var processedArgs = ProcessLogArguments(args);
            var formattedMessage = processedArgs.Length > 0 ? string.Format(message, processedArgs) : message;

            return $"{timestamp} - {formattedMessage}";
        }

        /// <summary>
        /// Processes log arguments to format byte sizes intelligently
        /// </summary>
        /// <param name="args">Original arguments</param>
        /// <returns>Processed arguments with formatted sizes</returns>
        private static object[] ProcessLogArguments(object[] args)
        {
            if (args == null || args.Length == 0)
                return args ?? new object[0];

            var processedArgs = new object[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                // Check if this looks like a byte size that should be formatted
                if (IsLikelyByteSize(arg))
                {
                    if (long.TryParse(arg.ToString(), out var bytes))
                    {
                        processedArgs[i] = SizeFormatter.FormatBytes(bytes);
                    }
                    else
                    {
                        processedArgs[i] = arg;
                    }
                }
                else
                {
                    processedArgs[i] = arg;
                }
            }

            return processedArgs;
        }

        /// <summary>
        /// Determines if an argument is likely a byte size that should be formatted
        /// </summary>
        /// <param name="arg">Argument to check</param>
        /// <returns>True if likely a byte size</returns>
        private static bool IsLikelyByteSize(object arg)
        {
            // Only format long integers that are likely byte sizes
            // We use a heuristic: values >= 1024 are likely byte sizes
            if (arg is long longValue)
            {
                return longValue >= 1024;
            }

            if (arg is int intValue)
            {
                return intValue >= 1024;
            }

            return false;
        }
    }
}
