// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed partial class UnixDomainSocketSecurity
    {
        internal const SecurityMode DefaultMode = SecurityMode.Transport;
        private SecurityMode _mode;

        public UnixDomainSocketSecurity()
            : this(DefaultMode, new UnixDomainSocketTransportSecurity())//, new MessageSecurityOverTcp())
        {
        }

        private UnixDomainSocketSecurity(SecurityMode mode, UnixDomainSocketTransportSecurity transportSecurity)
        {
            Fx.Assert(SecurityModeHelper.IsDefined(mode), string.Format("Invalid SecurityMode value: {0}.", mode.ToString()));

            _mode = mode;
            Transport = transportSecurity ?? new UnixDomainSocketTransportSecurity();
        }

        [DefaultValue(DefaultMode)]
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

        public UnixDomainSocketTransportSecurity Transport { get; set; }

        internal BindingElement CreateTransportSecurity()
        {
            if (_mode == SecurityMode.Transport)
            {
                return Transport.CreateTransportProtectionAndAuthentication();
            }
            else if(_mode == SecurityMode.None)
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
