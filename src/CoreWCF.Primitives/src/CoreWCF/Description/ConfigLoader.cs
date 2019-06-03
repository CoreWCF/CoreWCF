using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    internal class ConfigLoader
    {
        Dictionary<string, Binding> bindingTable;
        IContractResolver contractResolver;

        public ConfigLoader(IContractResolver contractResolver)
        {
            this.contractResolver = contractResolver;
            bindingTable = new Dictionary<string, Binding>();
        }

        internal ContractDescription LookupContract(string contractName, string serviceName)
        {
            ContractDescription contract = LookupContractForStandardEndpoint(contractName, serviceName);
            if (contract == null)
            {
                if (contractName == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SfxReflectedContractKeyNotFoundEmpty, serviceName)));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SfxReflectedContractKeyNotFound2, contractName, serviceName)));
                }
            }

            return contract;
        }

        internal ContractDescription LookupContractForStandardEndpoint(string contractName, string serviceName)
        {
            return contractResolver.ResolveContract(contractName);
        }
    }
}