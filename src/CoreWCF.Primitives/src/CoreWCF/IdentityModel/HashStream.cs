// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;

namespace CoreWCF.IdentityModel
{
    internal sealed class HashStream : Stream
    {
        private long _length;
        private bool _disposed;
        private bool _hashNeedsReset;
        private MemoryStream _logStream;

        /// <summary>
        /// Constructor for HashStream. The HashAlgorithm instance is owned by the caller.
        /// </summary>
        public HashStream(HashAlgorithm hash)
        {
            if (hash == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("hash");
            }

            Reset(hash);
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public HashAlgorithm Hash { get; private set; }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _length; }
            set
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }
        }

        public override void Flush()
        {
        }

        public void FlushHash()
        {
            FlushHash(null);
        }

        public void FlushHash(MemoryStream preCanonicalBytes)
        {
            Hash.TransformFinalBlock(CryptoHelper.EmptyBuffer, 0, 0);
            //TODO logs Pii data
            //if (DigestTraceRecordHelper.ShouldTraceDigest)
            //    DigestTraceRecordHelper.TraceDigest(this.logStream, this.hash);
        }

        public byte[] FlushHashAndGetValue()
        {
            return FlushHashAndGetValue(null);
        }

        public byte[] FlushHashAndGetValue(MemoryStream preCanonicalBytes)
        {
            FlushHash(preCanonicalBytes);
            return Hash.Hash;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        public void Reset()
        {
            if (_hashNeedsReset)
            {
                Hash.Initialize();
                _hashNeedsReset = false;
            }
            _length = 0;

            // if (DigestTraceRecordHelper.ShouldTraceDigest)
            //     this.logStream = new MemoryStream();
        }

        public void Reset(HashAlgorithm hash)
        {
            Hash = hash;
            _hashNeedsReset = false;
            _length = 0;

            //  if (DigestTraceRecordHelper.ShouldTraceDigest)
            //     this.logStream = new MemoryStream();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Hash.TransformBlock(buffer, offset, count, buffer, offset);
            _length += count;
            _hashNeedsReset = true;

            // if (DigestTraceRecordHelper.ShouldTraceDigest)
            //    this.logStream.Write(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        public override void SetLength(long length)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        #region IDisposable members

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                //
                // Free all of our managed resources
                //

                if (_logStream != null)
                {
                    _logStream.Dispose();
                    _logStream = null;
                }
            }

            // Free native resources, if any.

            _disposed = true;
        }

        #endregion
    }
}
