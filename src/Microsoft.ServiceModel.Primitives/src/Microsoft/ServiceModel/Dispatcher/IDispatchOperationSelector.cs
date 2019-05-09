using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IDispatchOperationSelector
    {
        string SelectOperation(ref Message message);
    }
}