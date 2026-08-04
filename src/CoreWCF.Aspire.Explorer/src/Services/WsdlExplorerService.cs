// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Services.Description;
using System.Xml;
using System.Xml.Schema;
using CoreWCF.Aspire.Explorer.Model;
using Microsoft.Extensions.Logging;
using WsdlBinding = System.Web.Services.Description.Binding;
using WsdlOperationBinding = System.Web.Services.Description.OperationBinding;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>
/// Fetches a CoreWCF service's WSDL (via HTTP GET on <c>?singleWsdl</c>) and parses it into a
/// <see cref="WsdlModel"/>, generating a sample SOAP request envelope for every operation.
/// </summary>
public sealed class WsdlExplorerService(HttpClient httpClient, ILogger<WsdlExplorerService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<WsdlExplorerService> _logger = logger;

    /// <summary>Loads and parses the WSDL for the given service.</summary>
    public async Task<WsdlModel> LoadAsync(CoreWcfServiceDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _logger.LogInformation("Fetching WSDL for {Service} from {Url}", descriptor.Name, descriptor.SingleWsdlUrl);

        await using var stream = await _httpClient.GetStreamAsync(descriptor.SingleWsdlUrl, cancellationToken).ConfigureAwait(false);
        return Parse(descriptor, stream);
    }

    /// <summary>Parses a WSDL document. Exposed for testing.</summary>
    public static WsdlModel Parse(CoreWcfServiceDescriptor descriptor, Stream wsdlStream)
    {
        var serviceDescription = ServiceDescription.Read(wsdlStream);
        var schemas = BuildSchemaSet(serviceDescription);
        var sampleGenerator = new SampleXmlGenerator(schemas);

        var model = new WsdlModel
        {
            ServiceName = descriptor.Name,
            TargetNamespace = serviceDescription.TargetNamespace ?? string.Empty,
        };

        foreach (PortType portType in serviceDescription.PortTypes)
        {
            var contract = new WsdlContract { Name = portType.Name };
            var (binding, soapVersion) = FindBinding(serviceDescription, portType.Name);

            foreach (Operation operation in portType.Operations)
            {
                var soapAction = FindSoapAction(binding, operation.Name);
                var requestElement = FindRequestElement(serviceDescription, operation);
                var body = requestElement is not null ? sampleGenerator.Generate(requestElement) : null;

                var wsdlOperation = new WsdlOperation
                {
                    Name = operation.Name,
                    SoapAction = soapAction,
                    SoapVersion = soapVersion,
                    SampleRequestEnvelope = SoapEnvelope.Wrap(body, soapVersion),
                };

                if (requestElement is not null)
                {
                    var shape = sampleGenerator.DescribeRequest(requestElement);
                    wsdlOperation.RequestWrapperName = shape.WrapperName;
                    wsdlOperation.RequestWrapperNamespace = shape.WrapperNamespace;
                    wsdlOperation.RequestParameters.AddRange(shape.Parameters);
                    wsdlOperation.CanUseFormattedRequest = shape.WrapperName is not null && shape.AllSimple;
                }

                contract.Operations.Add(wsdlOperation);
            }

            model.Contracts.Add(contract);
        }

        return model;
    }

    private static XmlSchemaSet BuildSchemaSet(ServiceDescription serviceDescription)
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        if (serviceDescription.Types?.Schemas is { } schemas)
        {
            foreach (XmlSchema schema in schemas)
            {
                set.Add(schema);
            }
        }

        return set;
    }

    private static (WsdlBinding? Binding, SoapVersion Version) FindBinding(ServiceDescription serviceDescription, string portTypeName)
    {
        foreach (WsdlBinding binding in serviceDescription.Bindings)
        {
            if (!string.Equals(binding.Type.Name, portTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            var version = SoapVersion.Soap11;
            foreach (ServiceDescriptionFormatExtension extension in binding.Extensions)
            {
                if (extension is Soap12Binding)
                {
                    version = SoapVersion.Soap12;
                }
            }

            return (binding, version);
        }

        return (null, SoapVersion.Soap11);
    }

    private static string FindSoapAction(WsdlBinding? binding, string operationName)
    {
        if (binding is null)
        {
            return string.Empty;
        }

        foreach (WsdlOperationBinding operationBinding in binding.Operations)
        {
            if (!string.Equals(operationBinding.Name, operationName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (ServiceDescriptionFormatExtension extension in operationBinding.Extensions)
            {
                switch (extension)
                {
                    case Soap12OperationBinding soap12:
                        return soap12.SoapAction ?? string.Empty;
                    case SoapOperationBinding soap:
                        return soap.SoapAction ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static XmlQualifiedName? FindRequestElement(ServiceDescription serviceDescription, Operation operation)
    {
        foreach (OperationMessage message in operation.Messages)
        {
            if (message is not OperationInput input)
            {
                continue;
            }

            var wsdlMessage = serviceDescription.Messages[input.Message.Name];
            if (wsdlMessage is null || wsdlMessage.Parts.Count == 0)
            {
                return null;
            }

            var element = wsdlMessage.Parts[0].Element;
            return element.IsEmpty ? null : element;
        }

        return null;
    }
}
