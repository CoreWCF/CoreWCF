// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;

namespace CoreWCF.IdentityModel
{
    internal sealed class HashStream : Stream
    {
        private HashAlgorithm hash;
        private long length;
        private bool disposed;
        private bool hashNeedsReset;
        private MemoryStream logStream;

        /// <summary>
        /// Constructor for HashStream. The HashAlgorithm instance is owned by the caller.
        /// </summary>
        public HashStream(HashAlgorithm hash)
        {
            if (hash == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("hash");

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

        public HashAlgorithm Hash
        {
            get { return this.hash; }
        }

        public override long Length
        {
            get { return this.length; }
        }

        public override long Position
        {
            get { return this.length; }
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

            this.hash.TransformFinalBlock(CryptoHelper.EmptyBuffer, 0, 0);
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
            return this.hash.Hash;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }

        public void Reset()
        {
            if (this.hashNeedsReset)
            {
                this.hash.Initialize();
                this.hashNeedsReset = false;
            }
            this.length = 0;

            // if (DigestTraceRecordHelper.ShouldTraceDigest)
            //     this.logStream = new MemoryStream();

        }

        public void Reset(HashAlgorithm hash)
        {
            this.hash = hash;
            this.hashNeedsReset = false;
            this.length = 0;

            //  if (DigestTraceRecordHelper.ShouldTraceDigest)
            //     this.logStream = new MemoryStream();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.hash.TransformBlock(buffer, offset, count, buffer, offset);
            this.length += count;
            this.hashNeedsReset = true;

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

            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                //
                // Free all of our managed resources
                //

                if (this.logStream != null)
                {
                    this.logStream.Dispose();
                    this.logStream = null;
                }
            }

            // Free native resources, if any.

            this.disposed = true;
        }

        #endregion
    }
}
