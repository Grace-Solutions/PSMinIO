using System;
using System.Collections.Generic;
using System.Management.Automation;
using PSMinIO.Core.Http;
using PSMinIO.Core.Models;
using PSMinIO.Core.S3;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets bucket policy
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOBucketPolicy")]
    [OutputType(typeof(BucketPolicyResult))]
    public class GetMinIOBucketPolicyCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var result = ExecuteOperation("GetBucketPolicy", () =>
            {
                WriteVerboseMessage("Getting bucket policy for: {0}", BucketName);

                // Get connection and create policy manager
                var connection = Connection;
                var httpClient = new MinIOHttpClient(connection.Configuration);
                var progressCollector = new ThreadSafeProgressCollector(this);
                var policyManager = new BucketPolicyManager(httpClient, progressCollector);

                // Get bucket policy
                var policyResult = policyManager.GetBucketPolicy(BucketName);

                if (policyResult.HasPolicy)
                {
                    WriteVerboseMessage("Retrieved bucket policy successfully");
                }
                else
                {
                    WriteVerboseMessage("No policy found for bucket: {0}", BucketName);
                }

                return policyResult;

            }, $"Bucket: {BucketName}");

            // Always return the result object
            WriteObject(result);
        }
    }

    /// <summary>
    /// Sets bucket policy
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "MinIOBucketPolicy", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(BucketPolicyResult))]
    public class SetMinIOBucketPolicyCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Bucket policy object
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "PolicyObject")]
        [ValidateNotNull]
        public BucketPolicy Policy { get; set; } = null!;

        /// <summary>
        /// Bucket policy as JSON string
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "PolicyJson")]
        [ValidateNotNullOrEmpty]
        public string PolicyJson { get; set; } = string.Empty;

        /// <summary>
        /// Create a simple read-only policy for public access
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ReadOnly")]
        public SwitchParameter ReadOnly { get; set; }

        /// <summary>
        /// Object prefix for the policy (optional)
        /// </summary>
        [Parameter(ParameterSetName = "ReadOnly")]
        [Parameter(ParameterSetName = "ReadWrite")]
        public string? ObjectPrefix { get; set; }

        /// <summary>
        /// Create a read-write policy for specific principals
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ReadWrite")]
        public SwitchParameter ReadWrite { get; set; }

        /// <summary>
        /// List of principals for read-write policy
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ReadWrite")]
        [ValidateNotNull]
        public string[] Principals { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            if (ShouldProcess(BucketName, "Set bucket policy"))
            {
                var result = ExecuteOperation("SetBucketPolicy", () =>
                {
                    WriteVerboseMessage("Setting bucket policy for: {0}", BucketName);

                    // Get connection and create policy manager
                    var connection = Connection;
                    var httpClient = new MinIOHttpClient(connection.Configuration);
                    var progressCollector = new ThreadSafeProgressCollector(this);
                    var policyManager = new BucketPolicyManager(httpClient, progressCollector);

                    BucketPolicyResult policyResult;

                    switch (ParameterSetName)
                    {
                        case "PolicyObject":
                            policyResult = policyManager.SetBucketPolicy(BucketName, Policy);
                            break;
                        case "PolicyJson":
                            policyResult = policyManager.SetBucketPolicyFromJson(BucketName, PolicyJson);
                            break;
                        case "ReadOnly":
                            var readOnlyPolicy = policyManager.CreateReadOnlyPolicy(BucketName, ObjectPrefix);
                            policyResult = policyManager.SetBucketPolicy(BucketName, readOnlyPolicy);
                            WriteVerboseMessage("Created read-only policy for public access");
                            break;
                        case "ReadWrite":
                            var readWritePolicy = policyManager.CreateReadWritePolicy(BucketName, new List<string>(Principals), ObjectPrefix);
                            policyResult = policyManager.SetBucketPolicy(BucketName, readWritePolicy);
                            WriteVerboseMessage("Created read-write policy for {0} principals", Principals.Length);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown parameter set: {ParameterSetName}");
                    }

                    if (policyResult.IsValid)
                    {
                        WriteVerboseMessage("Set bucket policy successfully");
                    }
                    else
                    {
                        WriteWarning($"Failed to set bucket policy: {policyResult.Error}");
                    }

                    return policyResult;

                }, $"Bucket: {BucketName}, ParameterSet: {ParameterSetName}");

                // Always return the result object
                WriteObject(result);
            }
        }
    }

    /// <summary>
    /// Removes bucket policy
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "MinIOBucketPolicy", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(BucketPolicyResult))]
    public class RemoveMinIOBucketPolicyCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            if (ShouldProcess(BucketName, "Remove bucket policy"))
            {
                var result = ExecuteOperation("RemoveBucketPolicy", () =>
                {
                    WriteVerboseMessage("Removing bucket policy for: {0}", BucketName);

                    // Get connection and create policy manager
                    var connection = Connection;
                    var httpClient = new MinIOHttpClient(connection.Configuration);
                    var progressCollector = new ThreadSafeProgressCollector(this);
                    var policyManager = new BucketPolicyManager(httpClient, progressCollector);

                    // Remove bucket policy
                    var policyResult = policyManager.DeleteBucketPolicy(BucketName);

                    if (policyResult.IsValid)
                    {
                        WriteVerboseMessage("Removed bucket policy successfully");
                    }
                    else
                    {
                        WriteWarning($"Failed to remove bucket policy: {policyResult.Error}");
                    }

                    return policyResult;

                }, $"Bucket: {BucketName}");

                // Always return the result object
                WriteObject(result);
            }
        }
    }
}
