// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed partial class NetTcpSecurity
    {
        internal const SecurityMode DefaultMode = SecurityMode.Transport;
        private SecurityMode mode;

        public NetTcpSecurity()
            : this(DefaultMode, new TcpTransportSecurity())
        {
        }

        private NetTcpSecurity(SecurityMode mode, TcpTransportSecurity transportSecurity)
        {
            Fx.Assert(SecurityModeHelper.IsDefined(mode), string.Format("Invalid SecurityMode value: {0}.", mode.ToString()));

            this.mode = mode;
            Transport = transportSecurity == null ? new TcpTransportSecurity() : transportSecurity;
        }

        [DefaultValue(DefaultMode)]
        public SecurityMode Mode
        {
            get { return mode; }
            set
            {
                if (!SecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                mode = value;
            }
        }

        public TcpTransportSecurity Transport { get; set; }

        internal BindingElement CreateTransportSecurity()
        {
            if (mode == SecurityMode.TransportWithMessageCredential)
            {
                throw new PlatformNotSupportedException("TransportWithMessageCredential");
                //return this.transportSecurity.CreateTransportProtectionOnly();
            }
            else if (mode == SecurityMode.Transport)
            {
                return Transport.CreateTransportProtectionAndAuthentication();
            }
            else
            {
                return null;
            }
        }
    }
}