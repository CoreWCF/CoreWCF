// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class BufferManagerOutputStream : BufferedOutputStream
    {
        private readonly string _quotaExceededString;

        public BufferManagerOutputStream(string quotaExceededString) : base()
        {
            _quotaExceededString = quotaExceededString;
        }

        public BufferManagerOutputStream(string quotaExceededString, int maxSize) : base(maxSize)
        {
            _quotaExceededString = quotaExceededString;
        }

        public BufferManagerOutputStream(string quotaExceededString, int initialSize, int maxSize, BufferManager bufferManager)
            : base(initialSize, maxSize, BufferManager.GetInternalBufferManager(bufferManager))
        {
            _quotaExceededString = quotaExceededString;
        }

        public void Init(int initialSize, int maxSizeQuota, BufferManager bufferManager)
        {
            Init(initialSize, maxSizeQuota, maxSizeQuota, bufferManager);
        }

        public void Init(int initialSize, int maxSizeQuota, int effectiveMaxSize, BufferManager bufferManager)
        {
            Reinitialize(initialSize, maxSizeQuota, effectiveMaxSize, BufferManager.GetInternalBufferManager(bufferManager));
        }

        protected override Exception CreateQuotaExceededException(int maxSizeQuota)
        {
            string excMsg = SR.Format(_quotaExceededString, maxSizeQuota);
            //if (TD.MaxSentMessageSizeExceededIsEnabled())
            //{
            //    TD.MaxSentMessageSizeExceeded(excMsg);
            //}
            return new QuotaExceededException(excMsg);
        }
    }
}
