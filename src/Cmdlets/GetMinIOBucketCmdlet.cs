using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets information about MinIO buckets
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOBucket")]
    [OutputType(typeof(MinIOBucketInfo))]
    public class GetMinIOBucketCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of a specific bucket to retrieve (supports wildcards)
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [SupportsWildcards]
        [Alias("Bucket", "Name")]
        public string? BucketName { get; set; }

        /// <summary>
        /// Include bucket statistics (object count and total size)
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeStats { get; set; }

        /// <summary>
        /// Include bucket policy information
        /// </summary>
        [Parameter]
        public SwitchParameter IncludePolicy { get; set; }

        /// <summary>
        /// Sort buckets by the specified property
        /// </summary>
        [Parameter]
        [ValidateSet("Name", "CreationDate", "ObjectCount", "TotalSize")]
        public string SortBy { get; set; } = "Name";

        /// <summary>
        /// Sort in descending order
        /// </summary>
        [Parameter]
        public SwitchParameter Descending { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ExecuteOperation("ListBuckets", () =>
            {
                WriteVerboseMessage("Retrieving bucket list from MinIO server");

                // Get all buckets
                var buckets = S3Client.ListBuckets();

                WriteVerboseMessage("Retrieved {0} buckets", buckets.Count);

                // Filter by name if specified
                if (!string.IsNullOrEmpty(BucketName))
                {
                    var wildcardPattern = new WildcardPattern(BucketName, WildcardOptions.IgnoreCase);
                    buckets = buckets.Where(b => b.Name != null && wildcardPattern.IsMatch(b.Name)).ToList();
                    WriteVerboseMessage("Filtered to {0} buckets matching pattern '{1}'", buckets.Count, BucketName);
                }

                // Enhance bucket information if requested
                if (IncludeStats.IsPresent || IncludePolicy.IsPresent)
                {
                    for (int i = 0; i < buckets.Count; i++)
                    {
                        var bucket = buckets[i];
                        WriteVerboseMessage("Enhancing information for bucket '{0}' ({1}/{2})", bucket.Name, i + 1, buckets.Count);

                        try
                        {
                            // Get bucket statistics
                            if (IncludeStats.IsPresent)
                            {
                                WriteVerboseMessage("Getting statistics for bucket '{0}'", bucket.Name);
                                var objects = S3Client.ListObjects(bucket.Name, recursive: true, maxObjects: int.MaxValue);
                                bucket.ObjectCount = objects.Count;
                                bucket.TotalSize = objects.Sum(o => o.Size);
                                WriteVerboseMessage("Bucket '{0}' contains {1} objects ({2})", 
                                    bucket.Name, bucket.ObjectCount, Utils.SizeFormatter.FormatBytes(bucket.TotalSize ?? 0));
                            }

                            // Get bucket policy
                            if (IncludePolicy.IsPresent)
                            {
                                WriteVerboseMessage("Getting policy for bucket '{0}'", bucket.Name);
                                try
                                {
                                    bucket.Policy = S3Client.GetBucketPolicy(bucket.Name);
                                    WriteVerboseMessage("Retrieved policy for bucket '{0}'", bucket.Name);
                                }
                                catch (Exception ex)
                                {
                                    WriteVerboseMessage("No policy found for bucket '{0}': {1}", bucket.Name, ex.Message);
                                    bucket.Policy = null;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteWarningMessage("Failed to enhance information for bucket '{0}': {1}", bucket.Name, ex.Message);
                        }
                    }
                }

                // Sort buckets
                buckets = SortBy switch
                {
                    "Name" => Descending.IsPresent 
                        ? buckets.OrderByDescending(b => b.Name).ToList()
                        : buckets.OrderBy(b => b.Name).ToList(),
                    "CreationDate" => Descending.IsPresent
                        ? buckets.OrderByDescending(b => b.CreationDate).ToList()
                        : buckets.OrderBy(b => b.CreationDate).ToList(),
                    "ObjectCount" => Descending.IsPresent
                        ? buckets.OrderByDescending(b => b.ObjectCount ?? 0).ToList()
                        : buckets.OrderBy(b => b.ObjectCount ?? 0).ToList(),
                    "TotalSize" => Descending.IsPresent
                        ? buckets.OrderByDescending(b => b.TotalSize ?? 0).ToList()
                        : buckets.OrderBy(b => b.TotalSize ?? 0).ToList(),
                    _ => buckets
                };

                WriteVerboseMessage("Returning {0} buckets sorted by {1} ({2})", 
                    buckets.Count, SortBy, Descending.IsPresent ? "descending" : "ascending");

                // Output results
                foreach (var bucket in buckets)
                {
                    WriteObject(bucket);
                }
            });
        }
    }
}
