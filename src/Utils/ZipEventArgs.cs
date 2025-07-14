using System;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Event arguments for zip progress updates
    /// </summary>
    public class ZipProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the current file being processed
        /// </summary>
        public string CurrentFileName { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage for the current file (0-100)
        /// </summary>
        public double CurrentFileProgress { get; set; }

        /// <summary>
        /// Bytes processed for the current file
        /// </summary>
        public long CurrentFileBytesProcessed { get; set; }

        /// <summary>
        /// Total size of the current file
        /// </summary>
        public long CurrentFileSize { get; set; }

        /// <summary>
        /// Number of files processed so far
        /// </summary>
        public int TotalFilesProcessed { get; set; }

        /// <summary>
        /// Total bytes processed across all files
        /// </summary>
        public long TotalBytesProcessed { get; set; }

        /// <summary>
        /// Elapsed time since compression started
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Current compression speed in bytes per second
        /// </summary>
        public double CurrentSpeed => ElapsedTime.TotalSeconds > 0 ? TotalBytesProcessed / ElapsedTime.TotalSeconds : 0;
    }

    /// <summary>
    /// Event arguments for when a file is added to the zip
    /// </summary>
    public class ZipFileEventArgs : EventArgs
    {
        /// <summary>
        /// Original file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the original file
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Entry name in the zip archive
        /// </summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>
        /// Uncompressed size of the file
        /// </summary>
        public long UncompressedSize { get; set; }

        /// <summary>
        /// Compressed size in the zip archive
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Compression ratio (compressed/uncompressed)
        /// </summary>
        public double CompressionRatio { get; set; }

        /// <summary>
        /// Time taken to process this file
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Compression efficiency percentage (100 - ratio * 100)
        /// </summary>
        public double CompressionEfficiency => (1 - CompressionRatio) * 100;
    }

    /// <summary>
    /// Event arguments for when zip compression is completed
    /// </summary>
    public class ZipCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Time when compression started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Time when compression completed
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total duration of compression
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Total number of files compressed
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Total uncompressed size of all files
        /// </summary>
        public long TotalUncompressedSize { get; set; }

        /// <summary>
        /// Total compressed size of the zip archive
        /// </summary>
        public long TotalCompressedSize { get; set; }

        /// <summary>
        /// Overall compression ratio
        /// </summary>
        public double CompressionRatio { get; set; }

        /// <summary>
        /// Average compression speed in bytes per second
        /// </summary>
        public double AverageCompressionSpeed { get; set; }

        /// <summary>
        /// Overall compression efficiency percentage
        /// </summary>
        public double CompressionEfficiency => (1 - CompressionRatio) * 100;

        /// <summary>
        /// Space saved by compression
        /// </summary>
        public long SpaceSaved => TotalUncompressedSize - TotalCompressedSize;
    }
}
