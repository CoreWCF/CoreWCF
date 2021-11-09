// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    public static class ParameterSymbolExtensions
    {
        public static bool IsMatchingParameter(this IParameterSymbol symbol, IParameterSymbol parameterSymbol)
            => SymbolEqualityComparer.Default.Equals(symbol.Type, parameterSymbol.Type)
                && symbol.Name == parameterSymbol.Name;
    }

    public static class SymbolExtensions
    {
        public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol attributeTypeSymbol)
            => symbol.GetAttributes().Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, attributeTypeSymbol));
    }
}
