// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Security;
using System.ComponentModel;
using System.Net.Security;
using System;

namespace CoreWCF
{
    public sealed class NamedPipeTransportSecurity
    {
        internal const ProtectionLevel DefaultProtectionLevel = ProtectionLevel.EncryptAndSign;
        private ProtectionLevel _protectionLevel;

        public NamedPipeTransportSecurity()
        {
            _protectionLevel = DefaultProtectionLevel;
        }

        [DefaultValue(ConnectionOrientedTransportDefaults.ProtectionLevel)]
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

        internal WindowsStreamSecurityBindingElement CreateTransportProtectionAndAuthentication()
        {
            WindowsStreamSecurityBindingElement result = new WindowsStreamSecurityBindingElement();
            result.ProtectionLevel = _protectionLevel;
            return result;
        }
    }
}
