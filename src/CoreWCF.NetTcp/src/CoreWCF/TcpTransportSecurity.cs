// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.Channels;
using CoreWCF.Security;

namespace CoreWCF
{
    public sealed partial class TcpTransportSecurity
    {
        internal const TcpClientCredentialType DefaultClientCredentialType = TcpClientCredentialType.Windows;
        internal const ProtectionLevel DefaultProtectionLevel = ProtectionLevel.EncryptAndSign;

        private TcpClientCredentialType _clientCredentialType;
        private ProtectionLevel _protectionLevel;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;
        private SslProtocols _sslProtocols;

        public TcpTransportSecurity()
        {
            _clientCredentialType = DefaultClientCredentialType;
            _protectionLevel = DefaultProtectionLevel;
            _extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
            _sslProtocols = TransportDefaults.SslProtocols;
        }

        [DefaultValue(DefaultClientCredentialType)]
        public TcpClientCredentialType ClientCredentialType
        {
            get { return _clientCredentialType; }
            set
            {
                if (!TcpClientCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _clientCredentialType = value;
            }
        }

        [DefaultValue(DefaultProtectionLevel)]
        public ProtectionLevel ProtectionLevel
        {
            get { return _protectionLevel; }
            set
            {
                if (!ProtectionLevelHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _protectionLevel = value;
            }
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

        [DefaultValue(TransportDefaults.SslProtocols)]
        public SslProtocols SslProtocols
        {
            get { return _sslProtocols; }
            set
            {
                SslProtocolsHelper.Validate(value);
                _sslProtocols = value;
            }
        }

        private SslStreamSecurityBindingElement CreateSslBindingElement(bool requireClientCertificate)
        {
            if (_protectionLevel != ProtectionLevel.EncryptAndSign)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.UnsupportedSslProtectionLevel, _protectionLevel)));
            }

            SslStreamSecurityBindingElement result = new SslStreamSecurityBindingElement
            {
                RequireClientCertificate = requireClientCertificate,
                SslProtocols = _sslProtocols
            };
            return result;
        }

        internal BindingElement CreateTransportProtectionOnly()
        {
            return CreateSslBindingElement(false);
        }

        internal BindingElement CreateTransportProtectionAndAuthentication()
        {
            if (_clientCredentialType == TcpClientCredentialType.Certificate || _clientCredentialType == TcpClientCredentialType.None)
            {
                return CreateSslBindingElement(_clientCredentialType == TcpClientCredentialType.Certificate);
            }
            else
            {
                WindowsStreamSecurityBindingElement result = new WindowsStreamSecurityBindingElement
                {
                    ProtectionLevel = _protectionLevel
                };
                return result;
            }
        }
    }
}
