using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal struct ErrorHandlerFaultInfo
    {
        Message fault;   // if this is null, then we aren't interested in sending back a fault
        bool isConsideredUnhandled;  // if this is true, it means Fault is the 'internal server error' fault
        string defaultFaultAction;

        public ErrorHandlerFaultInfo(string defaultFaultAction)
        {
            this.defaultFaultAction = defaultFaultAction;
            fault = null;
            isConsideredUnhandled = false;
        }

        public Message Fault
        {
            get { return fault; }
            set { fault = value; }
        }

        public string DefaultFaultAction
        {
            get { return defaultFaultAction; }
            set { defaultFaultAction = value; }
        }

        public bool IsConsideredUnhandled
        {
            get { return isConsideredUnhandled; }
            set { isConsideredUnhandled = value; }
        }
    }
}