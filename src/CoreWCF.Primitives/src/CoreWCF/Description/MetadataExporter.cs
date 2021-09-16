// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    //For export we provide a builder that allows the gradual construction of a set of MetadataDocuments
    public abstract class MetadataExporter
    {
        private PolicyVersion _policyVersion = PolicyVersion.Policy12;

        //prevent inheritance until we are ready to allow it.
        internal MetadataExporter()
        {
        }

        public PolicyVersion PolicyVersion
        {
            get
            {
                return _policyVersion;
            }
            set
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                _policyVersion = value;
            }
        }

        public Collection<MetadataConversionError> Errors { get; } = new Collection<MetadataConversionError>();
        public Dictionary<object, object> State { get; } = new Dictionary<object, object>();

        public abstract void ExportContract(ContractDescription contract);
        public abstract void ExportEndpoint(ServiceEndpoint endpoint);

        public abstract MetadataSet GetGeneratedMetadata();

        internal PolicyConversionContext ExportPolicy(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            PolicyConversionContext policyContext = new ExportedPolicyConversionContext(endpoint, bindingParameters);

            foreach (IPolicyExportExtension exporter in endpoint.Binding.CreateBindingElements().FindAll<IPolicyExportExtension>())
                try
                {
                    exporter.ExportPolicy(this, policyContext);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                        throw;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateExtensionException(exporter, e));
                }

            return policyContext;
        }

        protected internal PolicyConversionContext ExportPolicy(ServiceEndpoint endpoint)
        {
            return ExportPolicy(endpoint, null);
        }

        private sealed class ExportedPolicyConversionContext : PolicyConversionContext
        {
            private readonly BindingElementCollection bindingElements;
            private PolicyAssertionCollection bindingAssertions;
            private Dictionary<OperationDescription, PolicyAssertionCollection> operationBindingAssertions;
            private Dictionary<MessageDescription, PolicyAssertionCollection> messageBindingAssertions;
            private Dictionary<FaultDescription, PolicyAssertionCollection> faultBindingAssertions;
            private BindingParameterCollection bindingParameters;

            internal ExportedPolicyConversionContext(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
                : base(endpoint)
            {
                bindingElements = endpoint.Binding.CreateBindingElements();
                bindingAssertions = new PolicyAssertionCollection();
                operationBindingAssertions = new Dictionary<OperationDescription, PolicyAssertionCollection>();
                messageBindingAssertions = new Dictionary<MessageDescription, PolicyAssertionCollection>();
                faultBindingAssertions = new Dictionary<FaultDescription, PolicyAssertionCollection>();
                this.bindingParameters = bindingParameters;
            }

            public override BindingElementCollection BindingElements
            {
                get { return bindingElements; }
            }

            internal override BindingParameterCollection BindingParameters
            {
                get { return bindingParameters; }
            }

            public override PolicyAssertionCollection GetBindingAssertions()
            {
                return bindingAssertions;
            }

            public override PolicyAssertionCollection GetOperationBindingAssertions(OperationDescription operation)
            {
                lock (operationBindingAssertions)
                {
                    if (!operationBindingAssertions.ContainsKey(operation))
                        operationBindingAssertions.Add(operation, new PolicyAssertionCollection());
                }

                return operationBindingAssertions[operation];
            }

            public override PolicyAssertionCollection GetMessageBindingAssertions(MessageDescription message)
            {
                lock (messageBindingAssertions)
                {
                    if (!messageBindingAssertions.ContainsKey(message))
                        messageBindingAssertions.Add(message, new PolicyAssertionCollection());
                }
                return messageBindingAssertions[message];
            }

            public override PolicyAssertionCollection GetFaultBindingAssertions(FaultDescription fault)
            {
                lock (faultBindingAssertions)
                {
                    if (!faultBindingAssertions.ContainsKey(fault))
                        faultBindingAssertions.Add(fault, new PolicyAssertionCollection());
                }
                return faultBindingAssertions[fault];
            }

        }

        private Exception CreateExtensionException(IPolicyExportExtension exporter, Exception e)
        {
            string errorMessage = SR.Format(SR.PolicyExtensionExportError, exporter.GetType(), e.Message);
            return new InvalidOperationException(errorMessage, e);
        }
    }
}
