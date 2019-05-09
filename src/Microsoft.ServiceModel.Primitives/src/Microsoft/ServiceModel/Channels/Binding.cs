using System;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Description;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class Binding : IDefaultCommunicationTimeouts
    {
        TimeSpan closeTimeout = ServiceDefaults.CloseTimeout;
        string name;
        string namespaceIdentifier;
        TimeSpan openTimeout = ServiceDefaults.OpenTimeout;
        TimeSpan receiveTimeout = ServiceDefaults.ReceiveTimeout;
        TimeSpan sendTimeout = ServiceDefaults.SendTimeout;
        internal const string DefaultNamespace = NamingHelper.DefaultNamespace;

        protected Binding()
        {
            name = null;
            namespaceIdentifier = DefaultNamespace;
        }

        protected Binding(string name, string ns)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("name", SR.SFXBindingNameCannotBeNullOrEmpty);
            }
            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("ns");
            }

            if (ns.Length > 0)
            {
                NamingHelper.CheckUriParameter(ns, "ns");
            }

            this.name = name;
            namespaceIdentifier = ns;
        }

        public TimeSpan CloseTimeout
        {
            get { return closeTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRangeTooBig));
                }

                closeTimeout = value;
            }
        }

        public string Name
        {
            get
            {
                if (name == null)
                    name = GetType().Name;

                return name;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("value", SR.SFXBindingNameCannotBeNullOrEmpty);

                name = value;
            }
        }

        public string Namespace
        {
            get { return namespaceIdentifier; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }

                if (value.Length > 0)
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }
                namespaceIdentifier = value;
            }
        }

        public TimeSpan OpenTimeout
        {
            get { return openTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRangeTooBig));
                }

                openTimeout = value;
            }
        }

        public TimeSpan ReceiveTimeout
        {
            get { return receiveTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRangeTooBig));
                }

                receiveTimeout = value;
            }
        }

        public abstract string Scheme { get; }

        public MessageVersion MessageVersion
        {
            get
            {
                return GetProperty<MessageVersion>(new BindingParameterCollection());
            }
        }

        public TimeSpan SendTimeout
        {
            get { return sendTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SR.SFxTimeoutOutOfRangeTooBig));
                }

                sendTimeout = value;
            }
        }

        void ValidateSecurityCapabilities(ISecurityCapabilities runtimeSecurityCapabilities, BindingParameterCollection parameters)
        {
            ISecurityCapabilities bindingSecurityCapabilities = GetProperty<ISecurityCapabilities>(parameters);

            if (!SecurityCapabilities.IsEqual(bindingSecurityCapabilities, runtimeSecurityCapabilities))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.SecurityCapabilitiesMismatched, this)));
            }
        }

        public virtual IChannelListener<TChannel> BuildChannelListener<TChannel>(Uri listenUriBaseAddress, string listenUriRelativeAddress, ListenUriMode listenUriMode, BindingParameterCollection parameters)
            where TChannel : class, IChannel
        {
            EnsureInvariants();
            BindingContext context = new BindingContext(new CustomBinding(this), parameters, listenUriBaseAddress, listenUriRelativeAddress, listenUriMode);
            IChannelListener<TChannel> channelListener = context.BuildInnerChannelListener<TChannel>();
            context.ValidateBindingElementsConsumed();
            ValidateSecurityCapabilities(channelListener.GetProperty<ISecurityCapabilities>(), parameters);

            return channelListener;
        }

        internal bool CanBuildChannelListener<TChannel>(params object[] parameters) where TChannel : class, IChannel
        {
            return CanBuildChannelListener<TChannel>(new BindingParameterCollection(parameters));
        }

        public virtual bool CanBuildChannelListener<TChannel>(BindingParameterCollection parameters) where TChannel : class, IChannel
        {
            BindingContext context = new BindingContext(new CustomBinding(this), parameters);
            return context.CanBuildInnerChannelListener<TChannel>();
        }

        //public Microsoft.ServiceModel.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(params object[] parameters) { return default(Microsoft.ServiceModel.Channels.IChannelFactory<TChannel>); } // Client
        //public virtual Microsoft.ServiceModel.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(Microsoft.ServiceModel.Channels.BindingParameterCollection parameters) { return default(Microsoft.ServiceModel.Channels.IChannelFactory<TChannel>); } // Client
        //public bool CanBuildChannelFactory<TChannel>(params object[] parameters) { return default(bool); }
        //public virtual bool CanBuildChannelFactory<TChannel>(Microsoft.ServiceModel.Channels.BindingParameterCollection parameters) { return default(bool); }

        public abstract BindingElementCollection CreateBindingElements();

        public T GetProperty<T>(BindingParameterCollection parameters)
    where T : class
        {
            BindingContext context = new BindingContext(new CustomBinding(this), parameters);
            return context.GetInnerProperty<T>();
        }

        void EnsureInvariants()
        {
            EnsureInvariants(null);
        }

        internal void EnsureInvariants(string contractName)
        {
            BindingElementCollection elements = CreateBindingElements();
            TransportBindingElement transport = null;
            int index;
            for (index = 0; index < elements.Count; index++)
            {
                transport = elements[index] as TransportBindingElement;
                if (transport != null)
                    break;
            }

            if (transport == null)
            {
                if (contractName == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.CustomBindingRequiresTransport, Name)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.SFxCustomBindingNeedsTransport1, contractName)));
                }
            }
            if (index != elements.Count - 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.TransportBindingElementMustBeLast, Name, transport.GetType().Name)));
            }
            if (string.IsNullOrEmpty(transport.Scheme))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.InvalidBindingScheme, transport.GetType().Name)));
            }

            if (MessageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.MessageVersionMissingFromBinding, Name)));
            }
        }

    }
}