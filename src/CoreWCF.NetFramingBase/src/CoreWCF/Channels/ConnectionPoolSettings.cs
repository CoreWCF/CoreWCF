// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public class ConnectionPoolSettings
    {
        private TimeSpan _idleTimeout;
        private int _maxOutboundConnectionsPerEndpoint;

        protected ConnectionPoolSettings()
        {
            _idleTimeout = ConnectionOrientedTransportDefaults.IdleTimeout;
            _maxOutboundConnectionsPerEndpoint = ConnectionOrientedTransportDefaults.MaxOutboundConnectionsPerEndpoint;
        }

        protected ConnectionPoolSettings(ConnectionPoolSettings other)
        {
            _idleTimeout = other._idleTimeout;
            _maxOutboundConnectionsPerEndpoint = other._maxOutboundConnectionsPerEndpoint;
        }

        public TimeSpan IdleTimeout
        {
            get { return _idleTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(IdleTimeout), value,
                        SRCommon.SFxTimeoutOutOfRange0));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(IdleTimeout), value,
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _idleTimeout = value;
            }
        }

        public int MaxOutboundConnectionsPerEndpoint
        {
            get { return _maxOutboundConnectionsPerEndpoint; }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(MaxOutboundConnectionsPerEndpoint), value,
                        SRCommon.ValueMustBeNonNegative));
                }

                _maxOutboundConnectionsPerEndpoint = value;
            }
        }

        public bool IsMatch(ConnectionPoolSettings connectionPool)
        {
            if (_idleTimeout != connectionPool._idleTimeout)
            {
                return false;
            }

            if (_maxOutboundConnectionsPerEndpoint != connectionPool._maxOutboundConnectionsPerEndpoint)
            {
                return false;
            }

            return true;
        }
    }
}
