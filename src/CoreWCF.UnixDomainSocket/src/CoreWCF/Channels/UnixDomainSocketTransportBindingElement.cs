// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    public class UnixDomainSocketTransportBindingElement : ConnectionOrientedTransportBindingElement
    {
        private int _listenBacklog;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;

        public UnixDomainSocketTransportBindingElement() : base()
        {
            _listenBacklog = UnixDomainTransportDefaults.GetListenBacklog();
            _extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
        }
        protected UnixDomainSocketTransportBindingElement(UnixDomainSocketTransportBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            _listenBacklog = elementToBeCloned._listenBacklog;
        }

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
            get { return "net.uds"; }
        }

        /// <summary>
        /// TODO : check the scheme
        /// </summary>
         protected override string WsdlTransportUri => "http://schemas.microsoft.com/soap/uds";

        public override BindingElement Clone()
        {
            return new UnixDomainSocketTransportBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            else if (typeof(T) == typeof(ITransportCompressionSupport))
            {
                return (T)(object)new TransportCompressionSupportHelper();
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
    }
}
