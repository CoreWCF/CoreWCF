using System;
using System.Security.Authentication.ExtendedProtection;

namespace Microsoft.ServiceModel.Channels
{
    public sealed class ChannelBindingMessageProperty : IDisposable, IMessageProperty
    {
        internal const string PropertyName = "ChannelBindingMessageProperty";

        ChannelBinding channelBinding;
        object thisLock;
        bool ownsCleanup;
        int refCount;

        public ChannelBindingMessageProperty(ChannelBinding channelBinding, bool ownsCleanup)
        {
            this.channelBinding = channelBinding ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("channelBinding");
            refCount = 1;
            thisLock = new object();
            this.ownsCleanup = ownsCleanup;
        }

        public static string Name { get { return PropertyName; } }

        bool IsDisposed
        {
            get
            {
                return refCount <= 0;
            }
        }

        public ChannelBinding ChannelBinding
        {
            get
            {
                ThrowIfDisposed();
                return channelBinding;
            }
        }

        IMessageProperty IMessageProperty.CreateCopy()
        {
            lock (thisLock)
            {
                ThrowIfDisposed();
                refCount++;
                return this;
            }
        }

        void IDisposable.Dispose()
        {
            if (!IsDisposed)
            {
                lock (thisLock)
                {
                    if (!IsDisposed && --refCount == 0)
                    {
                        if (ownsCleanup)
                        {
                            // Accessing via IDisposable to avoid Security check (functionally the same)
                            ((IDisposable)channelBinding).Dispose();
                        }
                    }
                }
            }
        }

        void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }
}
