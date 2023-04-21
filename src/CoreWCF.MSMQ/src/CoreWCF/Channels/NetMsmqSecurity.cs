// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Channels
{
    public sealed class NetMsmqSecurity
    {
        internal const NetMsmqSecurityMode DefaultMode = NetMsmqSecurityMode.Transport;
        private NetMsmqSecurityMode _mode;
        private MsmqTransportSecurity _transportSecurity;
        private MessageSecurityOverMsmq _messageSecurity;

        public NetMsmqSecurity()
            : this(DefaultMode, null, null)
        {
        }

        internal NetMsmqSecurity(NetMsmqSecurityMode mode)
            : this(mode, null, null)
        {
        }

        private NetMsmqSecurity(NetMsmqSecurityMode mode, MsmqTransportSecurity transportSecurity, MessageSecurityOverMsmq messageSecurity)
        {
            // Fx.Assert(NetMsmqSecurityModeHelper.IsDefined(mode), string.Format("Invalid NetMsmqSecurityMode value: {0}.", mode.ToString()));

            _mode = mode;
            _transportSecurity = new MsmqTransportSecurity();
            _messageSecurity = new MessageSecurityOverMsmq();
        }

        [DefaultValue(DefaultMode)]
        public NetMsmqSecurityMode Mode
        {
            get { return _mode; }
            set
            {
                //if (!NetMsmqSecurityModeHelper.IsDefined(value))
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                //}
                _mode = value;
            }
        }

        public MsmqTransportSecurity Transport
        {
            get
            {
                if (_transportSecurity == null)
                    _transportSecurity = new MsmqTransportSecurity();
                return _transportSecurity;
            }
            set { _transportSecurity = value; }
        }

        public MessageSecurityOverMsmq Message
        {
            get
            {
                if (_messageSecurity == null)
                    _messageSecurity = new MessageSecurityOverMsmq();
                return _messageSecurity;
            }
            set { _messageSecurity = value; }
        }

        internal void ConfigureTransportSecurity(MsmqBindingElementBase msmq)
        {
            if (_mode == NetMsmqSecurityMode.Transport || _mode == NetMsmqSecurityMode.Both)
                msmq.MsmqTransportSecurity = Transport;
            else
                msmq.MsmqTransportSecurity.Disable();
        }


        internal SecurityBindingElement CreateMessageSecurity()
        {
            return Message.CreateSecurityBindingElement();
        }
    }
}
