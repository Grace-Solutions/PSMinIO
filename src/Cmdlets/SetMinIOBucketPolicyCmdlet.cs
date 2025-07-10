using System;
using System.IO;
using System.Management.Automation;
using System.Text.Json;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Sets the policy for a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "MinIOBucketPolicy", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    public class SetMinIOBucketPolicyCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to set the policy for
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Policy JSON string
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "PolicyJson")]
        [ValidateNotNullOrEmpty]
        [Alias("Json")]
        public string? PolicyJson { get; set; }

        /// <summary>
        /// Path to a file containing the policy JSON
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "PolicyFile")]
        [ValidateNotNullOrEmpty]
        [Alias("File")]
        public string? PolicyFilePath { get; set; }

        /// <summary>
        /// Use a predefined canned policy
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "CannedPolicy")]
        [ValidateSet("ReadOnly", "WriteOnly", "ReadWrite", "None")]
        public string? CannedPolicy { get; set; }

        /// <summary>
        /// Prefix for canned policies (default: *)
        /// </summary>
        [Parameter(ParameterSetName = "CannedPolicy")]
        [ValidateNotNullOrEmpty]
        public string Prefix { get; set; } = "*";

        /// <summary>
        /// Validate the policy JSON before setting it
        /// </summary>
        [Parameter]
        public SwitchParameter ValidateOnly { get; set; }

        /// <summary>
        /// Force setting the policy without confirmation
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConfiguration();
            ValidateBucketName(BucketName);

            // Get the policy JSON based on the parameter set
            var policyJson = GetPolicyJson();
            if (string.IsNullOrWhiteSpace(policyJson))
            {
                return; // Error already written
            }

            // Validate the policy JSON
            if (!ValidatePolicyJson(policyJson))
            {
                return; // Error already written
            }

            if (ValidateOnly.IsPresent)
            {
                WriteObject("Policy JSON is valid");
                return;
            }

            // Override confirmation if Force is specified
            if (Force.IsPresent)
            {
                ConfirmPreference = ConfirmImpact.None;
            }

            if (ShouldProcess(BucketName, "Set bucket policy"))
            {
                ExecuteOperation("SetBucketPolicy", () =>
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

                    MinIOLogger.WriteVerbose(this, "Setting policy for bucket '{0}'", BucketName);
                    MinIOLogger.WriteVerbose(this, "Policy JSON ({0} characters): {1}", 
                        policyJson.Length, policyJson.Length > 200 ? policyJson.Substring(0, 200) + "..." : policyJson);

                    Client.SetBucketPolicy(BucketName, policyJson);

                    MinIOLogger.WriteVerbose(this, "Successfully set policy for bucket '{0}'", BucketName);

                }, $"Bucket: {BucketName}");
            }
        }

        /// <summary>
        /// Gets the policy JSON based on the current parameter set
        /// </summary>
        /// <returns>Policy JSON string or null if error</returns>
        private string? GetPolicyJson()
        {
            switch (ParameterSetName)
            {
                case "PolicyJson":
                    return PolicyJson;

                case "PolicyFile":
                    return ReadPolicyFromFile();

                case "CannedPolicy":
                    return GenerateCannedPolicy();

                default:
                    WriteError(new ErrorRecord(
                        new InvalidOperationException("Unknown parameter set"),
                        "UnknownParameterSet",
                        ErrorCategory.InvalidArgument,
                        null));
                    return null;
            }
        }

        /// <summary>
        /// Reads policy JSON from a file
        /// </summary>
        /// <returns>Policy JSON string or null if error</returns>
        private string? ReadPolicyFromFile()
        {
            if (string.IsNullOrWhiteSpace(PolicyFilePath))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(PolicyFilePath);
                
                if (!File.Exists(fullPath))
                {
                    WriteError(new ErrorRecord(
                        new FileNotFoundException($"Policy file not found: {fullPath}"),
                        "PolicyFileNotFound",
                        ErrorCategory.ObjectNotFound,
                        fullPath));
                    return null;
                }

                var content = File.ReadAllText(fullPath);
                MinIOLogger.WriteVerbose(this, "Read policy from file '{0}' ({1} characters)", fullPath, content.Length);
                
                return content;
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"Failed to read policy file '{PolicyFilePath}': {ex.Message}", ex),
                    "PolicyFileReadError",
                    ErrorCategory.ReadError,
                    PolicyFilePath));
                return null;
            }
        }

        /// <summary>
        /// Generates a canned policy based on the specified type
        /// </summary>
        /// <returns>Policy JSON string</returns>
        private string GenerateCannedPolicy()
        {
            var bucketArn = $"arn:aws:s3:::{BucketName}";
            var objectArn = $"arn:aws:s3:::{BucketName}/{Prefix}";

            var policy = CannedPolicy?.ToLowerInvariant() switch
            {
                "readonly" => CreateReadOnlyPolicy(bucketArn, objectArn),
                "writeonly" => CreateWriteOnlyPolicy(bucketArn, objectArn),
                "readwrite" => CreateReadWritePolicy(bucketArn, objectArn),
                "none" => CreateEmptyPolicy(),
                _ => throw new ArgumentException($"Unknown canned policy: {CannedPolicy}")
            };

            MinIOLogger.WriteVerbose(this, "Generated {0} canned policy for bucket '{1}' with prefix '{2}'", 
                CannedPolicy, BucketName, Prefix);

            return JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Creates a read-only policy
        /// </summary>
        private object CreateReadOnlyPolicy(string bucketArn, string objectArn)
        {
            return new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:GetBucketLocation", "s3:ListBucket" },
                        Resource = bucketArn
                    },
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:GetObject" },
                        Resource = objectArn
                    }
                }
            };
        }

        /// <summary>
        /// Creates a write-only policy
        /// </summary>
        private object CreateWriteOnlyPolicy(string bucketArn, string objectArn)
        {
            return new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:GetBucketLocation", "s3:ListBucketMultipartUploads" },
                        Resource = bucketArn
                    },
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:PutObject", "s3:AbortMultipartUpload", "s3:DeleteObject", "s3:ListMultipartUploadParts" },
                        Resource = objectArn
                    }
                }
            };
        }

        /// <summary>
        /// Creates a read-write policy
        /// </summary>
        private object CreateReadWritePolicy(string bucketArn, string objectArn)
        {
            return new
            {
                Version = "2012-10-17",
                Statement = new[]
                {
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:GetBucketLocation", "s3:ListBucket", "s3:ListBucketMultipartUploads" },
                        Resource = bucketArn
                    },
                    new
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new[] { "s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:AbortMultipartUpload", "s3:ListMultipartUploadParts" },
                        Resource = objectArn
                    }
                }
            };
        }

        /// <summary>
        /// Creates an empty policy (removes all permissions)
        /// </summary>
        private object CreateEmptyPolicy()
        {
            return new
            {
                Version = "2012-10-17",
                Statement = new object[0]
            };
        }

        /// <summary>
        /// Validates the policy JSON
        /// </summary>
        /// <param name="policyJson">Policy JSON to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        private bool ValidatePolicyJson(string policyJson)
        {
            try
            {
                using var document = JsonDocument.Parse(policyJson);
                MinIOLogger.WriteVerbose(this, "Policy JSON is valid");
                return true;
            }
            catch (JsonException ex)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException($"Invalid policy JSON: {ex.Message}", ex),
                    "InvalidPolicyJson",
                    ErrorCategory.InvalidArgument,
                    policyJson));
                return false;
            }
        }
    }
}
