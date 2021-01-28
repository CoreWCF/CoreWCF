// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Dispatcher
{
    internal class ImmutableCommunicationTimeouts : IDefaultCommunicationTimeouts
    {
        private TimeSpan _close;
        private TimeSpan _open;
        private TimeSpan _receive;
        private TimeSpan _send;

        internal ImmutableCommunicationTimeouts()
            : this(null)
        {
        }

        internal ImmutableCommunicationTimeouts(IDefaultCommunicationTimeouts timeouts)
        {
            if (timeouts == null)
            {
                _close = ServiceDefaults.CloseTimeout;
                _open = ServiceDefaults.OpenTimeout;
                _receive = ServiceDefaults.ReceiveTimeout;
                _send = ServiceDefaults.SendTimeout;
            }
            else
            {
                _close = timeouts.CloseTimeout;
                _open = timeouts.OpenTimeout;
                _receive = timeouts.ReceiveTimeout;
                _send = timeouts.SendTimeout;
            }
        }

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout
        {
            get { return _close; }
        }

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout
        {
            get { return _open; }
        }

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout
        {
            get { return _receive; }
        }

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout
        {
            get { return _send; }
        }
    }
}
