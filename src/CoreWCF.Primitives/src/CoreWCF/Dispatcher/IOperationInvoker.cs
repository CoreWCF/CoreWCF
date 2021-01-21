// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    public interface IOperationInvoker
    {
        object[] AllocateInputs();

        ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs);
    }
}