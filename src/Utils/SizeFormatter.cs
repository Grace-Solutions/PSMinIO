using System;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Utility class for formatting byte sizes into human-readable strings
    /// </summary>
    public static class SizeFormatter
    {
        /// <summary>
        /// Size units in order from smallest to largest
        /// </summary>
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        /// <summary>
        /// Formats bytes into a human-readable string with appropriate units
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted string with appropriate unit</returns>
        public static string FormatBytes(long bytes, int decimalPlaces = 2)
        {
            if (bytes == 0)
                return "0 B";

            if (bytes < 0)
                return $"-{FormatBytes(-bytes, decimalPlaces)}";

            int unitIndex = 0;
            double size = bytes;

            // Find the appropriate unit
            while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            // Format with specified decimal places
            var formatString = $"{{0:F{decimalPlaces}}} {{1}}";
            return string.Format(formatString, size, SizeUnits[unitIndex]);
        }

        /// <summary>
        /// Formats bytes into a human-readable string with appropriate units (double overload)
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted string with appropriate unit</returns>
        public static string FormatBytes(double bytes, int decimalPlaces = 2)
        {
            return FormatBytes((long)Math.Round(bytes), decimalPlaces);
        }

        /// <summary>
        /// Formats bytes per second into a human-readable string
        /// </summary>
        /// <param name="bytesPerSecond">Bytes per second</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted string with appropriate unit and "/s" suffix</returns>
        public static string FormatBytesPerSecond(double bytesPerSecond, int decimalPlaces = 2)
        {
            return $"{FormatBytes(bytesPerSecond, decimalPlaces)}/s";
        }

        /// <summary>
        /// Formats a transfer rate with context
        /// </summary>
        /// <param name="bytesTransferred">Number of bytes transferred</param>
        /// <param name="elapsedTime">Time elapsed for the transfer</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted transfer rate string</returns>
        public static string FormatTransferRate(long bytesTransferred, TimeSpan elapsedTime, int decimalPlaces = 2)
        {
            if (elapsedTime.TotalSeconds <= 0)
                return "0 B/s";

            var bytesPerSecond = bytesTransferred / elapsedTime.TotalSeconds;
            return FormatBytesPerSecond(bytesPerSecond, decimalPlaces);
        }

        /// <summary>
        /// Formats a progress string showing current/total with percentages
        /// </summary>
        /// <param name="current">Current bytes processed</param>
        /// <param name="total">Total bytes to process</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted progress string</returns>
        public static string FormatProgress(long current, long total, int decimalPlaces = 2)
        {
            var currentFormatted = FormatBytes(current, decimalPlaces);
            var totalFormatted = FormatBytes(total, decimalPlaces);
            
            if (total > 0)
            {
                var percentage = (double)current / total * 100;
                return $"{currentFormatted} / {totalFormatted} ({percentage:F1}%)";
            }
            
            return $"{currentFormatted} / {totalFormatted}";
        }

        /// <summary>
        /// Gets the appropriate unit for a given byte size without formatting
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <returns>Appropriate unit string</returns>
        public static string GetAppropriateUnit(long bytes)
        {
            if (bytes == 0)
                return "B";

            int unitIndex = 0;
            double size = Math.Abs(bytes);

            while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return SizeUnits[unitIndex];
        }

        /// <summary>
        /// Converts bytes to the specified unit
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <param name="unit">Target unit (B, KB, MB, GB, TB, PB, EB)</param>
        /// <returns>Value in the specified unit</returns>
        public static double ConvertToUnit(long bytes, string unit)
        {
            var unitIndex = Array.IndexOf(SizeUnits, unit.ToUpperInvariant());
            if (unitIndex == -1)
                throw new ArgumentException($"Invalid unit: {unit}. Valid units are: {string.Join(", ", SizeUnits)}");

            if (unitIndex == 0) // Bytes
                return bytes;

            return bytes / Math.Pow(1024, unitIndex);
        }

        /// <summary>
        /// Parses a size string back to bytes (e.g., "1.5 GB" -> bytes)
        /// </summary>
        /// <param name="sizeString">Size string to parse</param>
        /// <returns>Number of bytes</returns>
        public static long ParseSizeString(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString))
                throw new ArgumentException("Size string cannot be null or empty");

            var parts = sizeString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid size string format: {sizeString}. Expected format: '1.5 GB'");

            if (!double.TryParse(parts[0], out var value))
                throw new ArgumentException($"Invalid numeric value: {parts[0]}");

            var unit = parts[1].ToUpperInvariant();
            var unitIndex = Array.IndexOf(SizeUnits, unit);
            if (unitIndex == -1)
                throw new ArgumentException($"Invalid unit: {unit}. Valid units are: {string.Join(", ", SizeUnits)}");

            return (long)(value * Math.Pow(1024, unitIndex));
        }

        /// <summary>
        /// Formats a size comparison between two values
        /// </summary>
        /// <param name="value1">First value in bytes</param>
        /// <param name="value2">Second value in bytes</param>
        /// <param name="label1">Label for first value</param>
        /// <param name="label2">Label for second value</param>
        /// <param name="decimalPlaces">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted comparison string</returns>
        public static string FormatComparison(long value1, long value2, string label1, string label2, int decimalPlaces = 2)
        {
            var formatted1 = FormatBytes(value1, decimalPlaces);
            var formatted2 = FormatBytes(value2, decimalPlaces);
            
            var difference = value1 - value2;
            var diffFormatted = FormatBytes(Math.Abs(difference), decimalPlaces);
            var diffDirection = difference >= 0 ? "larger" : "smaller";
            
            return $"{label1}: {formatted1}, {label2}: {formatted2} ({diffFormatted} {diffDirection})";
        }
    }
}
