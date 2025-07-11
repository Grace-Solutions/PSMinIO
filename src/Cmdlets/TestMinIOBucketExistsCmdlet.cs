using System.Management.Automation;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Tests whether a MinIO bucket exists
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "MinIOBucketExists")]
    [OutputType(typeof(bool))]
    public class TestMinIOBucketExistsCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to test
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(Name);

            var exists = ExecuteOperation("CheckBucketExists", () =>
            {
                WriteVerboseMessage("Checking if bucket '{0}' exists", Name);

                var bucketExists = S3Client.BucketExists(Name);
                
                WriteVerboseMessage("Bucket '{0}' {1}", Name, bucketExists ? "exists" : "does not exist");
                
                return bucketExists;
            }, $"Bucket: {Name}");

            WriteObject(exists);
        }
    }
}
