// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class BufferManagerOutputStream : BufferedOutputStream
    {
        private string quotaExceededString;

        public BufferManagerOutputStream(string quotaExceededString)
            : base()
        {
            this.quotaExceededString = quotaExceededString;
        }

        public BufferManagerOutputStream(string quotaExceededString, int maxSize)
            : base(maxSize)
        {
            this.quotaExceededString = quotaExceededString;
        }

        public BufferManagerOutputStream(string quotaExceededString, int initialSize, int maxSize, BufferManager bufferManager)
            : base(initialSize, maxSize, BufferManager.GetInternalBufferManager(bufferManager))
        {
            this.quotaExceededString = quotaExceededString;
        }

        public void Init(int initialSize, int maxSizeQuota, BufferManager bufferManager)
        {
            Init(initialSize, maxSizeQuota, maxSizeQuota, bufferManager);
        }

        public void Init(int initialSize, int maxSizeQuota, int effectiveMaxSize, BufferManager bufferManager)
        {
            base.Reinitialize(initialSize, maxSizeQuota, effectiveMaxSize, BufferManager.GetInternalBufferManager(bufferManager));
        }

        protected override Exception CreateQuotaExceededException(int maxSizeQuota)
        {
            string excMsg = SR.Format(quotaExceededString, maxSizeQuota);
            //if (TD.MaxSentMessageSizeExceededIsEnabled())
            //{
            //    TD.MaxSentMessageSizeExceeded(excMsg);
            //}
            return new QuotaExceededException(excMsg);
        }
    }
}