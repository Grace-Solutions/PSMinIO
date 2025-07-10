using System;
using System.Management.Automation;
using PSMinIO.Models;
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
        /// Region for bucket operations (default: us-east-1)
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
        /// Test the connection after establishing it
        /// </summary>
        [Parameter]
        public SwitchParameter TestConnection { get; set; }

        /// <summary>
        /// Store the connection in a session variable for reuse
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? SessionVariable { get; set; }

        /// <summary>
        /// Skip SSL certificate validation (use with caution)
        /// </summary>
        [Parameter]
        public SwitchParameter SkipCertificateValidation { get; set; }

        /// <summary>
        /// Skip SSL certificate validation (use with caution)
        /// </summary>
        [Parameter]
        public SwitchParameter SkipCertificateValidation { get; set; }

        /// <summary>
        /// Accept self-signed certificates
        /// </summary>
        [Parameter]
        public SwitchParameter AcceptSelfSignedCertificates { get; set; }

        /// <summary>
        /// Accept certificates with hostname mismatches
        /// </summary>
        [Parameter]
        public SwitchParameter AcceptHostnameMismatch { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Extract connection details from URI
            var useSSL = Endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
            var endpointHost = $"{Endpoint.Host}:{Endpoint.Port}";

            if (ShouldProcess($"MinIO Server: {Endpoint}", $"Establish connection"))
            {
                try
                {
                    MinIOLogger.WriteVerbose(this,
                        "Connecting to MinIO - Endpoint: {0}, UseSSL: {1}, Region: {2}, Timeout: {3}s, SkipCertValidation: {4}",
                        endpointHost, useSSL, Region, TimeoutSeconds, SkipCertificateValidation.IsPresent);

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
                        SkipCertificateValidation.IsPresent);

                    // Create connection
                    var connection = new MinIOConnection(configuration);

                    MinIOLogger.WriteVerbose(this, "MinIO connection created successfully");

                    // Test connection if requested
                    if (TestConnection.IsPresent)
                    {
                        MinIOLogger.WriteVerbose(this, "Testing MinIO connection...");
                        
                        var testResult = connection.TestConnection();
                        
                        if (testResult.Success)
                        {
                            MinIOLogger.WriteVerbose(this, 
                                "Connection test successful - found {0} buckets in {1}ms", 
                                testResult.BucketCount ?? 0, 
                                testResult.ResponseTime?.TotalMilliseconds ?? 0);

                            WriteInformation(new InformationRecord(
                                $"Connection test successful. Found {testResult.BucketCount ?? 0} buckets.", 
                                "ConnectionTest"));
                        }
                        else
                        {
                            WriteWarning($"Connection test failed: {testResult.Message}");
                            MinIOLogger.WriteVerbose(this, "Connection test failed: {0}", testResult.Message);
                        }
                    }

                    // Store in session variable if requested
                    if (!string.IsNullOrWhiteSpace(SessionVariable))
                    {
                        SessionState.PSVariable.Set(SessionVariable, connection);
                        MinIOLogger.WriteVerbose(this, "Connection stored in session variable: {0}", SessionVariable);
                    }

                    // Return the connection object
                    WriteObject(connection);

                    MinIOLogger.WriteVerbose(this, "Connect-MinIO completed successfully");
                }
                catch (Exception ex)
                {
                    var errorRecord = new ErrorRecord(
                        ex,
                        "ConnectionFailed",
                        ErrorCategory.ConnectionError,
                        Endpoint);
                    
                    WriteError(errorRecord);
                }
            }
        }

        /// <summary>
        /// Begins processing - validate parameters
        /// </summary>
        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            // Validate URI scheme
            if (!Endpoint.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !Endpoint.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Invalid URI scheme '{Endpoint.Scheme}'. Only 'http' and 'https' are supported."),
                    "InvalidUriScheme",
                    ErrorCategory.InvalidArgument,
                    Endpoint));
            }

            // Validate port is specified
            if (Endpoint.Port == -1)
            {
                WriteWarning($"No port specified in URI '{Endpoint}'. MinIO typically uses port 9000.");
            }
        }
    }
}
