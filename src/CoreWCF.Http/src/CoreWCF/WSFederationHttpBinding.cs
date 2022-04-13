// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;

namespace CoreWCF
{
    public class WSFederationHttpBinding : WSHttpBindingBase
    {
        private static readonly MessageSecurityVersion s_wSMessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
        private Uri _privacyNoticeAt;
        private int _privacyNoticeVersion;
        private WSFederationHttpSecurity _security = new WSFederationHttpSecurity();

        public WSFederationHttpBinding()
            : base()
        {
        }

        public WSFederationHttpBinding(WSFederationHttpSecurityMode securityMode)
            : this(securityMode, false)
        {
        }

        public WSFederationHttpBinding(WSFederationHttpSecurityMode securityMode, bool reliableSessionEnabled)
            : base(reliableSessionEnabled)
        {
            _security.Mode = securityMode;
        }


        internal WSFederationHttpBinding(WSFederationHttpSecurity security, PrivacyNoticeBindingElement privacy, bool reliableSessionEnabled)
            : base(reliableSessionEnabled)
        {
            _security = security;
            if (null != privacy)
            {
                _privacyNoticeAt = privacy.Url;
                _privacyNoticeVersion = privacy.Version;
            }
        }

        [DefaultValue(null)]
        public Uri PrivacyNoticeAt
        {
            get { return _privacyNoticeAt; }
            set { _privacyNoticeAt = value; }
        }

        [DefaultValue(0)]
        public int PrivacyNoticeVersion
        {
            get { return _privacyNoticeVersion; }
            set { _privacyNoticeVersion = value; }
        }

        public WSFederationHttpSecurity Security
        {
            get { return _security; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                _security = value;
            }
        }

        private PrivacyNoticeBindingElement CreatePrivacyPolicy()
        {
            PrivacyNoticeBindingElement privacy = null;

            if (PrivacyNoticeAt != null)
            {
                privacy = new PrivacyNoticeBindingElement();
                privacy.Url = PrivacyNoticeAt;
                privacy.Version = _privacyNoticeVersion;
            }

            return privacy;
        }

        protected override TransportBindingElement GetTransport()
        {
            if (_security.Mode == WSFederationHttpSecurityMode.None || _security.Mode == WSFederationHttpSecurityMode.Message)
            {
                return HttpTransport;
            }
            else
            {
                return HttpsTransport;
            }
        }

        internal static bool GetSecurityModeFromTransport(TransportBindingElement transport, HttpTransportSecurity transportSecurity, out WSFederationHttpSecurityMode mode)
        {
            mode = WSFederationHttpSecurityMode.None | WSFederationHttpSecurityMode.Message | WSFederationHttpSecurityMode.TransportWithMessageCredential;
            if (transport is HttpsTransportBindingElement)
            {
                mode = WSFederationHttpSecurityMode.TransportWithMessageCredential;
            }
            else if (transport is HttpTransportBindingElement)
            {
                mode = WSFederationHttpSecurityMode.None | WSFederationHttpSecurityMode.Message;
            }
            else
            {
                return false;
            }
            return true;
        }

        protected override SecurityBindingElement CreateMessageSecurity()
        {
           // return security.CreateMessageSecurity(this.ReliableSession.Enabled, WSMessageSecurityVersion);
           //TODO : For reliable session
            return _security.CreateMessageSecurity(false, s_wSMessageSecurityVersion);
        }

        public override BindingElementCollection CreateBindingElements()
        {   // return collection of BindingElements

            BindingElementCollection bindingElements = base.CreateBindingElements();
            // order of BindingElements is important

            PrivacyNoticeBindingElement privacy = CreatePrivacyPolicy();
            if (privacy != null)
            {
                // This must go first.
                bindingElements.Insert(0, privacy);
            }

            return bindingElements;
        }

    }
}
