using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets information about MinIO buckets
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOBucket", SupportsShouldProcess = true)]
    [OutputType(typeof(MinIOBucketInfo))]
    public class GetMinIOBucketCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of a specific bucket to retrieve. If not specified, all buckets are returned.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string? BucketName { get; set; }

        /// <summary>
        /// Include additional statistics like object count and total size for each bucket
        /// </summary>
        [Parameter]
        [Alias("Stats")]
        public SwitchParameter IncludeStatistics { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();

            if (!string.IsNullOrWhiteSpace(BucketName))
            {
                // Get specific bucket
                GetSpecificBucket(BucketName!);
            }
            else
            {
                // Get all buckets
                GetAllBuckets();
            }
        }

        /// <summary>
        /// Gets information about a specific bucket
        /// </summary>
        /// <param name="bucketName">Name of the bucket</param>
        private void GetSpecificBucket(string bucketName)
        {
            ValidateBucketName(bucketName);

            if (ShouldProcess(bucketName, "Get bucket information"))
            {
                ExecuteOperation("GetBucket", () =>
                {
                    // First check if bucket exists
                    var exists = Client.BucketExists(bucketName);
                    if (!exists)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Bucket '{bucketName}' does not exist"),
                            "BucketNotFound",
                            ErrorCategory.ObjectNotFound,
                            bucketName));
                        return;
                    }

                    // Get all buckets and find the specific one
                    var allBuckets = Client.ListBuckets();
                    var bucket = allBuckets.FirstOrDefault(b => 
                        string.Equals(b.Name, bucketName, StringComparison.OrdinalIgnoreCase));

                    if (bucket != null)
                    {
                        if (IncludeStatistics.IsPresent)
                        {
                            PopulateBucketStatistics(bucket);
                        }

                        WriteObject(bucket);
                    }
                    else
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Bucket '{bucketName}' not found in bucket list"),
                            "BucketNotFound",
                            ErrorCategory.ObjectNotFound,
                            bucketName));
                    }
                }, $"Bucket: {bucketName}");
            }
        }

        /// <summary>
        /// Gets information about all buckets
        /// </summary>
        private void GetAllBuckets()
        {
            if (ShouldProcess("All buckets", "Get bucket information"))
            {
                ExecuteOperation("ListBuckets", () =>
                {
                    var buckets = Client.ListBuckets();

                    if (IncludeStatistics.IsPresent)
                    {
                        MinIOLogger.WriteVerbose(this, "Gathering statistics for {0} buckets", buckets.Count);
                        
                        for (int i = 0; i < buckets.Count; i++)
                        {
                            var bucket = buckets[i];
                            
                            // Show progress for statistics gathering
                            var progressRecord = new ProgressRecord(1, "Gathering Bucket Statistics", 
                                $"Processing bucket: {bucket.Name}")
                            {
                                PercentComplete = (int)((double)(i + 1) / buckets.Count * 100)
                            };
                            WriteProgress(progressRecord);

                            PopulateBucketStatistics(bucket);
                        }

                        // Complete progress
                        WriteProgress(new ProgressRecord(1, "Gathering Bucket Statistics", "Completed")
                        {
                            PercentComplete = 100,
                            RecordType = ProgressRecordType.Completed
                        });
                    }

                    // Output all buckets
                    foreach (var bucket in buckets)
                    {
                        WriteObject(bucket);
                    }

                    MinIOLogger.WriteVerbose(this, "Retrieved information for {0} buckets", buckets.Count);
                });
            }
        }

        /// <summary>
        /// Populates additional statistics for a bucket
        /// </summary>
        /// <param name="bucket">Bucket to populate statistics for</param>
        private void PopulateBucketStatistics(MinIOBucketInfo bucket)
        {
            try
            {
                MinIOLogger.WriteVerbose(this, "Gathering statistics for bucket: {0}", bucket.Name);

                var objects = Client.ListObjects(bucket.Name, recursive: true);
                
                bucket.ObjectCount = objects.Count;
                bucket.Size = objects.Sum(obj => obj.Size);

                MinIOLogger.WriteVerbose(this, 
                    "Bucket '{0}' statistics: {1} objects, {2} bytes total", 
                    bucket.Name, bucket.ObjectCount, bucket.Size);
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Failed to gather statistics for bucket '{0}': {1}", 
                    bucket.Name, ex.Message);
                
                // Set statistics to null to indicate they couldn't be retrieved
                bucket.ObjectCount = null;
                bucket.Size = null;
            }
        }
    }
}
