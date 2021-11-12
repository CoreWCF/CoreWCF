// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    internal static class MethodDeclarationSyntaxExtensions
    {
        public static bool HasParentPartialClass(this MethodDeclarationSyntax methodDeclarationSyntax)
        {
            ClassDeclarationSyntax? parentClassDeclarationSyntax = methodDeclarationSyntax.Parent as ClassDeclarationSyntax;
            if (parentClassDeclarationSyntax == null)
            {
                return false;
            }

            if (parentClassDeclarationSyntax.Modifiers.Any(static x => x.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }

            return false;
        }
    }
}
