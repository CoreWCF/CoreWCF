// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed class BasicHttpSecurity
    {
        internal const BasicHttpSecurityMode DefaultMode = BasicHttpSecurityMode.None;
        private BasicHttpSecurityMode _mode;
        private HttpTransportSecurity _transportSecurity;
        private BasicHttpMessageSecurity _messageSecurity;

        public BasicHttpSecurity()
            : this(DefaultMode, new HttpTransportSecurity(), new BasicHttpMessageSecurity())
        {
        }

        private BasicHttpSecurity(BasicHttpSecurityMode mode, HttpTransportSecurity transportSecurity, BasicHttpMessageSecurity messageSecurity)
        {
            Fx.Assert(BasicHttpSecurityModeHelper.IsDefined(mode), string.Format("Invalid BasicHttpSecurityMode value: {0}.", mode.ToString()));
            if (mode == BasicHttpSecurityMode.Message ) // || mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                throw new PlatformNotSupportedException($"{nameof(BasicHttpSecurityMode.Message)}");
            }

            Mode = mode;
            _transportSecurity = transportSecurity ?? new HttpTransportSecurity();
            _messageSecurity = messageSecurity == null ? new BasicHttpMessageSecurity() : messageSecurity;
        }

        public BasicHttpSecurityMode Mode
        {
            get => _mode;
            set
            {
                if (!BasicHttpSecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _mode = value;
            }
        }

        public HttpTransportSecurity Transport
        {
            get => _transportSecurity;
            set => _transportSecurity = value ?? new HttpTransportSecurity();
        }

        public BasicHttpMessageSecurity Message
        {
            get => _messageSecurity;
            set => _messageSecurity = value ?? new BasicHttpMessageSecurity();
        }

        internal SecurityBindingElement CreateMessageSecurity()
        {
            if (_mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                return _messageSecurity.CreateMessageSecurity(Mode == BasicHttpSecurityMode.TransportWithMessageCredential);
            }
            else if(_mode == BasicHttpSecurityMode.Message)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", _mode)));
            }
            else
            {
                return null;
            }
        }

        internal void EnableTransportSecurity(HttpsTransportBindingElement https)
        {
            if (_mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                _transportSecurity.ConfigureTransportProtectionOnly(https);
            }
            else
            {
                _transportSecurity.ConfigureTransportProtectionAndAuthentication(https);
            }
        }

        internal void EnableTransportAuthentication(HttpTransportBindingElement http)
        {
            _transportSecurity.ConfigureTransportAuthentication(http);
        }

        internal void DisableTransportAuthentication(HttpTransportBindingElement http)
        {
            _transportSecurity.DisableTransportAuthentication(http);
        }
    }
}
