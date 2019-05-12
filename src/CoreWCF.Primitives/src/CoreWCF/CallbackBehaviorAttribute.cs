﻿namespace CoreWCF
{
    public sealed class CallbackBehaviorAttribute : System.Attribute, Description.IEndpointBehavior
    {
        public CallbackBehaviorAttribute() { }
        public bool AutomaticSessionShutdown { get { return default(bool); } set { } }
        public bool UseSynchronizationContext { get { return default(bool); } set { } }
        void Description.IEndpointBehavior.AddBindingParameters(Description.ServiceEndpoint serviceEndpoint, Channels.BindingParameterCollection parameters) { }
        void CoreWCF.Description.IEndpointBehavior.ApplyClientBehavior(CoreWCF.Description.ServiceEndpoint serviceEndpoint, CoreWCF.Dispatcher.ClientRuntime clientRuntime) { }
        void Description.IEndpointBehavior.ApplyDispatchBehavior(Description.ServiceEndpoint serviceEndpoint, Dispatcher.EndpointDispatcher endpointDispatcher) { }
        void Description.IEndpointBehavior.Validate(Description.ServiceEndpoint serviceEndpoint) { }
    }
}