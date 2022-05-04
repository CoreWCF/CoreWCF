// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal class FindAllServiceContractsVisitor : SymbolVisitor<IEnumerable<INamedTypeSymbol>>
    {
        private readonly INamedTypeSymbol?[] _serviceContractSymbols;

        public FindAllServiceContractsVisitor(params INamedTypeSymbol?[] serviceContractSymbols)
        {
            _serviceContractSymbols = serviceContractSymbols;
        }

        public override IEnumerable<INamedTypeSymbol> Visit(ISymbol? symbol) => base.Visit(symbol) ?? Enumerable.Empty<INamedTypeSymbol>();

        public override IEnumerable<INamedTypeSymbol> VisitNamespace(INamespaceSymbol symbol)
            => symbol.GetMembers().SelectMany(m => m.Accept(this));
        
        public override IEnumerable<INamedTypeSymbol> VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind == TypeKind.Interface)
            {
                if (symbol.HasOneOfAttributes(_serviceContractSymbols))
                {
                   yield return symbol;
                }
            }
        }
    }
}
