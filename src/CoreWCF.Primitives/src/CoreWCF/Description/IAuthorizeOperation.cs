// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    public interface IAuthorizeOperation
    {
        void BuildClaim(OperationDescription operationDescription, DispatchOperation dispatchOperation);
    }
}
