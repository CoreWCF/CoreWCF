using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    public abstract class StreamUpgradeProvider : CommunicationObject
    {
        TimeSpan closeTimeout;
        TimeSpan openTimeout;

        protected StreamUpgradeProvider()
            : this(null)
        {
        }

        protected StreamUpgradeProvider(IDefaultCommunicationTimeouts timeouts)
        {
            if (timeouts != null)
            {
                closeTimeout = timeouts.CloseTimeout;
                openTimeout = timeouts.OpenTimeout;
            }
            else
            {
                closeTimeout = ServiceDefaults.CloseTimeout;
                openTimeout = ServiceDefaults.OpenTimeout;
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return closeTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return closeTimeout; }
        }

        public virtual T GetProperty<T>() where T : class
        {
            return null;
        }

        public abstract StreamUpgradeAcceptor CreateUpgradeAcceptor();
    }
}
