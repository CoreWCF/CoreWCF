// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal struct ErrorHandlerFaultInfo
    {
        private string _defaultFaultAction;

        public ErrorHandlerFaultInfo(string defaultFaultAction)
        {
            _defaultFaultAction = defaultFaultAction;
            Fault = null;
            IsConsideredUnhandled = false;
        }

        public Message Fault { get; set; }

        public string DefaultFaultAction
        {
            get { return _defaultFaultAction; }
            set { _defaultFaultAction = value; }
        }

        public bool IsConsideredUnhandled { get; set; }
    }
}