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
        /// FileInfo object representing where the zip archive will be created
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNull]
        [Alias("ZipPath", "Archive", "Destination")]
        public FileInfo DestinationPath { get; set; } = null!;

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
        /// Compression level to use. Adaptive automatically selects optimal compression based on file type and size.
        /// </summary>
        [Parameter]
        [ValidateSet("Optimal", "Fastest", "NoCompression", "Adaptive")]
        public string CompressionLevel { get; set; } = "Adaptive";

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
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate destination path
            if (DestinationPath.Exists && !Force.IsPresent)
            {
                var errorRecord = new ErrorRecord(
                    new InvalidOperationException($"Zip file already exists: {DestinationPath.FullName}. Use -Force to overwrite."),
                    "ZipFileExists",
                    ErrorCategory.ResourceExists,
                    DestinationPath);
                ThrowTerminatingError(errorRecord);
            }

            // Ensure destination directory exists
            if (DestinationPath.Directory != null && !DestinationPath.Directory.Exists)
            {
                DestinationPath.Directory.Create();
                WriteVerboseMessage("Created destination directory: {0}", DestinationPath.Directory.FullName);
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

            if (ShouldProcess(DestinationPath.FullName, operationDescription))
            {
                var result = ExecuteOperation("CreateZipArchive", () =>
                {
                    WriteVerboseMessage("Creating zip archive: {0}", DestinationPath.FullName);
                    WriteVerboseMessage("Compression level: {0}, Mode: {1}", CompressionLevel, Mode);

                    ZipCreationResult zipResult;
                    using (var zipBuilder = ZipBuilder.CreateFile(this, DestinationPath.FullName, archiveMode))
                    {
                        switch (ParameterSetName)
                        {
                            case "Files":
                                zipResult = ProcessFiles(zipBuilder, compressionLevel);
                                break;
                            case "Directory":
                                zipResult = ProcessDirectory(zipBuilder, compressionLevel);
                                break;
                            default:
                                throw new InvalidOperationException($"Unknown parameter set: {ParameterSetName}");
                        }
                    }

                    WriteVerboseMessage("Zip archive created successfully: {0}", DestinationPath.FullName);
                    WriteVerboseMessage("Archive summary: {0} files, {1} -> {2} ({3:F1}% compression)",
                        zipResult.FileCount,
                        SizeFormatter.FormatBytes(zipResult.TotalUncompressedSize),
                        SizeFormatter.FormatBytes(zipResult.TotalCompressedSize),
                        zipResult.CompressionEfficiency);

                    return zipResult;
                }, $"Destination: {DestinationPath.FullName}, ParameterSet: {ParameterSetName}");

                // Always return the result object
                WriteObject(result);
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
                return zipBuilder.CreateResult(DestinationPath.FullName);
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
                return zipBuilder.CreateResult(DestinationPath.FullName);
            }

            WriteVerboseMessage("Adding {0} files to zip archive with {0} compression",
                validFiles.Length, CompressionLevel == "Adaptive" ? "adaptive" : CompressionLevel.ToLowerInvariant());

            // Add files to zip - pass null for adaptive compression
            var effectiveCompressionLevel = CompressionLevel == "Adaptive" ? null : (System.IO.Compression.CompressionLevel?)compressionLevel;
            zipBuilder.AddFiles(validFiles.Cast<FileSystemInfo>(), BasePath, effectiveCompressionLevel);

            return zipBuilder.CreateResult(DestinationPath.FullName);
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
                return zipBuilder.CreateResult(DestinationPath.FullName);
            }

            // Get files from directory
            var files = GetDirectoryFiles();
            if (files.Length == 0)
            {
                WriteWarning($"No files found in directory: {Directory.FullName}");
                return zipBuilder.CreateResult(DestinationPath.FullName);
            }

            WriteVerboseMessage("Adding directory to zip: {0} ({1} files) with {2} compression",
                Directory.Name, files.Length, CompressionLevel == "Adaptive" ? "adaptive" : CompressionLevel.ToLowerInvariant());

            // Determine base path for entries
            var basePath = BasePath ?? (IncludeBaseDirectory.IsPresent ? Directory.Parent?.FullName : Directory.FullName);

            // Add files to zip - pass null for adaptive compression
            var effectiveCompressionLevel = CompressionLevel == "Adaptive" ? null : (System.IO.Compression.CompressionLevel?)compressionLevel;
            zipBuilder.AddFiles(files.Cast<FileSystemInfo>(), basePath, effectiveCompressionLevel);

            return zipBuilder.CreateResult(DestinationPath.FullName);
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
