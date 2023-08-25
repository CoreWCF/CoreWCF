// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class WSHttpBinding : WSHttpBindingBase
    {
        private static readonly MessageSecurityVersion s_WSMessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;

        private WSHTTPSecurity _security = new WSHTTPSecurity();

        public WSHttpBinding() : base() { }

        public WSHttpBinding(SecurityMode securityMode) : this(securityMode, false) { }

        public WSHttpBinding(SecurityMode securityMode, bool reliableSessionEnabled) : base(reliableSessionEnabled)
        {
            _security.Mode = securityMode;
        }

        internal WSHttpBinding(WSHTTPSecurity security, bool reliableSessionEnabled) : base(reliableSessionEnabled)
        {
            _security = security ?? new WSHTTPSecurity();
        }

        public WSHTTPSecurity Security
        {
            get { return _security; }
            set
            {
                _security = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            return base.CreateBindingElements();
        }

        protected override TransportBindingElement GetTransport()
        {
            if (_security.Mode == SecurityMode.None || _security.Mode == SecurityMode.Message)
            {
                HttpTransport.ExtendedProtectionPolicy = _security.Transport.ExtendedProtectionPolicy;
                return HttpTransport;
            }
            else
            {
                _security.ApplyTransportSecurity(HttpsTransport);
                _security.ApplyAuthorizationPolicySupport(HttpsTransport);
                return HttpsTransport;
            }
        }

        protected override SecurityBindingElement CreateMessageSecurity()
        {
            return _security.CreateMessageSecurity(false, s_WSMessageSecurityVersion);
        }
    }
}
