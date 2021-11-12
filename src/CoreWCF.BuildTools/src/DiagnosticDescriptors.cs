// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal static class DiagnosticDescriptors
    {
        private static readonly DiagnosticDescriptor serviceShouldBePartialError = new DiagnosticDescriptor(id: "COREWCF_0100",
                                                                              title: "Parent class should be partial",
                                                                              messageFormat: "Parent class '{0}' of method '{1}' should be partial.",
                                                                              category: nameof(OperationParameterInjectionGenerator),
                                                                              DiagnosticSeverity.Error,
                                                                              isEnabledByDefault: true);

        internal static Diagnostic ServicesShouldBePartialError(string parentClassName, string methodName) => Diagnostic.Create(serviceShouldBePartialError, Location.None, parentClassName, methodName);
    }
}
