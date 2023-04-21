// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using System.ComponentModel;
using System;
using System.Runtime.Versioning;

namespace CoreWCF
{
    [SupportedOSPlatform("windows")]
    public sealed class NetNamedPipeSecurity
    {
        internal const NetNamedPipeSecurityMode DefaultMode = NetNamedPipeSecurityMode.Transport;
        private NetNamedPipeSecurityMode _mode;
        private NamedPipeTransportSecurity _transport = new NamedPipeTransportSecurity();

        public NetNamedPipeSecurity()
        {
            _mode = DefaultMode;
        }

        private NetNamedPipeSecurity(NetNamedPipeSecurityMode mode, NamedPipeTransportSecurity transport)
        {
            _mode = mode;
            _transport = transport == null ? new NamedPipeTransportSecurity() : transport;
        }

        [DefaultValue(DefaultMode)]
        public NetNamedPipeSecurityMode Mode
        {
            get { return _mode; }
            set
            {
                if (!NetNamedPipeSecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                _mode = value;
            }
        }

        public NamedPipeTransportSecurity Transport
        {
            get { return _transport; }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                _transport = value;
            }
        }

        internal WindowsStreamSecurityBindingElement CreateTransportSecurity()
        {
            if (_mode == NetNamedPipeSecurityMode.Transport)
            {
                return _transport.CreateTransportProtectionAndAuthentication();
            }
            else
            {
                return null;
            }
        }
    }
}
