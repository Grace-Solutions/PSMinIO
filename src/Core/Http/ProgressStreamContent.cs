using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSMinIO.Core.Http
{
    /// <summary>
    /// HTTP content that wraps a stream and provides progress reporting during upload
    /// </summary>
    public class ProgressStreamContent : HttpContent
    {
        private readonly Stream _stream;
        private readonly Action<long>? _progressCallback;
        private readonly long _totalBytes;

        /// <summary>
        /// Creates a new ProgressStreamContent instance
        /// </summary>
        /// <param name="stream">Source stream</param>
        /// <param name="contentType">Content type</param>
        /// <param name="progressCallback">Progress callback</param>
        public ProgressStreamContent(Stream stream, string contentType, Action<long>? progressCallback = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _progressCallback = progressCallback;
            _totalBytes = stream.CanSeek ? stream.Length : -1;

            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            if (_totalBytes >= 0)
            {
                Headers.ContentLength = _totalBytes;
            }
        }

        /// <summary>
        /// Serializes the HTTP content to a stream
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="context">Transport context</param>
        /// <returns>Task representing the async operation</returns>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            const int bufferSize = 8192;
            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;

            // Reset source stream position if possible
            if (_stream.CanSeek)
            {
                _stream.Position = 0;
            }

            while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                _progressCallback?.Invoke(totalBytesRead);
            }
        }

        /// <summary>
        /// Determines whether the HTTP content has a valid length in bytes
        /// </summary>
        /// <param name="length">Content length</param>
        /// <returns>True if length is valid</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return _totalBytes >= 0;
        }

        /// <summary>
        /// Disposes the content
        /// </summary>
        /// <param name="disposing">Whether disposing</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
