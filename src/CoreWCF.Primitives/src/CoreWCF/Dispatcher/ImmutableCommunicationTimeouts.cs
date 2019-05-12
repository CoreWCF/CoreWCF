using System;

namespace CoreWCF.Dispatcher
{
    internal class ImmutableCommunicationTimeouts : IDefaultCommunicationTimeouts
    {
        TimeSpan close;
        TimeSpan open;
        TimeSpan receive;
        TimeSpan send;

        internal ImmutableCommunicationTimeouts()
            : this(null)
        {
        }

        internal ImmutableCommunicationTimeouts(IDefaultCommunicationTimeouts timeouts)
        {
            if (timeouts == null)
            {
                close = ServiceDefaults.CloseTimeout;
                open = ServiceDefaults.OpenTimeout;
                receive = ServiceDefaults.ReceiveTimeout;
                send = ServiceDefaults.SendTimeout;
            }
            else
            {
                close = timeouts.CloseTimeout;
                open = timeouts.OpenTimeout;
                receive = timeouts.ReceiveTimeout;
                send = timeouts.SendTimeout;
            }
        }

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout
        {
            get { return close; }
        }

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout
        {
            get { return open; }
        }

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout
        {
            get { return receive; }
        }

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout
        {
            get { return send; }
        }
    }
}
