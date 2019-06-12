using System;
using System.Diagnostics.Contracts;
using System.IO;
using CoreWCF.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    // Enforces an overall timeout based on the TimeoutHelper passed in
    internal class TimeoutStream : DelegatingStream
    {
        private TimeoutHelper _timeoutHelper;
        private bool _disposed;
        private byte[] _oneByteArray = new byte[1];

        public TimeoutStream(Stream stream, TimeSpan timeout)
            : base(stream)
        {
            if (!stream.CanTimeout)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("stream", SR.StreamDoesNotSupportTimeout);
            }

            _timeoutHelper = new TimeoutHelper(timeout);
            ReadTimeout = TimeoutHelper.ToMilliseconds(timeout);
            WriteTimeout = ReadTimeout;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsyncInternal(buffer, offset, count, CancellationToken.None).WaitForCompletion();
        }

        public override int ReadByte()
        {
            int r = Read(_oneByteArray, 0, 1);
            if (r == 0)
                return -1;
            return _oneByteArray[0];
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Supporting a passed in cancellationToken as well as honoring the timeout token in this class would require
            // creating a linked token source on every call which is extra allocation and needs disposal. As this is an 
            // internal class, it's okay to add this extra constraint to usage of this method.
            Fx.Assert(!cancellationToken.CanBeCanceled, "cancellationToken shouldn't be cancellable");
            var cancelToken = _timeoutHelper.GetCancellationToken();
            return await base.ReadAsync(buffer, offset, count, cancelToken);
        }

        private async Task<int> ReadAsyncInternal(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await TaskHelpers.EnsureDefaultTaskScheduler();
            return await ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsyncInternal(buffer, offset, count, CancellationToken.None).WaitForCompletion();
        }

        public override void WriteByte(byte value)
        {
            _oneByteArray[0] = value;
            Write(_oneByteArray, 0, 1);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Supporting a passed in cancellationToken as well as honoring the timeout token in this class would require
            // creating a linked token source on every call which is extra allocation and needs disposal. As this is an 
            // internal class, it's okay to add this extra constraint to usage of this method.
            Fx.Assert(!cancellationToken.CanBeCanceled, "cancellationToken shouldn't be cancellable");
            var cancelToken = _timeoutHelper.GetCancellationToken();
            await base.WriteAsync(buffer, offset, count, cancelToken);
        }

        private async Task WriteAsyncInternal(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await TaskHelpers.EnsureDefaultTaskScheduler();
            await WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timeoutHelper = default(TimeoutHelper);
                }

                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}