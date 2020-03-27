using System;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    public interface IOperationInvoker
    {
        object[] AllocateInputs();

        ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs);
    }
}