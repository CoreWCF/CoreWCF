// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Description
{
    public class WsdlExporter : MetadataExporter
    {
        internal const string SessionOpenedAction = "http://schemas.microsoft.com/2011/02/session/onopen";
        internal const string DefaultNamespace = "http://tempuri.org/";
        internal const string DefaultServiceName = "service";
        private static XmlDocument s_xmlDocument;
        private bool _isFaulted = false;
        private readonly Dictionary<ContractDescription, WsdlContractConversionContext> _exportedContracts = new Dictionary<ContractDescription, WsdlContractConversionContext>();
        private readonly Dictionary<BindingDictionaryKey, WsdlEndpointConversionContext> _exportedBindings = new Dictionary<BindingDictionaryKey, WsdlEndpointConversionContext>();
        private readonly Dictionary<EndpointDictionaryKey, ServiceEndpoint> _exportedEndpoints = new Dictionary<EndpointDictionaryKey, ServiceEndpoint>();

        public override void ExportContract(ContractDescription contract)
        {
            if (_isFaulted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.WsdlExporterIsFaulted));
            }

            if (contract == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contract));
            }

            if (!_exportedContracts.ContainsKey(contract))
            {
                try
                {
                    WsdlNS.PortType wsdlPortType = CreateWsdlPortType(contract);
                    WsdlContractConversionContext contractContext;


                    contractContext = new WsdlContractConversionContext(contract, wsdlPortType);

                    foreach (OperationDescription operation in contract.Operations)
                    {
                        if (!OperationIsExportable(operation, out bool isWildcardAction))
                        {
                            string warningMsg = isWildcardAction ? SR.Format(SR.WarnSkippingOperationWithWildcardAction, contract.Name, contract.Namespace, operation.Name)
                                : SR.Format(SR.WarnSkippingOperationWithSessionOpenNotificationEnabled, "Action", SessionOpenedAction, contract.Name, contract.Namespace, operation.Name);

                            LogExportWarning(warningMsg);
                            continue;
                        }

                        WsdlNS.Operation wsdlOperation = CreateWsdlOperation(operation, contract);
                        wsdlPortType.Operations.Add(wsdlOperation);

                        contractContext.AddOperation(operation, wsdlOperation);

                        foreach (MessageDescription message in operation.Messages)
                        {
                            //Create Operation Message
                            WsdlNS.OperationMessage wsdlOperationMessage = CreateWsdlOperationMessage(message);
                            wsdlOperation.Messages.Add(wsdlOperationMessage);
                            contractContext.AddMessage(message, wsdlOperationMessage);
                        }

                        foreach (FaultDescription fault in operation.Faults)
                        {
                            //Create Operation Fault
                            WsdlNS.OperationFault wsdlOperationFault = CreateWsdlOperationFault(fault);
                            wsdlOperation.Faults.Add(wsdlOperationFault);
                            contractContext.AddFault(fault, wsdlOperationFault);
                        }
                    }

                    CallExportContract(contractContext);

                    _exportedContracts.Add(contract, contractContext);
                }
                catch
                {
                    _isFaulted = true;
                    throw;
                }
            }
        }

        public override void ExportEndpoint(ServiceEndpoint endpoint)
        {
            if (_isFaulted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.WsdlExporterIsFaulted));
            }

            if (endpoint == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoint));
            }

            ExportEndpoint(endpoint, new XmlQualifiedName(DefaultServiceName, DefaultNamespace), null);
        }

        public void ExportEndpoints(IEnumerable<ServiceEndpoint> endpoints, XmlQualifiedName wsdlServiceQName) => ExportEndpoints(endpoints, wsdlServiceQName, null);

        internal void ExportEndpoints(IEnumerable<ServiceEndpoint> endpoints, XmlQualifiedName wsdlServiceQName, BindingParameterCollection bindingParameters)
        {
            if (_isFaulted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.WsdlExporterIsFaulted));
            }

            if (endpoints == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoints));
            }

            if (wsdlServiceQName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wsdlServiceQName));
            }

            foreach (ServiceEndpoint endpoint in endpoints)
            {
                ExportEndpoint(endpoint, wsdlServiceQName, bindingParameters);
            }
        }

        public override MetadataSet GetGeneratedMetadata()
        {
            MetadataSet set = new MetadataSet();

            foreach (WsdlNS.ServiceDescription wsdl in GeneratedWsdlDocuments)
            {
                set.MetadataSections.Add(MetadataSection.CreateFromServiceDescription(wsdl));
            }

            foreach (XmlSchema schema in GeneratedXmlSchemas.Schemas())
            {
                set.MetadataSections.Add(MetadataSection.CreateFromSchema(schema));
            }

            return set;
        }

        public WsdlNS.ServiceDescriptionCollection GeneratedWsdlDocuments { get; } = new WsdlNS.ServiceDescriptionCollection();

        public XmlSchemaSet GeneratedXmlSchemas { get; } = GetEmptySchemaSet();

        private void ExportEndpoint(ServiceEndpoint endpoint, XmlQualifiedName wsdlServiceQName, BindingParameterCollection bindingParameters)
        {
            if (endpoint.Binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.EndpointsMustHaveAValidBinding1, endpoint.Name)));
            }

            EndpointDictionaryKey endpointKey = new EndpointDictionaryKey(endpoint, wsdlServiceQName);

            try
            {
                if (_exportedEndpoints.ContainsKey(endpointKey))
                {
                    return;
                }

                ExportContract(endpoint.Contract);

                // Retreive Conversion Context for Contract;
                // Note: Contract must have already been exported at this point.
                WsdlContractConversionContext contractContext = _exportedContracts[endpoint.Contract];


                WsdlNS.Binding wsdlBinding;
                wsdlBinding = CreateWsdlBindingAndPort(endpoint, wsdlServiceQName, out WsdlNS.Port wsdlPort, out bool newWsdlBinding, out bool bindingNameWasUniquified);


                if (!newWsdlBinding && wsdlPort == null)
                {
                    return;
                }

                // Create an Endpoint conversion context based on 
                // the contract's conversion context (reuse contract correlation information)
                WsdlEndpointConversionContext endpointContext;
                if (newWsdlBinding)
                {
                    endpointContext = new WsdlEndpointConversionContext(contractContext, endpoint, wsdlBinding, wsdlPort);

                    foreach (OperationDescription operation in endpoint.Contract.Operations)
                    {
                        if (!OperationIsExportable(operation))
                        {
                            continue;
                        }

                        WsdlNS.OperationBinding wsdlOperationBinding = CreateWsdlOperationBinding(operation);
                        wsdlBinding.Operations.Add(wsdlOperationBinding);

                        endpointContext.AddOperationBinding(operation, wsdlOperationBinding);

                        foreach (MessageDescription message in operation.Messages)
                        {
                            WsdlNS.MessageBinding wsdlMessageBinding = CreateWsdlMessageBinding(message, wsdlOperationBinding);
                            endpointContext.AddMessageBinding(message, wsdlMessageBinding);
                        }

                        foreach (FaultDescription fault in operation.Faults)
                        {
                            WsdlNS.FaultBinding wsdlFaultBinding = CreateWsdlFaultBinding(fault, wsdlOperationBinding);
                            endpointContext.AddFaultBinding(fault, wsdlFaultBinding);
                        }
                    }

                    // CSDMain 180381:  Added internal functionality for passing BindingParameters into the ExportPolicy process via PolicyConversionContext.
                    // However, in order to not change existing behavior, we only call the internal ExportPolicy method which accepts BindingParameters if they are not null
                    // (non-null binding parameters can only be passed in via internal code paths).  Otherwise, we call the existing ExportPolicy method, just like before.
                    PolicyConversionContext policyContext;
                    if (bindingParameters == null)
                    {
                        policyContext = ExportPolicy(endpoint);
                    }
                    else
                    {
                        policyContext = ExportPolicy(endpoint, bindingParameters);
                    }
                    // consider factoring this out of wsdl exporter
                    new WSPolicyAttachmentHelper(PolicyVersion).AttachPolicy(endpoint, endpointContext, policyContext);
                    _exportedBindings.Add(new BindingDictionaryKey(endpoint.Contract, endpoint.Binding), endpointContext);
                }
                else
                {
                    endpointContext = new WsdlEndpointConversionContext(_exportedBindings[new BindingDictionaryKey(endpoint.Contract, endpoint.Binding)], endpoint, wsdlPort);
                }

                CallExportEndpoint(endpointContext);
                _exportedEndpoints.Add(endpointKey, endpoint);
                if (bindingNameWasUniquified)
                {
                    Errors.Add(new MetadataConversionError(SR.Format(SR.WarnDuplicateBindingQNameNameOnExport, endpoint.Binding.Name, endpoint.Binding.Namespace, endpoint.Contract.Name), true /*isWarning*/));
                }
            }
            catch
            {
                _isFaulted = true;
                throw;
            }
        }

        public static void AddWSAddressingAssertion(MetadataExporter exporter, PolicyConversionContext context, AddressingVersion addressing) => WSAddressingHelper.AddWSAddressingAssertion(exporter, context, addressing);

        private void CallExportEndpoint(WsdlEndpointConversionContext endpointContext)
        {
            foreach (IWsdlExportExtension extension in endpointContext.ExportExtensions)
            {
                CallExtension(endpointContext, extension);
            }
        }

        private void CallExportContract(WsdlContractConversionContext contractContext)
        {
            foreach (IWsdlExportExtension extension in contractContext.ExportExtensions)
            {
                CallExtension(contractContext, extension);
            }
        }

        private WsdlNS.PortType CreateWsdlPortType(ContractDescription contract)
        {
            XmlQualifiedName wsdlPortTypeQName = WsdlNamingHelper.GetPortTypeQName(contract);

            WsdlNS.ServiceDescription wsdl = GetOrCreateWsdl(wsdlPortTypeQName.Namespace);
            WsdlNS.PortType wsdlPortType = new WsdlNS.PortType
            {
                Name = wsdlPortTypeQName.Name
            };
            if (wsdl.PortTypes[wsdlPortType.Name] != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.DuplicateContractQNameNameOnExport, contract.Name, contract.Namespace)));
            }

            NetSessionHelper.AddUsingSessionAttributeIfNeeded(wsdlPortType, contract);
            wsdl.PortTypes.Add(wsdlPortType);

            return wsdlPortType;
        }

        private WsdlNS.Operation CreateWsdlOperation(OperationDescription operation, ContractDescription contract)
        {
            WsdlNS.Operation wsdlOperation = new WsdlNS.Operation
            {
                Name = WsdlNamingHelper.GetWsdlOperationName(operation)
            };
            NetSessionHelper.AddInitiatingTerminatingAttributesIfNeeded(wsdlOperation, operation, contract);
            return wsdlOperation;
        }

        private WsdlNS.OperationMessage CreateWsdlOperationMessage(MessageDescription message)
        {
            WsdlNS.OperationMessage wsdlOperationMessage;

            if (message.Direction == MessageDirection.Input)
            {
                wsdlOperationMessage = new WsdlNS.OperationInput();
            }
            else
            {
                wsdlOperationMessage = new WsdlNS.OperationOutput();
            }

            if (message.MessageType != null)
            {
                string messageName = NamingHelper.TypeName(message.MessageType);
                wsdlOperationMessage.Name = NamingHelper.XmlName(messageName);
            }

            // consider factoring this out of wslExporter
            WSAddressingHelper.AddActionAttribute(message.Action, wsdlOperationMessage, PolicyVersion);
            return wsdlOperationMessage;
        }

        private WsdlNS.OperationFault CreateWsdlOperationFault(FaultDescription fault)
        {
            WsdlNS.OperationFault wsdlOperationFault;
            wsdlOperationFault = new WsdlNS.OperationFault
            {
                // operation fault name must not be empty (FaultDescription checks this)
                Name = fault.Name
            };

            // consider factoring this out of wslExporter
            WSAddressingHelper.AddActionAttribute(fault.Action, wsdlOperationFault, PolicyVersion);
            return wsdlOperationFault;
        }

        private WsdlNS.Binding CreateWsdlBindingAndPort(ServiceEndpoint endpoint, XmlQualifiedName wsdlServiceQName, out WsdlNS.Port wsdlPort, out bool newBinding, out bool bindingNameWasUniquified)
        {
            WsdlNS.ServiceDescription bindingWsdl;
            WsdlNS.Binding wsdlBinding;
            XmlQualifiedName wsdlBindingQName;
            XmlQualifiedName wsdlPortTypeQName;
            // There's a change in behavior from NetFx here. On NetFx, for any MessageEncodingBindingElement we
            // would call the internal virtual method IsWsdlExportable. This always returned true for all MEBE
            // implementations except WebMessageEncodingBindingElement which returned false. For CoreWCF,
            // we're not worrying about supressing exporting the wsdl. We'll either not add the relevant
            // interface, or we'll make the relevant method be a no-op to achieve the same thing.
            // bool printWsdlDeclaration = IsWsdlExportable(endpoint.Binding);

            if (!_exportedBindings.TryGetValue(new BindingDictionaryKey(endpoint.Contract, endpoint.Binding), out WsdlEndpointConversionContext bindingConversionContext))
            {
                wsdlBindingQName = WsdlNamingHelper.GetBindingQName(endpoint, this, out bindingNameWasUniquified);
                bindingWsdl = GetOrCreateWsdl(wsdlBindingQName.Namespace);
                wsdlBinding = new WsdlNS.Binding { Name = wsdlBindingQName.Name };
                newBinding = true;
                WsdlNS.PortType wsdlPortType = _exportedContracts[endpoint.Contract].WsdlPortType;
                wsdlPortTypeQName = new XmlQualifiedName(wsdlPortType.Name, wsdlPortType.ServiceDescription.TargetNamespace);
                wsdlBinding.Type = wsdlPortTypeQName;
                bindingWsdl.Bindings.Add(wsdlBinding);

                EnsureWsdlContainsImport(bindingWsdl, wsdlPortTypeQName.Namespace);
            }
            else
            {
                wsdlBindingQName = new XmlQualifiedName(bindingConversionContext.WsdlBinding.Name, bindingConversionContext.WsdlBinding.ServiceDescription.TargetNamespace);
                bindingNameWasUniquified = false;
                bindingWsdl = GeneratedWsdlDocuments[wsdlBindingQName.Namespace];
                wsdlBinding = bindingWsdl.Bindings[wsdlBindingQName.Name];
                newBinding = false;
            }

            //We can only create a Port if there is an address
            if (endpoint.Address != null)
            {
                WsdlNS.Service wsdlService = GetOrCreateWsdlService(wsdlServiceQName);
                wsdlPort = new WsdlNS.Port();
                string wsdlPortName = WsdlNamingHelper.GetPortName(endpoint, wsdlService);
                wsdlPort.Name = wsdlPortName;
                wsdlPort.Binding = wsdlBindingQName;
                WsdlNS.SoapAddressBinding addressBinding = SoapHelper.GetOrCreateSoapAddressBinding(wsdlBinding, wsdlPort, this);

                if (addressBinding != null)
                {
                    addressBinding.Location = endpoint.Address.Uri.AbsoluteUri;
                }

                EnsureWsdlContainsImport(wsdlService.ServiceDescription, wsdlBindingQName.Namespace);
                wsdlService.Ports.Add(wsdlPort);
            }
            else
            {
                wsdlPort = null;
            }

            return wsdlBinding;
        }

        public static void AddAddressToWsdlPort(WsdlNS.Port wsdlPort, EndpointAddress address, AddressingVersion addressingVersion) => WSAddressingHelper.AddAddressToWsdlPort(wsdlPort, address, addressingVersion);

        private WsdlNS.OperationBinding CreateWsdlOperationBinding(OperationDescription operation) => new WsdlNS.OperationBinding { Name = WsdlNamingHelper.GetWsdlOperationName(operation) };

        private WsdlNS.MessageBinding CreateWsdlMessageBinding(MessageDescription messageDescription, WsdlNS.OperationBinding wsdlOperationBinding)
        {
            WsdlNS.MessageBinding wsdlMessageBinding;
            if (messageDescription.Direction == MessageDirection.Input)
            {
                wsdlOperationBinding.Input = new WsdlNS.InputBinding();
                wsdlMessageBinding = wsdlOperationBinding.Input;
            }
            else
            {
                wsdlOperationBinding.Output = new WsdlNS.OutputBinding();
                wsdlMessageBinding = wsdlOperationBinding.Output;
            }

            if (messageDescription.MessageType != null)
            {
                string messageName = NamingHelper.TypeName(messageDescription.MessageType);
                wsdlMessageBinding.Name = NamingHelper.XmlName(messageName);
            }

            return wsdlMessageBinding;
        }

        private WsdlNS.FaultBinding CreateWsdlFaultBinding(FaultDescription faultDescription, WsdlNS.OperationBinding wsdlOperationBinding)
        {
            WsdlNS.FaultBinding wsdlFaultBinding = new WsdlNS.FaultBinding();
            wsdlOperationBinding.Faults.Add(wsdlFaultBinding);
            if (faultDescription.Name != null)
            {
                wsdlFaultBinding.Name = faultDescription.Name;
            }

            return wsdlFaultBinding;
        }

        internal static bool OperationIsExportable(OperationDescription operation) => OperationIsExportable(operation, out _);

        internal static bool OperationIsExportable(OperationDescription operation, out bool isWildcardAction)
        {
            isWildcardAction = false;

            if (operation.IsSessionOpenNotificationEnabled)
            {
                return false;
            }

            for (int i = 0; i < operation.Messages.Count; i++)
            {
                if (operation.Messages[i].Action == MessageHeaders.WildcardAction)
                {
                    isWildcardAction = true;
                    return false;
                }
            }

            return true;
        }

        internal static bool IsBuiltInOperationBehavior(IWsdlExportExtension extension)
        {
            // This is a change in behavior from NetFx due to dcsob.IsBuiltInOperationBehavior not being public.
            // In NetFx, a user added dcsob would be treated as not built-in and the default added dcsob would be
            // treated as a built in operation. Being build in meant being placed at the end of the list returned
            // from WsdlContractConversionContext.ExportExtensions. In CoreWCF, dcsob will always be at the end of
            // the list. The same is true of xsob too.
            if (extension is DataContractSerializerOperationBehavior || extension is XmlSerializerOperationBehavior)
            {
                return true;
            }

            return false;
        }

        private static XmlDocument XmlDoc
        {
            get
            {
                if (s_xmlDocument == null)
                {
                    NameTable nameTable = new NameTable();
                    nameTable.Add(MetadataStrings.WSPolicy.Elements.Policy);
                    nameTable.Add(MetadataStrings.WSPolicy.Elements.All);
                    nameTable.Add(MetadataStrings.WSPolicy.Elements.ExactlyOne);
                    nameTable.Add(MetadataStrings.WSPolicy.Attributes.PolicyURIs);
                    nameTable.Add(MetadataStrings.Wsu.Attributes.Id);
                    nameTable.Add(MetadataStrings.Addressing200408.Policy.UsingAddressing);
                    nameTable.Add(MetadataStrings.Addressing10.WsdlBindingPolicy.UsingAddressing);
                    nameTable.Add(MetadataStrings.Addressing10.MetadataPolicy.Addressing);
                    nameTable.Add(MetadataStrings.Addressing10.MetadataPolicy.AnonymousResponses);
                    nameTable.Add(MetadataStrings.Addressing10.MetadataPolicy.NonAnonymousResponses);
                    s_xmlDocument = new XmlDocument(nameTable);
                }

                return s_xmlDocument;
            }
        }

        // Generate WSDL Document if it doesn't already exist otherwise, return the appropriate WSDL document
        internal WsdlNS.ServiceDescription GetOrCreateWsdl(string ns)
        {
            // NOTE: this method is not thread safe
            WsdlNS.ServiceDescriptionCollection wsdlCollection = GeneratedWsdlDocuments;
            WsdlNS.ServiceDescription wsdl = wsdlCollection[ns];

            // Look for wsdl in service descriptions that have been created. If we cannot find it then we create it
            if (wsdl == null)
            {
                wsdl = new WsdlNS.ServiceDescription { TargetNamespace = ns };
                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces(new WsdlNamespaceHelper(PolicyVersion).SerializerNamespaces);
                if (!string.IsNullOrEmpty(wsdl.TargetNamespace))
                {
                    namespaces.Add("tns", wsdl.TargetNamespace);
                }

                wsdl.Namespaces = namespaces;
                wsdlCollection.Add(wsdl);
            }

            return wsdl;
        }

        private WsdlNS.Service GetOrCreateWsdlService(XmlQualifiedName wsdlServiceQName)
        {
            // NOTE: this method is not thread safe
            WsdlNS.ServiceDescription wsdl = GetOrCreateWsdl(wsdlServiceQName.Namespace);
            WsdlNS.Service wsdlService = wsdl.Services[wsdlServiceQName.Name];
            if (wsdlService == null)
            {
                //Service not found. Create service.
                wsdlService = new WsdlNS.Service { Name = wsdlServiceQName.Name };
                if (string.IsNullOrEmpty(wsdl.Name))
                {
                    wsdl.Name = wsdlService.Name;
                }

                wsdl.Services.Add(wsdlService);
            }
            return wsdlService;
        }

        private static void EnsureWsdlContainsImport(WsdlNS.ServiceDescription srcWsdl, string target)
        {
            if (srcWsdl.TargetNamespace == target)
            {
                return;
            }

            // FindImport
            foreach (WsdlNS.Import import in srcWsdl.Imports)
            {
                if (import.Namespace == target)
                {
                    return;
                }
            }

            {
                WsdlNS.Import import = new WsdlNS.Import
                {
                    Location = null,
                    Namespace = target
                };
                srcWsdl.Imports.Add(import);
                WsdlNamespaceHelper.FindOrCreatePrefix("i", target, srcWsdl);
                return;
            }
        }

        private void LogExportWarning(string warningMessage) => Errors.Add(new MetadataConversionError(warningMessage, true));

        internal static XmlSchemaSet GetEmptySchemaSet() => new XmlSchemaSet { XmlResolver = null };

        internal static class WSAddressingHelper
        {
            internal static void AddActionAttribute(string actionUri, WsdlNS.OperationMessage wsdlOperationMessage, PolicyVersion policyVersion)
            {
                XmlAttribute attribute;
                if (policyVersion == PolicyVersion.Policy12)
                {
                    attribute = XmlDoc.CreateAttribute(MetadataStrings.AddressingWsdl.Prefix,
                        MetadataStrings.AddressingWsdl.Action,
                        MetadataStrings.AddressingWsdl.NamespaceUri);
                }
                else
                {
                    attribute = XmlDoc.CreateAttribute(MetadataStrings.AddressingMetadata.Prefix,
                        MetadataStrings.AddressingMetadata.Action,
                        MetadataStrings.AddressingMetadata.NamespaceUri);
                }

                attribute.Value = actionUri;
                wsdlOperationMessage.ExtensibleAttributes = new XmlAttribute[] { attribute };
            }

            internal static void AddAddressToWsdlPort(WsdlNS.Port wsdlPort, EndpointAddress addr, AddressingVersion addressing)
            {
                if (addressing == AddressingVersion.None)
                {
                    return;
                }

                MemoryStream stream = new MemoryStream();
                XmlWriter xw = XmlWriter.Create(stream);
                xw.WriteStartElement("temp");

                if (addressing == AddressingVersion.WSAddressing10)
                {
                    xw.WriteAttributeString("xmlns", MetadataStrings.Addressing10.Prefix, null, MetadataStrings.Addressing10.NamespaceUri);
                }
                else if (addressing == AddressingVersion.WSAddressingAugust2004)
                {
                    xw.WriteAttributeString("xmlns", MetadataStrings.Addressing200408.Prefix, null, MetadataStrings.Addressing200408.NamespaceUri);
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(SR.Format(SR.AddressingVersionNotSupported, addressing)));
                }

                addr.WriteTo(addressing, xw);
                xw.WriteEndElement();

                xw.Flush();
                stream.Seek(0, SeekOrigin.Begin);

                XmlReader xr = XmlReader.Create(stream);
                xr.MoveToContent();
                XmlElement endpointRef = (XmlElement)XmlDoc.ReadNode(xr).ChildNodes[0];

                wsdlPort.Extensions.Add(endpointRef);
            }

            internal static void AddWSAddressingAssertion(MetadataExporter exporter, PolicyConversionContext context, AddressingVersion addressVersion)
            {
                XmlElement addressingAssertion;
                if (addressVersion == AddressingVersion.WSAddressingAugust2004)
                {
                    addressingAssertion = XmlDoc.CreateElement(MetadataStrings.Addressing200408.Policy.Prefix,
                        MetadataStrings.Addressing200408.Policy.UsingAddressing,
                        MetadataStrings.Addressing200408.Policy.NamespaceUri);
                }
                else if (addressVersion == AddressingVersion.WSAddressing10)
                {
                    if (exporter.PolicyVersion == PolicyVersion.Policy12)
                    {
                        addressingAssertion = XmlDoc.CreateElement(MetadataStrings.Addressing10.WsdlBindingPolicy.Prefix,
                            MetadataStrings.Addressing10.WsdlBindingPolicy.UsingAddressing,
                            MetadataStrings.Addressing10.WsdlBindingPolicy.NamespaceUri);
                    }
                    else
                    {
                        addressingAssertion = XmlDoc.CreateElement(MetadataStrings.Addressing10.MetadataPolicy.Prefix,
                            MetadataStrings.Addressing10.MetadataPolicy.Addressing,
                            MetadataStrings.Addressing10.MetadataPolicy.NamespaceUri);

                        // On NetFx, there is an enum SupportedAddressingMode which is only used by CompositeDuplexBindingElement
                        // which we don't support yet. This means all transports are treated as though they are using
                        // SupportedAddressingMode.Anonymous. This code has been modified to only have the behavior of this mode.

                        string responsesAssertionLocalName;
                        responsesAssertionLocalName = MetadataStrings.Addressing10.MetadataPolicy.AnonymousResponses;

                        XmlElement innerPolicyElement = XmlDoc.CreateElement(MetadataStrings.WSPolicy.Prefix,
                                MetadataStrings.WSPolicy.Elements.Policy,
                                MetadataStrings.WSPolicy.NamespaceUri15);

                        XmlElement responsesAssertion = XmlDoc.CreateElement(MetadataStrings.Addressing10.MetadataPolicy.Prefix,
                                responsesAssertionLocalName,
                                MetadataStrings.Addressing10.MetadataPolicy.NamespaceUri);

                        innerPolicyElement.AppendChild(responsesAssertion);
                        addressingAssertion.AppendChild(innerPolicyElement);
                    }
                }
                else if (addressVersion == AddressingVersion.None)
                {
                    // do nothing
                    addressingAssertion = null;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(SR.Format(SR.AddressingVersionNotSupported, addressVersion)));
                }

                if (addressingAssertion != null)
                {
                    context.GetBindingAssertions().Add(addressingAssertion);
                }
            }
        }

        private class WSPolicyAttachmentHelper
        {
            private readonly PolicyVersion _policyVersion;

            internal WSPolicyAttachmentHelper(PolicyVersion policyVersion)
            {
                _policyVersion = policyVersion;
            }

            internal void AttachPolicy(ServiceEndpoint endpoint, WsdlEndpointConversionContext endpointContext, PolicyConversionContext policyContext)
            {
                SortedList<string, string> policyKeys = new SortedList<string, string>();
                NamingHelper.DoesNameExist policyKeyIsUnique
                    = (string name, object nameCollection) => policyKeys.ContainsKey(name);

                string key, keyBase;
                ICollection<XmlElement> assertions;

                WsdlNS.ServiceDescription policyWsdl = endpointContext.WsdlBinding.ServiceDescription;

                assertions = policyContext.GetBindingAssertions();

                // Add [wsdl:Binding] level Policy
                WsdlNS.Binding wsdlBinding = endpointContext.WsdlBinding;
                if (assertions.Count > 0)
                {
                    keyBase = CreateBindingPolicyKey(wsdlBinding);
                    key = NamingHelper.GetUniqueName(keyBase, policyKeyIsUnique, null);
                    policyKeys.Add(key, key);
                    AttachItemPolicy(assertions, key, policyWsdl, wsdlBinding);
                }

                foreach (OperationDescription operation in endpoint.Contract.Operations)
                {
                    if (!OperationIsExportable(operation))
                    {
                        continue;
                    }

                    assertions = policyContext.GetOperationBindingAssertions(operation);

                    // Add [wsdl:Binding/wsdl:operation] policy
                    if (assertions.Count > 0)
                    {
                        WsdlNS.OperationBinding wsdlOperationBinding = endpointContext.GetOperationBinding(operation);
                        keyBase = CreateOperationBindingPolicyKey(wsdlOperationBinding);
                        key = NamingHelper.GetUniqueName(keyBase, policyKeyIsUnique, null);
                        policyKeys.Add(key, key);
                        AttachItemPolicy(assertions, key, policyWsdl, wsdlOperationBinding);
                    }

                    //
                    // Add [wsdl:Binding/wsdl:operation] child policy
                    //

                    foreach (MessageDescription message in operation.Messages)
                    {
                        assertions = policyContext.GetMessageBindingAssertions(message);

                        // Add [wsdl:Binding/wsdl:operation/wsdl:(input, output, message)] policy
                        if (assertions.Count > 0)
                        {
                            WsdlNS.MessageBinding wsdlMessageBinding = endpointContext.GetMessageBinding(message);
                            keyBase = CreateMessageBindingPolicyKey(wsdlMessageBinding, message.Direction);
                            key = NamingHelper.GetUniqueName(keyBase, policyKeyIsUnique, null);
                            policyKeys.Add(key, key);
                            AttachItemPolicy(assertions, key, policyWsdl, wsdlMessageBinding);
                        }
                    }

                    foreach (FaultDescription fault in operation.Faults)
                    {
                        assertions = policyContext.GetFaultBindingAssertions(fault);

                        // Add [wsdl:Binding/wsdl:operation/wsdl:fault] policy
                        if (assertions.Count > 0)
                        {
                            WsdlNS.FaultBinding wsdlFaultBinding = endpointContext.GetFaultBinding(fault);
                            keyBase = CreateFaultBindingPolicyKey(wsdlFaultBinding);
                            key = NamingHelper.GetUniqueName(keyBase, policyKeyIsUnique, null);
                            policyKeys.Add(key, key);
                            AttachItemPolicy(assertions, key, policyWsdl, wsdlFaultBinding);
                        }
                    }
                }
            }

            private void AttachItemPolicy(ICollection<XmlElement> assertions, string key, WsdlNS.ServiceDescription policyWsdl, WsdlNS.DocumentableItem item)
            {
                string policyKey = InsertPolicy(key, policyWsdl, assertions);
                InsertPolicyReference(policyKey, item);
            }

            private void InsertPolicyReference(string policyKey, WsdlNS.DocumentableItem item)
            {
                //Create wsp:PolicyReference Element On DocumentableItem
                //---------------------------------------------------------------------------------------------------------
                XmlElement policyReferenceElement = XmlDoc.CreateElement(MetadataStrings.WSPolicy.Prefix,
                                                            MetadataStrings.WSPolicy.Elements.PolicyReference,
                                                            _policyVersion.Namespace);

                //Create wsp:PolicyURIs Attribute On DocumentableItem
                //---------------------------------------------------------------------------------------------------------
                XmlAttribute uriAttribute = XmlDoc.CreateAttribute(MetadataStrings.WSPolicy.Attributes.URI);

                uriAttribute.Value = policyKey;
                policyReferenceElement.Attributes.Append(uriAttribute);
                item.Extensions.Add(policyReferenceElement);
            }

            private string InsertPolicy(string key, WsdlNS.ServiceDescription policyWsdl, ICollection<XmlElement> assertions)
            {
                // Create [wsp:Policy]
                XmlElement policyElement = CreatePolicyElement(assertions);

                //Create [wsp:Policy/@wsu:Id]
                XmlAttribute idAttribute = XmlDoc.CreateAttribute(MetadataStrings.Wsu.Prefix,
                                                            MetadataStrings.Wsu.Attributes.Id,
                                                            MetadataStrings.Wsu.NamespaceUri);
                idAttribute.Value = key;
                policyElement.SetAttributeNode(idAttribute);

                // Add wsp:Policy To WSDL
                if (policyWsdl != null)
                {
                    policyWsdl.Extensions.Add(policyElement);
                }

                return string.Format(CultureInfo.InvariantCulture, "#{0}", key);
            }

            private XmlElement CreatePolicyElement(ICollection<XmlElement> assertions)
            {
                // Create [wsp:Policy]
                XmlElement policyElement = XmlDoc.CreateElement(MetadataStrings.WSPolicy.Prefix,
                                                            MetadataStrings.WSPolicy.Elements.Policy,
                                                            _policyVersion.Namespace);

                // Create [wsp:Policy/wsp:ExactlyOne]
                XmlElement exactlyOneElement = XmlDoc.CreateElement(MetadataStrings.WSPolicy.Prefix,
                                                            MetadataStrings.WSPolicy.Elements.ExactlyOne,
                                                            _policyVersion.Namespace);
                policyElement.AppendChild(exactlyOneElement);

                // Create [wsp:Policy/wsp:ExactlyOne/wsp:All]
                XmlElement allElement = XmlDoc.CreateElement(MetadataStrings.WSPolicy.Prefix,
                                                            MetadataStrings.WSPolicy.Elements.All,
                                                            _policyVersion.Namespace);
                exactlyOneElement.AppendChild(allElement);

                // Add [wsp:Policy/wsp:ExactlyOne/wsp:All/*]
                foreach (XmlElement assertion in assertions)
                {
                    XmlNode iNode = XmlDoc.ImportNode(assertion, true);
                    allElement.AppendChild(iNode);
                }

                return policyElement;
            }

            private static string CreateBindingPolicyKey(WsdlNS.Binding wsdlBinding) => string.Format(CultureInfo.InvariantCulture, "{0}_policy", wsdlBinding.Name);

            private static string CreateOperationBindingPolicyKey(WsdlNS.OperationBinding wsdlOperationBinding) => string.Format(CultureInfo.InvariantCulture, "{0}_{1}_policy",
                    wsdlOperationBinding.Binding.Name,
                    wsdlOperationBinding.Name);

            private static string CreateMessageBindingPolicyKey(WsdlNS.MessageBinding wsdlMessageBinding, MessageDirection direction)
            {
                WsdlNS.OperationBinding wsdlOperationBinding = wsdlMessageBinding.OperationBinding;
                WsdlNS.Binding wsdlBinding = wsdlOperationBinding.Binding;

                if (direction == MessageDirection.Input)
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_Input_policy", wsdlBinding.Name, wsdlOperationBinding.Name);
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_output_policy", wsdlBinding.Name, wsdlOperationBinding.Name);
                }
            }

            private static string CreateFaultBindingPolicyKey(WsdlNS.FaultBinding wsdlFaultBinding)
            {
                WsdlNS.OperationBinding wsdlOperationBinding = wsdlFaultBinding.OperationBinding;
                WsdlNS.Binding wsdlBinding = wsdlOperationBinding.Binding;
                if (string.IsNullOrEmpty(wsdlFaultBinding.Name))
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_Fault", wsdlBinding.Name, wsdlOperationBinding.Name);
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_Fault", wsdlBinding.Name, wsdlOperationBinding.Name, wsdlFaultBinding.Name);
                }
            }

        }

        private class WsdlNamespaceHelper
        {
            private XmlSerializerNamespaces _xmlSerializerNamespaces;
            private readonly PolicyVersion _policyVersion;
            internal XmlSerializerNamespaces SerializerNamespaces
            {
                get
                {
                    if (_xmlSerializerNamespaces == null)
                    {
                        XmlSerializerNamespaceWrapper namespaces = new XmlSerializerNamespaceWrapper();
                        namespaces.Add("wsdl", WsdlNS.ServiceDescription.Namespace);
                        namespaces.Add("xsd", XmlSchema.Namespace);
                        namespaces.Add(MetadataStrings.WSPolicy.Prefix, _policyVersion.Namespace);
                        namespaces.Add(MetadataStrings.Wsu.Prefix, MetadataStrings.Wsu.NamespaceUri);
                        namespaces.Add(MetadataStrings.Addressing200408.Prefix, MetadataStrings.Addressing200408.NamespaceUri);
                        namespaces.Add(MetadataStrings.Addressing200408.Policy.Prefix, MetadataStrings.Addressing200408.Policy.NamespaceUri);
                        namespaces.Add(MetadataStrings.Addressing10.Prefix, MetadataStrings.Addressing10.NamespaceUri);
                        namespaces.Add(MetadataStrings.Addressing10.WsdlBindingPolicy.Prefix, MetadataStrings.Addressing10.WsdlBindingPolicy.NamespaceUri);
                        namespaces.Add(MetadataStrings.Addressing10.MetadataPolicy.Prefix, MetadataStrings.Addressing10.MetadataPolicy.NamespaceUri);
                        namespaces.Add(MetadataStrings.MetadataExchangeStrings.Prefix, MetadataStrings.MetadataExchangeStrings.Namespace);
                        namespaces.Add(NetSessionHelper.Prefix, NetSessionHelper.NamespaceUri);

                        namespaces.Add("soapenc", "http://schemas.xmlsoap.org/soap/encoding/");
                        namespaces.Add("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
                        namespaces.Add("soap", "http://schemas.xmlsoap.org/wsdl/soap/");

                        _xmlSerializerNamespaces = namespaces.GetNamespaces();
                    }

                    return _xmlSerializerNamespaces;
                }
            }

            internal WsdlNamespaceHelper(PolicyVersion policyVersion)
            {
                _policyVersion = policyVersion;
            }

            // doesn't care if you add a duplicate prefix
            private class XmlSerializerNamespaceWrapper
            {
                private readonly XmlSerializerNamespaces _namespaces = new XmlSerializerNamespaces();
                private readonly Dictionary<string, string> _lookup = new Dictionary<string, string>();

                internal void Add(string prefix, string namespaceUri)
                {
                    if (!_lookup.ContainsKey(prefix))
                    {
                        _namespaces.Add(prefix, namespaceUri);
                        _lookup.Add(prefix, namespaceUri);
                    }
                }

                internal XmlSerializerNamespaces GetNamespaces() => _namespaces;
            }

            internal static string FindOrCreatePrefix(string prefixBase, string ns, params WsdlNS.DocumentableItem[] scopes)
            {
                if (!(scopes.Length > 0))
                {
                    Fx.Assert("You must pass at least one namespaceScope");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "You must pass at least one namespaceScope")));
                }

                string prefix;
                if (string.IsNullOrEmpty(ns))
                {
                    prefix = string.Empty;
                }
                else
                {
                    //See if a prefix for the namespace has already been defined at one of the scopes
                    for (int j = 0; j < scopes.Length; j++)
                    {
                        if (TryMatchNamespace(scopes[j].Namespaces.ToArray(), ns, out prefix))
                        {
                            return prefix;
                        }
                    }

                    // Create prefix definition at the nearest scope.
                    int i = 0;
                    prefix = prefixBase + i.ToString(CultureInfo.InvariantCulture);

                    //hsomu, consider do we need to check at higher scopes as well?
                    while (PrefixExists(scopes[0].Namespaces.ToArray(), prefix))
                    {
                        prefix = prefixBase + (++i).ToString(CultureInfo.InvariantCulture);
                    }
                }

                scopes[0].Namespaces.Add(prefix, ns);
                return prefix;
            }

            private static bool PrefixExists(XmlQualifiedName[] prefixDefinitions, string prefix) => Array.Exists(prefixDefinitions, prefixDef => prefixDef.Name == prefix);

            private static bool TryMatchNamespace(XmlQualifiedName[] prefixDefinitions, string ns, out string prefix)
            {
                XmlQualifiedName foundPrefixDef = prefixDefinitions.Where(prefixDef => prefixDef.Namespace == ns).FirstOrDefault();
                prefix = foundPrefixDef?.Name;
                return foundPrefixDef != null;
            }
        }

        internal static class WsdlNamingHelper
        {
            internal static XmlQualifiedName GetPortTypeQName(ContractDescription contract) => new XmlQualifiedName(contract.Name, contract.Namespace);

            internal static string GetUniqueName(string baseName, Func<string, object, bool> doesNameExist, object nameCollection)
            {
                for (int i = 0; i < int.MaxValue; i++)
                {
                    string name = i > 0 ? baseName + i : baseName;
                    if (!doesNameExist(name, nameCollection))
                    {
                        return name;
                    }
                }

                Fx.Assert("Too Many Names");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Cannot generate unique name for name {0}", baseName)));
            }

            internal static XmlQualifiedName GetBindingQName(ServiceEndpoint endpoint, WsdlExporter exporter, out bool wasUniquified)
            {
                // due to problems in Sysytem.Web.Services.Descriprion.ServiceDescription.Write() (double encoding) method we cannot use encoded names for
                // wsdl:binding item: we need to make sure that XmlConvert.EncodeLocalName will not find any problems with the name, and leave it unchanged.
                // consider changing the name here to something that will not be encoded by XmlSerializer (GenerateSimpleXmlName()?)
                string localName = endpoint.Name;
                string bindingWsdlNamespace = endpoint.Binding.Namespace;
                string uniquifiedLocalName = NamingHelper.GetUniqueName(localName, WsdlBindingQNameExists(exporter, bindingWsdlNamespace), null);
                wasUniquified = localName != uniquifiedLocalName;
                return new XmlQualifiedName(uniquifiedLocalName, bindingWsdlNamespace);
            }

            private static NamingHelper.DoesNameExist WsdlBindingQNameExists(WsdlExporter exporter, string bindingWsdlNamespace)
            {
                bool WsdlBindingQNameExistsImpl(string localName, object nameCollection)
                {
                    WsdlNS.ServiceDescription wsdl = exporter.GeneratedWsdlDocuments[bindingWsdlNamespace];
                    if (wsdl != null && wsdl.Bindings[localName] != null)
                    {
                        return true;
                    }

                    return false;
                }

                return WsdlBindingQNameExistsImpl;
            }

            internal static string GetPortName(ServiceEndpoint endpoint, WsdlNS.Service wsdlService) => NamingHelper.GetUniqueName(endpoint.Name, ServiceContainsPort(wsdlService), null);

            private static NamingHelper.DoesNameExist ServiceContainsPort(WsdlNS.Service service)
            {
                return (string portName, object nameCollection) => service.Ports.Cast<WsdlNS.Port>().Any(port => port.Name == portName);
            }

            internal static string GetWsdlOperationName(OperationDescription operationDescription) => operationDescription.Name;
        }

        internal static class NetSessionHelper
        {
            internal const string NamespaceUri = "http://schemas.microsoft.com/ws/2005/12/wsdl/contract";
            internal const string Prefix = "msc";
            internal const string UsingSession = "usingSession";
            internal const string IsInitiating = "isInitiating";
            internal const string IsTerminating = "isTerminating";
            internal const string True = "true";
            internal const string False = "false";

            internal static void AddUsingSessionAttributeIfNeeded(WsdlNS.PortType wsdlPortType, ContractDescription contract)
            {
                bool sessionValue;

                if (contract.SessionMode == SessionMode.Required)
                {
                    sessionValue = true;
                }
                else if (contract.SessionMode == SessionMode.NotAllowed)
                {
                    sessionValue = false;
                }
                else
                {
                    return;
                }

                wsdlPortType.ExtensibleAttributes = CloneAndAddToAttributes(wsdlPortType.ExtensibleAttributes, Prefix,
                    UsingSession, NamespaceUri, ToValue(sessionValue));
            }

            internal static void AddInitiatingTerminatingAttributesIfNeeded(WsdlNS.Operation wsdlOperation,
                OperationDescription operation, ContractDescription contract)
            {
                if (contract.SessionMode == SessionMode.Required)
                {
                    AddInitiatingAttribute(wsdlOperation, operation.IsInitiating);
                    AddTerminatingAttribute(wsdlOperation, operation.IsTerminating);
                }
            }

            private static void AddInitiatingAttribute(System.Web.Services.Description.Operation wsdlOperation, bool isInitiating) => wsdlOperation.ExtensibleAttributes = CloneAndAddToAttributes(wsdlOperation.ExtensibleAttributes, Prefix,
                    IsInitiating, NamespaceUri, ToValue(isInitiating));

            private static void AddTerminatingAttribute(System.Web.Services.Description.Operation wsdlOperation, bool isTerminating) => wsdlOperation.ExtensibleAttributes = CloneAndAddToAttributes(wsdlOperation.ExtensibleAttributes, Prefix,
                    IsTerminating, NamespaceUri, ToValue(isTerminating));

            private static XmlAttribute[] CloneAndAddToAttributes(XmlAttribute[] originalAttributes, string prefix, string localName, string ns, string value)
            {
                XmlAttribute newAttribute = XmlDoc.CreateAttribute(prefix, localName, ns);
                newAttribute.Value = value;

                int originalAttributeCount = 0;
                if (originalAttributes != null)
                {
                    originalAttributeCount = originalAttributes.Length;
                }

                XmlAttribute[] attributes = new XmlAttribute[originalAttributeCount + 1];

                if (originalAttributes != null)
                {
                    originalAttributes.CopyTo(attributes, 0);
                }

                attributes[attributes.Length - 1] = newAttribute;

                return attributes;
            }

            private static string ToValue(bool b) => b ? True : False;
        }

        private void CallExtension(WsdlContractConversionContext contractContext, IWsdlExportExtension extension)
        {
            try
            {
                extension.ExportContract(this, contractContext);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ThrowExtensionException(contractContext.Contract, extension, e));
            }
        }

        private void CallExtension(WsdlEndpointConversionContext endpointContext, IWsdlExportExtension extension)
        {
            try
            {
                extension.ExportEndpoint(this, endpointContext);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ThrowExtensionException(endpointContext.Endpoint, extension, e));
            }
        }

        private Exception ThrowExtensionException(ContractDescription contract, IWsdlExportExtension exporter, Exception e)
        {
            string contractIdentifier = new XmlQualifiedName(contract.Name, contract.Namespace).ToString();
            string errorMessage = SR.Format(SR.WsdlExtensionContractExportError, exporter.GetType(), contractIdentifier);

            return new InvalidOperationException(errorMessage, e);
        }

        private Exception ThrowExtensionException(ServiceEndpoint endpoint, IWsdlExportExtension exporter, Exception e)
        {
            string endpointIdentifier;
            if (endpoint.Address != null && endpoint.Address.Uri != null)
            {
                endpointIdentifier = endpoint.Address.Uri.ToString();
            }
            else
            {
                endpointIdentifier = string.Format(CultureInfo.InvariantCulture,
                    "Contract={1}:{0} ,Binding={3}:{2}",
                    endpoint.Contract.Name,
                    endpoint.Contract.Namespace,
                    endpoint.Binding.Name,
                    endpoint.Binding.Namespace);
            }

            string errorMessage = SR.Format(SR.WsdlExtensionEndpointExportError, exporter.GetType(), endpointIdentifier);

            return new InvalidOperationException(errorMessage, e);
        }

        private sealed class BindingDictionaryKey
        {
            public readonly ContractDescription Contract;
            public readonly Binding Binding;

            public BindingDictionaryKey(ContractDescription contract, Binding binding)
            {
                Contract = contract;
                Binding = binding;
            }

            public override bool Equals(object obj)
            {
                if (obj is BindingDictionaryKey key && key.Binding == Binding && key.Contract == Contract)
                {
                    return true;
                }

                return false;
            }

            public override int GetHashCode() => Contract.GetHashCode() ^ Binding.GetHashCode();
        }

        private sealed class EndpointDictionaryKey
        {
            public readonly ServiceEndpoint Endpoint;
            public readonly XmlQualifiedName ServiceQName;

            public EndpointDictionaryKey(ServiceEndpoint endpoint, XmlQualifiedName serviceQName)
            {
                Endpoint = endpoint;
                ServiceQName = serviceQName;
            }

            public override bool Equals(object obj)
            {
                if (obj is EndpointDictionaryKey key && key.Endpoint == Endpoint && key.ServiceQName == ServiceQName)
                {
                    return true;
                }

                return false;
            }

            public override int GetHashCode() => Endpoint.GetHashCode() ^ ServiceQName.GetHashCode();
        }
    }
}
