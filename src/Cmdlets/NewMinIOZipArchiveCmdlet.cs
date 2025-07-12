using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Creates zip archives with comprehensive progress tracking and metrics
    /// </summary>
    [Cmdlet(VerbsCommon.New, "MinIOZipArchive", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(ZipCreationResult))]
    public class NewMinIOZipArchiveCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Path where the zip archive will be created
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("ZipPath", "Archive")]
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>
        /// Array of FileInfo objects to add to the zip
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Files")]
        [ValidateNotNull]
        [Alias("File", "Files")]
        public FileInfo[]? Path { get; set; }

        /// <summary>
        /// Directory to add to the zip archive
        /// </summary>
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Directory")]
        [ValidateNotNull]
        [Alias("Dir", "Folder")]
        public DirectoryInfo? Directory { get; set; }

        /// <summary>
        /// Base path to remove from entry names (creates relative paths in zip)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        [Alias("Base")]
        public string? BasePath { get; set; }

        /// <summary>
        /// Include the base directory name in zip entries (Directory parameter set only)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public SwitchParameter IncludeBaseDirectory { get; set; }

        /// <summary>
        /// Process directory contents recursively (Directory parameter set only)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public SwitchParameter Recursive { get; set; }

        /// <summary>
        /// Maximum depth for recursive processing (0 = unlimited)
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        [ValidateRange(0, int.MaxValue)]
        public int MaxDepth { get; set; } = 0;

        /// <summary>
        /// Script block to filter files for inclusion
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public ScriptBlock? InclusionFilter { get; set; }

        /// <summary>
        /// Script block to filter files for exclusion
        /// </summary>
        [Parameter(ParameterSetName = "Directory")]
        public ScriptBlock? ExclusionFilter { get; set; }

        /// <summary>
        /// Compression level to use
        /// </summary>
        [Parameter]
        [ValidateSet("Optimal", "Fastest", "NoCompression")]
        public string CompressionLevel { get; set; } = "Optimal";

        /// <summary>
        /// Archive mode (Create, Update for appending)
        /// </summary>
        [Parameter]
        [ValidateSet("Create", "Update")]
        public string Mode { get; set; } = "Create";

        /// <summary>
        /// Overwrite existing zip file without prompting
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Return the zip creation result
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate destination path
            var destinationInfo = new FileInfo(DestinationPath);
            if (destinationInfo.Exists && !Force.IsPresent)
            {
                var errorRecord = new ErrorRecord(
                    new InvalidOperationException($"Zip file already exists: {DestinationPath}. Use -Force to overwrite."),
                    "ZipFileExists",
                    ErrorCategory.ResourceExists,
                    DestinationPath);
                ThrowTerminatingError(errorRecord);
            }

            // Ensure destination directory exists
            if (destinationInfo.Directory != null && !destinationInfo.Directory.Exists)
            {
                destinationInfo.Directory.Create();
                WriteVerboseMessage("Created destination directory: {0}", destinationInfo.Directory.FullName);
            }

            // Parse compression level
            var compressionLevel = ParseCompressionLevel(CompressionLevel);
            var archiveMode = ParseArchiveMode(Mode);

            // Determine operation description for ShouldProcess
            var operationDescription = ParameterSetName switch
            {
                "Files" => $"Create zip archive with {Path?.Length ?? 0} files",
                "Directory" => $"Create zip archive from directory '{Directory?.Name}'",
                _ => "Create zip archive"
            };

            if (ShouldProcess(DestinationPath, operationDescription))
            {
                ExecuteOperation("CreateZipArchive", () =>
                {
                    WriteVerboseMessage("Creating zip archive: {0}", DestinationPath);
                    WriteVerboseMessage("Compression level: {0}, Mode: {1}", CompressionLevel, Mode);

                    ZipCreationResult result;
                    using (var zipBuilder = ZipBuilder.CreateFile(this, DestinationPath, archiveMode))
                    {
                        switch (ParameterSetName)
                        {
                            case "Files":
                                result = ProcessFiles(zipBuilder, compressionLevel);
                                break;
                            case "Directory":
                                result = ProcessDirectory(zipBuilder, compressionLevel);
                                break;
                            default:
                                throw new InvalidOperationException($"Unknown parameter set: {ParameterSetName}");
                        }
                    }

                    WriteVerboseMessage("Zip archive created successfully: {0}", DestinationPath);
                    WriteVerboseMessage("Archive summary: {0} files, {1} -> {2} ({3:F1}% compression)",
                        result.FileCount,
                        SizeFormatter.FormatBytes(result.TotalUncompressedSize),
                        SizeFormatter.FormatBytes(result.TotalCompressedSize),
                        result.CompressionEfficiency);

                    if (PassThru.IsPresent)
                    {
                        WriteObject(result);
                    }

                    return result;
                }, $"Destination: {DestinationPath}, ParameterSet: {ParameterSetName}");
            }
        }

        /// <summary>
        /// Processes files for zip creation
        /// </summary>
        private ZipCreationResult ProcessFiles(ZipBuilder zipBuilder, System.IO.Compression.CompressionLevel compressionLevel)
        {
            if (Path == null || Path.Length == 0)
            {
                WriteWarning("No files provided for zip archive");
                return zipBuilder.CreateResult(DestinationPath);
            }

            // Filter out files that don't exist
            var validFiles = Path.Where(f => f.Exists).ToArray();
            var skippedCount = Path.Length - validFiles.Length;

            if (skippedCount > 0)
            {
                WriteWarning($"Skipped {skippedCount} files that do not exist");
            }

            if (validFiles.Length == 0)
            {
                WriteWarning("No valid files found for zip archive");
                return zipBuilder.CreateResult(DestinationPath);
            }

            WriteVerboseMessage("Adding {0} files to zip archive", validFiles.Length);

            // Add files to zip
            zipBuilder.AddFiles(validFiles.Cast<FileSystemInfo>(), BasePath, compressionLevel);

            return zipBuilder.CreateResult(DestinationPath);
        }

        /// <summary>
        /// Processes directory for zip creation
        /// </summary>
        private ZipCreationResult ProcessDirectory(ZipBuilder zipBuilder, System.IO.Compression.CompressionLevel compressionLevel)
        {
            if (Directory == null || !Directory.Exists)
            {
                var errorRecord = new ErrorRecord(
                    new DirectoryNotFoundException($"Directory not found: {Directory?.FullName}"),
                    "DirectoryNotFound",
                    ErrorCategory.ObjectNotFound,
                    Directory);
                ThrowTerminatingError(errorRecord);
                return zipBuilder.CreateResult(DestinationPath);
            }

            // Get files from directory
            var files = GetDirectoryFiles();
            if (files.Length == 0)
            {
                WriteWarning($"No files found in directory: {Directory.FullName}");
                return zipBuilder.CreateResult(DestinationPath);
            }

            WriteVerboseMessage("Adding directory to zip: {0} ({1} files)", Directory.Name, files.Length);

            // Determine base path for entries
            var basePath = BasePath ?? (IncludeBaseDirectory.IsPresent ? Directory.Parent?.FullName : Directory.FullName);

            // Add files to zip
            zipBuilder.AddFiles(files.Cast<FileSystemInfo>(), basePath, compressionLevel);

            return zipBuilder.CreateResult(DestinationPath);
        }

        /// <summary>
        /// Gets files from directory based on filters and recursion settings
        /// </summary>
        private FileInfo[] GetDirectoryFiles()
        {
            var searchOption = Recursive.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory!.GetFiles("*", searchOption);

            // Apply depth filtering if MaxDepth is specified and we're recursive
            if (Recursive.IsPresent && MaxDepth > 0)
            {
                var basePath = Directory.FullName;
                allFiles = allFiles.Where(f =>
                {
                    var relativePath = f.FullName.Substring(basePath.Length).TrimStart('\\', '/');
                    var depth = relativePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                    return depth <= MaxDepth;
                }).ToArray();
            }

            // Apply inclusion filter
            if (InclusionFilter != null)
            {
                allFiles = allFiles.Where(f => EvaluateFilter(InclusionFilter, f)).ToArray();
            }

            // Apply exclusion filter
            if (ExclusionFilter != null)
            {
                allFiles = allFiles.Where(f => !EvaluateFilter(ExclusionFilter, f)).ToArray();
            }

            return allFiles;
        }

        /// <summary>
        /// Evaluates a script block filter against a file
        /// </summary>
        private bool EvaluateFilter(ScriptBlock filter, FileInfo file)
        {
            try
            {
                var result = filter.InvokeWithContext(null, new List<PSVariable> { new PSVariable("_", file) });
                return result.Count > 0 && LanguagePrimitives.IsTrue(result[0]);
            }
            catch (Exception ex)
            {
                WriteWarning($"Filter evaluation failed for {file.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses compression level string to enum
        /// </summary>
        private static System.IO.Compression.CompressionLevel ParseCompressionLevel(string level)
        {
            return level switch
            {
                "Optimal" => System.IO.Compression.CompressionLevel.Optimal,
                "Fastest" => System.IO.Compression.CompressionLevel.Fastest,
                "NoCompression" => System.IO.Compression.CompressionLevel.NoCompression,
                _ => System.IO.Compression.CompressionLevel.Optimal
            };
        }

        /// <summary>
        /// Parses archive mode string to enum
        /// </summary>
        private static ZipArchiveMode ParseArchiveMode(string mode)
        {
            return mode switch
            {
                "Create" => ZipArchiveMode.Create,
                "Update" => ZipArchiveMode.Update,
                _ => ZipArchiveMode.Create
            };
        }
    }
}
