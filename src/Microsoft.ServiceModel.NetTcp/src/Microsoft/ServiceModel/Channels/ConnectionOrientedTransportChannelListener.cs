using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    abstract class ConnectionOrientedTransportChannelListener
        : TransportChannelListener,
          IConnectionOrientedTransportFactorySettings,
          IConnectionOrientedListenerSettings
    {
        int connectionBufferSize;
        bool exposeConnectionProperty;
        TimeSpan channelInitializationTimeout;
        int maxBufferSize;
        int maxPendingConnections;
        TimeSpan maxOutputDelay;
        int maxPendingAccepts;
        TimeSpan idleTimeout;
        int maxPooledConnections;
        TransferMode transferMode;
        ISecurityCapabilities securityCapabilities;
        StreamUpgradeProvider upgrade;
        bool ownUpgrade;
        EndpointIdentity identity;

        protected ConnectionOrientedTransportChannelListener(ConnectionOrientedTransportBindingElement bindingElement,
            BindingContext context)
            : base(bindingElement, context, bindingElement.HostNameComparisonMode)
        {
            if (bindingElement.TransferMode == TransferMode.Buffered)
            {
                if (bindingElement.MaxReceivedMessageSize > int.MaxValue)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException("bindingElement.MaxReceivedMessageSize",
                        SR.MaxReceivedMessageSizeMustBeInIntegerRange));
                }

                if (bindingElement.MaxBufferSize != bindingElement.MaxReceivedMessageSize)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("bindingElement",
                        SR.MaxBufferSizeMustMatchMaxReceivedMessageSize);
                }
            }
            else
            {
                if (bindingElement.MaxBufferSize > bindingElement.MaxReceivedMessageSize)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("bindingElement",
                        SR.MaxBufferSizeMustNotExceedMaxReceivedMessageSize);
                }
            }


            connectionBufferSize = bindingElement.ConnectionBufferSize;
            exposeConnectionProperty = false; //bindingElement.ExposeConnectionProperty;
            InheritBaseAddressSettings = false; //bindingElement.InheritBaseAddressSettings;
            channelInitializationTimeout = bindingElement.ChannelInitializationTimeout;
            maxBufferSize = bindingElement.MaxBufferSize;
            maxPendingConnections = bindingElement.MaxPendingConnections;
            maxOutputDelay = bindingElement.MaxOutputDelay;
            maxPendingAccepts = bindingElement.MaxPendingAccepts;
            transferMode = bindingElement.TransferMode;

            Collection<StreamUpgradeBindingElement> upgradeBindingElements =
                context.BindingParameters.FindAll<StreamUpgradeBindingElement>();

            if (upgradeBindingElements.Count > 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MultipleStreamUpgradeProvidersInParameters));
            }
            else if ((upgradeBindingElements.Count == 1) && SupportsUpgrade(upgradeBindingElements[0]))
            {
                upgrade = upgradeBindingElements[0].BuildServerStreamUpgradeProvider(context);
                ownUpgrade = true;
                context.BindingParameters.Remove<StreamUpgradeBindingElement>();
                securityCapabilities = upgradeBindingElements[0].GetProperty<ISecurityCapabilities>(context);
            }
        }

        public int ConnectionBufferSize
        {
            get
            {
                return connectionBufferSize;
            }
        }

        public TimeSpan IdleTimeout
        {
            get { return idleTimeout; }
        }

        public int MaxPooledConnections
        {
            get { return maxPooledConnections; }
        }

        internal void SetIdleTimeout(TimeSpan idleTimeout)
        {
            this.idleTimeout = idleTimeout;
        }

        internal void InitializeMaxPooledConnections()
        {
            maxPooledConnections = ConnectionOrientedTransportDefaults.GetMaxConnections();
        }

        internal bool ExposeConnectionProperty
        {
            get { return exposeConnectionProperty; }
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get
            {
                return HostNameComparisonModeInternal;
            }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(EndpointIdentity))
            {
                return (T)(object)(identity);
            }
            else if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return (T)(object)securityCapabilities;
            }
            else
            {
                T result = base.GetProperty<T>();

                if (result == null && upgrade != null)
                {
                    result = upgrade.GetProperty<T>();
                }

                return result;
            }
        }

        public TimeSpan ChannelInitializationTimeout
        {
            get
            {
                return channelInitializationTimeout;
            }
        }

        public int MaxBufferSize
        {
            get
            {
                return maxBufferSize;
            }
        }

        public int MaxPendingConnections
        {
            get
            {
                return maxPendingConnections;
            }
        }

        public TimeSpan MaxOutputDelay
        {
            get
            {
                return maxOutputDelay;
            }
        }

        public int MaxPendingAccepts
        {
            get
            {
                return maxPendingAccepts;
            }
        }

        public StreamUpgradeProvider Upgrade
        {
            get
            {
                return upgrade;
            }
        }

        public TransferMode TransferMode
        {
            get
            {
                return transferMode;
            }
        }

        int IConnectionOrientedTransportFactorySettings.MaxBufferSize
        {
            get { return MaxBufferSize; }
        }

        TransferMode IConnectionOrientedTransportFactorySettings.TransferMode
        {
            get { return TransferMode; }
        }

        StreamUpgradeProvider IConnectionOrientedTransportFactorySettings.Upgrade
        {
            get { return Upgrade; }
        }

        internal override int GetMaxBufferSize()
        {
            return MaxBufferSize;
        }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            await base.OnOpenAsync(token);
            StreamUpgradeProvider localUpgrade = Upgrade;
            if (localUpgrade != null)
            {
                await localUpgrade.OpenAsync(token);
            }
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            StreamSecurityUpgradeProvider security = Upgrade as StreamSecurityUpgradeProvider;
            if (security != null)
            {
                identity = security.Identity;
            }
        }

        protected override void OnAbort()
        {
            StreamUpgradeProvider localUpgrade = GetUpgrade();
            if (localUpgrade != null)
            {
                localUpgrade.Abort();
            }
            base.OnAbort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            StreamUpgradeProvider localUpgrade = GetUpgrade();
            if (localUpgrade != null)
            {
                await localUpgrade.CloseAsync(token);
                await base.OnCloseAsync(token);
            }
            else
            {
                await base.OnCloseAsync(token);
            }
        }

        StreamUpgradeProvider GetUpgrade()
        {
            StreamUpgradeProvider result = null;

            lock (ThisLock)
            {
                if (ownUpgrade)
                {
                    result = upgrade;
                    ownUpgrade = false;
                }
            }

            return result;
        }

        protected override void ValidateUri(Uri uri)
        {
            base.ValidateUri(uri);
            int maxViaSize = ConnectionOrientedTransportDefaults.MaxViaSize;
            int encodedSize = Encoding.UTF8.GetByteCount(uri.AbsoluteUri);
            if (encodedSize > maxViaSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new QuotaExceededException(SR.Format(SR.UriLengthExceedsMaxSupportedSize, uri, encodedSize, maxViaSize)));
            }
        }

        protected virtual bool SupportsUpgrade(StreamUpgradeBindingElement upgradeBindingElement)
        {
            return true;
        }

        // transfers around the StreamUpgradeProvider from an ownership perspective
        protected class ConnectionOrientedTransportReplyChannelAcceptor : TransportReplyChannelAcceptor
        {
            StreamUpgradeProvider upgrade;

            public ConnectionOrientedTransportReplyChannelAcceptor(ConnectionOrientedTransportChannelListener listener)
                : base(listener)
            {
                upgrade = listener.GetUpgrade();
            }

            protected override ReplyChannel OnCreateChannel()
            {
                return new ConnectionOrientedTransportReplyChannel(ChannelManager, null);
            }

            protected override void OnAbort()
            {
                base.OnAbort();
                if (upgrade != null && !TransferUpgrade())
                {
                    upgrade.Abort();
                }
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                if (upgrade != null && !TransferUpgrade())
                {
                    return upgrade.CloseAsync(token);
                }

                return Task.CompletedTask;
            }

            // used to decouple our channel and listener lifetimes
            bool TransferUpgrade()
            {
                ConnectionOrientedTransportReplyChannel singletonChannel = (ConnectionOrientedTransportReplyChannel)base.GetCurrentChannel();
                if (singletonChannel == null)
                {
                    return false;
                }
                else
                {
                    return singletonChannel.TransferUpgrade(upgrade);
                }
            }

            // tracks StreamUpgradeProvider so that the channel can outlive the Listener
            class ConnectionOrientedTransportReplyChannel : TransportReplyChannel
            {
                StreamUpgradeProvider upgrade;

                public ConnectionOrientedTransportReplyChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
                    : base(channelManager, localAddress)
                {
                }

                public bool TransferUpgrade(StreamUpgradeProvider upgrade)
                {
                    lock (ThisLock)
                    {
                        if (State != CommunicationState.Opened)
                        {
                            return false;
                        }

                        this.upgrade = upgrade;
                        return true;
                    }
                }

                protected override void OnAbort()
                {
                    if (upgrade != null)
                    {
                        upgrade.Abort();
                    }
                    base.OnAbort();
                }

                protected override async Task OnCloseAsync(CancellationToken token)
                {
                    if (upgrade != null)
                    {
                        await upgrade.CloseAsync(token);
                    }

                    await base.OnCloseAsync(token);
                }
            }
        }
    }
}
