using System;
using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Channels
{
    class BufferManagerOutputStream : BufferedOutputStream
    {
        string quotaExceededString;

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