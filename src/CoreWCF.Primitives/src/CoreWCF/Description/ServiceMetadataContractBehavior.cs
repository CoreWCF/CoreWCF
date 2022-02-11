// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    public sealed class ServiceMetadataContractBehavior : IContractBehavior
    {
        public ServiceMetadataContractBehavior()
        {
        }

        public ServiceMetadataContractBehavior(bool metadataGenerationDisabled)
        {
            MetadataGenerationDisabled = metadataGenerationDisabled;
        }

        public bool MetadataGenerationDisabled { get; set; } = false;

        #region IContractBehavior Members
        void IContractBehavior.Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        }

        void IContractBehavior.ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
        }

        void IContractBehavior.AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        void IContractBehavior.ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
        }
        #endregion
    }
}
