namespace CoreWCF.Description
{
    public interface IEndpointBehavior
    {
        void AddBindingParameters(ServiceEndpoint endpoint, Channels.BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(CoreWCF.Description.ServiceEndpoint endpoint, CoreWCF.Dispatcher.ClientRuntime clientRuntime);
        void ApplyDispatchBehavior(ServiceEndpoint endpoint, Dispatcher.EndpointDispatcher endpointDispatcher);
        void Validate(ServiceEndpoint endpoint);
    }
}