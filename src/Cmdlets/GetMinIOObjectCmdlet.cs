using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets objects from MinIO buckets
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOObject")]
    [OutputType(typeof(MinIOObjectInfo))]
    public class GetMinIOObjectCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Object name or prefix to filter objects (supports wildcards)
        /// </summary>
        [Parameter(Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [SupportsWildcards]
        [Alias("Key", "Prefix")]
        public string? Name { get; set; }

        /// <summary>
        /// List objects recursively (default: true)
        /// </summary>
        [Parameter]
        public SwitchParameter Recursive { get; set; } = true;

        /// <summary>
        /// Maximum number of objects to return (default: 1000)
        /// </summary>
        [Parameter]
        [ValidateRange(1, 10000)]
        public int MaxObjects { get; set; } = 1000;

        /// <summary>
        /// Sort objects by the specified property
        /// </summary>
        [Parameter]
        [ValidateSet("Name", "Size", "LastModified")]
        public string SortBy { get; set; } = "Name";

        /// <summary>
        /// Sort in descending order
        /// </summary>
        [Parameter]
        public SwitchParameter Descending { get; set; }

        /// <summary>
        /// Include only directories (objects ending with /)
        /// </summary>
        [Parameter]
        public SwitchParameter DirectoriesOnly { get; set; }

        /// <summary>
        /// Exclude directories (objects ending with /)
        /// </summary>
        [Parameter]
        public SwitchParameter ExcludeDirectories { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateBucketName(BucketName);

            ExecuteOperation("ListObjects", () =>
            {
                WriteVerboseMessage("Listing objects in bucket '{0}'", BucketName);

                // Check if bucket exists
                if (!S3Client.BucketExists(BucketName))
                {
                    var errorRecord = new ErrorRecord(
                        new InvalidOperationException($"Bucket '{BucketName}' does not exist"),
                        "BucketNotFound",
                        ErrorCategory.ObjectNotFound,
                        BucketName);
                    ThrowTerminatingError(errorRecord);
                }

                // Determine prefix for API call
                string? apiPrefix = null;
                if (!string.IsNullOrEmpty(Name) && !WildcardPattern.ContainsWildcardCharacters(Name))
                {
                    // If no wildcards, use as prefix for efficient API filtering
                    apiPrefix = Name;
                }

                WriteVerboseMessage("Retrieving objects with prefix '{0}', recursive: {1}, max: {2}", 
                    apiPrefix ?? "(none)", Recursive.IsPresent, MaxObjects);

                // Get objects from MinIO
                var objects = S3Client.ListObjects(BucketName, apiPrefix, Recursive.IsPresent, MaxObjects);

                WriteVerboseMessage("Retrieved {0} objects from MinIO", objects.Count);

                // Apply client-side filtering if wildcards are used
                if (!string.IsNullOrEmpty(Name) && WildcardPattern.ContainsWildcardCharacters(Name))
                {
                    var wildcardPattern = new WildcardPattern(Name, WildcardOptions.IgnoreCase);
                    objects = objects.Where(o => o.Name != null && wildcardPattern.IsMatch(o.Name)).ToList();
                    WriteVerboseMessage("Filtered to {0} objects matching pattern '{1}'", objects.Count, Name);
                }

                // Apply directory filters
                if (DirectoriesOnly.IsPresent)
                {
                    objects = objects.Where(o => o.IsDirectory).ToList();
                    WriteVerboseMessage("Filtered to {0} directories only", objects.Count);
                }
                else if (ExcludeDirectories.IsPresent)
                {
                    objects = objects.Where(o => !o.IsDirectory).ToList();
                    WriteVerboseMessage("Filtered to {0} objects excluding directories", objects.Count);
                }

                // Sort objects
                objects = SortBy switch
                {
                    "Name" => Descending.IsPresent 
                        ? objects.OrderByDescending(o => o.Name).ToList()
                        : objects.OrderBy(o => o.Name).ToList(),
                    "Size" => Descending.IsPresent
                        ? objects.OrderByDescending(o => o.Size).ToList()
                        : objects.OrderBy(o => o.Size).ToList(),
                    "LastModified" => Descending.IsPresent
                        ? objects.OrderByDescending(o => o.LastModified).ToList()
                        : objects.OrderBy(o => o.LastModified).ToList(),
                    _ => objects
                };

                WriteVerboseMessage("Returning {0} objects sorted by {1} ({2})", 
                    objects.Count, SortBy, Descending.IsPresent ? "descending" : "ascending");

                // Output results
                foreach (var obj in objects)
                {
                    WriteObject(obj);
                }
            }, $"Bucket: {BucketName}, Name: {Name ?? "(all)"}");
        }
    }
}
