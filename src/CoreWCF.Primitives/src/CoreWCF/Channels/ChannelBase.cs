using System;
using CoreWCF.Diagnostics;

namespace CoreWCF.Channels
{
    public abstract class ChannelBase : CommunicationObject, IChannel, IDefaultCommunicationTimeouts
    {
        ChannelManagerBase channelManager;

        protected ChannelBase(ChannelManagerBase channelManager)
        {
            if (channelManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelManager));
            }

            this.channelManager = channelManager;
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

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return ((IDefaultCommunicationTimeouts)channelManager).CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return ((IDefaultCommunicationTimeouts)channelManager).OpenTimeout; }
        }

        protected TimeSpan DefaultReceiveTimeout
        {
            get { return ((IDefaultCommunicationTimeouts)channelManager).ReceiveTimeout; }
        }

        protected TimeSpan DefaultSendTimeout
        {
            get { return ((IDefaultCommunicationTimeouts)channelManager).SendTimeout; }
        }

        protected ChannelManagerBase Manager
        {
            get
            {
                return channelManager;
            }
        }

        public virtual T GetProperty<T>() where T : class
        {
            return null;
        }

        protected override void OnClosed()
        {
            base.OnClosed();
        }
    }

}