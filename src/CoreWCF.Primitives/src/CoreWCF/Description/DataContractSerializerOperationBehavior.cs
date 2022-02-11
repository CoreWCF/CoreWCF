// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    public class DataContractSerializerOperationBehavior : IOperationBehavior, IWsdlExportExtension
    {
        private readonly OperationDescription _operation;
        internal bool ignoreExtensionDataObject = DataContractSerializerDefaults.IgnoreExtensionDataObject;
        internal int maxItemsInObjectGraph = DataContractSerializerDefaults.MaxItemsInObjectGraph;

        public DataContractFormatAttribute DataContractFormatAttribute { get; }

        public DataContractSerializerOperationBehavior(OperationDescription operation)
            : this(operation, null)
        {
        }

        public DataContractSerializerOperationBehavior(OperationDescription operation, DataContractFormatAttribute dataContractFormatAttribute)
        {
            DataContractFormatAttribute = dataContractFormatAttribute ?? new DataContractFormatAttribute();
            _operation = operation;
        }

        public DataContractSerializerOperationBehavior(OperationDescription operation,
            DataContractFormatAttribute dataContractFormatAttribute, bool builtInOperationBehavior)
            : this(operation, dataContractFormatAttribute)
        {
            IsBuiltInOperationBehavior = builtInOperationBehavior;
        }

        internal bool IsBuiltInOperationBehavior { get; }

        public int MaxItemsInObjectGraph
        {
            get { return maxItemsInObjectGraph; }
            set
            {
                maxItemsInObjectGraph = value;
                MaxItemsInObjectGraphSetExplicit = true;
            }
        }

        internal bool MaxItemsInObjectGraphSetExplicit { get; set; }

        public bool IgnoreExtensionDataObject
        {
            get { return ignoreExtensionDataObject; }
            set
            {
                ignoreExtensionDataObject = value;
                IgnoreExtensionDataObjectSetExplicit = true;
            }
        }

        internal bool IgnoreExtensionDataObjectSetExplicit { get; set; }

        public DataContractResolver DataContractResolver { get; set; }

        public virtual XmlObjectSerializer CreateSerializer(Type type, string name, string ns, IList<Type> knownTypes)
        {
            return new DataContractSerializer(type, name, ns, knownTypes);
        }

        public virtual XmlObjectSerializer CreateSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns, IList<Type> knownTypes)
        {
            return new DataContractSerializer(type, name, ns, knownTypes);
        }

        internal object GetFormatter(OperationDescription operation, out bool formatRequest, out bool formatReply, bool isProxy)
        {
            MessageDescription request = operation.Messages[0];
            MessageDescription response = null;
            if (operation.Messages.Count == 2)
            {
                response = operation.Messages[1];
            }

            formatRequest = (request != null) && !request.IsUntypedMessage;
            formatReply = (response != null) && !response.IsUntypedMessage;

            if (formatRequest || formatReply)
            {
                if (PrimitiveOperationFormatter.IsContractSupported(operation))
                {
                    return new PrimitiveOperationFormatter(operation, DataContractFormatAttribute.Style == OperationFormatStyle.Rpc);
                }
                else
                {
                    return new DataContractSerializerOperationFormatter(operation, DataContractFormatAttribute, this);
                }
            }

            return null;
        }


        void IOperationBehavior.Validate(OperationDescription description)
        {
        }

        void IOperationBehavior.AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }

        void IOperationBehavior.ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (dispatch == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));
            }

            if (dispatch.Formatter != null)
            {
                return;
            }

            dispatch.Formatter = (IDispatchMessageFormatter)GetFormatter(description, out bool formatRequest, out bool formatReply, false);
            dispatch.DeserializeRequest = formatRequest;
            dispatch.SerializeReply = formatReply;
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (proxy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(proxy));
            }

            if (proxy.Formatter != null)
            {
                return;
            }

            proxy.Formatter = (IClientMessageFormatter)GetFormatter(description, out bool formatRequest, out bool formatReply, true);
            proxy.SerializeRequest = formatRequest;
            proxy.DeserializeReply = formatReply;
        }

        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext)
        {
            if (exporter == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            if (endpointContext == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointContext));

            MessageContractExporter.ExportMessageBinding(exporter, endpointContext, typeof(DataContractSerializerMessageContractExporter), _operation);
        }

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext contractContext)
        {
            if (exporter == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            if (contractContext == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractContext));

            new DataContractSerializerMessageContractExporter(exporter, contractContext, _operation, this).ExportMessageContract();
        }
    }
}
