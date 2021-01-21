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
        private SecurityMode mode;
        private HttpTransportSecurity transportSecurity;
        private NonDualMessageSecurityOverHttp messageSecurity;

        public WSHTTPSecurity()
            : this(DefaultMode, GetDefaultHttpTransportSecurity(), new NonDualMessageSecurityOverHttp())
        {
        }

        internal WSHTTPSecurity(SecurityMode mode, HttpTransportSecurity transportSecurity, NonDualMessageSecurityOverHttp messageSecurity)
        {
            this.mode = mode;
            this.transportSecurity = transportSecurity == null ? GetDefaultHttpTransportSecurity() : transportSecurity;
            this.messageSecurity = messageSecurity == null ? new NonDualMessageSecurityOverHttp() : messageSecurity;
        }

        internal static HttpTransportSecurity GetDefaultHttpTransportSecurity()
        {
            HttpTransportSecurity transportSecurity = new HttpTransportSecurity();
            transportSecurity.ClientCredentialType = HttpClientCredentialType.Windows;
            return transportSecurity;
        }

        public SecurityMode Mode
        {
            get { return mode; }
            set
            {
                if (!SecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                mode = value;
            }
        }

        public HttpTransportSecurity Transport
        {
            get { return transportSecurity; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));
                }
                transportSecurity = value;
            }
        }

        public NonDualMessageSecurityOverHttp Message
        {
            get { return messageSecurity; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));
                }
                messageSecurity = value;
            }
        }

        internal void ApplyTransportSecurity(HttpsTransportBindingElement https)
        {
            if (mode == SecurityMode.TransportWithMessageCredential)
            {
                transportSecurity.ConfigureTransportProtectionOnly(https);
            }
            else
            {
                transportSecurity.ConfigureTransportProtectionAndAuthentication(https);
            }
        }

        internal static void ApplyTransportSecurity(HttpsTransportBindingElement transport, HttpTransportSecurity transportSecurity)
        {
            HttpTransportSecurity.ConfigureTransportProtectionAndAuthentication(transport, transportSecurity);
        }

        internal SecurityBindingElement CreateMessageSecurity(bool isReliableSessionEnabled, MessageSecurityVersion version)
        {
            if (mode == SecurityMode.Message || mode == SecurityMode.TransportWithMessageCredential)
            {
                return messageSecurity.CreateSecurityBindingElement(Mode == SecurityMode.TransportWithMessageCredential, isReliableSessionEnabled, version);
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

    }
}
