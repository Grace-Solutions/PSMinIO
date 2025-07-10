using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Tests whether a MinIO bucket exists
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "MinIOBucketExists", SupportsShouldProcess = false)]
    [OutputType(typeof(bool))]
    public class TestMinIOBucketExistsCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to test
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Return detailed information instead of just true/false
        /// </summary>
        [Parameter]
        public SwitchParameter Detailed { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketName(BucketName);

            ExecuteOperation("TestBucketExists", () =>
            {
                MinIOLogger.WriteVerbose(this, "Checking if bucket '{0}' exists", BucketName);

                var exists = Client.BucketExists(BucketName);

                if (Detailed.IsPresent)
                {
                    // Return detailed information
                    var result = new PSObject();
                    result.Properties.Add(new PSNoteProperty("BucketName", BucketName));
                    result.Properties.Add(new PSNoteProperty("Exists", exists));
                    result.Properties.Add(new PSNoteProperty("Endpoint", Configuration.Endpoint));
                    result.Properties.Add(new PSNoteProperty("CheckedAt", System.DateTime.UtcNow));

                    if (exists)
                    {
                        try
                        {
                            // Try to get additional bucket information
                            var allBuckets = Client.ListBuckets();
                            var bucket = allBuckets.Find(b => 
                                string.Equals(b.Name, BucketName, System.StringComparison.OrdinalIgnoreCase));

                            if (bucket != null)
                            {
                                result.Properties.Add(new PSNoteProperty("Created", bucket.Created));
                                result.Properties.Add(new PSNoteProperty("Region", bucket.Region));
                            }
                        }
                        catch (System.Exception ex)
                        {
                            MinIOLogger.WriteVerbose(this, 
                                "Could not retrieve additional bucket information: {0}", ex.Message);
                        }
                    }

                    WriteObject(result);
                }
                else
                {
                    // Return simple boolean result
                    WriteObject(exists);
                }

                MinIOLogger.WriteVerbose(this, 
                    "Bucket '{0}' exists: {1}", BucketName, exists);

            }, $"Bucket: {BucketName}");
        }
    }

    /// <summary>
    /// Custom output type for detailed bucket existence information
    /// </summary>
    public class BucketExistenceInfo
    {
        /// <summary>
        /// Name of the bucket that was tested
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the bucket exists
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// MinIO endpoint that was checked
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// When the check was performed
        /// </summary>
        public System.DateTime CheckedAt { get; set; }

        /// <summary>
        /// Creation date of the bucket (if it exists and information is available)
        /// </summary>
        public System.DateTime? Created { get; set; }

        /// <summary>
        /// Region of the bucket (if it exists and information is available)
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Creates a new BucketExistenceInfo instance
        /// </summary>
        public BucketExistenceInfo()
        {
            CheckedAt = System.DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new BucketExistenceInfo instance with specified values
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        /// <param name="exists">Whether the bucket exists</param>
        /// <param name="endpoint">MinIO endpoint</param>
        public BucketExistenceInfo(string bucketName, bool exists, string endpoint)
        {
            BucketName = bucketName ?? string.Empty;
            Exists = exists;
            Endpoint = endpoint ?? string.Empty;
            CheckedAt = System.DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the bucket existence info
        /// </summary>
        public override string ToString()
        {
            return $"Bucket '{BucketName}' exists: {Exists} (checked at {CheckedAt:yyyy-MM-dd HH:mm:ss} UTC)";
        }
    }
}
