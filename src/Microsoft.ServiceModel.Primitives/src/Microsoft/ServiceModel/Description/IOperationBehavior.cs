using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Dispatcher;

namespace Microsoft.ServiceModel.Description
{
    public interface IOperationBehavior
    {
        void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation);
        void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation);
        void Validate(OperationDescription operationDescription);
    }
}