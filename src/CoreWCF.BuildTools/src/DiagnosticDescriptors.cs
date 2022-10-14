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

        internal static Diagnostic RaiseParentClassShouldBePartialError(string parentClassName, string methodName, Location location) => Diagnostic.Create(ParentClassShouldBePartialError, location, parentClassName, methodName);

        internal static readonly DiagnosticDescriptor AllowAnonymousAttributeIsNotSupported = new DiagnosticDescriptor(id: "COREWCF_0200",
            title: "[AllowAnonymous] attribute is not supported",
            messageFormat: "[AllowAnonymous] attribute is not supported",
            category: nameof(AuthorizationAttributesAnalyzer),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Diagnostic AllowAnonymousAttributeIsNotSupportedWarning(Location location) => Diagnostic.Create(AllowAnonymousAttributeIsNotSupported, location);

        internal static readonly DiagnosticDescriptor AuthorizeAttributeIsNotSupportedOnClass = new DiagnosticDescriptor(id: "COREWCF_0201",
            title: "[Authorize] attribute is not supported",
            messageFormat: "[Authorize] attribute is not supported on class '{0}'",
            category: nameof(AuthorizationAttributesAnalyzer),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Diagnostic AuthorizeAttributeIsNotSupportedOnClassWarning(string className, Location location) => Diagnostic.Create(AuthorizeAttributeIsNotSupportedOnClass, location, className);
    }
}
