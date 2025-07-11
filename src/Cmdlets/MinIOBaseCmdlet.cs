using System;
using System.Management.Automation;
using PSMinIO.Core;
using PSMinIO.Core.S3;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Base class for all MinIO PowerShell cmdlets
    /// Provides common functionality including connection management, logging, and client access
    /// </summary>
    public abstract class MinIOBaseCmdlet : PSCmdlet
    {
        private MinIOConnection? _connection;

        /// <summary>
        /// MinIO connection to use for operations. Can be provided via parameter or retrieved from session.
        /// </summary>
        [Parameter(ValueFromPipeline = true)]
        [Alias("Connection")]
        public MinIOConnection? MinIOConnection { get; set; }

        /// <summary>
        /// Name of session variable containing the MinIO connection (default: MinIOConnection)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string SessionVariable { get; set; } = "MinIOConnection";

        /// <summary>
        /// Gets the MinIO connection instance
        /// </summary>
        protected MinIOConnection Connection
        {
            get
            {
                if (_connection == null)
                {
                    _connection = GetConnection();
                    if (_connection == null)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException("No MinIO connection available. Use Connect-MinIO to establish a connection, or provide a connection via the -MinIOConnection parameter."),
                            "NoConnection",
                            ErrorCategory.ConnectionError,
                            null));
                    }

                    if (!_connection!.IsValid)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException($"MinIO connection is not valid. Status: {_connection.Status}"),
                            "InvalidConnection",
                            ErrorCategory.ConnectionError,
                            _connection));
                    }
                }

                return _connection!;
            }
        }

        /// <summary>
        /// Gets the MinIO S3 client instance
        /// </summary>
        protected MinIOS3Client S3Client => Connection.S3Client;

        /// <summary>
        /// Gets the connection from parameter or session variable
        /// </summary>
        /// <returns>MinIO connection or null if not found</returns>
        private MinIOConnection? GetConnection()
        {
            // First, try the parameter
            if (MinIOConnection != null)
            {
                return MinIOConnection;
            }

            // Then try the session variable
            try
            {
                var sessionConnection = SessionState.PSVariable.GetValue(SessionVariable);
                if (sessionConnection is MinIOConnection connection)
                {
                    return connection;
                }
            }
            catch
            {
                // Ignore errors when accessing session variables
            }

            return null;
        }

        /// <summary>
        /// Executes an operation with consistent error handling and logging
        /// </summary>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="operation">Operation to execute</param>
        /// <param name="details">Optional operation details for logging</param>
        protected void ExecuteOperation(string operationName, Action operation, string? details = null)
        {
            var startTime = DateTime.UtcNow;
            MinIOLogger.LogOperationStart(this, operationName, details);

            try
            {
                operation();
                var duration = DateTime.UtcNow - startTime;
                MinIOLogger.LogOperationComplete(this, operationName, duration, details);
            }
            catch (Exception ex)
            {
                MinIOLogger.LogOperationFailure(this, operationName, ex, details);

                // Use centralized enhanced error handling
                MinIOErrorHandler.HandleError(this, ex, operationName, details);
            }
        }

        /// <summary>
        /// Executes an operation with return value and consistent error handling and logging
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="operation">Operation to execute</param>
        /// <param name="details">Optional operation details for logging</param>
        /// <returns>Operation result</returns>
        protected T ExecuteOperation<T>(string operationName, Func<T> operation, string? details = null)
        {
            var startTime = DateTime.UtcNow;
            MinIOLogger.LogOperationStart(this, operationName, details);

            try
            {
                var result = operation();
                var duration = DateTime.UtcNow - startTime;
                MinIOLogger.LogOperationComplete(this, operationName, duration, details);
                return result;
            }
            catch (Exception ex)
            {
                MinIOLogger.LogOperationFailure(this, operationName, ex, details);

                // Use centralized enhanced error handling
                MinIOErrorHandler.HandleError(this, ex, operationName, details);

                // This line will never be reached, but is required for compilation
                throw;
            }
        }

        /// <summary>
        /// Determines the appropriate PowerShell error category for an exception
        /// </summary>
        /// <param name="exception">Exception to categorize</param>
        /// <returns>Appropriate error category</returns>
        protected ErrorCategory GetErrorCategory(Exception exception)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException => ErrorCategory.InvalidArgument,
                UnauthorizedAccessException => ErrorCategory.PermissionDenied,
                System.Net.Http.HttpRequestException => ErrorCategory.ConnectionError,
                TimeoutException => ErrorCategory.OperationTimeout,
                InvalidOperationException => ErrorCategory.InvalidOperation,
                System.IO.FileNotFoundException => ErrorCategory.ObjectNotFound,
                System.IO.DirectoryNotFoundException => ErrorCategory.ObjectNotFound,
                System.IO.IOException => ErrorCategory.WriteError,
                NotSupportedException => ErrorCategory.NotImplemented,
                _ => ErrorCategory.NotSpecified
            };
        }

        /// <summary>
        /// Validates that a bucket name is valid according to S3 naming rules
        /// </summary>
        /// <param name="bucketName">Bucket name to validate</param>
        /// <param name="parameterName">Parameter name for error reporting</param>
        protected void ValidateBucketName(string bucketName, string parameterName = "BucketName")
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Bucket name cannot be null or empty", parameterName),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }

            // Basic S3 bucket name validation
            if (bucketName.Length < 3 || bucketName.Length > 63)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Bucket name must be between 3 and 63 characters long", parameterName),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(bucketName, @"^[a-z0-9][a-z0-9\-]*[a-z0-9]$"))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Bucket name contains invalid characters. Must contain only lowercase letters, numbers, and hyphens", parameterName),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }
        }

        /// <summary>
        /// Validates that an object name is valid
        /// </summary>
        /// <param name="objectName">Object name to validate</param>
        /// <param name="parameterName">Parameter name for error reporting</param>
        protected void ValidateObjectName(string objectName, string parameterName = "ObjectName")
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Object name cannot be null or empty", parameterName),
                    "InvalidObjectName",
                    ErrorCategory.InvalidArgument,
                    objectName));
            }

            if (objectName.Length > 1024)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Object name cannot be longer than 1024 characters", parameterName),
                    "InvalidObjectName",
                    ErrorCategory.InvalidArgument,
                    objectName));
            }
        }

        /// <summary>
        /// Writes verbose output with consistent formatting
        /// </summary>
        /// <param name="message">Message to write</param>
        /// <param name="args">Format arguments</param>
        protected void WriteVerboseMessage(string message, params object[] args)
        {
            MinIOLogger.WriteVerbose(this, message, args);
        }

        /// <summary>
        /// Writes warning output with consistent formatting
        /// </summary>
        /// <param name="message">Warning message to write</param>
        /// <param name="args">Format arguments</param>
        protected void WriteWarningMessage(string message, params object[] args)
        {
            MinIOLogger.WriteWarning(this, message, args);
        }

        /// <summary>
        /// Writes debug output with consistent formatting
        /// </summary>
        /// <param name="message">Debug message to write</param>
        /// <param name="args">Format arguments</param>
        protected void WriteDebugMessage(string message, params object[] args)
        {
            MinIOLogger.WriteDebug(this, message, args);
        }
    }
}
