using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using SessionIdleManager = CoreWCF.Channels.ServiceChannel.SessionIdleManager;
using System.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class ChannelHandler
    {
        public static readonly TimeSpan CloseAfterFaultTimeout = TimeSpan.FromSeconds(10);
        public const string MessageBufferPropertyName = "_RequestMessageBuffer_";

        internal bool HasRegisterBeenCalled
        {
            get { return false; }
        }

        struct RequestInfo
        {
            public EndpointDispatcher Endpoint;
            public InstanceContext ExistingInstanceContext;
            public ServiceChannel Channel;
            public bool EndpointLookupDone;
            public DispatchRuntime DispatchRuntime;
            public RequestContext RequestContext;
            public ChannelHandler ChannelHandler;

            public RequestInfo(ChannelHandler channelHandler)
            {
                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                DispatchRuntime = null;
                RequestContext = null;
                ChannelHandler = channelHandler;
            }

            public void Cleanup()
            {
                Endpoint = null;
                ExistingInstanceContext = null;
                Channel = null;
                EndpointLookupDone = false;
                RequestContext = null;
            }
        }
    }

}