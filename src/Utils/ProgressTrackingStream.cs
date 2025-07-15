using System;
using System.IO;

namespace PSMinIO.Utils
{
    /// <summary>
    /// Stream wrapper that tracks read progress and reports it via callback
    /// </summary>
    public class ProgressTrackingStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _totalBytes;
        private readonly Action<long, long> _progressCallback;
        private long _bytesRead = 0;
        private readonly long _startPosition;

        public ProgressTrackingStream(Stream baseStream, long totalBytes, Action<long, long> progressCallback)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _totalBytes = totalBytes;
            _progressCallback = progressCallback ?? throw new ArgumentNullException(nameof(progressCallback));
            _startPosition = baseStream.Position;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _totalBytes;
        public override long Position 
        { 
            get => _bytesRead;
            set => throw new NotSupportedException("Setting position is not supported");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Limit read to remaining bytes
            var remainingBytes = _totalBytes - _bytesRead;
            var bytesToRead = (int)Math.Min(count, remainingBytes);
            
            if (bytesToRead <= 0)
                return 0;

            var bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
            _bytesRead += bytesRead;

            // Report progress
            _progressCallback(_bytesRead, _totalBytes);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Writing is not supported");
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seeking is not supported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Setting length is not supported");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Don't dispose the base stream - let the caller handle it
            }
            base.Dispose(disposing);
        }
    }
}
