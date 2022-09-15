﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.BuildTools
{
    internal static class ParameterSymbolExtensions
    {
        public static bool IsMatchingParameter(this IParameterSymbol symbol, IParameterSymbol parameterSymbol)
            => SymbolEqualityComparer.Default.Equals(symbol.Type, parameterSymbol.Type)
                && symbol.Name == parameterSymbol.Name;
    }

    internal static class SymbolExtensions
    {
        public static bool HasOneOfAttributes(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypeSymbols)
        {
            if(attributeTypeSymbols.Length == 0)
            {
                return false;
            }

            foreach (var attribute in symbol.GetAttributes())
            {
                foreach (var namedTypeSymbol in attributeTypeSymbols.Where(static s => s is not null))
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, namedTypeSymbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    internal static class NamedTypeSymbolExtensions
    {
        public static bool IsPartial(this INamedTypeSymbol namedTypeSymbol, out INamedTypeSymbol parentType)
        {
            bool result = namedTypeSymbol.DeclaringSyntaxReferences.Select(static s => s.GetSyntax()).OfType<ClassDeclarationSyntax>().All(static c => c.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));
            if (result && namedTypeSymbol.ContainingType != null)
            {
                return namedTypeSymbol.ContainingType.IsPartial(out parentType);
            }
            parentType = namedTypeSymbol;
            return result;
        }
    }
}
