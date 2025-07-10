using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets statistical information about MinIO storage
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOStats", SupportsShouldProcess = false)]
    [OutputType(typeof(MinIOStats))]
    public class GetMinIOStatsCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Include detailed per-bucket statistics
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeBucketDetails { get; set; }

        /// <summary>
        /// Include object count and size calculations (may be slow for large buckets)
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeObjectCounts { get; set; }

        /// <summary>
        /// Maximum number of objects to count per bucket (default: unlimited)
        /// </summary>
        [Parameter]
        [ValidateRange(1, int.MaxValue)]
        public int? MaxObjectsToCount { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConfiguration();

            ExecuteOperation("GetStats", () =>
            {
                MinIOLogger.WriteVerbose(this, "Gathering MinIO statistics...");

                var config = Configuration;
                var stats = new MinIOStats
                {
                    Endpoint = config.Endpoint,
                    UseSSL = config.UseSSL,
                    ConnectionStatus = "Connected"
                };

                try
                {
                    // Get basic bucket information
                    var buckets = Client.ListBuckets();
                    stats.TotalBuckets = buckets.Count;

                    MinIOLogger.WriteVerbose(this, "Found {0} buckets", buckets.Count);

                    if (IncludeObjectCounts.IsPresent && buckets.Count > 0)
                    {
                        GatherObjectStatistics(buckets, stats);
                    }

                    // Create detailed output if requested
                    if (IncludeBucketDetails.IsPresent)
                    {
                        var detailedStats = CreateDetailedStats(stats, buckets);
                        WriteObject(detailedStats);
                    }
                    else
                    {
                        WriteObject(stats);
                    }

                    MinIOLogger.WriteVerbose(this, "Statistics gathering completed");
                }
                catch (Exception ex)
                {
                    stats.ConnectionStatus = $"Error: {ex.Message}";
                    WriteObject(stats);
                    throw;
                }

            }, "Gathering statistics");
        }

        /// <summary>
        /// Gathers object statistics across all buckets
        /// </summary>
        /// <param name="buckets">List of buckets</param>
        /// <param name="stats">Stats object to update</param>
        private void GatherObjectStatistics(System.Collections.Generic.List<MinIOBucketInfo> buckets, MinIOStats stats)
        {
            MinIOLogger.WriteVerbose(this, "Gathering object statistics for {0} buckets...", buckets.Count);

            long totalObjects = 0;
            long totalSize = 0;

            for (int i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];

                // Show progress
                var progressRecord = new ProgressRecord(1, "Gathering Statistics", 
                    $"Processing bucket: {bucket.Name}")
                {
                    PercentComplete = (int)((double)(i + 1) / buckets.Count * 100)
                };
                WriteProgress(progressRecord);

                try
                {
                    MinIOLogger.WriteVerbose(this, "Processing bucket: {0}", bucket.Name);

                    var objects = Client.ListObjects(bucket.Name, recursive: true);
                    
                    // Apply limit if specified
                    if (MaxObjectsToCount.HasValue && objects.Count > MaxObjectsToCount.Value)
                    {
                        MinIOLogger.WriteVerbose(this, 
                            "Limiting object count for bucket '{0}' to {1} objects", 
                            bucket.Name, MaxObjectsToCount.Value);
                        objects = objects.Take(MaxObjectsToCount.Value).ToList();
                    }

                    var bucketObjectCount = objects.Count;
                    var bucketSize = objects.Sum(obj => obj.Size);

                    totalObjects += bucketObjectCount;
                    totalSize += bucketSize;

                    MinIOLogger.WriteVerbose(this, 
                        "Bucket '{0}': {1} objects, {2} bytes", 
                        bucket.Name, bucketObjectCount, bucketSize);
                }
                catch (Exception ex)
                {
                    MinIOLogger.WriteWarning(this, 
                        "Failed to get statistics for bucket '{0}': {1}", 
                        bucket.Name, ex.Message);
                }
            }

            // Complete progress
            WriteProgress(new ProgressRecord(1, "Gathering Statistics", "Completed")
            {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            });

            stats.TotalObjects = totalObjects;
            stats.TotalSize = totalSize;

            MinIOLogger.WriteVerbose(this, 
                "Total statistics: {0} objects, {1} bytes across {2} buckets", 
                totalObjects, totalSize, buckets.Count);
        }

        /// <summary>
        /// Creates detailed statistics with per-bucket information
        /// </summary>
        /// <param name="stats">Base statistics</param>
        /// <param name="buckets">List of buckets</param>
        /// <returns>PSObject with detailed statistics</returns>
        private PSObject CreateDetailedStats(MinIOStats stats, System.Collections.Generic.List<MinIOBucketInfo> buckets)
        {
            var detailedStats = new PSObject();

            // Add basic statistics
            detailedStats.Properties.Add(new PSNoteProperty("TotalBuckets", stats.TotalBuckets));
            detailedStats.Properties.Add(new PSNoteProperty("TotalObjects", stats.TotalObjects));
            detailedStats.Properties.Add(new PSNoteProperty("TotalSize", stats.TotalSize));
            detailedStats.Properties.Add(new PSNoteProperty("TotalSizeFormatted", FormatBytes(stats.TotalSize)));
            detailedStats.Properties.Add(new PSNoteProperty("LastUpdated", stats.LastUpdated));
            detailedStats.Properties.Add(new PSNoteProperty("Endpoint", stats.Endpoint));
            detailedStats.Properties.Add(new PSNoteProperty("UseSSL", stats.UseSSL));
            detailedStats.Properties.Add(new PSNoteProperty("ConnectionStatus", stats.ConnectionStatus));

            // Add calculated statistics
            detailedStats.Properties.Add(new PSNoteProperty("AverageObjectSize", stats.AverageObjectSize));
            detailedStats.Properties.Add(new PSNoteProperty("AverageObjectSizeFormatted", FormatBytes((long)stats.AverageObjectSize)));
            detailedStats.Properties.Add(new PSNoteProperty("AverageObjectsPerBucket", stats.AverageObjectsPerBucket));

            // Add bucket details if object counts were gathered
            if (IncludeObjectCounts.IsPresent)
            {
                var bucketDetails = new System.Collections.Generic.List<PSObject>();

                foreach (var bucket in buckets)
                {
                    try
                    {
                        var objects = Client.ListObjects(bucket.Name, recursive: true);
                        
                        // Apply limit if specified
                        if (MaxObjectsToCount.HasValue && objects.Count > MaxObjectsToCount.Value)
                        {
                            objects = objects.Take(MaxObjectsToCount.Value).ToList();
                        }

                        var bucketDetail = new PSObject();
                        bucketDetail.Properties.Add(new PSNoteProperty("Name", bucket.Name));
                        bucketDetail.Properties.Add(new PSNoteProperty("Created", bucket.Created));
                        bucketDetail.Properties.Add(new PSNoteProperty("ObjectCount", objects.Count));
                        
                        var bucketSize = objects.Sum(obj => obj.Size);
                        bucketDetail.Properties.Add(new PSNoteProperty("Size", bucketSize));
                        bucketDetail.Properties.Add(new PSNoteProperty("SizeFormatted", FormatBytes(bucketSize)));
                        
                        var avgObjectSize = objects.Count > 0 ? (double)bucketSize / objects.Count : 0;
                        bucketDetail.Properties.Add(new PSNoteProperty("AverageObjectSize", avgObjectSize));
                        bucketDetail.Properties.Add(new PSNoteProperty("AverageObjectSizeFormatted", FormatBytes((long)avgObjectSize)));

                        bucketDetails.Add(bucketDetail);
                    }
                    catch (Exception ex)
                    {
                        var bucketDetail = new PSObject();
                        bucketDetail.Properties.Add(new PSNoteProperty("Name", bucket.Name));
                        bucketDetail.Properties.Add(new PSNoteProperty("Created", bucket.Created));
                        bucketDetail.Properties.Add(new PSNoteProperty("Error", ex.Message));
                        bucketDetails.Add(bucketDetail);
                    }
                }

                detailedStats.Properties.Add(new PSNoteProperty("BucketDetails", bucketDetails.ToArray()));
            }
            else
            {
                // Just add basic bucket information
                var bucketSummary = buckets.Select(b => new PSObject(new
                {
                    Name = b.Name,
                    Created = b.Created,
                    Region = b.Region
                })).ToArray();

                detailedStats.Properties.Add(new PSNoteProperty("Buckets", bucketSummary));
            }

            return detailedStats;
        }

        /// <summary>
        /// Formats bytes into a human-readable string
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <returns>Formatted string</returns>
        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
