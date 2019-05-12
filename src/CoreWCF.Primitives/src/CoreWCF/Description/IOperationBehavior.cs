using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    public interface IOperationBehavior
    {
        void AddBindingParameters(OperationDescription operationDescription, BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation);
        void ApplyDispatchBehavior(OperationDescription operationDescription, DispatchOperation dispatchOperation);
        void Validate(OperationDescription operationDescription);
    }
}