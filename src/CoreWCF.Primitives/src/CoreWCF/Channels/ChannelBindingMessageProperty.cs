using System;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Channels
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
            this.channelBinding = channelBinding ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelBinding));
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

        public static bool TryGet(Message message, out ChannelBindingMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            return TryGet(message.Properties, out property);
        }

        public static bool TryGet(MessageProperties properties, out ChannelBindingMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("properties");
            }

            property = null;
            object value;

            if (properties.TryGetValue(ChannelBindingMessageProperty.Name, out value))
            {
                property = value as ChannelBindingMessageProperty;
                return property != null;
            }

            return false;
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
