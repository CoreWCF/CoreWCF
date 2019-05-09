using System;

namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IOperationInvoker
    {
        bool IsSynchronous { get; }

        object[] AllocateInputs();

        object Invoke(object instance, object[] inputs, out object[] outputs);

        // TODO: Switch to Task based invoker
        IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state);

        object InvokeEnd(object instance, out object[] outputs, IAsyncResult result);
    }
}