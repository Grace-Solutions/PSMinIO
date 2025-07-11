using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets information about objects in a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObject", SupportsShouldProcess = true)]
    [OutputType(typeof(MinIOObjectInfo))]
    public class GetMinIOObjectCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to list objects from
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Optional prefix to filter objects
        /// </summary>
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
        [Alias("Filter")]
        public string? Prefix { get; set; }

        /// <summary>
        /// Specific object name to retrieve. If specified, only this object is returned.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [Alias("Object", "Name")]
        public string? ObjectName { get; set; }

        /// <summary>
        /// Whether to list objects recursively (default: true)
        /// </summary>
        [Parameter]
        public SwitchParameter Recursive { get; set; } = true;

        /// <summary>
        /// Include all versions of objects (for versioned buckets)
        /// </summary>
        [Parameter]
        [Alias("Versions")]
        public SwitchParameter IncludeVersions { get; set; }

        /// <summary>
        /// Maximum number of objects to return
        /// </summary>
        [Parameter]
        [ValidateRange(1, 10000)]
        [Alias("Limit")]
        public int? MaxObjects { get; set; }

        /// <summary>
        /// Only return objects (exclude directory markers)
        /// </summary>
        [Parameter]
        [Alias("FilesOnly")]
        public SwitchParameter ObjectsOnly { get; set; }

        /// <summary>
        /// Sort objects by the specified property
        /// </summary>
        [Parameter]
        [ValidateSet("Name", "Size", "LastModified", "ETag")]
        public string? SortBy { get; set; }

        /// <summary>
        /// Sort in descending order (default: ascending)
        /// </summary>
        [Parameter]
        [Alias("Desc")]
        public SwitchParameter Descending { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var targetDescription = !string.IsNullOrWhiteSpace(ObjectName)
                ? $"object '{ObjectName}'"
                : !string.IsNullOrWhiteSpace(Prefix)
                    ? $"objects with prefix '{Prefix}'"
                    : "all objects";

            // Determine the prefix to use
            var searchPrefix = !string.IsNullOrWhiteSpace(ObjectName) ? ObjectName : Prefix;

            if (ShouldProcess(BucketName, $"List {targetDescription}"))
            {
                ExecuteOperation("ListObjects", () =>
                {
                    // Check if bucket exists
                    var bucketExists = Client.BucketExists(BucketName);
                    if (!bucketExists)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Bucket '{BucketName}' does not exist"),
                            "BucketNotFound",
                            ErrorCategory.ObjectNotFound,
                            BucketName));
                        return;
                    }

                    MinIOLogger.WriteVerbose(this, 
                        "Listing objects in bucket '{0}' with prefix '{1}', recursive: {2}", 
                        BucketName, searchPrefix ?? "(none)", Recursive.IsPresent);

                    // Get objects from MinIO
                    var objects = Client.ListObjects(BucketName, searchPrefix, Recursive.IsPresent, IncludeVersions.IsPresent);

                    MinIOLogger.WriteVerbose(this, "Found {0} objects", objects.Count);

                    // Filter for exact object name match if specified
                    if (!string.IsNullOrWhiteSpace(ObjectName))
                    {
                        objects = objects.Where(obj => 
                            string.Equals(obj.Name, ObjectName, StringComparison.Ordinal)).ToList();
                    }

                    // Filter out directory markers if ObjectsOnly is specified
                    if (ObjectsOnly.IsPresent)
                    {
                        objects = objects.Where(obj => 
                            !obj.Name.EndsWith("/") && obj.Size > 0).ToList();
                    }

                    // Apply sorting if specified
                    if (!string.IsNullOrWhiteSpace(SortBy))
                    {
                        objects = SortBy.ToLowerInvariant() switch
                        {
                            "name" => Descending.IsPresent 
                                ? objects.OrderByDescending(o => o.Name).ToList()
                                : objects.OrderBy(o => o.Name).ToList(),
                            "size" => Descending.IsPresent 
                                ? objects.OrderByDescending(o => o.Size).ToList()
                                : objects.OrderBy(o => o.Size).ToList(),
                            "lastmodified" => Descending.IsPresent 
                                ? objects.OrderByDescending(o => o.LastModified).ToList()
                                : objects.OrderBy(o => o.LastModified).ToList(),
                            "etag" => Descending.IsPresent 
                                ? objects.OrderByDescending(o => o.ETag).ToList()
                                : objects.OrderBy(o => o.ETag).ToList(),
                            _ => objects
                        };
                    }

                    // Apply limit if specified
                    if (MaxObjects.HasValue && objects.Count > MaxObjects.Value)
                    {
                        MinIOLogger.WriteVerbose(this, 
                            "Limiting results to {0} objects", MaxObjects.Value);
                        objects = objects.Take(MaxObjects.Value).ToList();
                    }

                    MinIOLogger.WriteVerbose(this, 
                        "Returning {0} objects after filtering and sorting", objects.Count);

                    // Output objects
                    foreach (var obj in objects)
                    {
                        WriteObject(obj);
                    }

                }, $"Bucket: {BucketName}, Prefix: {searchPrefix ?? "(none)"}");
            }
        }
    }
}
