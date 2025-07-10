using System;
using System.IO;
using System.Management.Automation;
using PSMinIO.Models;

namespace PSMinIO.Utils
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

                    MinIOLogger.LogOperationStart(this, "UsingConnection", $"Endpoint: {_connection.Configuration.Endpoint}");
                }
                return _connection;
            }
        }

        /// <summary>
        /// Gets the MinIO client wrapper instance
        /// </summary>
        protected MinIOClientWrapper Client => Connection.Client;

        /// <summary>
        /// Gets the current MinIO configuration
        /// </summary>
        protected MinIOConfiguration Configuration => Connection.Configuration;

        /// <summary>
        /// Gets the MinIO connection from parameter or session variable
        /// </summary>
        /// <returns>MinIO connection or null if not found</returns>
        private MinIOConnection? GetConnection()
        {
            // First, check if connection was provided via parameter
            if (MinIOConnection != null)
            {
                MinIOLogger.WriteVerbose(this, "Using MinIO connection from parameter");
                return MinIOConnection;
            }

            // Next, check session variable
            try
            {
                var sessionConnection = SessionState.PSVariable.GetValue(SessionVariable) as MinIOConnection;
                if (sessionConnection != null)
                {
                    MinIOLogger.WriteVerbose(this, "Using MinIO connection from session variable: {0}", SessionVariable);
                    return sessionConnection;
                }
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteVerbose(this, "Failed to retrieve connection from session variable '{0}': {1}", SessionVariable, ex.Message);
            }

            MinIOLogger.WriteVerbose(this, "No MinIO connection found in parameter or session variable '{0}'", SessionVariable);
            return null;
        }

        /// <summary>
        /// Validates that the MinIO connection is available and valid
        /// </summary>
        protected void ValidateConnection()
        {
            var connection = Connection; // This will throw if invalid
        }

        /// <summary>
        /// Executes an operation with proper error handling and logging
        /// </summary>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="details">Optional operation details for logging</param>
        protected void ExecuteOperation(string operationName, Action operation, string? details = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operationName));

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
                
                // Determine appropriate error category
                var category = GetErrorCategory(ex);
                
                WriteError(new ErrorRecord(ex, $"{operationName}Failed", category, null));
            }
        }

        /// <summary>
        /// Executes an operation with return value and proper error handling and logging
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="details">Optional operation details for logging</param>
        /// <returns>Result of the operation</returns>
        protected T ExecuteOperation<T>(string operationName, Func<T> operation, string? details = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

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
                
                // Determine appropriate error category
                var category = GetErrorCategory(ex);
                
                ThrowTerminatingError(new ErrorRecord(ex, $"{operationName}Failed", category, null));
                
                // This line will never be reached, but is required for compilation
                throw;
            }
        }

        /// <summary>
        /// Determines the appropriate PowerShell error category based on the exception type
        /// </summary>
        /// <param name="exception">The exception to categorize</param>
        /// <returns>Appropriate ErrorCategory</returns>
        protected virtual ErrorCategory GetErrorCategory(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => ErrorCategory.InvalidArgument,
                ArgumentException => ErrorCategory.InvalidArgument,
                UnauthorizedAccessException => ErrorCategory.PermissionDenied,
                System.Net.WebException => ErrorCategory.ConnectionError,
                System.Net.Http.HttpRequestException => ErrorCategory.ConnectionError,
                TimeoutException => ErrorCategory.OperationTimeout,
                InvalidOperationException => ErrorCategory.InvalidOperation,
                NotSupportedException => ErrorCategory.NotImplemented,
                FileNotFoundException => ErrorCategory.ObjectNotFound,
                DirectoryNotFoundException => ErrorCategory.ObjectNotFound,
                _ => ErrorCategory.NotSpecified
            };
        }

        /// <summary>
        /// Validates a bucket name according to MinIO/S3 naming rules
        /// </summary>
        /// <param name="bucketName">Bucket name to validate</param>
        /// <param name="parameterName">Parameter name for error reporting</param>
        protected void ValidateBucketName(string bucketName, string parameterName = "BucketName")
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"{parameterName} cannot be null or empty"),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }

            // Basic bucket name validation (simplified)
            if (bucketName.Length < 3 || bucketName.Length > 63)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"{parameterName} must be between 3 and 63 characters long"),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }
        }

        /// <summary>
        /// Validates an object name
        /// </summary>
        /// <param name="objectName">Object name to validate</param>
        /// <param name="parameterName">Parameter name for error reporting</param>
        protected void ValidateObjectName(string objectName, string parameterName = "ObjectName")
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"{parameterName} cannot be null or empty"),
                    "InvalidObjectName",
                    ErrorCategory.InvalidArgument,
                    objectName));
            }
        }

        /// <summary>
        /// Cleans up resources when the cmdlet is disposed
        /// </summary>
        protected override void EndProcessing()
        {
            try
            {
                // Note: We don't dispose the connection here as it may be shared across cmdlets
                // The connection should be disposed by the user when no longer needed
                _connection = null;
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteVerbose(this, $"Error during cleanup: {ex.Message}");
            }

            base.EndProcessing();
        }

        /// <summary>
        /// Handles stopping the cmdlet (Ctrl+C)
        /// </summary>
        protected override void StopProcessing()
        {
            try
            {
                _connection?.Client.CancelOperations();
                MinIOLogger.WriteVerbose(this, "Operation cancelled by user");
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteVerbose(this, $"Error cancelling operations: {ex.Message}");
            }

            base.StopProcessing();
        }
    }
}
