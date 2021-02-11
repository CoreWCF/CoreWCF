// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public interface IContractBehavior
    {
        void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, Channels.BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(CoreWCF.Description.ContractDescription contractDescription, CoreWCF.Description.ServiceEndpoint endpoint, CoreWCF.Dispatcher.ClientRuntime clientRuntime);
        void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, Dispatcher.DispatchRuntime dispatchRuntime);
        void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint);
    }
}