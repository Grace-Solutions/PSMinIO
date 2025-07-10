using System;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Removes an object from a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "MinIOObject", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class RemoveMinIOObjectCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket containing the object
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the object to remove
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Object", "Key")]
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// Force removal without confirmation prompts
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Remove all objects matching the specified prefix (use with caution)
        /// </summary>
        [Parameter]
        [Alias("Recursive")]
        public SwitchParameter RemovePrefix { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConfiguration();
            ValidateBucketName(BucketName);
            ValidateObjectName(ObjectName);

            // Override confirmation if Force is specified
            if (Force.IsPresent)
            {
                ConfirmPreference = ConfirmImpact.None;
            }

            var actionDescription = RemovePrefix.IsPresent 
                ? $"Remove all objects with prefix '{ObjectName}' from bucket '{BucketName}'"
                : $"Remove object '{ObjectName}' from bucket '{BucketName}'";

            if (ShouldProcess($"{BucketName}/{ObjectName}", actionDescription))
            {
                ExecuteOperation("RemoveObject", () =>
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

                    if (RemovePrefix.IsPresent)
                    {
                        RemoveObjectsWithPrefix();
                    }
                    else
                    {
                        RemoveSingleObject();
                    }

                }, $"Bucket: {BucketName}, Object: {ObjectName}");
            }
        }

        /// <summary>
        /// Removes a single object
        /// </summary>
        private void RemoveSingleObject()
        {
            // Check if object exists
            var objectExists = CheckObjectExists();
            if (!objectExists)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"Object '{ObjectName}' does not exist in bucket '{BucketName}'"),
                    "ObjectNotFound",
                    ErrorCategory.ObjectNotFound,
                    ObjectName));
                return;
            }

            MinIOLogger.WriteVerbose(this, 
                "Removing object '{0}' from bucket '{1}'", ObjectName, BucketName);

            Client.DeleteObject(BucketName, ObjectName);

            MinIOLogger.WriteVerbose(this, 
                "Successfully removed object '{0}' from bucket '{1}'", ObjectName, BucketName);
        }

        /// <summary>
        /// Removes all objects with the specified prefix
        /// </summary>
        private void RemoveObjectsWithPrefix()
        {
            MinIOLogger.WriteVerbose(this, 
                "Finding objects with prefix '{0}' in bucket '{1}'", ObjectName, BucketName);

            try
            {
                var objects = Client.ListObjects(BucketName, ObjectName, true);
                
                if (objects.Count == 0)
                {
                    MinIOLogger.WriteVerbose(this, 
                        "No objects found with prefix '{0}' in bucket '{1}'", ObjectName, BucketName);
                    return;
                }

                MinIOLogger.WriteVerbose(this, 
                    "Found {0} objects with prefix '{1}' in bucket '{2}'", 
                    objects.Count, ObjectName, BucketName);

                // Additional confirmation for prefix removal if not forced
                if (!Force.IsPresent && objects.Count > 1)
                {
                    var message = $"This will remove {objects.Count} objects with prefix '{ObjectName}'. Are you sure?";
                    if (!ShouldContinue(message, "Confirm Multiple Object Removal"))
                    {
                        return;
                    }
                }

                // Remove objects with progress reporting
                for (int i = 0; i < objects.Count; i++)
                {
                    var obj = objects[i];
                    
                    var progressRecord = new ProgressRecord(1, "Removing Objects", 
                        $"Removing object: {obj.Name}")
                    {
                        PercentComplete = (int)((double)(i + 1) / objects.Count * 100)
                    };
                    WriteProgress(progressRecord);

                    try
                    {
                        Client.DeleteObject(BucketName, obj.Name);
                        MinIOLogger.WriteVerbose(this, "Removed object: {0}", obj.Name);
                    }
                    catch (Exception ex)
                    {
                        MinIOLogger.WriteWarning(this, 
                            "Failed to remove object '{0}': {1}", obj.Name, ex.Message);
                        
                        if (!Force.IsPresent)
                        {
                            WriteError(new ErrorRecord(
                                new InvalidOperationException($"Failed to remove object '{obj.Name}': {ex.Message}", ex),
                                "ObjectRemovalFailed",
                                ErrorCategory.InvalidOperation,
                                obj.Name));
                        }
                    }
                }

                // Complete progress
                WriteProgress(new ProgressRecord(1, "Removing Objects", "Completed")
                {
                    PercentComplete = 100,
                    RecordType = ProgressRecordType.Completed
                });

                MinIOLogger.WriteVerbose(this, 
                    "Finished removing objects with prefix '{0}' from bucket '{1}'", ObjectName, BucketName);
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Failed to list objects with prefix '{0}' in bucket '{1}': {2}", 
                    ObjectName, BucketName, ex.Message);
                
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"Cannot list objects with prefix '{ObjectName}': {ex.Message}", ex),
                    "ObjectListingFailed",
                    ErrorCategory.InvalidOperation,
                    ObjectName));
            }
        }

        /// <summary>
        /// Checks if the specified object exists
        /// </summary>
        /// <returns>True if object exists, false otherwise</returns>
        private bool CheckObjectExists()
        {
            try
            {
                var objects = Client.ListObjects(BucketName, ObjectName, false);
                return objects.Any(obj => 
                    string.Equals(obj.Name, ObjectName, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Could not check if object '{0}' exists: {1}", ObjectName, ex.Message);
                
                // If we can't verify existence, assume it exists and let the delete operation handle it
                return true;
            }
        }
    }
}
