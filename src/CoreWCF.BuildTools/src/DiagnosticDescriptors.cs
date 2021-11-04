// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal static class DiagnosticDescriptors
    {
        private static readonly DiagnosticDescriptor serviceShouldBePartialError = new DiagnosticDescriptor(id: "COREWCF_0100",
                                                                              title: "Service implementation should be partial",
                                                                              messageFormat: "Service implementation '{0}' for contract '{1}' should be partial.",
                                                                              category: nameof(OperationParameterInjectionGenerator),
                                                                              DiagnosticSeverity.Error,
                                                                              isEnabledByDefault: true);

        internal static Diagnostic ServicesShouldBePartialError(string serviceImplementation, string serviceContract) => Diagnostic.Create(serviceShouldBePartialError, Location.None, serviceImplementation, serviceContract);
    }
}
