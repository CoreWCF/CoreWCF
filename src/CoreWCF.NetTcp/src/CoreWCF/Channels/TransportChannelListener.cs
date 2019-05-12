using CoreWCF.Runtime;
using CoreWCF.Description;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    abstract class TransportChannelListener
        : ChannelListenerBase, ITransportFactorySettings
    {
        // Double-checked locking pattern requires volatile for read/write synchronization
        static volatile bool addressPrefixesInitialized = false;
        static volatile string exactGeneratedAddressPrefix;
        static volatile string strongWildcardGeneratedAddressPrefix;
        static volatile string weakWildcardGeneratedAddressPrefix;
        static object staticLock = new object();

        Uri baseUri;
        BufferManager bufferManager;
        HostNameComparisonMode hostNameComparisonMode;
        bool inheritBaseAddressSettings;
        bool manualAddressing;
        long maxBufferPoolSize;
        long maxReceivedMessageSize;
        MessageEncoderFactory messageEncoderFactory;
        MessageVersion messageVersion;
        Uri uri;
        string hostedVirtualPath;
        Action messageReceivedCallback;
        TransportManagerContainer transportManagerContainer;

        protected TransportChannelListener(TransportBindingElement bindingElement, BindingContext context)
            : this(bindingElement, context, TransportDefaults.GetDefaultMessageEncoderFactory())
        {
        }

        protected TransportChannelListener(TransportBindingElement bindingElement, BindingContext context,
            MessageEncoderFactory defaultMessageEncoderFactory)
            : this(bindingElement, context, defaultMessageEncoderFactory, TransportDefaults.HostNameComparisonMode)
        {
        }

        protected TransportChannelListener(TransportBindingElement bindingElement, BindingContext context,
            HostNameComparisonMode hostNameComparisonMode)
            : this(bindingElement, context, TransportDefaults.GetDefaultMessageEncoderFactory(), hostNameComparisonMode)
        {
        }

        protected TransportChannelListener(TransportBindingElement bindingElement, BindingContext context,
            MessageEncoderFactory defaultMessageEncoderFactory, HostNameComparisonMode hostNameComparisonMode)
            : base(context.Binding)
        {
            HostNameComparisonModeHelper.Validate(hostNameComparisonMode);
            this.hostNameComparisonMode = hostNameComparisonMode;
            manualAddressing = bindingElement.ManualAddressing;
            maxBufferPoolSize = bindingElement.MaxBufferPoolSize;
            maxReceivedMessageSize = bindingElement.MaxReceivedMessageSize;

            Collection<MessageEncodingBindingElement> messageEncoderBindingElements
                = context.BindingParameters.FindAll<MessageEncodingBindingElement>();

            if (messageEncoderBindingElements.Count > 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MultipleMebesInParameters));
            }
            else if (messageEncoderBindingElements.Count == 1)
            {
                messageEncoderFactory = messageEncoderBindingElements[0].CreateMessageEncoderFactory();
                context.BindingParameters.Remove<MessageEncodingBindingElement>();
            }
            else
            {
                messageEncoderFactory = defaultMessageEncoderFactory;
            }

            if (null != messageEncoderFactory)
                messageVersion = messageEncoderFactory.MessageVersion;
            else
                messageVersion = MessageVersion.None;

            if ((context.ListenUriMode == ListenUriMode.Unique) && (context.ListenUriBaseAddress == null))
            {
                UriBuilder uriBuilder = new UriBuilder(Scheme, DnsCache.MachineName);
                uriBuilder.Path = GeneratedAddressPrefix;
                context.ListenUriBaseAddress = uriBuilder.Uri;
            }

            UriHelper.ValidateBaseAddress(context.ListenUriBaseAddress, "baseAddress");
            if (context.ListenUriBaseAddress.Scheme != Scheme)
            {
                // URI schemes are case-insensitive, so try a case insensitive compare now
                if (string.Compare(context.ListenUriBaseAddress.Scheme, Scheme, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                        "context.ListenUriBaseAddress",
                        SR.Format(SR.InvalidUriScheme, context.ListenUriBaseAddress.Scheme, Scheme));
                }
            }

            Fx.Assert(context.ListenUriRelativeAddress != null, ""); // validated by BindingContext
            if (context.ListenUriMode == ListenUriMode.Explicit)
            {
                SetUri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
            }
            else // ListenUriMode.Unique:
            {
                string relativeAddress = context.ListenUriRelativeAddress;
                if (relativeAddress.Length > 0 && !relativeAddress.EndsWith("/", StringComparison.Ordinal))
                {
                    relativeAddress += "/";
                }

                SetUri(context.ListenUriBaseAddress, relativeAddress + Guid.NewGuid().ToString());
            }

            transportManagerContainer = new TransportManagerContainer(this);
        }

        internal Uri BaseUri
        {
            get
            {
                return baseUri;
            }
        }

        string GeneratedAddressPrefix
        {
            get
            {
                EnsureAddressPrefixesInitialized();

                // We use different address prefixes based on hostname comparison mode in order to avoid creating
                // starved reservations.  For example, if we register http://+:80/TLA/G1 and http://*:80/TLA/G1, the
                // latter will never receive any traffic.  We handle this case by instead using http://+:80/TLA/G1
                // and http://*:80/TLA/G2.
                switch (hostNameComparisonMode)
                {
                    case HostNameComparisonMode.Exact:
                        return exactGeneratedAddressPrefix;
                    case HostNameComparisonMode.StrongWildcard:
                        return strongWildcardGeneratedAddressPrefix;
                    case HostNameComparisonMode.WeakWildcard:
                        return weakWildcardGeneratedAddressPrefix;
                    default:
                        Fx.Assert("invalid HostnameComparisonMode value");
                        return null;
                }
            }
        }

        internal string HostedVirtualPath
        {
            get
            {
                return hostedVirtualPath;
            }
        }

        internal bool InheritBaseAddressSettings
        {
            get
            {
                return inheritBaseAddressSettings;
            }

            set
            {
                inheritBaseAddressSettings = value;
            }
        }

        public BufferManager BufferManager
        {
            get
            {
                return bufferManager;
            }
        }

        internal HostNameComparisonMode HostNameComparisonModeInternal
        {
            get
            {
                return hostNameComparisonMode;
            }
        }

        public bool ManualAddressing
        {
            get
            {
                return manualAddressing;
            }
        }

        public long MaxBufferPoolSize
        {
            get
            {
                return maxBufferPoolSize;
            }
        }

        public virtual long MaxReceivedMessageSize
        {
            get
            {
                return maxReceivedMessageSize;
            }
        }

        public MessageEncoderFactory MessageEncoderFactory
        {
            get
            {
                return messageEncoderFactory;
            }
        }

        public MessageVersion MessageVersion
        {
            get
            {
                return messageVersion;
            }
        }

        internal abstract UriPrefixTable<ITransportManagerRegistration> TransportManagerTable
        {
            get;
        }
        
        public abstract string Scheme { get; }

        public override Uri Uri
        {
            get
            {
                return uri;
            }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(MessageVersion))
            {
                return (T)(object)MessageVersion;
            }

            if (typeof(T) == typeof(FaultConverter))
            {
                if (null == MessageEncoderFactory)
                    return null;
                else
                    return MessageEncoderFactory.Encoder.GetProperty<T>();
            }

            if (typeof(T) == typeof(ITransportFactorySettings))
            {
                return (T)(object)this;
            }

            return base.GetProperty<T>();
        }

        internal bool IsScopeIdCompatible(HostNameComparisonMode hostNameComparisonMode, Uri uri)
        {
            if (this.hostNameComparisonMode != hostNameComparisonMode)
            {
                return false;
            }

            if (hostNameComparisonMode == HostNameComparisonMode.Exact && uri.HostNameType == UriHostNameType.IPv6)
            {
                // the hostname type of the channel listener MUST be IPv6 if we got here.
                // as this should have been enforced by UriPrefixTable.
                if (Uri.HostNameType != UriHostNameType.IPv6)
                {
                    return false;
                }

                IPAddress channelListenerIP = IPAddress.Parse(Uri.DnsSafeHost);
                IPAddress otherIP = IPAddress.Parse(uri.DnsSafeHost);

                if (channelListenerIP.ScopeId != otherIP.ScopeId)
                {
                    return false;
                }
            }

            return true;
        }

        internal virtual void ApplyHostedContext(string virtualPath, bool isMetadataListener)
        {
            // Save the original hosted virtual path.
            hostedVirtualPath = virtualPath;
        }

        static Uri AddSegment(Uri baseUri, Uri fullUri)
        {
            Uri result = null;
            if (baseUri.AbsolutePath.Length < fullUri.AbsolutePath.Length)
            {
                UriBuilder builder = new UriBuilder(baseUri);
                TcpChannelListener.FixIpv6Hostname(builder, baseUri);
                if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    builder.Path = builder.Path + "/";
                    baseUri = builder.Uri;
                }
                Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
                string relativePath = relativeUri.OriginalString;
                int slashIndex = relativePath.IndexOf('/');
                string segment = (slashIndex == -1) ? relativePath : relativePath.Substring(0, slashIndex);
                builder.Path = builder.Path + segment;
                result = builder.Uri;
            }
            return result;
        }

        internal virtual ITransportManagerRegistration CreateTransportManagerRegistration()
        {
            return CreateTransportManagerRegistration(BaseUri);
        }

        internal abstract ITransportManagerRegistration CreateTransportManagerRegistration(Uri listenUri);

        static void EnsureAddressPrefixesInitialized()
        {
            if (!addressPrefixesInitialized)
            {
                lock (staticLock)
                {
                    if (!addressPrefixesInitialized)
                    {
                        // we use the ephemeral namespace prefix plus a GUID for our App-Domain (which is the
                        // extent to which we can share a TransportManager prefix)
                        exactGeneratedAddressPrefix = "Temporary_Listen_Addresses/" + Guid.NewGuid().ToString();
                        strongWildcardGeneratedAddressPrefix = "Temporary_Listen_Addresses/" + Guid.NewGuid().ToString();
                        weakWildcardGeneratedAddressPrefix = "Temporary_Listen_Addresses/" + Guid.NewGuid().ToString();
                        addressPrefixesInitialized = true;
                    }
                }
            }
        }

        internal virtual int GetMaxBufferSize()
        {
            if (MaxReceivedMessageSize > int.MaxValue)
                return int.MaxValue;
            else
                return (int)MaxReceivedMessageSize;
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            bufferManager = BufferManager.CreateBufferManager(MaxBufferPoolSize, GetMaxBufferSize());
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return transportManagerContainer.OpenAsync(new SelectTransportManagersCallback(SelectTransportManagers));
        }

        protected override void OnOpened()
        {
            base.OnOpened();
        }

        internal TransportManagerContainer GetTransportManagers()
        {
            return TransportManagerContainer.TransferTransportManagers(transportManagerContainer);
        }

        protected override void OnAbort()
        {
            transportManagerContainer.Abort();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return transportManagerContainer.CloseAsync(token);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            if (bufferManager != null)
            {
                bufferManager.Clear();
            }
        }

        bool TryGetTransportManagerRegistration(out ITransportManagerRegistration registration)
        {
            if (!InheritBaseAddressSettings)
            {
                return TryGetTransportManagerRegistration(hostNameComparisonMode, out registration);
            }

            if (TryGetTransportManagerRegistration(HostNameComparisonMode.StrongWildcard, out registration))
            {
                return true;
            }

            if (TryGetTransportManagerRegistration(HostNameComparisonMode.Exact, out registration))
            {
                return true;
            }

            if (TryGetTransportManagerRegistration(HostNameComparisonMode.WeakWildcard, out registration))
            {
                return true;
            }

            registration = null;
            return false;
        }

        protected virtual bool TryGetTransportManagerRegistration(HostNameComparisonMode hostNameComparisonMode,
            out ITransportManagerRegistration registration)
        {
            return TransportManagerTable.TryLookupUri(Uri, hostNameComparisonMode, out registration);
        }

        // This is virtual so that PeerChannelListener and MsmqChannelListener can override it.
        // Will be called under "lock (this.TransportManagerTable)" from TransportManagerContainer.Open
        internal virtual IList<TransportManager> SelectTransportManagers()
        {
            IList<TransportManager> foundTransportManagers = null;

            // Look up an existing transport manager registration.
            ITransportManagerRegistration registration;
            if (!TryGetTransportManagerRegistration(out registration))
            {
                // Don't create TransportManagerRegistration in hosted case.
                if (HostedVirtualPath == null)
                {
                    // Create a new registration at the default point in the URI hierarchy.
                    registration = CreateTransportManagerRegistration();
                    TransportManagerTable.RegisterUri(registration.ListenUri, hostNameComparisonMode, registration);
                }
            }

            // Use the registration to select/create a set of compatible transport managers.
            if (registration != null)
            {
                foundTransportManagers = registration.Select(this);
                if (foundTransportManagers == null)
                {
                    // Don't create TransportManagerRegistration in hosted case.
                    if (HostedVirtualPath == null)
                    {
                        // Create a new registration one segment down from the existing incompatible registration.
                        Uri nextUri = AddSegment(registration.ListenUri, Uri);
                        if (nextUri != null)
                        {
                            registration = CreateTransportManagerRegistration(nextUri);
                            TransportManagerTable.RegisterUri(nextUri, hostNameComparisonMode, registration);
                            foundTransportManagers = registration.Select(this);
                        }
                    }
                }
            }

            if (foundTransportManagers == null)
            {
                ThrowTransportManagersNotFound();
            }

            return foundTransportManagers;
        }

        void ThrowTransportManagersNotFound()
        {
            if (HostedVirtualPath != null)
            {
                if ((string.Compare(Uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) == 0) ||
                    (string.Compare(Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) == 0)
                    )
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(
                            SR.Format(SR.Hosting_NoHttpTransportManagerForUri, Uri)));
                }
                else if ((string.Compare(Uri.Scheme, Uri.UriSchemeNetTcp, StringComparison.OrdinalIgnoreCase) == 0) ||
                         (string.Compare(Uri.Scheme, Uri.UriSchemeNetPipe, StringComparison.OrdinalIgnoreCase) == 0)
                         )
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(SR.Format(
                            SR.Hosting_NoTcpPipeTransportManagerForUri, Uri)));
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.NoCompatibleTransportManagerForUri, Uri)));
        }

        protected void SetUri(Uri baseAddress, string relativeAddress)
        {
            Uri fullUri = baseAddress;

            // Ensure that baseAddress Path does end with a slash if we have a relative address
            if (relativeAddress != string.Empty)
            {
                if (!baseAddress.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
                {
                    UriBuilder uriBuilder = new UriBuilder(baseAddress);
                    TcpChannelListener.FixIpv6Hostname(uriBuilder, baseAddress);
                    uriBuilder.Path = uriBuilder.Path + "/";
                    baseAddress = uriBuilder.Uri;
                }

                fullUri = new Uri(baseAddress, relativeAddress);

                // now see if we need to update our base address (for cases like relative path = "/foo")
                if (!baseAddress.IsBaseOf(fullUri))
                {
                    baseAddress = fullUri;
                }
            }

            baseUri = baseAddress;
            ValidateUri(fullUri);
            uri = fullUri;
        }

        protected virtual void ValidateUri(Uri uri)
        {
        }

        long ITransportFactorySettings.MaxReceivedMessageSize
        {
            get { return MaxReceivedMessageSize; }
        }

        BufferManager ITransportFactorySettings.BufferManager
        {
            get { return BufferManager; }
        }

        bool ITransportFactorySettings.ManualAddressing
        {
            get { return ManualAddressing; }
        }

        MessageEncoderFactory ITransportFactorySettings.MessageEncoderFactory
        {
            get { return MessageEncoderFactory; }
        }

        public IAnonymousUriPrefixMatcher AnonymousUriPrefixMatcher => throw new NotImplementedException();

        internal void SetMessageReceivedCallback(Action messageReceivedCallback)
        {
            this.messageReceivedCallback = messageReceivedCallback;
        }

        internal void RaiseMessageReceived()
        {
            Action callback = messageReceivedCallback;
            if (callback != null)
            {
                callback();
            }
        }
    }

    interface ITransportManagerRegistration
    {
        HostNameComparisonMode HostNameComparisonMode { get; }
        Uri ListenUri { get; }
        IList<TransportManager> Select(TransportChannelListener factory);
    }

    abstract class TransportManagerRegistration : ITransportManagerRegistration
    {
        HostNameComparisonMode hostNameComparisonMode;
        Uri listenUri;

        protected TransportManagerRegistration(Uri listenUri, HostNameComparisonMode hostNameComparisonMode)
        {
            this.listenUri = listenUri;
            this.hostNameComparisonMode = hostNameComparisonMode;
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get { return hostNameComparisonMode; }
        }

        public Uri ListenUri
        {
            get
            {
                return listenUri;
            }
        }

        public abstract IList<TransportManager> Select(TransportChannelListener factory);
    }
}
