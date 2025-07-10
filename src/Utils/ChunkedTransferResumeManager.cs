using System;
using System.IO;
using System.Text.Json;
using PSMinIO.Models;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Manages resume data for chunked transfer operations
    /// </summary>
    public static class ChunkedTransferResumeManager
    {
        private static readonly string DefaultResumeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PSMinIO", "Resume");

        /// <summary>
        /// Saves transfer state for resume functionality
        /// </summary>
        /// <param name="transferState">Transfer state to save</param>
        /// <param name="customPath">Custom path for resume data (optional)</param>
        /// <returns>Path where resume data was saved</returns>
        public static string SaveTransferState(ChunkedTransferState transferState, string? customPath = null)
        {
            var resumeDirectory = customPath ?? DefaultResumeDirectory;
            
            // Ensure directory exists
            if (!Directory.Exists(resumeDirectory))
            {
                Directory.CreateDirectory(resumeDirectory);
            }

            // Generate unique filename based on transfer details
            var fileName = GenerateResumeFileName(transferState);
            var filePath = Path.Combine(resumeDirectory, fileName);

            // Serialize and save
            var json = JsonSerializer.Serialize(transferState, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(filePath, json);
            return filePath;
        }

        /// <summary>
        /// Loads transfer state for resume functionality
        /// </summary>
        /// <param name="bucketName">Bucket name</param>
        /// <param name="objectName">Object name</param>
        /// <param name="filePath">Local file path</param>
        /// <param name="transferType">Transfer type</param>
        /// <param name="customPath">Custom path for resume data (optional)</param>
        /// <returns>Transfer state if found, null otherwise</returns>
        public static ChunkedTransferState? LoadTransferState(
            string bucketName, 
            string objectName, 
            string filePath, 
            ChunkedTransferType transferType,
            string? customPath = null)
        {
            var resumeDirectory = customPath ?? DefaultResumeDirectory;
            
            if (!Directory.Exists(resumeDirectory))
                return null;

            // Generate expected filename
            var tempState = new ChunkedTransferState
            {
                BucketName = bucketName,
                ObjectName = objectName,
                FilePath = filePath,
                TransferType = transferType
            };
            
            var fileName = GenerateResumeFileName(tempState);
            var resumeFilePath = Path.Combine(resumeDirectory, fileName);

            if (!File.Exists(resumeFilePath))
                return null;

            try
            {
                var json = File.ReadAllText(resumeFilePath);
                var transferState = JsonSerializer.Deserialize<ChunkedTransferState>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return transferState;
            }
            catch (Exception)
            {
                // If we can't deserialize, treat as no resume data
                return null;
            }
        }

        /// <summary>
        /// Deletes resume data after successful completion
        /// </summary>
        /// <param name="transferState">Transfer state to clean up</param>
        /// <param name="customPath">Custom path for resume data (optional)</param>
        public static void CleanupResumeData(ChunkedTransferState transferState, string? customPath = null)
        {
            var resumeDirectory = customPath ?? DefaultResumeDirectory;
            var fileName = GenerateResumeFileName(transferState);
            var filePath = Path.Combine(resumeDirectory, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Validates if resume data is still valid
        /// </summary>
        /// <param name="transferState">Transfer state to validate</param>
        /// <param name="currentFileInfo">Current file information</param>
        /// <returns>True if resume data is valid</returns>
        public static bool IsResumeDataValid(ChunkedTransferState transferState, FileInfo? currentFileInfo = null)
        {
            // Check if transfer state is reasonable
            if (transferState == null)
                return false;

            // For uploads, validate source file hasn't changed
            if (transferState.TransferType == ChunkedTransferType.Upload && currentFileInfo != null)
            {
                if (!currentFileInfo.Exists)
                    return false;

                // Check if file size or last modified time changed
                if (currentFileInfo.Length != transferState.TotalSize ||
                    currentFileInfo.LastWriteTimeUtc != transferState.LastModified)
                {
                    return false;
                }
            }

            // Check if resume data is not too old (e.g., older than 7 days)
            if (DateTime.UtcNow - transferState.LastUpdated > TimeSpan.FromDays(7))
                return false;

            return true;
        }

        /// <summary>
        /// Gets all resume files in the directory
        /// </summary>
        /// <param name="customPath">Custom path for resume data (optional)</param>
        /// <returns>Array of resume file paths</returns>
        public static string[] GetResumeFiles(string? customPath = null)
        {
            var resumeDirectory = customPath ?? DefaultResumeDirectory;
            
            if (!Directory.Exists(resumeDirectory))
                return Array.Empty<string>();

            return Directory.GetFiles(resumeDirectory, "*.psminioResume");
        }

        /// <summary>
        /// Cleans up old resume files
        /// </summary>
        /// <param name="olderThanDays">Delete files older than this many days</param>
        /// <param name="customPath">Custom path for resume data (optional)</param>
        /// <returns>Number of files cleaned up</returns>
        public static int CleanupOldResumeFiles(int olderThanDays = 7, string? customPath = null)
        {
            var resumeFiles = GetResumeFiles(customPath);
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var cleanedCount = 0;

            foreach (var file in resumeFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        cleanedCount++;
                    }
                }
                catch (Exception)
                {
                    // Ignore cleanup errors
                }
            }

            return cleanedCount;
        }

        /// <summary>
        /// Generates a unique filename for resume data
        /// </summary>
        /// <param name="transferState">Transfer state</param>
        /// <returns>Unique filename</returns>
        private static string GenerateResumeFileName(ChunkedTransferState transferState)
        {
            // Create a hash of the key components to ensure uniqueness
            var key = $"{transferState.BucketName}|{transferState.ObjectName}|{transferState.FilePath}|{transferState.TransferType}";
            var hash = key.GetHashCode().ToString("X8");
            
            // Include readable components for easier identification
            var safeBucketName = MakeSafeFileName(transferState.BucketName);
            var safeObjectName = MakeSafeFileName(Path.GetFileName(transferState.ObjectName));
            var transferType = transferState.TransferType.ToString().ToLower();
            
            return $"{safeBucketName}_{safeObjectName}_{transferType}_{hash}.psminioResume";
        }

        /// <summary>
        /// Makes a string safe for use as a filename
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Safe filename string</returns>
        private static string MakeSafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            var invalidChars = Path.GetInvalidFileNameChars();
            var safe = input;
            
            foreach (var c in invalidChars)
            {
                safe = safe.Replace(c, '_');
            }

            // Limit length and remove leading/trailing dots and spaces
            safe = safe.Trim(' ', '.');
            if (safe.Length > 50)
                safe = safe.Substring(0, 50);

            return string.IsNullOrEmpty(safe) ? "unknown" : safe;
        }
    }
}
