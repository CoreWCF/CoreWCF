// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal static class DiagnosticDescriptors
    {
        internal static readonly DiagnosticDescriptor ParentClassShouldBePartialError = new DiagnosticDescriptor(id: "COREWCF_0100",
                                                                              title: "Parent class should be partial",
                                                                              messageFormat: "Parent class '{0}' of method '{1}' should be partial",
                                                                              category: nameof(OperationParameterInjectionGenerator),
                                                                              DiagnosticSeverity.Error,
                                                                              isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ParentClassShouldImplementAServiceContractError = new DiagnosticDescriptor(id: "COREWCF_0101",
                                                                      title: "Parent class should be a ServiceContract implementation",
                                                                      messageFormat: "Parent class '{0}' of method '{1}' should either implement an interface marked with a ServiceContract attribute or inherit a class implementing an interface marked with a ServiceContract attribute",
                                                                      category: nameof(OperationParameterInjectionGenerator),
                                                                      DiagnosticSeverity.Error,
                                                                      isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor OperationContractShouldNotBeAlreadyImplementedError = new DiagnosticDescriptor(id: "COREWCF_0102",
                                                              title: "OperationContract is already imlemented",
                                                              messageFormat: "OperationContract '{0}.{1}' is already implemented",
                                                              category: nameof(OperationParameterInjectionGenerator),
                                                              DiagnosticSeverity.Error,
                                                              isEnabledByDefault: true);

        internal static Diagnostic RaiseParentClassShouldBePartialError(string parentClassName, string methodName, Location location) => Diagnostic.Create(ParentClassShouldBePartialError, location, parentClassName, methodName);

        internal static Diagnostic RaiseParentClassShouldImplementAServiceContractError(string parentClassName, string methodName, Location location) => Diagnostic.Create(ParentClassShouldImplementAServiceContractError, location, parentClassName, methodName);

        internal static Diagnostic RaiseOperationContractShouldNotBeAlreadyImplementedError(string parentClassName, string methodName, Location location) => Diagnostic.Create(OperationContractShouldNotBeAlreadyImplementedError, location, parentClassName, methodName);
    }
}
