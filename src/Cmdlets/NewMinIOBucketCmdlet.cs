using System;
using System.Management.Automation;
using PSMinIO.Core.Models;

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
        /// Region where the bucket should be created (default: us-east-1)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Force creation even if bucket already exists
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Return the created bucket information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(BucketName);

            if (ShouldProcess(BucketName, "Create MinIO bucket"))
            {
                ExecuteOperation("CreateBucket", () =>
                {
                    WriteVerboseMessage("Creating bucket '{0}' in region '{1}'", BucketName, Region);

                    // Check if bucket already exists
                    bool bucketExists = false;
                    try
                    {
                        bucketExists = S3Client.BucketExists(BucketName);
                        WriteVerboseMessage("Bucket existence check: {0}", bucketExists ? "exists" : "does not exist");
                    }
                    catch (Exception ex)
                    {
                        WriteVerboseMessage("Could not check bucket existence: {0}", ex.Message);
                    }

                    if (bucketExists)
                    {
                        if (Force.IsPresent)
                        {
                            WriteVerboseMessage("Bucket '{0}' already exists, but Force parameter specified", BucketName);
                        }
                        else
                        {
                            var errorMessage = $"Bucket '{BucketName}' already exists. Use -Force to suppress this error.";
                            var errorRecord = new ErrorRecord(
                                new InvalidOperationException(errorMessage),
                                "BucketAlreadyExists",
                                ErrorCategory.ResourceExists,
                                BucketName);

                            ThrowTerminatingError(errorRecord);
                        }
                    }
                    else
                    {
                        // Create the bucket
                        try
                        {
                            S3Client.CreateBucket(BucketName, Region);
                            WriteVerboseMessage("Successfully created bucket '{0}'", BucketName);
                        }
                        catch (Exception ex)
                        {
                            WriteVerboseMessage("Failed to create bucket '{0}': {1}", BucketName, ex.Message);
                            throw;
                        }
                    }

                    // Return bucket information if requested
                    if (PassThru.IsPresent)
                    {
                        WriteVerboseMessage("Retrieving information for created bucket '{0}'", BucketName);

                        try
                        {
                            // Get updated bucket list to find our bucket
                            var buckets = S3Client.ListBuckets();
                            var createdBucket = buckets.Find(b => string.Equals(b.Name, BucketName, StringComparison.OrdinalIgnoreCase));

                            if (createdBucket != null)
                            {
                                createdBucket.Region = Region;
                                WriteObject(createdBucket);
                            }
                            else
                            {
                                // Create a basic bucket info object if we can't find it in the list
                                var bucketInfo = new MinIOBucketInfo(BucketName, DateTime.UtcNow, Region);
                                WriteObject(bucketInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteWarningMessage("Bucket created successfully but could not retrieve bucket information: {0}", ex.Message);

                            // Create a basic bucket info object
                            var bucketInfo = new MinIOBucketInfo(BucketName, DateTime.UtcNow, Region);
                            WriteObject(bucketInfo);
                        }
                    }
                    else
                    {
                        WriteVerboseMessage("Bucket '{0}' created successfully", BucketName);
                    }
                }, $"Bucket: {BucketName}, Region: {Region}");
            }
        }
    }
}
