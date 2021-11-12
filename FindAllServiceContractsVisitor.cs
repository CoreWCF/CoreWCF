using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal class FindAllServiceContractsVisitor : SymbolVisitor
    {
        private readonly IList<INamedTypeSymbol> _symbols;
        private readonly IEnumerable<INamedTypeSymbol> _serviceContractSymbols;

        public FindAllServiceContractsVisitor(IList<INamedTypeSymbol> symbols, IEnumerable<INamedTypeSymbol> serviceContractSymbols)
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
                if (_serviceContractSymbols.Any(x => symbol.HasAttribute(x)))
                {
                    _symbols.Add(symbol);
                }
            }
        }
    }
}
