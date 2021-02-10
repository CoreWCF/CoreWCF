// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Dispatcher
{
    public interface IParameterInspector
    {
        object BeforeCall(string operationName, object[] inputs);
        void AfterCall(string operationName, object[] outputs, object returnValue, object correlationState);
    }
}