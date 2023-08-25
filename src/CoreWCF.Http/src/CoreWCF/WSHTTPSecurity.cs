// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;

namespace CoreWCF
{
    public sealed class WSHTTPSecurity
    {
        internal const SecurityMode DefaultMode = SecurityMode.Message;
        private SecurityMode _mode;
        private HttpTransportSecurity _transportSecurity;
        private NonDualMessageSecurityOverHttp _messageSecurity;

        public WSHTTPSecurity()
            : this(DefaultMode, GetDefaultHttpTransportSecurity(), new NonDualMessageSecurityOverHttp())
        {
        }

        internal WSHTTPSecurity(SecurityMode mode, HttpTransportSecurity transportSecurity, NonDualMessageSecurityOverHttp messageSecurity)
        {
            _mode = mode;
            _transportSecurity = transportSecurity ?? GetDefaultHttpTransportSecurity();
            _messageSecurity = messageSecurity ?? new NonDualMessageSecurityOverHttp();
        }

        internal static HttpTransportSecurity GetDefaultHttpTransportSecurity()
        {
            HttpTransportSecurity transportSecurity = new HttpTransportSecurity
            {
                ClientCredentialType = HttpClientCredentialType.Windows
            };
            return transportSecurity;
        }

        public SecurityMode Mode
        {
            get { return _mode; }
            set
            {
                if (!SecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _mode = value;
            }
        }

        public HttpTransportSecurity Transport
        {
            get { return _transportSecurity; }
            set
            {
                _transportSecurity = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        public NonDualMessageSecurityOverHttp Message
        {
            get { return _messageSecurity; }
            set
            {
                _messageSecurity = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(value)));
            }
        }

        internal void ApplyTransportSecurity(HttpsTransportBindingElement https)
        {
            if (_mode == SecurityMode.TransportWithMessageCredential)
            {
                _transportSecurity.ConfigureTransportProtectionOnly(https);
            }
            else
            {
                _transportSecurity.ConfigureTransportProtectionAndAuthentication(https);
            }
        }

        internal static void ApplyTransportSecurity(HttpsTransportBindingElement transport, HttpTransportSecurity transportSecurity)
        {
            HttpTransportSecurity.ConfigureTransportProtectionAndAuthentication(transport, transportSecurity);
        }

        internal SecurityBindingElement CreateMessageSecurity(bool isReliableSessionEnabled, MessageSecurityVersion version)
        {
            if (_mode == SecurityMode.Message || _mode == SecurityMode.TransportWithMessageCredential)
            {
                return _messageSecurity.CreateSecurityBindingElement(Mode == SecurityMode.TransportWithMessageCredential, isReliableSessionEnabled, version);
            }
            else
            {
                return null;
            }
        }

        //internal static bool TryCreate(SecurityBindingElement sbe, UnifiedSecurityMode mode, HttpTransportSecurity transportSecurity, bool isReliableSessionEnabled, out WSHttpSecurity security)
        //{
        //    security = null;
        //    NonDualMessageSecurityOverHttp messageSecurity = null;
        //    SecurityMode securityMode = SecurityMode.None;
        //    if (sbe != null)
        //    {
        //        mode &= UnifiedSecurityMode.Message | UnifiedSecurityMode.TransportWithMessageCredential;
        //        securityMode = SecurityModeHelper.ToSecurityMode(mode);
        //        Fx.Assert(SecurityModeHelper.IsDefined(securityMode), string.Format("Invalid SecurityMode value: {0}.", mode.ToString()));
        //        if (!MessageSecurityOverHttp.TryCreate(sbe, securityMode == SecurityMode.TransportWithMessageCredential, isReliableSessionEnabled, out messageSecurity))
        //        {
        //            return false;
        //        }
        //    }
        //    else
        //    {
        //        mode &= ~(UnifiedSecurityMode.Message | UnifiedSecurityMode.TransportWithMessageCredential);
        //        securityMode = SecurityModeHelper.ToSecurityMode(mode);
        //    }
        //    Fx.Assert(SecurityModeHelper.IsDefined(securityMode), string.Format("Invalid SecurityMode value: {0}.", securityMode.ToString()));
        //    security = new WSHttpSecurity(securityMode, transportSecurity, messageSecurity);
        //    return true;
        //}

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeMode()
        {
            return Mode != DefaultMode;
        }

        internal void ApplyAuthorizationPolicySupport(HttpTransportBindingElement httpTransport)
        {
            httpTransport.AlwaysUseAuthorizationPolicySupport =
                Transport.AlwaysUseAuthorizationPolicySupport
                || Transport.ClientCredentialType == HttpClientCredentialType.InheritedFromHost;
        }
    }
}
