// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Description
{
    public class WsdlEndpointConversionContext
    {
        private readonly Dictionary<OperationDescription, WsdlNS.OperationBinding> _wsdlOperationBindings;
        private readonly Dictionary<WsdlNS.OperationBinding, OperationDescription> _operationDescriptionBindings;
        private readonly Dictionary<MessageDescription, WsdlNS.MessageBinding> _wsdlMessageBindings;
        private readonly Dictionary<FaultDescription, WsdlNS.FaultBinding> _wsdlFaultBindings;
        private readonly Dictionary<WsdlNS.MessageBinding, MessageDescription> _messageDescriptionBindings;
        private readonly Dictionary<WsdlNS.FaultBinding, FaultDescription> _faultDescriptionBindings;

        internal WsdlEndpointConversionContext(WsdlContractConversionContext contractContext, ServiceEndpoint endpoint, WsdlNS.Binding wsdlBinding, WsdlNS.Port wsdlport)
        {

            Endpoint = endpoint;
            WsdlBinding = wsdlBinding;
            WsdlPort = wsdlport;
            ContractConversionContext = contractContext;

            _wsdlOperationBindings = new Dictionary<OperationDescription, WsdlNS.OperationBinding>();
            _operationDescriptionBindings = new Dictionary<WsdlNS.OperationBinding, OperationDescription>();
            _wsdlMessageBindings = new Dictionary<MessageDescription, WsdlNS.MessageBinding>();
            _messageDescriptionBindings = new Dictionary<WsdlNS.MessageBinding, MessageDescription>();
            _wsdlFaultBindings = new Dictionary<FaultDescription, WsdlNS.FaultBinding>();
            _faultDescriptionBindings = new Dictionary<WsdlNS.FaultBinding, FaultDescription>();
        }

        internal WsdlEndpointConversionContext(WsdlEndpointConversionContext bindingContext, ServiceEndpoint endpoint, WsdlNS.Port wsdlport)
        {

            Endpoint = endpoint;
            WsdlBinding = bindingContext.WsdlBinding;
            WsdlPort = wsdlport;
            ContractConversionContext = bindingContext.ContractConversionContext;

            _wsdlOperationBindings = bindingContext._wsdlOperationBindings;
            _operationDescriptionBindings = bindingContext._operationDescriptionBindings;
            _wsdlMessageBindings = bindingContext._wsdlMessageBindings;
            _messageDescriptionBindings = bindingContext._messageDescriptionBindings;
            _wsdlFaultBindings = bindingContext._wsdlFaultBindings;
            _faultDescriptionBindings = bindingContext._faultDescriptionBindings;
        }

        internal IEnumerable<IWsdlExportExtension> ExportExtensions
        {
            get
            {
                foreach (IWsdlExportExtension extension in Endpoint.EndpointBehaviors.FindAll<IEndpointBehavior, IWsdlExportExtension>())
                {
                    yield return extension;
                }

                foreach (IWsdlExportExtension extension in Endpoint.Binding.CreateBindingElements().FindAll<IWsdlExportExtension>())
                {
                    yield return extension;
                }

                foreach (IWsdlExportExtension extension in Endpoint.Contract.ContractBehaviors.FindAll<IContractBehavior, IWsdlExportExtension>())
                {
                    yield return extension;
                }

                foreach (OperationDescription operation in Endpoint.Contract.Operations)
                {
                    if (!WsdlExporter.OperationIsExportable(operation))
                    {
                        continue;
                    }

                    // In 3.0SP1, the DCSOB and XSOB were moved from before to after the custom behaviors.  For
                    // IWsdlExportExtension compat, run them in the pre-SP1 order.
                    Collection<IWsdlExportExtension> extensions = operation.OperationBehaviors.FindAll<IOperationBehavior, IWsdlExportExtension>();
                    for (int i = 0; i < extensions.Count;)
                    {
                        if (WsdlExporter.IsBuiltInOperationBehavior(extensions[i]))
                        {
                            yield return extensions[i];
                            extensions.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    foreach (IWsdlExportExtension extension in extensions)
                    {
                        yield return extension;
                    }
                }
            }
        }

        public ServiceEndpoint Endpoint { get; }
        public WsdlNS.Binding WsdlBinding { get; }
        public WsdlNS.Port WsdlPort { get; }
        public WsdlContractConversionContext ContractConversionContext { get; }
        public WsdlNS.OperationBinding GetOperationBinding(OperationDescription operation) => _wsdlOperationBindings[operation];
        public WsdlNS.MessageBinding GetMessageBinding(MessageDescription message) => _wsdlMessageBindings[message];
        public WsdlNS.FaultBinding GetFaultBinding(FaultDescription fault) => _wsdlFaultBindings[fault];
        public OperationDescription GetOperationDescription(WsdlNS.OperationBinding operationBinding) => _operationDescriptionBindings[operationBinding];
        public MessageDescription GetMessageDescription(WsdlNS.MessageBinding messageBinding) => _messageDescriptionBindings[messageBinding];
        public FaultDescription GetFaultDescription(WsdlNS.FaultBinding faultBinding) => _faultDescriptionBindings[faultBinding];

        // --------------------------------------------------------------------------------------------------

        internal void AddOperationBinding(OperationDescription operationDescription, WsdlNS.OperationBinding wsdlOperationBinding)
        {
            _wsdlOperationBindings.Add(operationDescription, wsdlOperationBinding);
            _operationDescriptionBindings.Add(wsdlOperationBinding, operationDescription);
        }

        internal void AddMessageBinding(MessageDescription messageDescription, WsdlNS.MessageBinding wsdlMessageBinding)
        {
            _wsdlMessageBindings.Add(messageDescription, wsdlMessageBinding);
            _messageDescriptionBindings.Add(wsdlMessageBinding, messageDescription);
        }

        internal void AddFaultBinding(FaultDescription faultDescription, WsdlNS.FaultBinding wsdlFaultBinding)
        {
            _wsdlFaultBindings.Add(faultDescription, wsdlFaultBinding);
            _faultDescriptionBindings.Add(wsdlFaultBinding, faultDescription);
        }
    }
}
