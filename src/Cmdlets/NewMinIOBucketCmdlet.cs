using System;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Creates a new MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOBucket", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(MinIOBucketInfo))]
    public class NewMinIOBucketCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to create
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Region where the bucket should be created
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? Region { get; set; }

        /// <summary>
        /// Force creation even if bucket already exists (no error will be thrown)
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketNameForCreation(BucketName);

            // Use region from parameter or configuration
            var region = Region ?? Configuration.Region;

            if (ShouldProcess(BucketName, $"Create bucket in region '{region}'"))
            {
                ExecuteOperation("CreateBucket", () =>
                {
                    // Check if bucket already exists
                    var exists = Client.BucketExists(BucketName);
                    
                    if (exists)
                    {
                        if (Force.IsPresent)
                        {
                            MinIOLogger.WriteVerbose(this, 
                                "Bucket '{0}' already exists, but Force parameter specified", BucketName);
                        }
                        else
                        {
                            WriteError(new ErrorRecord(
                                new InvalidOperationException($"Bucket '{BucketName}' already exists. Use -Force to suppress this error."),
                                "BucketAlreadyExists",
                                ErrorCategory.ResourceExists,
                                BucketName));
                            return;
                        }
                    }
                    else
                    {
                        // Create the bucket
                        MinIOLogger.WriteVerbose(this, 
                            "Creating bucket '{0}' in region '{1}'", BucketName, region);

                        Client.CreateBucket(BucketName, region);

                        MinIOLogger.WriteVerbose(this,
                            "Successfully created bucket '{0}'", BucketName);
                    }

                    // Always return bucket information
                    try
                    {
                        // Get the created bucket information
                        var allBuckets = Client.ListBuckets();
                        var createdBucket = allBuckets.Find(b =>
                            string.Equals(b.Name, BucketName, StringComparison.OrdinalIgnoreCase));

                        if (createdBucket != null)
                        {
                            createdBucket.Region = region;
                            WriteObject(createdBucket);
                        }
                        else
                        {
                            // Fallback: create a basic bucket info object
                            var bucketInfo = new MinIOBucketInfo(BucketName, DateTime.UtcNow, region);
                            WriteObject(bucketInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        MinIOLogger.WriteWarning(this,
                            "Bucket created successfully, but failed to retrieve bucket information: {0}",
                            ex.Message);

                        // Still return a basic bucket info object
                        var bucketInfo = new MinIOBucketInfo(BucketName, DateTime.UtcNow, region);
                        WriteObject(bucketInfo);
                    }

                }, $"Bucket: {BucketName}, Region: {region}");
            }
        }

        /// <summary>
        /// Validates the bucket name according to MinIO/S3 naming conventions for bucket creation
        /// </summary>
        /// <param name="bucketName">Bucket name to validate</param>
        private void ValidateBucketNameForCreation(string bucketName, string parameterName = "BucketName")
        {
            base.ValidateBucketName(bucketName, parameterName);

            // Additional validation for bucket creation
            if (bucketName.Contains("..") || bucketName.StartsWith(".") || bucketName.EndsWith("."))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Bucket name '{bucketName}' contains invalid characters or patterns"),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }

            // Check for uppercase letters (not allowed in S3/MinIO)
            if (bucketName != bucketName.ToLowerInvariant())
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"Bucket name '{bucketName}' must be lowercase"),
                    "InvalidBucketName",
                    ErrorCategory.InvalidArgument,
                    bucketName));
            }

            // Check for invalid characters
            foreach (char c in bucketName)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '.')
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException($"Bucket name '{bucketName}' contains invalid character '{c}'. Only lowercase letters, numbers, hyphens, and periods are allowed."),
                        "InvalidBucketName",
                        ErrorCategory.InvalidArgument,
                        bucketName));
                }
            }
        }
    }
}
