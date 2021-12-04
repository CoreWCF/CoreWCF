﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal sealed class OperationContractSpec
    {
        public OperationContractSpec(INamedTypeSymbol serviceContract, INamedTypeSymbol serviceContractImplementation,
            IMethodSymbol missingOperationContract, IMethodSymbol userProvidedOperationContractImplementation)
        {
            this.ServiceContract = serviceContract;
            this.ServiceContractImplementation = serviceContractImplementation;
            this.UserProvidedOperationContractImplementation = userProvidedOperationContractImplementation;
            this.MissingOperationContract = missingOperationContract;
        }

        public INamedTypeSymbol ServiceContract { get; }
        public INamedTypeSymbol ServiceContractImplementation { get; }
        public IMethodSymbol MissingOperationContract { get; }
        public IMethodSymbol UserProvidedOperationContractImplementation { get; }
    }
}
