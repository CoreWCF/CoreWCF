// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WsdlNS = System.Web.Services.Description;
using System.Linq;
using System;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    // This class is created as part of the export process and passed to
    // Wsdlmporter and WsdlExporter implementations as a utility for
    // Correlating between the WSDL OM and the WCF OM
    // in the conversion process.
    public class WsdlContractConversionContext
    {

        readonly ContractDescription contract;
        readonly WsdlNS.PortType wsdlPortType;

        readonly Dictionary<OperationDescription, WsdlNS.Operation> wsdlOperations;
        readonly Dictionary<WsdlNS.Operation, OperationDescription> operationDescriptions;
        readonly Dictionary<MessageDescription, WsdlNS.OperationMessage> wsdlOperationMessages;
        readonly Dictionary<FaultDescription, WsdlNS.OperationFault> wsdlOperationFaults;
        readonly Dictionary<WsdlNS.OperationMessage, MessageDescription> messageDescriptions;
        readonly Dictionary<WsdlNS.OperationFault, FaultDescription> faultDescriptions;
        readonly Dictionary<WsdlNS.Operation, Collection<WsdlNS.OperationBinding>> operationBindings;

        internal WsdlContractConversionContext(ContractDescription contract, WsdlNS.PortType wsdlPortType)
        {

            this.contract = contract;
            this.wsdlPortType = wsdlPortType;

            this.wsdlOperations = new Dictionary<OperationDescription, WsdlNS.Operation>();
            this.operationDescriptions = new Dictionary<WsdlNS.Operation, OperationDescription>();
            this.wsdlOperationMessages = new Dictionary<MessageDescription, WsdlNS.OperationMessage>();
            this.messageDescriptions = new Dictionary<WsdlNS.OperationMessage, MessageDescription>();
            this.wsdlOperationFaults = new Dictionary<FaultDescription, WsdlNS.OperationFault>();
            this.faultDescriptions = new Dictionary<WsdlNS.OperationFault, FaultDescription>();
            this.operationBindings = new Dictionary<WsdlNS.Operation, Collection<WsdlNS.OperationBinding>>();
        }

        internal IEnumerable<IWsdlExportExtension> ExportExtensions
        {
            get
            {
                IEnumerable<IWsdlExportExtension> result = contract.ContractBehaviors.Where(cb => cb is IWsdlExportExtension).Select(cb => cb as IWsdlExportExtension);
                foreach (OperationDescription operation in contract.Operations)
                {
                    if (!WsdlExporter.OperationIsExportable(operation))
                    {
                        continue;
                    }

                    // In 3.0SP1, the DCSOB and XSOB were moved from before to after the custom behaviors.  For
                    // IWsdlExportExtension compat, run them in the pre-SP1 order.
                    // TEF QFE 367607
                    var operationExtensions = operation.OperationBehaviors.Where(ob => ob is IWsdlExportExtension).Select(ob => ob as IWsdlExportExtension);
                    result = result.Concat(operationExtensions.Where(oe => WsdlExporter.IsBuiltInOperationBehavior(oe)));
                    result = result.Concat(operationExtensions.Where(oe => !WsdlExporter.IsBuiltInOperationBehavior(oe)));
                }

                return result;
            }
        }

        public ContractDescription Contract { get { return contract; } }

        public WsdlNS.PortType WsdlPortType { get { return wsdlPortType; } }

        public WsdlNS.Operation GetOperation(OperationDescription operation) => this.wsdlOperations[operation];

        public WsdlNS.OperationMessage GetOperationMessage(MessageDescription message) => this.wsdlOperationMessages[message];

        public WsdlNS.OperationFault GetOperationFault(FaultDescription fault) => this.wsdlOperationFaults[fault];

        public OperationDescription GetOperationDescription(WsdlNS.Operation operation) => this.operationDescriptions[operation];

        public MessageDescription GetMessageDescription(WsdlNS.OperationMessage operationMessage) => this.messageDescriptions[operationMessage];

        public FaultDescription GetFaultDescription(WsdlNS.OperationFault operationFault) => this.faultDescriptions[operationFault];

        // --------------------------------------------------------------------------------------------------

        internal void AddOperation(OperationDescription operationDescription, WsdlNS.Operation wsdlOperation)
        {
            this.wsdlOperations.Add(operationDescription, wsdlOperation);
            this.operationDescriptions.Add(wsdlOperation, operationDescription);
        }

        internal void AddMessage(MessageDescription messageDescription, WsdlNS.OperationMessage wsdlOperationMessage)
        {
            this.wsdlOperationMessages.Add(messageDescription, wsdlOperationMessage);
            this.messageDescriptions.Add(wsdlOperationMessage, messageDescription);
        }

        internal void AddFault(FaultDescription faultDescription, WsdlNS.OperationFault wsdlOperationFault)
        {
            this.wsdlOperationFaults.Add(faultDescription, wsdlOperationFault);
            this.faultDescriptions.Add(wsdlOperationFault, faultDescription);
        }

        internal Collection<WsdlNS.OperationBinding> GetOperationBindings(WsdlNS.Operation operation)
        {
            Collection<WsdlNS.OperationBinding> bindings;
            if (!this.operationBindings.TryGetValue(operation, out bindings))
            {
                bindings = new Collection<WsdlNS.OperationBinding>();
                WsdlNS.ServiceDescriptionCollection wsdlDocuments = WsdlPortType.ServiceDescription.ServiceDescriptions;
                foreach (WsdlNS.ServiceDescription wsdl in wsdlDocuments)
                {
                    foreach (WsdlNS.Binding wsdlBinding in wsdl.Bindings)
                    {
                        if (wsdlBinding.Type.Name == WsdlPortType.Name && wsdlBinding.Type.Namespace == WsdlPortType.ServiceDescription.TargetNamespace)
                        {
                            foreach (WsdlNS.OperationBinding operationBinding in wsdlBinding.Operations)
                            {
                                if (Binding2DescriptionHelper.Match(operationBinding, operation) != Binding2DescriptionHelper.MatchResult.None)
                                {
                                    bindings.Add(operationBinding);
                                    break;
                                }
                            }
                        }
                    }
                }
                this.operationBindings.Add(operation, bindings);
            }
            return bindings;
        }

        internal static class Binding2DescriptionHelper
        {
            internal enum MatchResult
            {
                None,
                Partial,
                Exact
            }

            internal static MatchResult Match(WsdlNS.OperationBinding wsdlOperationBinding, WsdlNS.Operation wsdlOperation)
            {
                // This method checks if there is a match based on Names, between the specified OperationBinding and Operation.
                // When searching for the Operation associated with an OperationBinding, we need to return an exact match if possible,
                // or a partial match otherwise (when some of the Names are null).
                // Bug 16833 @ CSDMain requires that partial matches are allowed, while the TFS bug 477838 requires that exact matches are done (when possible).
                if (wsdlOperationBinding.Name != wsdlOperation.Name)
                {
                    return MatchResult.None;
                }

                MatchResult result = MatchResult.Exact;

                foreach (WsdlNS.OperationMessage wsdlOperationMessage in wsdlOperation.Messages)
                {
                    WsdlNS.MessageBinding wsdlMessageBinding;
                    if (wsdlOperationMessage is WsdlNS.OperationInput)
                        wsdlMessageBinding = wsdlOperationBinding.Input;
                    else
                        wsdlMessageBinding = wsdlOperationBinding.Output;

                    if (wsdlMessageBinding == null)
                    {
                        return MatchResult.None;
                    }

                    switch (MatchOperationParameterName(wsdlMessageBinding, wsdlOperationMessage))
                    {
                        case MatchResult.None:
                            return MatchResult.None;
                        case MatchResult.Partial:
                            result = MatchResult.Partial;
                            break;
                    }
                }

                return result;
            }

            static MatchResult MatchOperationParameterName(WsdlNS.MessageBinding wsdlMessageBinding, WsdlNS.OperationMessage wsdlOperationMessage)
            {
                string wsdlOperationMessageName = wsdlOperationMessage.Name;
                string wsdlMessageBindingName = wsdlMessageBinding.Name;

                if (wsdlOperationMessageName == wsdlMessageBindingName)
                {
                    return MatchResult.Exact;
                }

                string wsdlOperationMessageDecodedName = WsdlNamingHelper.GetOperationMessageName(wsdlOperationMessage);
                if ((wsdlOperationMessageName == null) && (wsdlMessageBindingName == wsdlOperationMessageDecodedName))
                {
                    return MatchResult.Partial;
                }
                else if ((wsdlMessageBindingName == null) && (wsdlOperationMessageName == wsdlOperationMessageDecodedName))
                {
                    return MatchResult.Partial;
                }
                else
                {
                    return MatchResult.None;
                }
            }
        }

        static class WsdlNamingHelper
        {
            internal static string GetOperationMessageName(WsdlNS.OperationMessage wsdlOperationMessage)
            {
                string messageName = null;
                if (!string.IsNullOrEmpty(wsdlOperationMessage.Name))
                {
                    messageName = wsdlOperationMessage.Name;
                }
                else if (wsdlOperationMessage.Operation.Messages.Count == 1)
                {
                    messageName = wsdlOperationMessage.Operation.Name;
                }
                else if (wsdlOperationMessage.Operation.Messages.IndexOf(wsdlOperationMessage) == 0)
                {
                    if (wsdlOperationMessage is WsdlNS.OperationInput)
                        messageName = wsdlOperationMessage.Operation.Name + "Request";
                    else if (wsdlOperationMessage is WsdlNS.OperationOutput)
                        messageName = wsdlOperationMessage.Operation.Name + "Solicit";
                }
                else if (wsdlOperationMessage.Operation.Messages.IndexOf(wsdlOperationMessage) == 1)
                {
                    messageName = wsdlOperationMessage.Operation.Name + "Response";
                }
                else
                {
                    Fx.Assert("Unsupported WSDL OM (More than 2 OperationMessages encountered in an Operation or WsdlOM is invalid)");
                }

                // This is the same validation that ServiceDescriptor.XmlName performs
                try
                {
                    if (messageName != null) XmlConvert.VerifyNCName(messageName);
                }
                catch (XmlException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(e.Message, nameof(WsdlNS.OperationMessage.Name)));
                }

                // The original code returned XmlName and the called used DecodedName, this does the equivalent
                return XmlConvert.DecodeName(messageName);
            }
        }
    }
}
