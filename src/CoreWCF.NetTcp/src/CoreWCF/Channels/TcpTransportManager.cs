using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    abstract class TcpTransportManager : ConnectionOrientedTransportManager<TcpChannelListener>
    {
        internal TcpTransportManager()
        {
        }

        internal override string Scheme
        {
            get { return Uri.UriSchemeNetTcp; }
        }

        protected virtual bool IsCompatible(TcpChannelListener channelListener)
        {
            return base.IsCompatible(channelListener);
        }
    }
}
