using System;
using System.Management.Automation;
using PSMinIO.Core;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Establishes a connection to a MinIO server
    /// </summary>
    [Cmdlet(VerbsCommunications.Connect, "MinIO", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Low)]
    [OutputType(typeof(MinIOConnection))]
    public class ConnectMinIOCmdlet : PSCmdlet
    {
        /// <summary>
        /// MinIO server URI (e.g., https://minio.example.com:9000)
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNull]
        [Alias("Server", "Url")]
        public Uri Endpoint { get; set; } = null!;

        /// <summary>
        /// Access key for authentication
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("AccessKeyId")]
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Secret key for authentication
        /// </summary>
        [Parameter(Position = 2, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("SecretAccessKey")]
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Optional region for bucket operations (default: us-east-1)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Connection timeout in seconds (default: 30)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Skip SSL certificate validation (for development/self-signed certificates)
        /// </summary>
        [Parameter]
        public SwitchParameter SkipCertificateValidation { get; set; }

        /// <summary>
        /// Maximum number of concurrent connections (default: 10)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 100)]
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Default chunk size for multipart uploads in MB (default: 5)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 1024)]
        public int DefaultChunkSizeMB { get; set; } = 5;

        /// <summary>
        /// Maximum retry attempts for failed operations (default: 3)
        /// </summary>
        [Parameter]
        [ValidateRange(0, 10)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Name of session variable to store the connection (default: MinIOConnection)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string SessionVariable { get; set; } = "MinIOConnection";

        /// <summary>
        /// Test the connection after establishing it
        /// </summary>
        [Parameter]
        public SwitchParameter TestConnection { get; set; }

        /// <summary>
        /// Return the connection object instead of storing it in session variable
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Extract endpoint components
            var endpointHost = $"{Endpoint.Host}:{Endpoint.Port}";
            var useSSL = Endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

            if (ShouldProcess($"MinIO Server: {Endpoint}", $"Establish connection"))
            {
                try
                {
                    MinIOLogger.LogConnection(this, endpointHost, useSSL, Region, TimeoutSeconds);

                    // Show warning when certificate validation is skipped
                    if (SkipCertificateValidation.IsPresent)
                    {
                        WriteWarning("SSL certificate validation is disabled. This should only be used in development environments with self-signed certificates.");
                    }

                    // Create configuration
                    var configuration = new MinIOConfiguration(
                        endpointHost,
                        AccessKey,
                        SecretKey,
                        useSSL,
                        Region,
                        TimeoutSeconds,
                        SkipCertificateValidation.IsPresent)
                    {
                        MaxConnections = MaxConnections,
                        DefaultChunkSize = DefaultChunkSizeMB * 1024 * 1024, // Convert MB to bytes
                        MaxRetries = MaxRetries
                    };

                    // Validate configuration
                    if (!configuration.IsValid)
                    {
                        var errors = string.Join(", ", configuration.GetValidationErrors());
                        throw new ArgumentException($"Invalid configuration: {errors}");
                    }

                    // Create connection
                    var connection = new MinIOConnection(configuration);

                    // Test connection if requested
                    if (TestConnection.IsPresent)
                    {
                        WriteVerboseMessage("Testing connection by listing buckets...");
                        
                        if (!connection.TestConnection())
                        {
                            throw new InvalidOperationException("Connection test failed. Please verify your credentials and endpoint.");
                        }

                        WriteVerboseMessage("Connection test successful");
                    }

                    // Store in session variable unless PassThru is specified
                    if (!PassThru.IsPresent)
                    {
                        SessionState.PSVariable.Set(SessionVariable, connection);
                        WriteVerboseMessage("Connection stored in session variable: {0}", SessionVariable);
                    }

                    // Output connection object if PassThru is specified or if not storing in session
                    if (PassThru.IsPresent)
                    {
                        WriteObject(connection);
                    }
                    else
                    {
                        WriteVerboseMessage("Connected to MinIO server: {0}", endpointHost);
                        WriteVerboseMessage("Use Get-MinIOBucket or other MinIO cmdlets to interact with the server");
                    }
                }
                catch (Exception ex)
                {
                    var errorRecord = new ErrorRecord(
                        ex,
                        "ConnectionFailed",
                        ErrorCategory.ConnectionError,
                        Endpoint);

                    ThrowTerminatingError(errorRecord);
                }
            }
        }

        /// <summary>
        /// Writes verbose output with consistent formatting
        /// </summary>
        /// <param name="message">Message to write</param>
        /// <param name="args">Format arguments</param>
        private void WriteVerboseMessage(string message, params object[] args)
        {
            MinIOLogger.WriteVerbose(this, message, args);
        }
    }
}
