// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.BuildTools
{
    internal static class MethodSymbolExtensions
    {
        public static bool? IsGeneratedCode(this IMethodSymbol methodSymbol)
            => methodSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath.EndsWith(".g.cs");

        public static bool IsMatchingUserProvidedMethod(this IMethodSymbol methodSymbol, IMethodSymbol userProvidedMethodSymbol, INamedTypeSymbol? coreWCFInjectedAttribute, INamedTypeSymbol? fromServicesAttribute)
        {
            int parameterFound = 0;
            if (methodSymbol.Name != userProvidedMethodSymbol.Name)
            {
                return false;
            }

            var parameters = methodSymbol.Parameters;

            for (int i = 0,j = 0; i < userProvidedMethodSymbol.Parameters.Length; i++)
            {
                IParameterSymbol parameterSymbol = userProvidedMethodSymbol.Parameters[i];
                if (parameterSymbol.GetOneAttributeOf(coreWCFInjectedAttribute, fromServicesAttribute) is not null)
                {
                    continue;
                }

                if (parameterSymbol.IsMatchingParameter(parameters[j]))
                {
                    j++;
                    parameterFound++;
                }
            }

            return parameterFound == parameters.Length;
        }
    }

    internal static class ParameterSymbolExtensions
    {
        public static bool IsMatchingParameter(this IParameterSymbol symbol, IParameterSymbol parameterSymbol)
            => SymbolEqualityComparer.Default.Equals(symbol.Type, parameterSymbol.Type);

        public static string? GetInjectedPropertyNameValue(this IParameterSymbol symbol, INamedTypeSymbol? coreWcfInjectedAttributeSymbol)
        {
            foreach (AttributeData attributeData in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, coreWcfInjectedAttributeSymbol))
                {
                    if (attributeData.NamedArguments.Length > 0 && attributeData.NamedArguments[0].Key == "PropertyName")
                    {
                        return attributeData.NamedArguments[0].Value.Value!.ToString();
                    }
                }
            }

            return null;
        }
    }

    internal static class SymbolExtensions
    {
        public static AttributeData? GetOneAttributeOf(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypeSymbols)
        {
            if (attributeTypeSymbols.Length == 0)
            {
                return null;
            }

            foreach (var attribute in symbol.GetAttributes())
            {
                foreach (var namedTypeSymbol in attributeTypeSymbols.Where(static s => s is not null))
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, namedTypeSymbol))
                    {
                        return attribute;
                    }
                }
            }

            return null;
        }

        public static bool HasOneAttributeInheritFrom(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypeSymbols)
        {
            if (attributeTypeSymbols.Length == 0)
            {
                return false;
            }

            foreach (var attribute in symbol.GetAttributes())
            {
                foreach (var @interface in attribute.AttributeClass!.AllInterfaces)
                {
                    foreach (var namedTypeSymbol in attributeTypeSymbols.Where(static s => s is not null))
                    {
                        if (SymbolEqualityComparer.Default.Equals(@interface, namedTypeSymbol))
                        {
                            return true;
                        }
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

    internal static class TypedConstantExtensions
    {
        public static string ToSafeCSharpString(this TypedConstant typedConstant)
        {
            if (typedConstant.Kind == TypedConstantKind.Array)
            {
                return $"new [] {typedConstant.ToCSharpString()}";
            }

            return typedConstant.ToCSharpString();

        }
    }
}
