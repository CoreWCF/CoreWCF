namespace Microsoft.ServiceModel.Description
{
    public interface IEndpointBehavior
    {
        void AddBindingParameters(ServiceEndpoint endpoint, Channels.BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(Microsoft.ServiceModel.Description.ServiceEndpoint endpoint, Microsoft.ServiceModel.Dispatcher.ClientRuntime clientRuntime);
        void ApplyDispatchBehavior(ServiceEndpoint endpoint, Dispatcher.EndpointDispatcher endpointDispatcher);
        void Validate(ServiceEndpoint endpoint);
    }
}