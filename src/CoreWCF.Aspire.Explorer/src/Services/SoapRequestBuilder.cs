// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using CoreWCF.Aspire.Explorer.Model;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>
/// Builds a SOAP request envelope from the formatted parameter grid (Name / Type / Value), for
/// operations whose parameters are all simple types. Mirrors the "Formatted" request view of the classic
/// WCF Test Client.
/// </summary>
public static class SoapRequestBuilder
{
    /// <summary>Builds the full SOAP envelope for an operation from its current parameter values.</summary>
    public static string BuildEnvelope(WsdlOperation operation)
    {
        if (operation.RequestWrapperName is null)
        {
            return operation.SampleRequestEnvelope;
        }

        XName wrapperName = operation.RequestWrapperNamespace is { Length: > 0 } ns
            ? XName.Get(operation.RequestWrapperName, ns)
            : XName.Get(operation.RequestWrapperName);

        var wrapper = new XElement(wrapperName);
        foreach (var parameter in operation.RequestParameters)
        {
            XName elementName = parameter.Namespace is { Length: > 0 } pns
                ? XName.Get(parameter.Name, pns)
                : XName.Get(parameter.Name);
            wrapper.Add(new XElement(elementName, parameter.Value));
        }

        return SoapEnvelope.Wrap(wrapper, operation.SoapVersion);
    }
}
