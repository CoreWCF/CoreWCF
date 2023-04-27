// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    public class TcpTransportBindingElement : ConnectionOrientedTransportBindingElement
    {
        private int _listenBacklog;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;

        public TcpTransportBindingElement() : base()
        {
            _listenBacklog = TcpTransportDefaults.GetListenBacklog();
            ConnectionPoolSettings = new TcpConnectionPoolSettings();
            _extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
        }
        protected TcpTransportBindingElement(TcpTransportBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            _listenBacklog = elementToBeCloned._listenBacklog;
            ConnectionPoolSettings = elementToBeCloned.ConnectionPoolSettings.Clone();
            _extendedProtectionPolicy = elementToBeCloned.ExtendedProtectionPolicy;
        }

        public TcpConnectionPoolSettings ConnectionPoolSettings { get; }

        public int ListenBacklog
        {
            get
            {
                return _listenBacklog;
            }

            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SRCommon.ValueMustBePositive));
                }

                _listenBacklog = value;
            }
        }

        public override string Scheme
        {
            get { return "net.tcp"; }
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy
        {
            get
            {
                return _extendedProtectionPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.PolicyEnforcement == PolicyEnforcement.Always &&
                    !ExtendedProtectionPolicy.OSSupportsExtendedProtection)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new PlatformNotSupportedException(SR.ExtendedProtectionNotSupported));
                }

                _extendedProtectionPolicy = value;
            }
        }

        protected override string WsdlTransportUri => "http://schemas.microsoft.com/soap/tcp";

        public override BindingElement Clone()
        {
            return new TcpTransportBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(IBindingDeliveryCapabilities))
            {
                return (T)(object)new BindingDeliveryCapabilitiesHelper();
            }
            if (typeof(T) == typeof(ExtendedProtectionPolicy))
            {
                return (T)(object)ExtendedProtectionPolicy;
            }
            else if (typeof(T) == typeof(ITransportCompressionSupport))
            {
                return (T)(object)new TransportCompressionSupportHelper();
            }
            else if (typeof(T) == typeof(ConnectionPoolSettings))
            {
                return (T)(object)ConnectionPoolSettings;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            return innerDispatcher;
        }

        private class BindingDeliveryCapabilitiesHelper : IBindingDeliveryCapabilities
        {
            internal BindingDeliveryCapabilitiesHelper() { }
            bool IBindingDeliveryCapabilities.AssuresOrderedDelivery => true;
            bool IBindingDeliveryCapabilities.QueuedDelivery => false;
        }
    }
}
