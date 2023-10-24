// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed class UnixDomainSocketSecurity
    {
        private static UnixDomainSocketSecurityMode s_defaultMode =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                UnixDomainSocketSecurityMode.Transport : UnixDomainSocketSecurityMode.TransportCredentialOnly;


        private UnixDomainSocketSecurityMode _mode;

        public UnixDomainSocketSecurity()
        {
            _mode = s_defaultMode;
            Transport = new UnixDomainSocketTransportSecurity();
        }

        private UnixDomainSocketSecurity(UnixDomainSocketSecurityMode mode, UnixDomainSocketTransportSecurity transportSecurity)
        {
            Fx.Assert(UnixDomainSocketSecurityModeHelper.IsDefined(mode),
                            string.Format("Invalid SecurityMode value: {0} = {1} (default is {2} = {3}).",
                                            (int)mode,
                                            mode.ToString(),
                                            (int)s_defaultMode,
                                            s_defaultMode.ToString()));

            _mode = mode;
            Transport = transportSecurity ?? new UnixDomainSocketTransportSecurity();
        }


        public UnixDomainSocketSecurityMode Mode
        {
            get { return _mode; }
            set
            {
                if (!UnixDomainSocketSecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _mode = value;
            }
        }

        public UnixDomainSocketTransportSecurity Transport { get; set; }

        internal BindingElement CreateTransportSecurity()
        {
            if (_mode == UnixDomainSocketSecurityMode.Transport || _mode == UnixDomainSocketSecurityMode.TransportCredentialOnly)
            {
                if ((_mode == UnixDomainSocketSecurityMode.TransportCredentialOnly && Transport.ClientCredentialType != UnixDomainSocketClientCredentialType.PosixIdentity)
                    ||
                    (_mode == UnixDomainSocketSecurityMode.Transport && Transport.ClientCredentialType == UnixDomainSocketClientCredentialType.PosixIdentity))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", _mode)));
                }
                return Transport.CreateTransportProtectionAndAuthentication();
            }
            else if (_mode == UnixDomainSocketSecurityMode.None)
            {
                return null;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

    }
}
