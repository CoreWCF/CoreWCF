// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal class FindAllServiceContractsVisitor : SymbolVisitor
    {
        private readonly IList<INamedTypeSymbol> _symbols;
        private readonly INamedTypeSymbol[] _serviceContractSymbols;

        public FindAllServiceContractsVisitor(IList<INamedTypeSymbol> symbols, params INamedTypeSymbol[] serviceContractSymbols)
        {
            _symbols = symbols;
            _serviceContractSymbols = serviceContractSymbols;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var child in symbol.GetMembers())
            {
                child.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind == TypeKind.Interface)
            {
                if (symbol.HasOneOfAttributes(_serviceContractSymbols))
                {
                    _symbols.Add(symbol);
                }
            }
        }
    }
}
