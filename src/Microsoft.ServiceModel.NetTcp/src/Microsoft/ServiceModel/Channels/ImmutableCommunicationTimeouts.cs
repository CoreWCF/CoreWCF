using System;

namespace Microsoft.ServiceModel.Channels
{
    class ImmutableCommunicationTimeouts : IDefaultCommunicationTimeouts
    {
        TimeSpan _close;
        TimeSpan _open;
        TimeSpan _receive;
        TimeSpan _send;

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
    }
}
