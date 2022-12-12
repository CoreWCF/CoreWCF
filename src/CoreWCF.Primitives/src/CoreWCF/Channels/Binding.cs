// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class Binding : IDefaultCommunicationTimeouts
    {
        private TimeSpan _closeTimeout = ServiceDefaults.CloseTimeout;
        private string _name;
        private string _namespaceIdentifier;
        private TimeSpan _openTimeout = ServiceDefaults.OpenTimeout;
        private TimeSpan _receiveTimeout = ServiceDefaults.ReceiveTimeout;
        private TimeSpan _sendTimeout = ServiceDefaults.SendTimeout;
        internal const string DefaultNamespace = NamingHelper.DefaultNamespace;

        protected Binding()
        {
            _name = null;
            _namespaceIdentifier = DefaultNamespace;
        }

        protected Binding(string name, string ns)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(name), SR.SFXBindingNameCannotBeNullOrEmpty);
            }
            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            if (ns.Length > 0)
            {
                NamingHelper.CheckUriParameter(ns, nameof(ns));
            }

            _name = name;
            _namespaceIdentifier = ns;
        }

        public TimeSpan CloseTimeout
        {
            get { return _closeTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _closeTimeout = value;
            }
        }

        public string Name
        {
            get
            {
                if (_name == null)
                {
                    _name = GetType().Name;
                }

                return _name;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.SFXBindingNameCannotBeNullOrEmpty);
                }

                _name = value;
            }
        }

        public string Namespace
        {
            get { return _namespaceIdentifier; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.Length > 0)
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }
                _namespaceIdentifier = value;
            }
        }

        public TimeSpan OpenTimeout
        {
            get { return _openTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _openTimeout = value;
            }
        }

        public TimeSpan ReceiveTimeout
        {
            get { return _receiveTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _receiveTimeout = value;
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
            get { return _sendTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRange0));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _sendTimeout = value;
            }
        }

        private void ValidateSecurityCapabilities(ISecurityCapabilities runtimeSecurityCapabilities, BindingParameterCollection parameters)
        {
            ISecurityCapabilities bindingSecurityCapabilities = GetProperty<ISecurityCapabilities>(parameters);

            if (!SecurityCapabilities.IsEqual(bindingSecurityCapabilities, runtimeSecurityCapabilities))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.SecurityCapabilitiesMismatched, this)));
            }
        }

        public virtual bool CanBuildServiceDispatcher<TChannel>(BindingParameterCollection parameters) where TChannel : class, IChannel
        {
            BindingContext context = new BindingContext(new CustomBinding(this), parameters);
            return context.CanBuildNextServiceDispatcher<TChannel>();
        }

        public virtual IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingParameterCollection parameters, IServiceDispatcher dispatcher)
where TChannel : class, IChannel
        {
            UriBuilder listenUriBuilder = new UriBuilder(Scheme, DnsCache.MachineName);
            return BuildServiceDispatcher<TChannel>(listenUriBuilder.Uri, string.Empty, parameters, dispatcher);
        }

        public virtual IServiceDispatcher BuildServiceDispatcher<TChannel>(Uri listenUriBaseAddress, BindingParameterCollection parameters, IServiceDispatcher dispatcher)
where TChannel : class, IChannel
        {
            return BuildServiceDispatcher<TChannel>(listenUriBaseAddress, string.Empty, parameters, dispatcher);
        }

        public virtual IServiceDispatcher BuildServiceDispatcher<TChannel>(Uri listenUriBaseAddress, string listenUriRelativeAddress, BindingParameterCollection parameters, IServiceDispatcher dispatcher)
            where TChannel : class, IChannel
        {
            EnsureInvariants();
            if (!(this is CustomBinding binding))
            {
                binding = new CustomBinding(this);
            }

            BindingContext context = new BindingContext(binding, parameters, listenUriBaseAddress, listenUriRelativeAddress);
            IServiceDispatcher serviceDispatcher = context.BuildNextServiceDispatcher<TChannel>(dispatcher);
            context.ValidateBindingElementsConsumed();

            // TODO: Work out how to validate security capabilities
            //this.ValidateSecurityCapabilities(serviceDispatcher.GetProperty<ISecurityCapabilities>(), parameters);

            return serviceDispatcher;
        }

        public abstract BindingElementCollection CreateBindingElements();

        public T GetProperty<T>(BindingParameterCollection parameters) where T : class
        {
            BindingContext context = new BindingContext(new CustomBinding(this), parameters);
            return context.GetInnerProperty<T>();
        }

        private void EnsureInvariants()
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
                {
                    break;
                }
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