using System;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Utility class for formatting byte sizes in human-readable format
    /// </summary>
    public static class SizeFormatter
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// Formats a byte count as a human-readable string with appropriate units
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted size string (e.g., "1.23 MB")</returns>
        public static string FormatBytes(long bytes, int decimalPlaces = 2)
        {
            if (bytes == 0)
                return "0 B";

            if (bytes < 0)
                return $"-{FormatBytes(-bytes, decimalPlaces)}";

            int unitIndex = 0;
            double size = bytes;

            while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size.ToString($"F{decimalPlaces}")} {SizeUnits[unitIndex]}";
        }

        /// <summary>
        /// Formats a byte count as a human-readable string with appropriate units (double overload)
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted size string (e.g., "1.23 MB")</returns>
        public static string FormatBytes(double bytes, int decimalPlaces = 2)
        {
            return FormatBytes((long)Math.Round(bytes), decimalPlaces);
        }

        /// <summary>
        /// Parses a human-readable size string back to bytes
        /// </summary>
        /// <param name="sizeString">Size string (e.g., "1.5 MB")</param>
        /// <returns>Number of bytes</returns>
        /// <exception cref="ArgumentException">Thrown when the size string is invalid</exception>
        public static long ParseBytes(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString))
                throw new ArgumentException("Size string cannot be null or empty", nameof(sizeString));

            sizeString = sizeString.Trim().ToUpperInvariant();

            // Handle just numbers (assume bytes)
            if (double.TryParse(sizeString, out double justNumber))
                return (long)justNumber;

            // Find the last space or digit-to-letter boundary
            int unitStartIndex = -1;
            for (int i = sizeString.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(sizeString[i]) || sizeString[i] == '.')
                {
                    unitStartIndex = i + 1;
                    break;
                }
            }

            if (unitStartIndex == -1 || unitStartIndex >= sizeString.Length)
                throw new ArgumentException($"Invalid size string format: {sizeString}", nameof(sizeString));

            string numberPart = sizeString.Substring(0, unitStartIndex).Trim();
            string unitPart = sizeString.Substring(unitStartIndex).Trim();

            if (!double.TryParse(numberPart, out double value))
                throw new ArgumentException($"Invalid numeric value: {numberPart}", nameof(sizeString));

            long multiplier = GetMultiplierForUnit(unitPart);
            return (long)(value * multiplier);
        }

        /// <summary>
        /// Gets the multiplier for a given unit
        /// </summary>
        /// <param name="unit">Unit string (e.g., "MB", "GB")</param>
        /// <returns>Multiplier value</returns>
        private static long GetMultiplierForUnit(string unit)
        {
            return unit switch
            {
                "B" or "BYTE" or "BYTES" => 1L,
                "KB" or "KILOBYTE" or "KILOBYTES" => 1024L,
                "MB" or "MEGABYTE" or "MEGABYTES" => 1024L * 1024L,
                "GB" or "GIGABYTE" or "GIGABYTES" => 1024L * 1024L * 1024L,
                "TB" or "TERABYTE" or "TERABYTES" => 1024L * 1024L * 1024L * 1024L,
                "PB" or "PETABYTE" or "PETABYTES" => 1024L * 1024L * 1024L * 1024L * 1024L,
                _ => throw new ArgumentException($"Unknown unit: {unit}")
            };
        }

        /// <summary>
        /// Formats transfer speed in bytes per second
        /// </summary>
        /// <param name="bytesPerSecond">Transfer speed in bytes per second</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted speed string (e.g., "1.23 MB/s")</returns>
        public static string FormatSpeed(double bytesPerSecond, int decimalPlaces = 2)
        {
            return $"{FormatBytes(bytesPerSecond, decimalPlaces)}/s";
        }

        /// <summary>
        /// Formats a time duration in a human-readable format
        /// </summary>
        /// <param name="duration">Duration to format</param>
        /// <returns>Formatted duration string (e.g., "1h 23m 45s")</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
                return $"{duration.TotalMilliseconds:F0}ms";

            if (duration.TotalMinutes < 1)
                return $"{duration.TotalSeconds:F1}s";

            if (duration.TotalHours < 1)
                return $"{duration.Minutes}m {duration.Seconds}s";

            if (duration.TotalDays < 1)
                return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";

            return $"{duration.Days}d {duration.Hours}h {duration.Minutes}m";
        }

        /// <summary>
        /// Calculates and formats estimated time remaining
        /// </summary>
        /// <param name="bytesTransferred">Bytes already transferred</param>
        /// <param name="totalBytes">Total bytes to transfer</param>
        /// <param name="elapsedTime">Time elapsed so far</param>
        /// <returns>Formatted ETA string</returns>
        public static string FormatETA(long bytesTransferred, long totalBytes, TimeSpan elapsedTime)
        {
            if (bytesTransferred <= 0 || totalBytes <= 0 || elapsedTime.TotalSeconds <= 0)
                return "Unknown";

            if (bytesTransferred >= totalBytes)
                return "Complete";

            double bytesPerSecond = bytesTransferred / elapsedTime.TotalSeconds;
            long remainingBytes = totalBytes - bytesTransferred;
            double remainingSeconds = remainingBytes / bytesPerSecond;

            return FormatDuration(TimeSpan.FromSeconds(remainingSeconds));
        }
    }
}
