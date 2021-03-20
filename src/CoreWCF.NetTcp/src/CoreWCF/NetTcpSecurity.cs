// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF
{
    public sealed partial class NetTcpSecurity
    {
        internal const SecurityMode DefaultMode = SecurityMode.Transport;
        private SecurityMode _mode;
        MessageSecurityOverTcp _messageSecurity;

        public NetTcpSecurity()
            : this(DefaultMode, new TcpTransportSecurity(), new MessageSecurityOverTcp())
        {
        }

        private NetTcpSecurity(SecurityMode mode, TcpTransportSecurity transportSecurity, MessageSecurityOverTcp messageSecurity)
        {
            Fx.Assert(SecurityModeHelper.IsDefined(mode), string.Format("Invalid SecurityMode value: {0}.", mode.ToString()));

            _mode = mode;
            Transport = transportSecurity ?? new TcpTransportSecurity();
            Message = messageSecurity ?? new MessageSecurityOverTcp();
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

        public TcpTransportSecurity Transport { get; set; }

        public MessageSecurityOverTcp Message { get; set; }

        internal BindingElement CreateTransportSecurity()
        {
            if (_mode == SecurityMode.TransportWithMessageCredential)
            {
                return Transport.CreateTransportProtectionOnly();
            }
            else if (_mode == SecurityMode.Transport)
            {
                return Transport.CreateTransportProtectionAndAuthentication();
            }
            else
            {
                return null;
            }
        }

        internal SecurityBindingElement CreateMessageSecurity(bool isReliableSessionEnabled)
        {
            if (_mode == SecurityMode.Message)
            {
                return Message.CreateSecurityBindingElement(false, isReliableSessionEnabled, null);
            }
            else if (_mode == SecurityMode.TransportWithMessageCredential)
            {
                return Message.CreateSecurityBindingElement(true, isReliableSessionEnabled, this.CreateTransportSecurity());
            }
            else
            {
                return null;
            }
        }
    }
}
