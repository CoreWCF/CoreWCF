// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF.Aspire.Explorer.Model;

/// <summary>The SOAP version a binding/operation uses.</summary>
public enum SoapVersion
{
    Soap11,
    Soap12,
}

/// <summary>Parsed representation of a service's WSDL: its contracts and their operations.</summary>
public sealed class WsdlModel
{
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>The XML target namespace of the WSDL document.</summary>
    public string TargetNamespace { get; set; } = string.Empty;

    /// <summary>Contracts (WSDL port types) exposed by the service.</summary>
    public List<WsdlContract> Contracts { get; } = new();
}

/// <summary>A WSDL contract (port type) and its operations.</summary>
public sealed class WsdlContract
{
    public string Name { get; set; } = string.Empty;

    public List<WsdlOperation> Operations { get; } = new();
}

/// <summary>A single WSDL operation, with everything needed to build and send a request.</summary>
public sealed class WsdlOperation
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The SOAPAction associated with the operation (may be empty).</summary>
    public string SoapAction { get; set; } = string.Empty;

    public SoapVersion SoapVersion { get; set; } = SoapVersion.Soap11;

    /// <summary>A best-effort sample SOAP request envelope, ready to edit and send.</summary>
    public string SampleRequestEnvelope { get; set; } = string.Empty;

    /// <summary>Local name of the request wrapper element (document/literal wrapped).</summary>
    public string? RequestWrapperName { get; set; }

    /// <summary>Namespace of the request wrapper element.</summary>
    public string? RequestWrapperNamespace { get; set; }

    /// <summary>The request parameters (immediate children of the wrapper element).</summary>
    public List<WsdlParameter> RequestParameters { get; } = new();

    /// <summary>
    /// True when the request has a resolved wrapper and every parameter is a simple type, so the
    /// "Formatted" parameter grid can build the request. Otherwise the XML view must be used.
    /// </summary>
    public bool CanUseFormattedRequest { get; set; }
}

/// <summary>A single request parameter (Name / Type / Value), as shown in the formatted request grid.</summary>
public sealed class WsdlParameter
{
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;

    /// <summary>Display type name (for example <c>string</c>, <c>int</c>).</summary>
    public string TypeName { get; set; } = "string";

    public bool IsSimple { get; set; } = true;

    /// <summary>Sample/default value.</summary>
    public string SampleValue { get; set; } = string.Empty;

    /// <summary>The current, user-editable value.</summary>
    public string Value { get; set; } = string.Empty;
}
