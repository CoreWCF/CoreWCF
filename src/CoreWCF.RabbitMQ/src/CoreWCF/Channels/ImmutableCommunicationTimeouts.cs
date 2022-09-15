// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    //TODO : Remove this file
    internal class ImmutableCommunicationTimeouts //: IDefaultCommunicationTimeouts
    {
        /* 
        private readonly TimeSpan _close;
        private readonly TimeSpan _open;
        private readonly TimeSpan _receive;
        private readonly TimeSpan _send;

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

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout => _close;

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout => _open;

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout => _receive;

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout => _send;
        */
    }
}
