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
    public class DataContractSerializerOperationBehavior : IOperationBehavior //, IWsdlExportExtension
    {
        private readonly bool builtInOperationBehavior;
        private OperationDescription operation;
        private DataContractFormatAttribute dataContractFormatAttribute;
        internal bool ignoreExtensionDataObject = DataContractSerializerDefaults.IgnoreExtensionDataObject;
        private bool ignoreExtensionDataObjectSetExplicit;
        internal int maxItemsInObjectGraph = DataContractSerializerDefaults.MaxItemsInObjectGraph;
        private bool maxItemsInObjectGraphSetExplicit;
        private DataContractResolver dataContractResolver;

        public DataContractFormatAttribute DataContractFormatAttribute
        {
            get { return dataContractFormatAttribute; }
        }

        public DataContractSerializerOperationBehavior(OperationDescription operation)
            : this(operation, null)
        {
        }

        public DataContractSerializerOperationBehavior(OperationDescription operation, DataContractFormatAttribute dataContractFormatAttribute)
        {
            this.dataContractFormatAttribute = dataContractFormatAttribute ?? new DataContractFormatAttribute();
            this.operation = operation;
        }

        public DataContractSerializerOperationBehavior(OperationDescription operation,
            DataContractFormatAttribute dataContractFormatAttribute, bool builtInOperationBehavior)
            : this(operation, dataContractFormatAttribute)
        {
            this.builtInOperationBehavior = builtInOperationBehavior;
        }

        internal bool IsBuiltInOperationBehavior
        {
            get { return builtInOperationBehavior; }
        }

        public int MaxItemsInObjectGraph
        {
            get { return maxItemsInObjectGraph; }
            set
            {
                maxItemsInObjectGraph = value;
                maxItemsInObjectGraphSetExplicit = true;
            }
        }

        internal bool MaxItemsInObjectGraphSetExplicit
        {
            get { return maxItemsInObjectGraphSetExplicit; }
            set { maxItemsInObjectGraphSetExplicit = value; }
        }

        public bool IgnoreExtensionDataObject
        {
            get { return ignoreExtensionDataObject; }
            set
            {
                ignoreExtensionDataObject = value;
                ignoreExtensionDataObjectSetExplicit = true;
            }
        }

        internal bool IgnoreExtensionDataObjectSetExplicit
        {
            get { return ignoreExtensionDataObjectSetExplicit; }
            set { ignoreExtensionDataObjectSetExplicit = value; }
        }

        public DataContractResolver DataContractResolver
        {
            get { return dataContractResolver; }
            set { dataContractResolver = value; }
        }

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
                response = operation.Messages[1];

            formatRequest = (request != null) && !request.IsUntypedMessage;
            formatReply = (response != null) && !response.IsUntypedMessage;

            if (formatRequest || formatReply)
            {
                if (PrimitiveOperationFormatter.IsContractSupported(operation))
                    return new PrimitiveOperationFormatter(operation, dataContractFormatAttribute.Style == OperationFormatStyle.Rpc);
                else
                    return new DataContractSerializerOperationFormatter(operation, dataContractFormatAttribute, this);
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));

            if (dispatch == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));

            if (dispatch.Formatter != null)
                return;

            bool formatRequest;
            bool formatReply;
            dispatch.Formatter = (IDispatchMessageFormatter)GetFormatter(description, out formatRequest, out formatReply, false);
            dispatch.DeserializeRequest = formatRequest;
            dispatch.SerializeReply = formatReply;
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            if (description == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));

            if (proxy == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(proxy));

            if (proxy.Formatter != null)
                return;

            bool formatRequest;
            bool formatReply;
            proxy.Formatter = (IClientMessageFormatter)GetFormatter(description, out formatRequest, out formatReply, true);
            proxy.SerializeRequest = formatRequest;
            proxy.DeserializeReply = formatReply;
        }
    }
}