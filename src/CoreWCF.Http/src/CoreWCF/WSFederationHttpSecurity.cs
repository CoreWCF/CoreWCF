// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF
{
    public sealed class WSFederationHttpSecurity
    {
        internal const WSFederationHttpSecurityMode DefaultMode = WSFederationHttpSecurityMode.Message;
        private WSFederationHttpSecurityMode _mode;
        private FederatedMessageSecurityOverHttp _messageSecurity;

        public WSFederationHttpSecurity()
            : this(DefaultMode, new FederatedMessageSecurityOverHttp())
        {
        }

        private WSFederationHttpSecurity(WSFederationHttpSecurityMode mode, FederatedMessageSecurityOverHttp messageSecurity)
        {
            Fx.Assert(WSFederationHttpSecurityModeHelper.IsDefined(mode), string.Format("Invalid WSFederationHttpSecurityMode value: {0}", mode.ToString()));
            _mode = mode;
            _messageSecurity = messageSecurity == null ? new FederatedMessageSecurityOverHttp() : messageSecurity;
        }

        public WSFederationHttpSecurityMode Mode
        {
            get { return _mode; }
            set
            {
                if (!WSFederationHttpSecurityModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _mode = value;
            }
        }

        public FederatedMessageSecurityOverHttp Message
        {
            get { return _messageSecurity; }
            set { _messageSecurity = value; }
        }

        internal SecurityBindingElement CreateMessageSecurity(bool isReliableSessionEnabled, MessageSecurityVersion version)
        {
            if (_mode == WSFederationHttpSecurityMode.Message || _mode == WSFederationHttpSecurityMode.TransportWithMessageCredential)
            {
                return _messageSecurity.CreateSecurityBindingElement(Mode == WSFederationHttpSecurityMode.TransportWithMessageCredential, isReliableSessionEnabled, version);
            }
            else
            {
                return null;
            }
        }

    }
}
