// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal struct ErrorHandlerFaultInfo
    {
        public ErrorHandlerFaultInfo(string defaultFaultAction)
        {
            DefaultFaultAction = defaultFaultAction;
            Fault = null;
            IsConsideredUnhandled = false;
        }

        public Message Fault { get; set; }

        public string DefaultFaultAction { get; set; }

        public bool IsConsideredUnhandled { get; set; }
    }
}