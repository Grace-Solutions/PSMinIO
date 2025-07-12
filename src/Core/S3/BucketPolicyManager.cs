using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PSMinIO.Core.Http;
using PSMinIO.Utils;

namespace PSMinIO.Core.S3
{
    /// <summary>
    /// Manages bucket policies with comprehensive validation and formatting
    /// </summary>
    public class BucketPolicyManager
    {
        private readonly MinIOHttpClient _httpClient;
        private readonly ThreadSafeProgressCollector _progressCollector;

        public BucketPolicyManager(MinIOHttpClient httpClient, ThreadSafeProgressCollector progressCollector)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _progressCollector = progressCollector ?? throw new ArgumentNullException(nameof(progressCollector));
        }

        /// <summary>
        /// Gets the bucket policy
        /// </summary>
        public BucketPolicyResult GetBucketPolicy(string bucketName)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            _progressCollector.QueueVerboseMessage("Getting bucket policy for: {0}", bucketName);

            try
            {
                var queryParams = new Dictionary<string, string> { { "policy", "" } };
                var response = _httpClient.ExecuteRequestForString(HttpMethod.Get, $"/{bucketName}", queryParams);

                var policy = JsonSerializer.Deserialize<BucketPolicy>(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                _progressCollector.QueueVerboseMessage("Successfully retrieved bucket policy");

                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    Policy = policy,
                    PolicyJson = response,
                    IsValid = ValidatePolicy(policy!),
                    HasPolicy = true
                };
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("NoSuchBucketPolicy"))
            {
                _progressCollector.QueueVerboseMessage("No policy found for bucket: {0}", bucketName);
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    HasPolicy = false,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                _progressCollector.QueueVerboseMessage("Failed to get bucket policy: {0}", ex.Message);
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    HasPolicy = false,
                    IsValid = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets the bucket policy
        /// </summary>
        public BucketPolicyResult SetBucketPolicy(string bucketName, BucketPolicy policy)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            _progressCollector.QueueVerboseMessage("Setting bucket policy for: {0}", bucketName);

            // Validate policy before setting
            if (!ValidatePolicy(policy))
            {
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    Policy = policy,
                    IsValid = false,
                    Error = "Policy validation failed"
                };
            }

            try
            {
                var policyJson = JsonSerializer.Serialize(policy, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                var queryParams = new Dictionary<string, string> { { "policy", "" } };
                using var content = new StringContent(policyJson, Encoding.UTF8, "application/json");

                _httpClient.ExecuteRequest(HttpMethod.Put, $"/{bucketName}", queryParams, content: content);

                _progressCollector.QueueVerboseMessage("Successfully set bucket policy");

                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    Policy = policy,
                    PolicyJson = policyJson,
                    IsValid = true,
                    HasPolicy = true
                };
            }
            catch (Exception ex)
            {
                _progressCollector.QueueVerboseMessage("Failed to set bucket policy: {0}", ex.Message);
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    Policy = policy,
                    IsValid = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Sets bucket policy from JSON string
        /// </summary>
        public BucketPolicyResult SetBucketPolicyFromJson(string bucketName, string policyJson)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
            if (string.IsNullOrEmpty(policyJson)) throw new ArgumentException("Policy JSON cannot be null or empty", nameof(policyJson));

            try
            {
                var policy = JsonSerializer.Deserialize<BucketPolicy>(policyJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                return SetBucketPolicy(bucketName, policy!);
            }
            catch (JsonException ex)
            {
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    PolicyJson = policyJson,
                    IsValid = false,
                    Error = $"Invalid JSON: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Deletes the bucket policy
        /// </summary>
        public BucketPolicyResult DeleteBucketPolicy(string bucketName)
        {
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

            _progressCollector.QueueVerboseMessage("Deleting bucket policy for: {0}", bucketName);

            try
            {
                var queryParams = new Dictionary<string, string> { { "policy", "" } };
                _httpClient.ExecuteRequest(HttpMethod.Delete, $"/{bucketName}", queryParams);

                _progressCollector.QueueVerboseMessage("Successfully deleted bucket policy");

                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    HasPolicy = false,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                _progressCollector.QueueVerboseMessage("Failed to delete bucket policy: {0}", ex.Message);
                return new BucketPolicyResult
                {
                    BucketName = bucketName,
                    IsValid = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Creates a simple read-only policy for public access
        /// </summary>
        public BucketPolicy CreateReadOnlyPolicy(string bucketName, string? objectPrefix = null)
        {
            var resource = string.IsNullOrEmpty(objectPrefix) 
                ? $"arn:aws:s3:::{bucketName}/*" 
                : $"arn:aws:s3:::{bucketName}/{objectPrefix}*";

            return new BucketPolicy
            {
                Version = "2012-10-17",
                Statement = new List<PolicyStatement>
                {
                    new PolicyStatement
                    {
                        Effect = "Allow",
                        Principal = new { AWS = "*" },
                        Action = new List<string> { "s3:GetObject" },
                        Resource = new List<string> { resource }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a read-write policy for specific principals
        /// </summary>
        public BucketPolicy CreateReadWritePolicy(string bucketName, List<string> principals, string? objectPrefix = null)
        {
            var resource = string.IsNullOrEmpty(objectPrefix) 
                ? $"arn:aws:s3:::{bucketName}/*" 
                : $"arn:aws:s3:::{bucketName}/{objectPrefix}*";

            return new BucketPolicy
            {
                Version = "2012-10-17",
                Statement = new List<PolicyStatement>
                {
                    new PolicyStatement
                    {
                        Effect = "Allow",
                        Principal = new { AWS = principals },
                        Action = new List<string> { "s3:GetObject", "s3:PutObject", "s3:DeleteObject" },
                        Resource = new List<string> { resource }
                    }
                }
            };
        }

        /// <summary>
        /// Validates a bucket policy
        /// </summary>
        private bool ValidatePolicy(BucketPolicy policy)
        {
            if (policy == null) return false;
            if (string.IsNullOrEmpty(policy.Version)) return false;
            if (policy.Statement == null || policy.Statement.Count == 0) return false;

            foreach (var statement in policy.Statement)
            {
                if (string.IsNullOrEmpty(statement.Effect)) return false;
                if (statement.Effect != "Allow" && statement.Effect != "Deny") return false;
                if (statement.Action == null || statement.Action.Count == 0) return false;
                if (statement.Resource == null || statement.Resource.Count == 0) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Bucket policy structure
    /// </summary>
    public class BucketPolicy
    {
        public string Version { get; set; } = "2012-10-17";
        public List<PolicyStatement> Statement { get; set; } = new List<PolicyStatement>();
    }

    /// <summary>
    /// Policy statement structure
    /// </summary>
    public class PolicyStatement
    {
        public string Effect { get; set; } = string.Empty;
        public object? Principal { get; set; }
        public List<string> Action { get; set; } = new List<string>();
        public List<string> Resource { get; set; } = new List<string>();
        public Dictionary<string, object>? Condition { get; set; }
    }

    /// <summary>
    /// Result of bucket policy operations
    /// </summary>
    public class BucketPolicyResult
    {
        public string BucketName { get; set; } = string.Empty;
        public BucketPolicy? Policy { get; set; }
        public string? PolicyJson { get; set; }
        public bool HasPolicy { get; set; }
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public DateTime OperationTime { get; set; } = DateTime.UtcNow;
    }
}
