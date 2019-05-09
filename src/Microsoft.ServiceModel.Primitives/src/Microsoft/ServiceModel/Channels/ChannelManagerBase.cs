using System;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class ChannelManagerBase : CommunicationObject, IDefaultCommunicationTimeouts
    {
        protected ChannelManagerBase()
        {
        }

        protected abstract TimeSpan DefaultReceiveTimeout { get; }
        protected abstract TimeSpan DefaultSendTimeout { get; }

        internal TimeSpan InternalReceiveTimeout
        {
            get { return DefaultReceiveTimeout; }
        }

        internal TimeSpan InternalSendTimeout
        {
            get { return DefaultSendTimeout; }
        }

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout
        {
            get { return DefaultCloseTimeout; }
        }

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout
        {
            get { return DefaultOpenTimeout; }
        }

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout
        {
            get { return DefaultReceiveTimeout; }
        }

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout
        {
            get { return DefaultSendTimeout; }
        }

        internal Exception CreateChannelTypeNotSupportedException(Type type)
        {
            return new ArgumentException(SR.Format(SR.ChannelTypeNotSupported, type), "TChannel");
        }
    }

}