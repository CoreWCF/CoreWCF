using System;
using CoreWCF.Channels;
using System.ComponentModel;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed class BasicHttpSecurity
    {
        internal const BasicHttpSecurityMode DefaultMode = BasicHttpSecurityMode.None;
        BasicHttpSecurityMode _mode;
        HttpTransportSecurity _transportSecurity;

        public BasicHttpSecurity()
            : this(DefaultMode, new HttpTransportSecurity())
        {
        }

        BasicHttpSecurity(BasicHttpSecurityMode mode, HttpTransportSecurity transportSecurity)
        {
            Fx.Assert(BasicHttpSecurityModeHelper.IsDefined(mode), string.Format("Invalid BasicHttpSecurityMode value: {0}.", mode.ToString()));
            if (mode == BasicHttpSecurityMode.Message || mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                throw new PlatformNotSupportedException($"{nameof(BasicHttpSecurityMode.Message)}, {nameof(BasicHttpSecurityMode.TransportWithMessageCredential)}");
            }

            Mode = mode;
            _transportSecurity = transportSecurity ?? new HttpTransportSecurity();
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

        internal void EnableTransportSecurity(HttpsTransportBindingElement https)
        {
            if (_mode == BasicHttpSecurityMode.TransportWithMessageCredential)
            {
                throw new PlatformNotSupportedException(nameof(BasicHttpSecurityMode.TransportWithMessageCredential));
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
