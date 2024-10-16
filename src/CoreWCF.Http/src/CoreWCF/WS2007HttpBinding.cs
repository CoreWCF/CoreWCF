// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF
{
    public class WS2007HttpBinding : WSHttpBinding
    {
        private static readonly MessageSecurityVersion s_WS2007MessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10;

        public WS2007HttpBinding()
            : base()
        {
            HttpsTransport.MessageSecurityVersion = s_WS2007MessageSecurityVersion;
        }

        public WS2007HttpBinding(SecurityMode securityMode)
            : this(securityMode, reliableSessionEnabled: false)
        {
        }

        public WS2007HttpBinding(SecurityMode securityMode, bool reliableSessionEnabled)
            : base(securityMode, reliableSessionEnabled)
        {
            HttpsTransport.MessageSecurityVersion = s_WS2007MessageSecurityVersion;
        }

        internal WS2007HttpBinding(WSHTTPSecurity security, bool reliableSessionEnabled)
            : base(security, reliableSessionEnabled)
        {
            HttpsTransport.MessageSecurityVersion = s_WS2007MessageSecurityVersion;
        }

        public override BindingElementCollection CreateBindingElements()
        {
            return base.CreateBindingElements();
        }

        protected override TransportBindingElement GetTransport()
        {
            return base.GetTransport();
        }

        protected override SecurityBindingElement CreateMessageSecurity()
        {
            return this.Security.CreateMessageSecurity(isReliableSessionEnabled: false, s_WS2007MessageSecurityVersion);
        }
    }
}
