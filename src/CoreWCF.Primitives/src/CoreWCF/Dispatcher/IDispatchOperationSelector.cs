using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IDispatchOperationSelector
    {
        string SelectOperation(ref Message message);
    }
}