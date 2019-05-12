using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    abstract class DelegatingStream : Stream
    {
        Stream _stream;
        private bool _disposed;

        protected DelegatingStream(Stream stream)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            _stream = stream;
        }

        protected Stream BaseStream => _stream;

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanTimeout => _stream.CanTimeout;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position
        {
            get
            {
                return _stream.Position;
            }
            set
            {
                _stream.Position = value;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return _stream.ReadTimeout;
            }
            set
            {
                _stream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return _stream.WriteTimeout;
            }
            set
            {
                _stream.WriteTimeout = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _stream.Dispose();
                }

                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return _stream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            _stream.WriteByte(value);
        }
    }
}