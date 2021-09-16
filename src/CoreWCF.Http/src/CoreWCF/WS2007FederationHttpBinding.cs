// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF
{
    public class WS2007FederationHttpBinding : WSFederationHttpBinding
    {
        // static readonly ReliableMessagingVersion WS2007ReliableMessagingVersion = ReliableMessagingVersion.WSReliableMessaging11;
        //static readonly TransactionProtocol WS2007TransactionProtocol = TransactionProtocol.WSAtomicTransaction11;
        private static readonly MessageSecurityVersion _ws2007MessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10;

        public WS2007FederationHttpBinding()
            : base()
        {
          //  this.ReliableSessionBindingElement.ReliableMessagingVersion = WS2007ReliableMessagingVersion;
           // this.TransactionFlowBindingElement.TransactionProtocol = WS2007TransactionProtocol;
            HttpsTransport.MessageSecurityVersion = _ws2007MessageSecurityVersion;
        }

        public WS2007FederationHttpBinding(WSFederationHttpSecurityMode securityMode)
            : this(securityMode, false)
        {
        }

        public WS2007FederationHttpBinding(WSFederationHttpSecurityMode securityMode, bool reliableSessionEnabled)
            : base(securityMode, reliableSessionEnabled)
        {
           // this.ReliableSessionBindingElement.ReliableMessagingVersion = WS2007ReliableMessagingVersion;
           // this.TransactionFlowBindingElement.TransactionProtocol = WS2007TransactionProtocol;
            HttpsTransport.MessageSecurityVersion = _ws2007MessageSecurityVersion;
        }

        private WS2007FederationHttpBinding(WSFederationHttpSecurity security, PrivacyNoticeBindingElement privacy, bool reliableSessionEnabled)
            : base(security, privacy, reliableSessionEnabled)
        {
          //  this.ReliableSessionBindingElement.ReliableMessagingVersion = WS2007ReliableMessagingVersion;
           // this.TransactionFlowBindingElement.TransactionProtocol = WS2007TransactionProtocol;
            HttpsTransport.MessageSecurityVersion = _ws2007MessageSecurityVersion;
        }

        protected override SecurityBindingElement CreateMessageSecurity()
        {
            return Security.CreateMessageSecurity(false, _ws2007MessageSecurityVersion);
        }

    }
}
