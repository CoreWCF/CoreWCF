// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    internal class ConfigLoader
    {
        private readonly Dictionary<string, Binding> bindingTable;
        private readonly IContractResolver contractResolver;

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