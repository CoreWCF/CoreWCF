// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools
{
    internal static class DiagnosticDescriptors
    {
        private static readonly DiagnosticDescriptor parentClassShouldBePartialError = new DiagnosticDescriptor(id: "COREWCF_0100",
                                                                              title: "Parent class should be partial",
                                                                              messageFormat: "Parent class '{0}' of method '{1}' should be partial",
                                                                              category: nameof(OperationParameterInjectionGenerator),
                                                                              DiagnosticSeverity.Error,
                                                                              isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor parentClassShouldImplementAServiceContract = new DiagnosticDescriptor(id: "COREWCF_0101",
                                                                      title: "Parent class should be a ServiceContract implementation",
                                                                      messageFormat: "Parent class '{0}' of method '{1}' should either implement an interface marked with a ServiceContract attribute or inherit a class implementing an interface merked with a ServiceContract attribute",
                                                                      category: nameof(OperationParameterInjectionGenerator),
                                                                      DiagnosticSeverity.Error,
                                                                      isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor operationContractShouldNotBeAlreadyImplemented = new DiagnosticDescriptor(id: "COREWCF_0102",
                                                              title: "OperationContract is already imlemented",
                                                              messageFormat: "OperationContract '{0}.{1}' is already implemented",
                                                              category: nameof(OperationParameterInjectionGenerator),
                                                              DiagnosticSeverity.Error,
                                                              isEnabledByDefault: true);

        internal static Diagnostic ParentClassShouldBePartialError(string parentClassName, string methodName) => Diagnostic.Create(parentClassShouldBePartialError, Location.None, parentClassName, methodName);

        internal static Diagnostic ParentClassShouldImplementAServiceContract(string parentClassName, string methodName) => Diagnostic.Create(parentClassShouldImplementAServiceContract, Location.None, parentClassName, methodName);

        internal static Diagnostic OperationContractShouldNotBeAlreadyImplemented(string parentClassName, string methodName) => Diagnostic.Create(operationContractShouldNotBeAlreadyImplemented, Location.None, parentClassName, methodName);
    }
}
