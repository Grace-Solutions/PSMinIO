using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using PSMinIO.Core.Models;
using PSMinIO.Utils;

namespace PSMinIO.Cmdlets
{
    /// <summary>
    /// Opens and reads zip archive information with proper disposal handling
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "MinIOZipArchive")]
    [OutputType(typeof(ZipArchiveInfo))]
    public class GetMinIOZipArchiveCmdlet : MinIOBaseCmdlet
    {
        /// <summary>
        /// Path to the zip archive file to read
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        [Alias("ZipPath", "Archive", "FullName")]
        public FileInfo ZipFile { get; set; } = null!;

        /// <summary>
        /// Include detailed entry information for each file in the archive
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeEntries { get; set; }

        /// <summary>
        /// Validate the archive integrity by attempting to read all entries
        /// </summary>
        [Parameter]
        public SwitchParameter ValidateIntegrity { get; set; }

        /// <summary>
        /// Filter entries by name pattern (supports wildcards)
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string? Filter { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate zip file exists
            if (!ZipFile.Exists)
            {
                var errorRecord = new ErrorRecord(
                    new FileNotFoundException($"Zip file not found: {ZipFile.FullName}"),
                    "ZipFileNotFound",
                    ErrorCategory.ObjectNotFound,
                    ZipFile);
                ThrowTerminatingError(errorRecord);
            }

            var archiveInfo = ExecuteOperation("ReadZipArchive", () =>
            {
                WriteVerboseMessage("Reading zip archive: {0}", ZipFile.FullName);

                ZipArchiveInfo result;
                using (var fileStream = ZipFile.OpenRead())
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    // Create archive info
                    result = new ZipArchiveInfo
                    {
                        ZipFilePath = ZipFile.FullName,
                        ZipFileName = ZipFile.Name,
                        ZipFileSize = ZipFile.Length,
                        CreationTime = ZipFile.CreationTime,
                        LastWriteTime = ZipFile.LastWriteTime,
                        EntryCount = archive.Entries.Count
                    };

                    // Calculate total uncompressed size
                    result.TotalUncompressedSize = archive.Entries.Sum(e => e.Length);
                    result.TotalCompressedSize = archive.Entries.Sum(e => e.CompressedLength);
                    result.CompressionRatio = result.TotalUncompressedSize > 0
                        ? (double)result.TotalCompressedSize / result.TotalUncompressedSize
                        : 0;

                    WriteVerboseMessage("Archive contains {0} entries, {1} -> {2} ({3:F1}% compression)",
                        result.EntryCount,
                        SizeFormatter.FormatBytes(result.TotalUncompressedSize),
                        SizeFormatter.FormatBytes(result.TotalCompressedSize),
                        (1 - result.CompressionRatio) * 100);

                    // Include detailed entries if requested
                    if (IncludeEntries.IsPresent)
                    {
                        var entries = archive.Entries.AsEnumerable();

                        // Apply filter if specified
                        if (!string.IsNullOrEmpty(Filter))
                        {
                            var wildcardPattern = new WildcardPattern(Filter, WildcardOptions.IgnoreCase);
                            entries = entries.Where(e => wildcardPattern.IsMatch(e.FullName));
                        }

                        result.Entries = entries.Select(entry => new ZipEntryInfo
                        {
                            FullName = entry.FullName,
                            Name = entry.Name,
                            Length = entry.Length,
                            CompressedLength = entry.CompressedLength,
                            CompressionRatio = entry.Length > 0 ? (double)entry.CompressedLength / entry.Length : 0,
                            LastWriteTime = entry.LastWriteTime,
                            IsDirectory = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/")
                        }).ToArray();

                        WriteVerboseMessage("Included {0} entry details", result.Entries.Length);
                    }

                    // Validate integrity if requested
                    if (ValidateIntegrity.IsPresent)
                    {
                        WriteVerboseMessage("Validating archive integrity...");
                        var validationStart = DateTime.UtcNow;
                        var validEntries = 0;
                        var invalidEntries = 0;

                        foreach (var entry in archive.Entries)
                        {
                            try
                            {
                                using (var entryStream = entry.Open())
                                {
                                    // Read a small portion to validate the entry can be opened
                                    var buffer = new byte[1024];
                                    entryStream.Read(buffer, 0, buffer.Length);
                                }
                                validEntries++;
                            }
                            catch (Exception ex)
                            {
                                invalidEntries++;
                                WriteWarning($"Entry '{entry.FullName}' failed validation: {ex.Message}");
                            }
                        }

                        var validationDuration = DateTime.UtcNow - validationStart;
                        result.IsValid = invalidEntries == 0;
                        result.ValidationDuration = validationDuration;

                        WriteVerboseMessage("Validation completed in {0}: {1} valid, {2} invalid entries",
                            SizeFormatter.FormatDuration(validationDuration),
                            validEntries,
                            invalidEntries);

                        if (invalidEntries > 0)
                        {
                            WriteWarning($"Archive validation found {invalidEntries} corrupted entries");
                        }
                    }
                }

                WriteVerboseMessage("Successfully read zip archive information");
                return result;

            }, $"ZipFile: {ZipFile.FullName}");

            // Always return the archive info object
            WriteObject(archiveInfo);
        }
    }

    /// <summary>
    /// Information about a zip archive
    /// </summary>
    public class ZipArchiveInfo
    {
        public string ZipFilePath { get; set; } = string.Empty;
        public string ZipFileName { get; set; } = string.Empty;
        public long ZipFileSize { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public int EntryCount { get; set; }
        public long TotalUncompressedSize { get; set; }
        public long TotalCompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public double CompressionEfficiency => (1 - CompressionRatio) * 100;
        public long SpaceSaved => TotalUncompressedSize - TotalCompressedSize;
        public ZipEntryInfo[]? Entries { get; set; }
        public bool? IsValid { get; set; }
        public TimeSpan? ValidationDuration { get; set; }
    }

    /// <summary>
    /// Information about a zip archive entry
    /// </summary>
    public class ZipEntryInfo
    {
        public string FullName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Length { get; set; }
        public long CompressedLength { get; set; }
        public double CompressionRatio { get; set; }
        public double CompressionEfficiency => (1 - CompressionRatio) * 100;
        public long SpaceSaved => Length - CompressedLength;
        public DateTimeOffset LastWriteTime { get; set; }
        public bool IsDirectory { get; set; }
    }
}
