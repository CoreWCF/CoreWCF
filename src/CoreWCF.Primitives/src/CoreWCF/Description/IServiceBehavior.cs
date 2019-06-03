namespace CoreWCF.Description
{
    public interface IServiceBehavior
    {
        void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase);
        void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, Channels.BindingParameterCollection bindingParameters);
        void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase);
    }
}