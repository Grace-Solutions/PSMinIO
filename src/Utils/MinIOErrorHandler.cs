using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Management.Automation;
using System.Text;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Centralized error handling utility for PSMinIO cmdlets
    /// Provides comprehensive error information including script details, line numbers, and context
    /// </summary>
    public static class MinIOErrorHandler
    {
        /// <summary>
        /// Handles and logs detailed error information for PSMinIO operations
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for logging</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="operationName">Name of the operation that failed</param>
        /// <param name="operationDetails">Additional details about the operation</param>
        /// <param name="errorCategory">PowerShell error category</param>
        /// <param name="targetObject">The target object related to the error</param>
        public static void HandleError(
            PSCmdlet cmdlet,
            Exception exception,
            string operationName,
            string? operationDetails = null,
            ErrorCategory errorCategory = ErrorCategory.NotSpecified,
            object? targetObject = null)
        {
            if (cmdlet == null)
                throw new ArgumentNullException(nameof(cmdlet));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            // Create detailed error information
            var errorDetails = CreateDetailedErrorInfo(exception, operationName, operationDetails);
            
            // Log detailed error information
            LogDetailedError(cmdlet, errorDetails);
            
            // Create and throw PowerShell error record
            var errorRecord = CreateErrorRecord(exception, operationName, errorCategory, targetObject);
            cmdlet.ThrowTerminatingError(errorRecord);
        }

        /// <summary>
        /// Handles and logs detailed error information for PSMinIO operations (non-terminating)
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for logging</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="operationName">Name of the operation that failed</param>
        /// <param name="operationDetails">Additional details about the operation</param>
        /// <param name="errorCategory">PowerShell error category</param>
        /// <param name="targetObject">The target object related to the error</param>
        public static void HandleNonTerminatingError(
            PSCmdlet cmdlet,
            Exception exception,
            string operationName,
            string? operationDetails = null,
            ErrorCategory errorCategory = ErrorCategory.NotSpecified,
            object? targetObject = null)
        {
            if (cmdlet == null)
                throw new ArgumentNullException(nameof(cmdlet));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            // Create detailed error information
            var errorDetails = CreateDetailedErrorInfo(exception, operationName, operationDetails);

            // Log detailed error information
            LogDetailedError(cmdlet, errorDetails);

            // Create and write PowerShell error record
            try
            {
                var errorRecord = CreateErrorRecord(exception, operationName, errorCategory, targetObject);
                cmdlet.WriteError(errorRecord);
            }
            catch (InvalidOperationException)
            {
                // Ignore threading errors - this means we're being called from a background thread
                // The error details are still captured in the exception
            }
        }

        /// <summary>
        /// Creates detailed error information dictionary
        /// </summary>
        private static OrderedDictionary CreateDetailedErrorInfo(Exception exception, string operationName, string? operationDetails)
        {
            var errorDetails = new OrderedDictionary();
            
            // Basic error information
            errorDetails.Add("Operation", operationName);
            errorDetails.Add("Message", exception.Message);
            errorDetails.Add("ExceptionType", exception.GetType().FullName);
            
            // Operation details if provided
            if (!string.IsNullOrEmpty(operationDetails))
            {
                errorDetails.Add("OperationDetails", operationDetails);
            }
            
            // Inner exception information
            if (exception.InnerException != null)
            {
                errorDetails.Add("InnerExceptionType", exception.InnerException.GetType().FullName);
                errorDetails.Add("InnerExceptionMessage", exception.InnerException.Message);
            }
            
            // Stack trace information
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                var stackLines = exception.StackTrace.Split('\n');
                if (stackLines.Length > 0)
                {
                    errorDetails.Add("StackTraceTop", stackLines[0].Trim());
                }
            }
            
            // System information
            errorDetails.Add("Timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            errorDetails.Add("MachineName", Environment.MachineName);
            errorDetails.Add("ProcessId", Process.GetCurrentProcess().Id);
            
            return errorDetails;
        }

        /// <summary>
        /// Logs detailed error information using PowerShell's Write-Warning
        /// Only logs if called from the main PowerShell thread to avoid threading issues
        /// </summary>
        private static void LogDetailedError(PSCmdlet cmdlet, OrderedDictionary errorDetails)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                // Log header
                cmdlet.WriteWarning($"{timestamp} - ERROR: PSMinIO Operation Failed");

                // Log each error detail
                foreach (DictionaryEntry detail in errorDetails)
                {
                    var key = detail.Key?.ToString() ?? "Unknown";
                    var value = detail.Value?.ToString() ?? "N/A";

                    // Truncate very long values for readability
                    if (value.Length > 200)
                    {
                        value = value.Substring(0, 197) + "...";
                    }

                    cmdlet.WriteWarning($"{timestamp} - ERROR: {key}: {value}");
                }
            }
            catch (InvalidOperationException)
            {
                // Ignore threading errors - this means we're being called from a background thread
                // The error details will still be included in the exception that gets thrown
            }
        }

        /// <summary>
        /// Creates a PowerShell ErrorRecord with appropriate categorization
        /// </summary>
        private static ErrorRecord CreateErrorRecord(Exception exception, string operationName, ErrorCategory errorCategory, object? targetObject)
        {
            // Determine error category if not specified
            if (errorCategory == ErrorCategory.NotSpecified)
            {
                errorCategory = DetermineErrorCategory(exception);
            }
            
            // Create error ID
            var errorId = $"PSMinIO.{operationName}.{exception.GetType().Name}";
            
            return new ErrorRecord(exception, errorId, errorCategory, targetObject);
        }

        /// <summary>
        /// Determines appropriate PowerShell error category based on exception type
        /// </summary>
        private static ErrorCategory DetermineErrorCategory(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => ErrorCategory.InvalidArgument,
                ArgumentException => ErrorCategory.InvalidArgument,
                UnauthorizedAccessException => ErrorCategory.PermissionDenied,
                System.Net.Http.HttpRequestException => ErrorCategory.ConnectionError,
                TimeoutException => ErrorCategory.OperationTimeout,
                System.IO.FileNotFoundException => ErrorCategory.ObjectNotFound,
                System.IO.DirectoryNotFoundException => ErrorCategory.ObjectNotFound,
                System.IO.IOException => ErrorCategory.WriteError,
                InvalidOperationException => ErrorCategory.InvalidOperation,
                NotSupportedException => ErrorCategory.NotImplemented,
                _ => ErrorCategory.NotSpecified
            };
        }

        /// <summary>
        /// Creates a formatted error message for operation failures
        /// </summary>
        /// <param name="operationName">Name of the failed operation</param>
        /// <param name="details">Operation details</param>
        /// <param name="exception">The exception that occurred</param>
        /// <returns>Formatted error message</returns>
        public static string CreateOperationErrorMessage(string operationName, string? details, Exception exception)
        {
            var message = new StringBuilder();
            message.Append($"Operation failed: {operationName}");
            
            if (!string.IsNullOrEmpty(details))
            {
                message.Append($" - {details}");
            }
            
            message.Append($": {exception.Message}");
            
            return message.ToString();
        }

        /// <summary>
        /// Validates common parameters and throws appropriate exceptions
        /// </summary>
        /// <param name="bucketName">Bucket name to validate</param>
        /// <param name="objectName">Object name to validate (optional)</param>
        public static void ValidateParameters(string? bucketName, string? objectName = null)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            }
            
            if (objectName != null && string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be empty when specified", nameof(objectName));
            }
        }
    }
}
