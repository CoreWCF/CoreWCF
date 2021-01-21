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

        private TcpClientCredentialType clientCredentialType;
        private ProtectionLevel protectionLevel;
        private ExtendedProtectionPolicy extendedProtectionPolicy;
        SslProtocols sslProtocols;

        public TcpTransportSecurity()
        {
            clientCredentialType = DefaultClientCredentialType;
            protectionLevel = DefaultProtectionLevel;
            extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
            sslProtocols = TransportDefaults.SslProtocols;
        }

        [DefaultValue(DefaultClientCredentialType)]
        public TcpClientCredentialType ClientCredentialType
        {
            get { return clientCredentialType; }
            set
            {
                if (!TcpClientCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                clientCredentialType = value;
            }
        }

        [DefaultValue(DefaultProtectionLevel)]
        public ProtectionLevel ProtectionLevel
        {
            get { return protectionLevel; }
            set
            {
                if (!ProtectionLevelHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                protectionLevel = value;
            }
        }

        public ExtendedProtectionPolicy ExtendedProtectionPolicy
        {
            get
            {
                return extendedProtectionPolicy;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.PolicyEnforcement == PolicyEnforcement.Always &&
                    !System.Security.Authentication.ExtendedProtection.ExtendedProtectionPolicy.OSSupportsExtendedProtection)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new PlatformNotSupportedException(SR.ExtendedProtectionNotSupported));
                }
                extendedProtectionPolicy = value;
            }
        }

        [DefaultValue(TransportDefaults.SslProtocols)]
        public SslProtocols SslProtocols
        {
            get { return sslProtocols; }
            set
            {
                SslProtocolsHelper.Validate(value);
                sslProtocols = value;
            }
        }

        SslStreamSecurityBindingElement CreateSslBindingElement(bool requireClientCertificate)
        {
            if (protectionLevel != ProtectionLevel.EncryptAndSign)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.UnsupportedSslProtectionLevel, protectionLevel)));
            }

            SslStreamSecurityBindingElement result = new SslStreamSecurityBindingElement();
            result.RequireClientCertificate = requireClientCertificate;
            result.SslProtocols = sslProtocols;
            return result;
        }

        internal BindingElement CreateTransportProtectionAndAuthentication()
        {
            if (clientCredentialType == TcpClientCredentialType.Certificate || clientCredentialType == TcpClientCredentialType.None)
            {
                return CreateSslBindingElement(clientCredentialType == TcpClientCredentialType.Certificate);
            }
            else
            {
                WindowsStreamSecurityBindingElement result = new WindowsStreamSecurityBindingElement();
                result.ProtectionLevel = this.protectionLevel;
                return result;
            }
        }
    }
}