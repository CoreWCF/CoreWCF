namespace Microsoft.ServiceModel.Description
{
    public interface IContractBehavior
    {
        void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, Channels.BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(Microsoft.ServiceModel.Description.ContractDescription contractDescription, Microsoft.ServiceModel.Description.ServiceEndpoint endpoint, Microsoft.ServiceModel.Dispatcher.ClientRuntime clientRuntime);
        void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, Dispatcher.DispatchRuntime dispatchRuntime);
        void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint);
    }
}