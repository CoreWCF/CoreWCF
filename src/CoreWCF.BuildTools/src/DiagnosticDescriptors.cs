// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace CoreWCF.BuildTools;

internal static class DiagnosticDescriptors
{
    internal static class OperationParameterInjectionGenerator_01XX
    {
        internal static readonly DiagnosticDescriptor ParentClassShouldBePartialError = new DiagnosticDescriptor(id: "COREWCF_0100",
            title: "Parent class should be partial",
            messageFormat: "Parent class '{0}' of method '{1}' should be partial",
            category: nameof(OperationParameterInjectionGenerator),
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static Diagnostic RaiseParentClassShouldBePartialError(string parentClassName, string methodName, Location location) => Diagnostic.Create(ParentClassShouldBePartialError, location, parentClassName, methodName);

        internal static readonly DiagnosticDescriptor PropertyNameCannotBeNullOrEmptyError = new DiagnosticDescriptor(id: "COREWCF_0103",
            title: "PropertyName cannot be null or empty",
            messageFormat: "PropertyName value of CoreWCF.InjectedAttribute cannot be null or empty",
            category: nameof(OperationParameterInjectionGenerator),
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static Diagnostic RaisePropertyNameCannotBeNullOrEmptyError(Location location) => Diagnostic.Create(PropertyNameCannotBeNullOrEmptyError, location);

        internal static readonly DiagnosticDescriptor EitherPropertyNameOrServiceKeyShouldBeSpecified = new DiagnosticDescriptor(id: "COREWCF_0104",
            title: "Either PropertyName or ServiceKey should be specified",
            messageFormat: "PropertyName value of CoreWCF.InjectedAttribute cannot be null or empty",
            category: nameof(OperationParameterInjectionGenerator),
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static Diagnostic RaiseEitherPropertyNameOrServiceKeyShouldBeSpecified(Location location) => Diagnostic.Create(EitherPropertyNameOrServiceKeyShouldBeSpecified, location);
    }

    internal static class AuthorizationAttributesAnalyzer_02XX
    {
        internal static readonly DiagnosticDescriptor AllowAnonymousAttributeIsNotSupported = new DiagnosticDescriptor(id: "COREWCF_0200",
            title: "[AllowAnonymous] attribute is not supported",
            messageFormat: "[AllowAnonymous] attribute is not supported",
            category: nameof(AuthorizationAttributesAnalyzer),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Diagnostic AllowAnonymousAttributeIsNotSupportedWarning(Location location) => Diagnostic.Create(AllowAnonymousAttributeIsNotSupported, location);

        internal static readonly DiagnosticDescriptor AuthorizeDataAuthenticationSchemesPropertyIsNotSupported = new DiagnosticDescriptor(id: "COREWCF_0201",
            title: "Specifying 'AuthenticationSchemes' property of [Authorize] attribute is not supported",
            messageFormat: "Specifying 'AuthenticationSchemes' property of [Authorize] attribute is not supported",
            category: nameof(AuthorizationAttributesAnalyzer),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Diagnostic AuthorizeDataAuthenticationSchemesPropertyIsNotSupportedWarning(Location location) => Diagnostic.Create(AuthorizeDataAuthenticationSchemesPropertyIsNotSupported, location);

        internal static readonly DiagnosticDescriptor AuthorizeDataRolesPropertyIsNotSupported = new DiagnosticDescriptor(id: "COREWCF_0202",
            title: "Specifying 'Roles' property of [Authorize] attribute is not supported",
            messageFormat: "Specifying 'Roles' property of [Authorize] attribute is not supported",
            category: nameof(AuthorizationAttributesAnalyzer),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Diagnostic AuthorizeDataRolesPropertyIsNotSupportedWarning(Location location) => Diagnostic.Create(AuthorizeDataRolesPropertyIsNotSupported, location);
    }

    internal static class PrivateServiceContractAnalyzer__03XX
    {
        internal static readonly DiagnosticDescriptor PrivateServiceContract = new DiagnosticDescriptor(id: "COREWCF_0300",
            title: "Private ServiceContract interface",
            messageFormat: "ServiceContract '{0}' accessibility should at least be internal to generate an OperationInvoker implementation",
            category: nameof(PrivateServiceContractAnalyzer),
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        internal static Diagnostic PrivateServiceContractError(string serviceContract, Location location) =>
            Diagnostic.Create(PrivateServiceContract, location, serviceContract);
    }
}

