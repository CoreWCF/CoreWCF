// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal sealed class InternalDuplexBindingElement
    {
        // In WCF, this binding is used to connect together a IChannelListener for receiving
        // messages, and an IChannelFactory for sending the reply. We aren't supporting
        // this composite model in CoreWCF, at least not at the moment. If we did, it
        // would be in another package as it would add a dependency on the WCF Client.
        // This class now reports whether a duplex binding is needed so we can fail if
        // needed.
        public static bool RequiresDuplexBinding(BindingContext context)
        {
            if (context.CanBuildNextServiceDispatcher<IDuplexChannel>())
                return false;
            //if (context.RemainingBindingElements.Find<CompositeDuplexBindingElement>() == null)
            //    return;

            if (context.CanBuildNextServiceDispatcher<IOutputChannel>() &&
                context.CanBuildNextServiceDispatcher<IInputChannel>())
            {
                if (context.CanBuildNextServiceDispatcher<IReplyChannel>())
                    return false;
                if (context.CanBuildNextServiceDispatcher<IReplySessionChannel>())
                    return false;
                if (context.CanBuildNextServiceDispatcher<IInputSessionChannel>())
                    return false;
                if (context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>())
                    return false;

                return true;
            }

            return false;
        }
    }
}
