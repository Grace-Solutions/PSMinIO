using System;
using System.Management.Automation;
using System.Text.Json;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Gets the policy for a MinIO bucket
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOBucketPolicy", SupportsShouldProcess = false)]
    [OutputType(typeof(string), typeof(PSObject))]
    public class GetMinIOBucketPolicyCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Name of the bucket to get the policy for
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Bucket")]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Return the policy as a formatted object instead of raw JSON
        /// </summary>
        [Parameter]
        public SwitchParameter AsObject { get; set; }

        /// <summary>
        /// Pretty-print the JSON output
        /// </summary>
        [Parameter]
        public SwitchParameter PrettyPrint { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            ValidateConnection();
            ValidateBucketName(BucketName);

            ExecuteOperation("GetBucketPolicy", () =>
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

                MinIOLogger.WriteVerbose(this, "Retrieving policy for bucket '{0}'", BucketName);

                try
                {
                    var policyJson = Client.GetBucketPolicy(BucketName);

                    if (string.IsNullOrWhiteSpace(policyJson))
                    {
                        MinIOLogger.WriteVerbose(this, "No policy is set for bucket '{0}'", BucketName);
                        
                        if (AsObject.IsPresent)
                        {
                            var emptyPolicy = new PSObject();
                            emptyPolicy.Properties.Add(new PSNoteProperty("BucketName", BucketName));
                            emptyPolicy.Properties.Add(new PSNoteProperty("HasPolicy", false));
                            emptyPolicy.Properties.Add(new PSNoteProperty("Policy", null));
                            emptyPolicy.Properties.Add(new PSNoteProperty("RetrievedAt", DateTime.UtcNow));
                            WriteObject(emptyPolicy);
                        }
                        else
                        {
                            WriteObject(string.Empty);
                        }
                        return;
                    }

                    MinIOLogger.WriteVerbose(this, "Retrieved policy for bucket '{0}' ({1} characters)", 
                        BucketName, policyJson.Length);

                    if (AsObject.IsPresent)
                    {
                        // Parse and return as structured object
                        var policyObject = CreatePolicyObject(policyJson);
                        WriteObject(policyObject);
                    }
                    else
                    {
                        // Return as JSON string
                        if (PrettyPrint.IsPresent)
                        {
                            var formattedJson = FormatJson(policyJson);
                            WriteObject(formattedJson);
                        }
                        else
                        {
                            WriteObject(policyJson);
                        }
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("NoSuchBucketPolicy") || 
                                          ex.Message.Contains("policy does not exist"))
                {
                    MinIOLogger.WriteVerbose(this, "No policy is set for bucket '{0}'", BucketName);
                    
                    if (AsObject.IsPresent)
                    {
                        var emptyPolicy = new PSObject();
                        emptyPolicy.Properties.Add(new PSNoteProperty("BucketName", BucketName));
                        emptyPolicy.Properties.Add(new PSNoteProperty("HasPolicy", false));
                        emptyPolicy.Properties.Add(new PSNoteProperty("Policy", null));
                        emptyPolicy.Properties.Add(new PSNoteProperty("RetrievedAt", DateTime.UtcNow));
                        WriteObject(emptyPolicy);
                    }
                    else
                    {
                        WriteObject(string.Empty);
                    }
                }

            }, $"Bucket: {BucketName}");
        }

        /// <summary>
        /// Creates a structured policy object from JSON
        /// </summary>
        /// <param name="policyJson">Policy JSON string</param>
        /// <returns>PSObject containing policy information</returns>
        private PSObject CreatePolicyObject(string policyJson)
        {
            var policyObject = new PSObject();
            policyObject.Properties.Add(new PSNoteProperty("BucketName", BucketName));
            policyObject.Properties.Add(new PSNoteProperty("HasPolicy", true));
            policyObject.Properties.Add(new PSNoteProperty("RetrievedAt", DateTime.UtcNow));

            try
            {
                // Parse the JSON to extract key information
                using var document = JsonDocument.Parse(policyJson);
                var root = document.RootElement;

                // Add raw policy
                policyObject.Properties.Add(new PSNoteProperty("PolicyJson", policyJson));

                // Extract version if present
                if (root.TryGetProperty("Version", out var versionElement))
                {
                    policyObject.Properties.Add(new PSNoteProperty("Version", versionElement.GetString()));
                }

                // Extract statements if present
                if (root.TryGetProperty("Statement", out var statementsElement))
                {
                    var statements = new System.Collections.Generic.List<PSObject>();
                    
                    if (statementsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var statement in statementsElement.EnumerateArray())
                        {
                            var statementObj = CreateStatementObject(statement);
                            statements.Add(statementObj);
                        }
                    }
                    else if (statementsElement.ValueKind == JsonValueKind.Object)
                    {
                        var statementObj = CreateStatementObject(statementsElement);
                        statements.Add(statementObj);
                    }

                    policyObject.Properties.Add(new PSNoteProperty("Statements", statements.ToArray()));
                    policyObject.Properties.Add(new PSNoteProperty("StatementCount", statements.Count));
                }

                // Add formatted JSON
                var formattedJson = FormatJson(policyJson);
                policyObject.Properties.Add(new PSNoteProperty("FormattedPolicy", formattedJson));
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, "Could not parse policy JSON: {0}", ex.Message);
                policyObject.Properties.Add(new PSNoteProperty("PolicyJson", policyJson));
                policyObject.Properties.Add(new PSNoteProperty("ParseError", ex.Message));
            }

            return policyObject;
        }

        /// <summary>
        /// Creates a statement object from a JSON element
        /// </summary>
        /// <param name="statementElement">JSON element representing a statement</param>
        /// <returns>PSObject containing statement information</returns>
        private PSObject CreateStatementObject(JsonElement statementElement)
        {
            var statementObj = new PSObject();

            try
            {
                if (statementElement.TryGetProperty("Effect", out var effectElement))
                {
                    statementObj.Properties.Add(new PSNoteProperty("Effect", effectElement.GetString()));
                }

                if (statementElement.TryGetProperty("Principal", out var principalElement))
                {
                    statementObj.Properties.Add(new PSNoteProperty("Principal", principalElement.ToString()));
                }

                if (statementElement.TryGetProperty("Action", out var actionElement))
                {
                    if (actionElement.ValueKind == JsonValueKind.Array)
                    {
                        var actions = new System.Collections.Generic.List<string>();
                        foreach (var action in actionElement.EnumerateArray())
                        {
                            actions.Add(action.GetString() ?? string.Empty);
                        }
                        statementObj.Properties.Add(new PSNoteProperty("Actions", actions.ToArray()));
                    }
                    else
                    {
                        statementObj.Properties.Add(new PSNoteProperty("Actions", new[] { actionElement.GetString() ?? string.Empty }));
                    }
                }

                if (statementElement.TryGetProperty("Resource", out var resourceElement))
                {
                    if (resourceElement.ValueKind == JsonValueKind.Array)
                    {
                        var resources = new System.Collections.Generic.List<string>();
                        foreach (var resource in resourceElement.EnumerateArray())
                        {
                            resources.Add(resource.GetString() ?? string.Empty);
                        }
                        statementObj.Properties.Add(new PSNoteProperty("Resources", resources.ToArray()));
                    }
                    else
                    {
                        statementObj.Properties.Add(new PSNoteProperty("Resources", new[] { resourceElement.GetString() ?? string.Empty }));
                    }
                }
            }
            catch (Exception ex)
            {
                MinIOLogger.WriteWarning(this, "Could not parse statement: {0}", ex.Message);
                statementObj.Properties.Add(new PSNoteProperty("ParseError", ex.Message));
            }

            return statementObj;
        }

        /// <summary>
        /// Formats JSON string for pretty printing
        /// </summary>
        /// <param name="json">JSON string to format</param>
        /// <returns>Formatted JSON string</returns>
        private string FormatJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(document, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch
            {
                // If formatting fails, return original
                return json;
            }
        }
    }
}
