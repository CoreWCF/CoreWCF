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
            get => _policyVersion;
            set => _policyVersion = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
        }

        public Collection<MetadataConversionError> Errors { get; } = new Collection<MetadataConversionError>();
        public Dictionary<object, object> State { get; } = new Dictionary<object, object>();

        public abstract void ExportContract(ContractDescription contract);
        public abstract void ExportEndpoint(ServiceEndpoint endpoint);

        public abstract MetadataSet GetGeneratedMetadata();

        internal PolicyConversionContext ExportPolicy(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            PolicyConversionContext policyContext = new ExportedPolicyConversionContext(endpoint, bindingParameters);

            foreach (IPolicyExportExtension exporter in policyContext.BindingElements.FindAll<IPolicyExportExtension>())
            {
                try
                {
                    exporter.ExportPolicy(this, policyContext);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateExtensionException(exporter, e));
                }
            }

            return policyContext;
        }

        protected internal PolicyConversionContext ExportPolicy(ServiceEndpoint endpoint)
        {
            return ExportPolicy(endpoint, null);
        }

        private sealed class ExportedPolicyConversionContext : PolicyConversionContext
        {
            private readonly PolicyAssertionCollection _bindingAssertions;
            private readonly Dictionary<OperationDescription, PolicyAssertionCollection> _operationBindingAssertions;
            private readonly Dictionary<MessageDescription, PolicyAssertionCollection> _messageBindingAssertions;
            private readonly Dictionary<FaultDescription, PolicyAssertionCollection> _faultBindingAssertions;

            internal ExportedPolicyConversionContext(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
                : base(endpoint)
            {
                BindingElements = endpoint.Binding.CreateBindingElements();
                _bindingAssertions = new PolicyAssertionCollection();
                _operationBindingAssertions = new Dictionary<OperationDescription, PolicyAssertionCollection>();
                _messageBindingAssertions = new Dictionary<MessageDescription, PolicyAssertionCollection>();
                _faultBindingAssertions = new Dictionary<FaultDescription, PolicyAssertionCollection>();
                BindingParameters = bindingParameters;
            }

            public override BindingElementCollection BindingElements { get; }

            public override BindingParameterCollection BindingParameters { get; }

            public override PolicyAssertionCollection GetBindingAssertions() => _bindingAssertions;

            public override PolicyAssertionCollection GetOperationBindingAssertions(OperationDescription operation)
            {
                lock (_operationBindingAssertions)
                {
                    if (!_operationBindingAssertions.ContainsKey(operation))
                    {
                        _operationBindingAssertions.Add(operation, new PolicyAssertionCollection());
                    }
                }

                return _operationBindingAssertions[operation];
            }

            public override PolicyAssertionCollection GetMessageBindingAssertions(MessageDescription message)
            {
                lock (_messageBindingAssertions)
                {
                    if (!_messageBindingAssertions.ContainsKey(message))
                    {
                        _messageBindingAssertions.Add(message, new PolicyAssertionCollection());
                    }
                }
                return _messageBindingAssertions[message];
            }

            public override PolicyAssertionCollection GetFaultBindingAssertions(FaultDescription fault)
            {
                lock (_faultBindingAssertions)
                {
                    if (!_faultBindingAssertions.ContainsKey(fault))
                    {
                        _faultBindingAssertions.Add(fault, new PolicyAssertionCollection());
                    }
                }
                return _faultBindingAssertions[fault];
            }

        }

        private Exception CreateExtensionException(IPolicyExportExtension exporter, Exception e)
        {
            string errorMessage = SR.Format(SR.PolicyExtensionExportError, exporter.GetType(), e.Message);
            return new InvalidOperationException(errorMessage, e);
        }
    }
}
