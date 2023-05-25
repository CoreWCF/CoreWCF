// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF.Channels;
using CoreWCF.Security;

namespace CoreWCF
{
    public sealed partial class UnixDomainSocketTransportSecurity
    {
        internal const UnixDomainSocketClientCredentialType DefaultClientCredentialType = UnixDomainSocketClientCredentialType.Default;
        internal const ProtectionLevel DefaultProtectionLevel = ProtectionLevel.EncryptAndSign;

        private UnixDomainSocketClientCredentialType _clientCredentialType;
        private ProtectionLevel _protectionLevel;
        private ExtendedProtectionPolicy _extendedProtectionPolicy;
        private SslProtocols _sslProtocols;

        public UnixDomainSocketTransportSecurity()
        {
            _clientCredentialType = DefaultClientCredentialType;
            _protectionLevel = DefaultProtectionLevel;
            _extendedProtectionPolicy = ChannelBindingUtility.DefaultPolicy;
            _sslProtocols = TransportDefaults.SslProtocols;
        }

        [DefaultValue(DefaultClientCredentialType)]
        public UnixDomainSocketClientCredentialType ClientCredentialType
        {
            get { return _clientCredentialType; }
            set
            {
                if (!UnixDomainSocketClientCredentialTypeHelper.IsDefined(value))
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

        internal BindingElement CreatePosixIdentityOnlyBinding()
        {
            return new UnixPosixIdentityBindingElement();
        }

        internal BindingElement CreateTransportProtectionAndAuthentication()
        {
            if (_clientCredentialType == UnixDomainSocketClientCredentialType.Default)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new WindowsStreamSecurityBindingElement
                    {
                        ProtectionLevel = _protectionLevel
                    };
                }
                else
                {
                    return CreatePosixIdentityOnlyBinding();
                }
            }else if (_clientCredentialType == UnixDomainSocketClientCredentialType.Certificate)
            {
                return CreateSslBindingElement(true);

            }else if(_clientCredentialType == UnixDomainSocketClientCredentialType.Windows)
            {
                return new WindowsStreamSecurityBindingElement
                {
                    ProtectionLevel = _protectionLevel
                };

            }else if (_clientCredentialType == UnixDomainSocketClientCredentialType.IdentityOnly)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new NotSupportedException();
                }
                return CreatePosixIdentityOnlyBinding();
            }
            else
            {
                return null;
            }
        }
    }
}
