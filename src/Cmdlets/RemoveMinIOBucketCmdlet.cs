using System;
using System.Management.Automation;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Removes a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "MinIOBucket", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class RemoveMinIOBucketCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to remove
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Force removal without confirmation prompts
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Remove all objects in the bucket before removing the bucket itself
        /// </summary>
        [Parameter]
        [Alias("Recursive")]
        public SwitchParameter RemoveObjects { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketName(BucketName);

            // Force parameter is handled by ShouldProcess automatically

            var actionDescription = RemoveObjects.IsPresent 
                ? $"Remove bucket '{BucketName}' and all its objects"
                : $"Remove bucket '{BucketName}'";

            if (ShouldProcess(BucketName, actionDescription))
            {
                ExecuteOperation("RemoveBucket", () =>
                {
                    // Check if bucket exists
                    var exists = Client.BucketExists(BucketName);
                    if (!exists)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Bucket '{BucketName}' does not exist"),
                            "BucketNotFound",
                            ErrorCategory.ObjectNotFound,
                            BucketName));
                        return;
                    }

                    // If RemoveObjects is specified, remove all objects first
                    if (RemoveObjects.IsPresent)
                    {
                        RemoveAllObjectsFromBucket();
                    }
                    else
                    {
                        // Check if bucket is empty before attempting to remove
                        CheckBucketEmpty();
                    }

                    // Remove the bucket
                    MinIOLogger.WriteVerbose(this, "Removing bucket '{0}'", BucketName);
                    Client.DeleteBucket(BucketName);
                    MinIOLogger.WriteVerbose(this, "Successfully removed bucket '{0}'", BucketName);

                }, $"Bucket: {BucketName}");
            }
        }

        /// <summary>
        /// Removes all objects from the bucket
        /// </summary>
        private void RemoveAllObjectsFromBucket()
        {
            MinIOLogger.WriteVerbose(this, "Removing all objects from bucket '{0}'", BucketName);

            try
            {
                var objects = Client.ListObjects(BucketName, recursive: true);
                
                if (objects.Count == 0)
                {
                    MinIOLogger.WriteVerbose(this, "Bucket '{0}' is already empty", BucketName);
                    return;
                }

                MinIOLogger.WriteVerbose(this, "Found {0} objects to remove from bucket '{1}'", objects.Count, BucketName);

                // Show progress for object removal
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
                    }
                }

                // Complete progress
                WriteProgress(new ProgressRecord(1, "Removing Objects", "Completed")
                {
                    PercentComplete = 100,
                    RecordType = ProgressRecordType.Completed
                });

                MinIOLogger.WriteVerbose(this, "Finished removing objects from bucket '{0}'", BucketName);
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Failed to list or remove objects from bucket '{0}': {1}", BucketName, ex.Message);
                
                if (!Force.IsPresent)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"Cannot remove objects from bucket '{BucketName}': {ex.Message}"),
                        "ObjectRemovalFailed",
                        ErrorCategory.InvalidOperation,
                        BucketName));
                    return;
                }
            }
        }

        /// <summary>
        /// Checks if the bucket is empty and warns if it's not
        /// </summary>
        private void CheckBucketEmpty()
        {
            try
            {
                var objects = Client.ListObjects(BucketName, recursive: true);
                
                if (objects.Count > 0)
                {
                    var message = $"Bucket '{BucketName}' contains {objects.Count} objects. " +
                                 "Use -RemoveObjects parameter to remove all objects first, or remove them manually.";
                    
                    WriteError(new ErrorRecord(
                        new InvalidOperationException(message),
                        "BucketNotEmpty",
                        ErrorCategory.InvalidOperation,
                        BucketName));
                }
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, 
                    "Could not check if bucket '{0}' is empty: {1}", BucketName, ex.Message);
                
                if (!Force.IsPresent)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"Cannot verify bucket '{BucketName}' is empty: {ex.Message}"),
                        "BucketCheckFailed",
                        ErrorCategory.InvalidOperation,
                        BucketName));
                }
            }
        }
    }
}
